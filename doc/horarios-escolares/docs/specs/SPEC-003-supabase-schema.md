# SPEC-003: Supabase — Schema inicial + RLS + Auth

**Estado:** Draft  
**Prioridad:** P0  
**Fase:** 0 — Infraestructura  
**Estimación:** 2 días  
**Depende de:** —  

---

## Contexto

Define el schema completo de PostgreSQL en Supabase, las políticas de Row Level Security que garantizan el aislamiento entre colegios, y la configuración de Auth para magic link y Google OAuth.

---

## Schema SQL completo

```sql
-- ─────────────────────────────────────────────
-- EXTENSIONES
-- ─────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ─────────────────────────────────────────────
-- SCHOOLS
-- ─────────────────────────────────────────────
CREATE TABLE schools (
  id            UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  name          TEXT NOT NULL,
  slug          TEXT UNIQUE NOT NULL,        -- para URLs públicas
  schedule_type TEXT NOT NULL DEFAULT 'continua' CHECK (schedule_type IN ('continua', 'partida')),
  morning_start TIME NOT NULL DEFAULT '09:00',
  afternoon_start TIME,                       -- solo jornada partida
  slot_minutes  INT NOT NULL DEFAULT 50,
  break_after_slot INT NOT NULL DEFAULT 2,   -- recreo tras el slot N
  break_minutes INT NOT NULL DEFAULT 30,
  working_days  INT[] NOT NULL DEFAULT '{1,2,3,4,5}', -- 1=Lunes...5=Viernes
  created_at    TIMESTAMPTZ DEFAULT NOW(),
  updated_at    TIMESTAMPTZ DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- SCHOOL MEMBERS (relación usuario-colegio)
-- ─────────────────────────────────────────────
CREATE TABLE school_members (
  id         UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id  UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
  user_id    UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
  role       TEXT NOT NULL CHECK (role IN ('school_admin', 'teacher', 'super_admin')),
  created_at TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE (school_id, user_id)
);

-- ─────────────────────────────────────────────
-- TEACHERS
-- ─────────────────────────────────────────────
CREATE TABLE teachers (
  id                     UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id              UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
  user_id                UUID REFERENCES auth.users(id),  -- null hasta que acepten invitación
  full_name              TEXT NOT NULL,
  email                  TEXT NOT NULL,
  teacher_type           TEXT NOT NULL DEFAULT 'definitivo' 
                           CHECK (teacher_type IN ('definitivo', 'interino', 'especialista')),
  max_weekly_hours       INT NOT NULL DEFAULT 25,
  max_daily_consecutive  INT NOT NULL DEFAULT 4,
  specialties            TEXT[] NOT NULL DEFAULT '{}',
  created_at             TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE (school_id, email)
);

-- ─────────────────────────────────────────────
-- CLASSROOMS
-- ─────────────────────────────────────────────
CREATE TABLE classrooms (
  id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id    UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
  name         TEXT NOT NULL,
  classroom_type TEXT NOT NULL DEFAULT 'regular'
                  CHECK (classroom_type IN ('regular', 'gym', 'music', 'lab', 'it', 'support')),
  capacity     INT NOT NULL DEFAULT 30,
  is_shared    BOOLEAN NOT NULL DEFAULT FALSE,
  created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- CURRICULUM TEMPLATES
-- ─────────────────────────────────────────────
CREATE TABLE curriculum_templates (
  id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id    UUID REFERENCES schools(id) ON DELETE CASCADE, -- null = plantilla oficial
  name         TEXT NOT NULL,
  region       TEXT NOT NULL DEFAULT 'madrid',
  stage        TEXT NOT NULL DEFAULT 'primaria',
  course_level INT,                                           -- null = aplica a todos los cursos
  is_official  BOOLEAN NOT NULL DEFAULT FALSE,
  created_at   TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE subject_allocations (
  id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  template_id             UUID NOT NULL REFERENCES curriculum_templates(id) ON DELETE CASCADE,
  subject_name            TEXT NOT NULL,
  weekly_hours_min        INT NOT NULL,
  weekly_hours_max        INT NOT NULL,
  weekly_hours_default    INT NOT NULL,
  requires_specialist     BOOLEAN NOT NULL DEFAULT FALSE,
  required_classroom_type TEXT,
  max_consecutive_slots   INT NOT NULL DEFAULT 2,
  splittable_across_days  BOOLEAN NOT NULL DEFAULT TRUE
);

-- ─────────────────────────────────────────────
-- ACADEMIC STRUCTURE
-- ─────────────────────────────────────────────
CREATE TABLE course_groups (
  id               UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id        UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
  course_level     INT NOT NULL CHECK (course_level BETWEEN 1 AND 6),
  group_label      TEXT NOT NULL,                            -- 'A', 'B', 'C'
  student_count    INT NOT NULL DEFAULT 25,
  tutor_id         UUID REFERENCES teachers(id),
  home_classroom_id UUID REFERENCES classrooms(id),
  created_at       TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE (school_id, course_level, group_label)
);

-- ─────────────────────────────────────────────
-- ASSIGNMENTS (profesor → asignatura → grupo)
-- ─────────────────────────────────────────────
CREATE TABLE assignments (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
  teacher_id      UUID NOT NULL REFERENCES teachers(id) ON DELETE CASCADE,
  group_id        UUID NOT NULL REFERENCES course_groups(id) ON DELETE CASCADE,
  allocation_id   UUID NOT NULL REFERENCES subject_allocations(id),
  weekly_hours    INT NOT NULL,                              -- horas reales asignadas
  created_at      TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE (teacher_id, group_id, allocation_id)
);

-- ─────────────────────────────────────────────
-- TEACHER CONSTRAINTS
-- ─────────────────────────────────────────────
CREATE TABLE teacher_constraints (
  id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id    UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
  teacher_id   UUID NOT NULL REFERENCES teachers(id) ON DELETE CASCADE,
  constraint_type TEXT NOT NULL CHECK (constraint_type IN ('unavailable', 'preference')),
  day_of_week  INT NOT NULL CHECK (day_of_week BETWEEN 1 AND 5),
  slot_index   INT NOT NULL,
  weight       INT NOT NULL DEFAULT 10 CHECK (weight BETWEEN 1 AND 10), -- solo soft
  reason       TEXT,
  created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- SCHEDULES (versionado)
-- ─────────────────────────────────────────────
CREATE TABLE schedules (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
  academic_year   TEXT NOT NULL,                             -- '2025-2026'
  status          TEXT NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft', 'generated', 'published', 'archived')),
  generated_at    TIMESTAMPTZ,
  published_at    TIMESTAMPTZ,
  generation_seconds INT,
  total_conflicts INT NOT NULL DEFAULT 0,
  created_by      UUID REFERENCES auth.users(id),
  created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE schedule_entries (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  schedule_id     UUID NOT NULL REFERENCES schedules(id) ON DELETE CASCADE,
  school_id       UUID NOT NULL REFERENCES schools(id),      -- desnormalizado para RLS
  group_id        UUID NOT NULL REFERENCES course_groups(id),
  allocation_id   UUID NOT NULL REFERENCES subject_allocations(id),
  teacher_id      UUID NOT NULL REFERENCES teachers(id),
  classroom_id    UUID NOT NULL REFERENCES classrooms(id),
  day_of_week     INT NOT NULL CHECK (day_of_week BETWEEN 1 AND 5),
  slot_index      INT NOT NULL,
  is_manual_override BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE schedule_conflicts (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  schedule_id     UUID NOT NULL REFERENCES schedules(id) ON DELETE CASCADE,
  conflict_type   TEXT NOT NULL CHECK (conflict_type IN ('teacher', 'classroom', 'normative', 'soft')),
  severity        TEXT NOT NULL CHECK (severity IN ('error', 'warning')),
  description     TEXT NOT NULL,
  suggestions     TEXT[] NOT NULL DEFAULT '{}',
  group_id        UUID REFERENCES course_groups(id),
  teacher_id      UUID REFERENCES teachers(id),
  day_of_week     INT,
  slot_index      INT
);

-- ─────────────────────────────────────────────
-- ÍNDICES
-- ─────────────────────────────────────────────
CREATE INDEX idx_school_members_user ON school_members(user_id);
CREATE INDEX idx_teachers_school ON teachers(school_id);
CREATE INDEX idx_teachers_user ON teachers(user_id);
CREATE INDEX idx_schedule_entries_schedule ON schedule_entries(schedule_id);
CREATE INDEX idx_schedule_entries_teacher ON schedule_entries(teacher_id);
CREATE INDEX idx_schedules_school_status ON schedules(school_id, status);

-- ─────────────────────────────────────────────
-- ROW LEVEL SECURITY
-- ─────────────────────────────────────────────
ALTER TABLE schools ENABLE ROW LEVEL SECURITY;
ALTER TABLE school_members ENABLE ROW LEVEL SECURITY;
ALTER TABLE teachers ENABLE ROW LEVEL SECURITY;
ALTER TABLE classrooms ENABLE ROW LEVEL SECURITY;
ALTER TABLE course_groups ENABLE ROW LEVEL SECURITY;
ALTER TABLE assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE schedules ENABLE ROW LEVEL SECURITY;
ALTER TABLE schedule_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE schedule_conflicts ENABLE ROW LEVEL SECURITY;
ALTER TABLE teacher_constraints ENABLE ROW LEVEL SECURITY;

-- Helper function: obtener school_id del usuario actual
CREATE OR REPLACE FUNCTION get_user_school_id()
RETURNS UUID AS $$
  SELECT school_id FROM school_members
  WHERE user_id = auth.uid()
  LIMIT 1;
$$ LANGUAGE SQL SECURITY DEFINER STABLE;

-- Helper function: verificar si el usuario es admin de su colegio
CREATE OR REPLACE FUNCTION is_school_admin()
RETURNS BOOLEAN AS $$
  SELECT EXISTS (
    SELECT 1 FROM school_members
    WHERE user_id = auth.uid() AND role = 'school_admin'
  );
$$ LANGUAGE SQL SECURITY DEFINER STABLE;

-- SCHOOLS: cada usuario ve solo su colegio
CREATE POLICY "users_see_own_school" ON schools
  FOR SELECT USING (id = get_user_school_id());

CREATE POLICY "admin_manage_school" ON schools
  FOR ALL USING (id = get_user_school_id() AND is_school_admin());

-- TEACHERS: todos ven los profesores de su colegio
CREATE POLICY "school_members_see_teachers" ON teachers
  FOR SELECT USING (school_id = get_user_school_id());

CREATE POLICY "admin_manage_teachers" ON teachers
  FOR ALL USING (school_id = get_user_school_id() AND is_school_admin());

-- SCHEDULE ENTRIES: profesores solo ven las suyas
CREATE POLICY "teacher_own_entries" ON schedule_entries
  FOR SELECT USING (
    school_id = get_user_school_id()
    AND (
      is_school_admin()
      OR teacher_id = (SELECT id FROM teachers WHERE user_id = auth.uid())
    )
  );

CREATE POLICY "admin_manage_entries" ON schedule_entries
  FOR ALL USING (school_id = get_user_school_id() AND is_school_admin());

-- SCHEDULES: admin gestiona, teacher solo ve published
CREATE POLICY "admin_all_schedules" ON schedules
  FOR ALL USING (school_id = get_user_school_id() AND is_school_admin());

CREATE POLICY "teacher_published_schedule" ON schedules
  FOR SELECT USING (
    school_id = get_user_school_id()
    AND status = 'published'
  );

-- curriculum_templates: oficiales visibles para todos (sin RLS)
-- las personalizadas filtradas por school_id
CREATE POLICY "see_templates" ON curriculum_templates
  FOR SELECT USING (
    is_official = TRUE
    OR school_id = get_user_school_id()
  );

-- ─────────────────────────────────────────────
-- DATOS SEMILLA — Plantilla LOMLOE Madrid 2024
-- ─────────────────────────────────────────────
INSERT INTO curriculum_templates (name, region, stage, is_official) 
VALUES ('LOMLOE Madrid 2024 — Primaria', 'madrid', 'primaria', TRUE)
RETURNING id;

-- Las subject_allocations se insertan con el id devuelto
-- (ver seed script separado: docs/database/seed-lomloe-madrid.sql)
```

