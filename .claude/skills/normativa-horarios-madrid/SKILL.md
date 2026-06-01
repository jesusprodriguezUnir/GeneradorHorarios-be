# Skill: Normativa de Horarios Escolares — Comunidad de Madrid

## Cuándo aplicar esta skill

Aplica las reglas de este archivo cuando trabajes en **cualquiera** de los siguientes contextos:

- Modificar el seed de datos (`DbInitializer.cs`, `SeedOptions.cs`)
- Cambiar las constantes de `LomloeMadrid.cs`
- Ajustar el validador normativo (`INormativeValidator`, `NormativeValidator`)
- Configurar la jornada de un centro (`School.ScheduleType`, `SlotsPerDay`, `BreakMinutes`…)
- Diseñar o revisar las `SubjectAllocation` de primaria de Madrid
- Revisar las constraints del motor de generación que afecten a horas mínimas o recreo

## Marco normativo vigente

| Norma | Descripción |
|-------|-------------|
| **Decreto 61/2022**, de 13 de julio (BOCM 14 jul 2022) | Ordenación y currículo de Educación Primaria en la CAM. Anexo IV: distribución horaria. |
| **Decreto 94/2025**, de 23 de diciembre (BOCM 26 dic 2025) | Regula la jornada escolar en Infantil y Primaria de la CAM. |
| **Orden 130/2023**, de 23 de enero (BOCM 30 ene 2023) | Organización y funcionamiento de centros de Primaria. |

## Reglas clave

### Jornada lectiva

- **Mínimo lectivo semanal:** 22,5 horas (sin contar el recreo).
- **Recreo mínimo diario:** 30 minutos/día → 2,5 h/semana adicionales.
- **Total presencia mínima:** 25 h/semana (22,5 lectivo + 2,5 recreo).
- La jornada puede ser **continua** (mañana) o **partida** (mañana + tarde).
- Los centros tienen autonomía para elegir la modalidad dentro del marco legal.

### Distribución horaria por área (Decreto 61/2022, Anexo IV)

Horas semanales por grupo, aplicables a todos los cursos (1º–6º) de primaria:

| Área | Clave | Min | Max | Recom. estándar | Recom. bilingüe | Especialista | Aula |
|------|-------|-----|-----|-----------------|-----------------|--------------|------|
| Lengua Castellana y Literatura | `len` | 4 | 6 | 5 | 5 | No | — |
| Matemáticas | `mat` | 4 | 6 | 5 | 5 | No | — |
| Conocimiento del Medio | `cie` | 3 | 4 | 3 | 3 | No | — |
| 1ª Lengua Extranjera (Inglés) | `ing` | 3 | 5 | 4 | 5 | Sí | — |
| Educación Física | `ef` | 2 | 3 | 3 | 3 | Sí | gym |
| Educación Artística — Música | `mus` | 1 | 2 | 1 | 1 | Sí | music |
| Educación Artística — Plástica | `art` | 1 | 2 | 2 | 2 | No | — |
| Religión / Valores Sociales y Cívicos | `rel` | 1 | 2 | 1 | 1 | No | — |
| Libre Configuración del Centro | `tut` | 0 | 2 | 1 | 0 | No | — |
| **Total** | | | | **25 h** | **25 h** | | |

> **Nota:** El total de 25 h es la capacidad máxima de la rejilla (5 tramos × 60 min × 5 días).
> El mínimo legal son 22,5 h. El recreo (30 min/día) no computa como hora lectiva.

### Autonomía del centro

Dentro de los márgenes Min–Max, cada colegio puede adaptar:
- Número de horas por área (respetando mínimos y sin superar máximos).
- Distribución de la Libre Configuración (0–2 h).
- Tipo de jornada (continua / partida).
- Modalidad lingüística (estándar / bilingüe inglés).

## Impacto en el código

La fuente única de las cifras legales es:
- **`apps/api/Domain/Normative/LomloeMadrid.cs`** — constantes y tabla por área.
- **`apps/api/Infrastructure/Normative/NormativeValidator.cs`** — comprobaciones automáticas.

Si descubres que una cifra de esta skill difiere de la normativa oficial (BOCM),
actualiza **primero** `LomloeMadrid.cs` y **después** este archivo para mantener
la coherencia. Ver `reference/fuentes.md` para los enlaces oficiales.

## Dimensionamiento de profesores para 6×3 líneas

Para un colegio de 6 cursos × 3 líneas = 18 grupos (modalidad estándar):

| Rol | Nº | Horas/semana |
|-----|----|-------------|
| Tutores generalistas (1 por grupo) | 18 | ~15 h cada uno |
| Especialistas Inglés | 3 | ~24 h (6 grupos × 4 h) |
| Especialistas EF | 3 | ~18 h (6 grupos × 3 h) |
| Especialista Música | 1 | 18 h |
| Especialista Religión | 1 | 18 h |
| **Total** | **26** | |

Para bilingüe: 5 especialistas de Inglés (≤20 h cada uno).

## Recursos de referencia

Ver `reference/fuentes.md` para los enlaces oficiales.
