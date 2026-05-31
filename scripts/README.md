# Manual de Gestión y Sembrado del Entorno de Pruebas (Lectivo)

Este directorio contiene una suite de scripts PowerShell diseñados para automatizar la construcción, destrucción y siembra de un **juego de datos realista de 18 grupos** (6 cursos × 3 líneas) adaptado al perfil de un colegio concertado de la Comunidad de Madrid con jornada partida (LOMLOE).

## Requisitos Previos

Asegúrate de cumplir con las siguientes herramientas instaladas en tu máquina de desarrollo (Windows):
1. **PowerShell Core (pwsh):** Versión 7 o superior.
2. **Docker Desktop:** Ejecutándose con soporte para contenedores Linux.
3. **.NET 8.0 SDK o superior:** Necesario para compilar y arrancar la API localmente.

---

## Descripción de los Scripts

El suite consta de los siguientes ficheros:

| Script | Propósito | Operación |
| :--- | :--- | :--- |
| `_common.ps1` | Módulo interno compartido | Centraliza la configuración de la API (`http://localhost:5000/api`) y define helpers como `Invoke-Api` y `Wait-ForApi`. **No se ejecuta directamente**. |
| `purge-data.ps1` | Purga limpia de datos | Elimina mediante API REST todas las asignaciones, grupos, aulas y profesores (preservando la cuenta de `laura.fernandez` para no romper el usuario de pruebas). |
| `seed-dataset.ps1` | Siembra de 18 grupos | Limpia la base de datos, reconfigura el centro a Concertado Jornada Partida, inyecta 22 aulas, 27 profesores y asigna 25 horas semanales a cada uno de los 18 grupos de manera balanceada. |
| `reset-db.ps1` | Reinicio duro del contenedor | Hace un `docker compose down -v` para borrar el volumen de SQL Server, levanta uno limpio y espera a que el contenedor esté saludable (`healthy`). |
| `rebuild-all.ps1` | Orquestador completo | Ejecuta el ciclo completo: reinicia la base de datos, arranca la API en una ventana externa, espera a que esté lista y siembra el juego de datos de 18 grupos. |

---

## Guía de Uso Rápido

### 1. Reconstrucción Completa (Flujo Recomendado)

Para levantar un entorno totalmente limpio desde cero, ejecuta el orquestador:

```powershell
pwsh scripts/rebuild-all.ps1
```

**¿Qué hace este comando automáticamente por ti?**
1. Apaga y destruye el contenedor y los datos previos de SQL Server en Docker.
2. Levanta un nuevo SQL Server limpio y espera su estado saludable.
3. Abre una terminal externa y lanza la API en background con `dotnet run`.
4. Monitorea y espera a que la API responda y aplique las migraciones de Entity Framework Core de forma automática.
5. Inyecta el juego de datos de 18 grupos y reconfigura el centro escolar.

---

### 2. Ciclos Rápidos de Siembra (Sin reiniciar base de datos)

Si estás modificando código de la API y quieres volver a probar el generador con datos limpios sin necesidad de borrar los contenedores Docker, ejecuta:

```powershell
pwsh scripts/seed-dataset.ps1
```

Este script es **idempotente**: limpia ordenadamente los grupos, profesores y asignaciones previos creados por la API antes de volver a sembrar las 18 líneas.

---

### 3. Purga Total de Entidades

Si quieres dejar el colegio completamente vacío (solo con el administrador, Laura Fernández y las asignaturas base oficiales de la LOMLOE), ejecuta:

```powershell
pwsh scripts/purge-data.ps1
```

---

## Detalles del Juego de Datos (18 Grupos)

El dataset generado en `seed-dataset.ps1` tiene las siguientes características realistas:
- **Colegio:** Colegio Concertado Santa María (Madrid). Configurado con jornada partida (`MorningStart = "09:00"`, `SlotMinutes = 60`, `BreakAfterSlot = 2`, `BreakMinutes = 30`).
- **Aulas:** 18 Aulas ordinarias (`Aula 1ºA` a `Aula 6ºC`) + 4 Aulas Especiales compartidas (`Gimnasio`, `Aula de Música`, `Aula de Plástica` e `Informática`).
- **Grupos:** 6 Cursos (1º a 6º) con 3 líneas (A, B y C). Cada grupo tiene entre 22 y 28 estudiantes asignados aleatoriamente.
- **Profesorado:**
  - **18 Tutores ordinarios** (`Tutor 1ºA` a `Tutor 6ºC`), dedicados a impartir 15 horas lectivas de materias generalistas a sus propios grupos (Lengua Castellana [5h], Matemáticas [5h], Conocimiento del Medio [3h], Plástica [2h]).
  - **3 Especialistas de Inglés**, cada uno encargado de 6 grupos (24 horas lectivas semanales).
  - **3 Especialistas de Educación Física**, cada uno encargado de 6 grupos (18 horas lectivas semanales).
  - **1 Especialista de Música** que atiende a los 18 grupos en el Aula de Música (18 horas semanales).
  - **1 Especialista de Religión** que atiende a los 18 grupos (18 horas semanales).
  - **1 Especialista de Segunda Lengua Extranjera** que atiende a los 18 grupos (18 horas semanales).

### Carga Horaria y Solubilidad
Cada grupo tiene exactamente **25 horas semanales** de clase. El reparto de especialistas ha sido diseñado mediante un algoritmo equilibrado para evitar sobrecargar a ningún docente por encima de su límite máximo de 25 horas. Este juego de datos es **100% soluble** por el motor de backtracking de Lectivo.

---

## Consideraciones Técnicas y Limitaciones

> [!WARNING]
> **Preservación de Cuenta:** La profesora `laura.fernandez@ceip-miguel-hernandez.es` no se elimina en `purge-data.ps1` porque su ID está fuertemente acoplado a su cuenta de usuario de pruebas. En su lugar, el script la reutiliza asignándole la tutoría de **3ºA** de forma transparente.

> [!NOTE]
> **Rejilla del Motor:** Aunque el centro se configura en jornada partida, la lógica de cálculo horaria actual del motor de Lectivo (`SlotCalculator.Compute`) limita la rejilla horaria de generación a 5 slots lectivos continuos al día. La partición de tarde se habilitará en futuras iteraciones del generador.
