-- ─────────────────────────────────────────────────────────────────────────────
-- SEED: Plantilla LOMLOE Madrid 2024 — Primaria
-- Fuente: Decreto 82/2022 + Resolución de 11 de julio de 2022 (CAM)
-- Ejecutar DESPUÉS del schema de SPEC-003
-- ─────────────────────────────────────────────────────────────────────────────

DO $$
DECLARE
  template_id UUID;
BEGIN

INSERT INTO curriculum_templates (name, region, stage, is_official)
VALUES ('LOMLOE Madrid 2024 — Primaria', 'madrid', 'primaria', TRUE)
RETURNING id INTO template_id;

-- ── Áreas troncales ───────────────────────────────────────────────────────────

INSERT INTO subject_allocations (
  template_id, subject_name,
  weekly_hours_min, weekly_hours_max, weekly_hours_default,
  requires_specialist, required_classroom_type, max_consecutive_slots, splittable_across_days
) VALUES

-- Lengua Castellana y Literatura
(template_id, 'Lengua Castellana y Literatura', 4, 6, 5,
 FALSE, NULL, 2, TRUE),

-- Matemáticas
(template_id, 'Matemáticas', 4, 6, 5,
 FALSE, NULL, 2, TRUE),

-- Conocimiento del Medio Natural, Social y Cultural
(template_id, 'Conocimiento del Medio', 3, 4, 3,
 FALSE, NULL, 2, TRUE),

-- Lengua Extranjera (Inglés)
(template_id, 'Inglés', 3, 5, 4,
 TRUE, NULL, 2, TRUE),  -- requiere especialista en muchos colegios

-- ── Áreas específicas ─────────────────────────────────────────────────────────

-- Educación Física
(template_id, 'Educación Física', 2, 3, 3,
 TRUE, 'gym', 1, TRUE),  -- especialista, requiere gimnasio, no consecutivas

-- Educación Artística: Música
(template_id, 'Música', 1, 2, 1,
 TRUE, 'music', 1, FALSE),  -- especialista, requiere aula de música

-- Educación Artística: Plástica y Visual
(template_id, 'Plástica y Educación Visual', 1, 2, 2,
 FALSE, NULL, 2, TRUE),

-- ── Religión / Valores Sociales y Cívicos (optativa) ─────────────────────────

(template_id, 'Religión / Valores Sociales y Cívicos', 1, 2, 1,
 FALSE, NULL, 1, FALSE),

-- ── Segunda Lengua Extranjera (optativa según colegio) ───────────────────────

(template_id, 'Segunda Lengua Extranjera', 1, 2, 1,
 TRUE, NULL, 1, FALSE),

-- ── Horas de libre configuración del centro ──────────────────────────────────

(template_id, 'Libre Configuración del Centro', 0, 2, 1,
 FALSE, NULL, 2, TRUE);

RAISE NOTICE 'Plantilla LOMLOE Madrid 2024 insertada con id: %', template_id;

END $$;

-- ─────────────────────────────────────────────────────────────────────────────
-- VERIFICACIÓN
-- ─────────────────────────────────────────────────────────────────────────────

SELECT
  ct.name as plantilla,
  sa.subject_name as asignatura,
  sa.weekly_hours_default as horas_semana,
  sa.requires_specialist as especialista,
  sa.required_classroom_type as aula_especial
FROM curriculum_templates ct
JOIN subject_allocations sa ON sa.template_id = ct.id
WHERE ct.is_official = TRUE
ORDER BY sa.weekly_hours_default DESC;