---

## Criterios de aceptación

**ESCENARIO 1: Aislamiento entre colegios**
```
Given: El colegio A y el colegio B están registrados
And:   Un profesor del colegio A está autenticado
When:  Consulta schedule_entries
Then:  Solo ve las entradas de su colegio (school_id del colegio A)
And:   No puede ver ni por error los datos del colegio B
```

**ESCENARIO 2: Profesor ve solo su horario**
```
Given: Ana García (teacher) está autenticada en el colegio A
And:   El horario está en estado published
When:  Consulta schedule_entries
Then:  Solo ve las entradas donde teacher_id = su ID
And:   No ve los horarios de otros profesores
```

**ESCENARIO 3: Admin gestiona todo su colegio**
```
Given: El director del colegio A está autenticado
When:  Hace INSERT en schedule_entries para su colegio
Then:  La operación tiene éxito
When:  Intenta INSERT con school_id del colegio B
Then:  Supabase devuelve error de RLS (violación de política)
```

**ESCENARIO 4: Plantilla LOMLOE disponible**
```
Given: Cualquier usuario autenticado
When:  Consulta curriculum_templates WHERE is_official = TRUE
Then:  Ve la plantilla LOMLOE Madrid 2024
And:   Ve sus subject_allocations con las horas correctas
```

---

## Fuera de alcance

- Migración de datos (no hay datos previos)
- Supabase Storage (imágenes de perfil — v2)
- Funciones Edge de Supabase (toda la lógica va en .NET)
