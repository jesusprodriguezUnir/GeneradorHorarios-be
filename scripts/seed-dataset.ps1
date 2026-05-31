# Script principal para sembrar el juego de datos de 18 grupos
# Consecución: concertado, jornada partida, LOMLOE Madrid

. "$PSScriptRoot/_common.ps1"

Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host "      LECTIVO: CONSTRUCCIÓN DEL JUEGO DE DATOS DE 18 GRUPOS" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta

# 1. Esperar a que la API responda
Wait-ForApi

# 2. Purgar datos previos antes de sembrar
Write-Host "`nLimpiando datos previos antes de sembrar..." -ForegroundColor Yellow
& pwsh -NoProfile -File "$PSScriptRoot/purge-data.ps1"
if ($LASTEXITCODE -ne 0) { throw "La purga previa falló con código $LASTEXITCODE." }

# 3. Reconfigurar el colegio a concierto y jornada partida
Write-Host "`nReconfigurando colegio a 'Jornada Partida'..." -ForegroundColor Cyan
$schoolUpdate = @{
    Name           = "Colegio Concertado Santa María (Madrid)"
    ScheduleType   = "partida"
    MorningStart   = "09:00"
    SlotMinutes    = 60
    BreakAfterSlot = 2
    BreakMinutes   = 30
}
$school = Invoke-Api -Method PUT -Uri "schools/me" -Body $schoolUpdate
Write-Host "Colegio configurado: $($school.name) [Tipo: $($school.scheduleType)]" -ForegroundColor Green

# 4. Obtener las Asignaturas existentes y mapear sus IDs
Write-Host "`nMapeando asignaturas de la plantilla oficial..." -ForegroundColor Cyan
$subjects = Invoke-Api -Method GET -Uri "subjects"
$subjectMap = @{}
foreach ($s in $subjects) {
    $name = $s.subjectName
    if ($name -like "*Lengua Castellana*") { $subjectMap["len"] = $s.id }
    elseif ($name -like "*Matemáticas*") { $subjectMap["mat"] = $s.id }
    elseif ($name -like "*Conocimiento del Medio*") { $subjectMap["cie"] = $s.id }
    elseif ($name -like "*Inglés*") { $subjectMap["ing"] = $s.id }
    elseif ($name -like "*Educación Física*") { $subjectMap["ef"] = $s.id }
    elseif ($name -like "*Música*") { $subjectMap["mus"] = $s.id }
    elseif ($name -like "*Plástica*") { $subjectMap["art"] = $s.id }
    elseif ($name -like "*Religión*") { $subjectMap["rel"] = $s.id }
    elseif ($name -like "*Segunda Lengua*") { $subjectMap["l2"] = $s.id }
    elseif ($name -like "*Libre Configuración*") { $subjectMap["lib"] = $s.id }
}

Write-Host "Asignaturas mapeadas con éxito:" -ForegroundColor Green
$subjectMap.Keys | ForEach-Object { Write-Host "  - $_ -> $($subjectMap[$_])" -ForegroundColor DarkGray }

# 5. Crear Aulas
Write-Host "`nCreando aulas en el centro..." -ForegroundColor Cyan
$classroomsMap = @{} # Mapea "levelLine" -> ClassroomId
$specialClassrooms = @{}

# Aulas especiales
$gymRes = Invoke-Api -Method POST -Uri "classrooms" -Body @{ Name = "Gimnasio"; ClassroomType = "gym"; Capacity = 50; IsShared = $true }
$specialClassrooms["gym"] = $gymRes.id

$musicRes = Invoke-Api -Method POST -Uri "classrooms" -Body @{ Name = "Aula de Música"; ClassroomType = "music"; Capacity = 30; IsShared = $true }
$specialClassrooms["music"] = $musicRes.id

$artRes = Invoke-Api -Method POST -Uri "classrooms" -Body @{ Name = "Aula de Plástica (Taller)"; ClassroomType = "regular"; Capacity = 30; IsShared = $true }
$specialClassrooms["art"] = $artRes.id

$itRes = Invoke-Api -Method POST -Uri "classrooms" -Body @{ Name = "Aula de Informática"; ClassroomType = "it"; Capacity = 30; IsShared = $true }
$specialClassrooms["it"] = $itRes.id

Write-Host "Aulas especiales creadas: Gimnasio, Música, Plástica e Informática." -ForegroundColor Green

# 18 aulas de tutoría (1ºA a 6ºC)
$aulaCount = 0
for ($level = 1; $level -le 6; $level++) {
    foreach ($line in @("A", "B", "C")) {
        $name = "Aula ${level}º${line}"
        $res = Invoke-Api -Method POST -Uri "classrooms" -Body @{ Name = $name; ClassroomType = "regular"; Capacity = 28; IsShared = $false }
        $classroomsMap["$level$line"] = $res.id
        $aulaCount++
    }
}
Write-Host "$aulaCount aulas de clase creadas con éxito." -ForegroundColor Green

# 6. Mapear y Crear Profesores
Write-Host "`nCreando personal docente..." -ForegroundColor Cyan

# Comprobar si existe Laura Fernández para no duplicar y reutilizarla
$teachersList = Invoke-Api -Method GET -Uri "teachers"
$lauraTeacher = $null
foreach ($t in $teachersList) {
    if ($t.email -eq "laura.fernandez@ceip-miguel-hernandez.es") {
        $lauraTeacher = $t
        break
    }
}

$tutorsMap = @{} # Mapea "levelLine" -> TeacherId
$tutorCreatedCount = 0

for ($level = 1; $level -le 6; $level++) {
    foreach ($line in @("A", "B", "C")) {
        $key = "$level$line"
        if ($key -eq "3A" -and $null -ne $lauraTeacher) {
            $tutorsMap[$key] = $lauraTeacher.id
            Write-Host "Reutilizando a Laura Fernández como tutora de 3ºA." -ForegroundColor DarkCyan
            continue
        }

        $name = "Tutor ${level}º${line}"
        $email = "tutor.$(($level.ToString() + $line).ToLower())@santa-maria.es"
        # Paletas de color representativas
        $color = if ($level -le 2) { "len" } elseif ($level -le 4) { "mat" } else { "cie" }
        
        $body = @{
            FullName = $name
            Email = $email
            TeacherType = "definitivo"
            MaxWeeklyHours = 25
            Specialties = @("Generalista")
            ColorKey = $color
        }
        $res = Invoke-Api -Method POST -Uri "teachers" -Body $body
        $tutorsMap[$key] = $res.id
        $tutorCreatedCount++
    }
}
Write-Host "Se crearon $tutorCreatedCount tutores de aula." -ForegroundColor Green

# Crear especialistas balanceados
Write-Host "Creando especialistas..." -ForegroundColor DarkCyan

# 3 de Inglés
$englishTeachers = @()
for ($i = 1; $i -le 3; $i++) {
    $res = Invoke-Api -Method POST -Uri "teachers" -Body @{
        FullName = "Especialista Inglés $i"
        Email = "english$i@santa-maria.es"
        TeacherType = "especialista"
        MaxWeeklyHours = 25
        Specialties = @("Inglés")
        ColorKey = "ing"
    }
    $englishTeachers += $res.id
}

# 3 de Educación Física
$efTeachers = @()
for ($i = 1; $i -le 3; $i++) {
    $res = Invoke-Api -Method POST -Uri "teachers" -Body @{
        FullName = "Especialista Ed. Física $i"
        Email = "ef$i@santa-maria.es"
        TeacherType = "especialista"
        MaxWeeklyHours = 25
        Specialties = @("Educación Física")
        ColorKey = "ef"
    }
    $efTeachers += $res.id
}

# 1 de Música
$musRes = Invoke-Api -Method POST -Uri "teachers" -Body @{
    FullName = "Especialista Música"
    Email = "music1@santa-maria.es"
    TeacherType = "especialista"
    MaxWeeklyHours = 25
    Specialties = @("Música")
    ColorKey = "mus"
}
$musicTeacher = $musRes.id

# 1 de Religión
$relRes = Invoke-Api -Method POST -Uri "teachers" -Body @{
    FullName = "Especialista Religión"
    Email = "religion1@santa-maria.es"
    TeacherType = "especialista"
    MaxWeeklyHours = 25
    Specialties = @("Religión")
    ColorKey = "rel"
}
$religionTeacher = $relRes.id

# 1 de 2ª Lengua (Francés / Alemán)
$l2Res = Invoke-Api -Method POST -Uri "teachers" -Body @{
    FullName = "Especialista 2ª Lengua"
    Email = "l2_1@santa-maria.es"
    TeacherType = "especialista"
    MaxWeeklyHours = 25
    Specialties = @("Segunda Lengua")
    ColorKey = "ing"
}
$l2Teacher = $l2Res.id

Write-Host "9 especialistas de materias creados y balanceados." -ForegroundColor Green

# 7. Crear los 18 Grupos de Alumnos
Write-Host "`nCreando grupos de alumnos (Líneas A, B y C)..." -ForegroundColor Cyan
$groupsMap = @{} # Mapea "levelLine" -> GroupId

for ($level = 1; $level -le 6; $level++) {
    foreach ($line in @("A", "B", "C")) {
        $key = "$level$line"
        $studentCount = Get-Random -Minimum 22 -Maximum 28
        $body = @{
            CourseLevel = $level
            GroupLabel = $line
            StudentCount = $studentCount
            TutorId = $tutorsMap[$key]
            HomeClassroomId = $classroomsMap[$key]
        }
        $res = Invoke-Api -Method POST -Uri "groups" -Body $body
        $groupsMap[$key] = $res.id
    }
}
Write-Host "18 grupos de alumnos creados correctamente." -ForegroundColor Green

# 8. Crear Asignaciones de Horas Semanales (25 horas por grupo)
Write-Host "`nRepartiendo asignaturas y creando asignaciones semanales..." -ForegroundColor Cyan
$assignmentCount = 0

for ($level = 1; $level -le 6; $level++) {
    foreach ($line in @("A", "B", "C")) {
        $key = "$level$line"
        $groupId = $groupsMap[$key]
        $tutorId = $tutorsMap[$key]

        # ── 1. Asignaciones Troncales del Tutor (15 horas en total) ──
        # Lengua Castellana: 5 horas
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $tutorId; GroupId = $groupId; AllocationId = $subjectMap["len"]; WeeklyHours = 5 } | Out-Null
        # Matemáticas: 5 horas
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $tutorId; GroupId = $groupId; AllocationId = $subjectMap["mat"]; WeeklyHours = 5 } | Out-Null
        # Conocimiento del Medio: 3 horas
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $tutorId; GroupId = $groupId; AllocationId = $subjectMap["cie"]; WeeklyHours = 3 } | Out-Null
        # Plástica: 2 horas
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $tutorId; GroupId = $groupId; AllocationId = $subjectMap["art"]; WeeklyHours = 2 } | Out-Null

        # ── 2. Asignaciones de Especialistas (10 horas en total) ──
        # Inglés: 4 horas (balanceado entre los 3 especialistas de inglés: 6 grupos cada uno, 24 horas semanales)
        $englishId = if ($level -le 2) { $englishTeachers[0] } elseif ($level -le 4) { $englishTeachers[1] } else { $englishTeachers[2] }
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $englishId; GroupId = $groupId; AllocationId = $subjectMap["ing"]; WeeklyHours = 4 } | Out-Null

        # Ed. Física: 3 horas (balanceado entre los 3 especialistas de EF: 6 grupos cada uno, 18 horas semanales)
        $efId = if ($level -le 2) { $efTeachers[0] } elseif ($level -le 4) { $efTeachers[1] } else { $efTeachers[2] }
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $efId; GroupId = $groupId; AllocationId = $subjectMap["ef"]; WeeklyHours = 3 } | Out-Null

        # Música: 1 hora (18 horas totales para el profesor de música)
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $musicTeacher; GroupId = $groupId; AllocationId = $subjectMap["mus"]; WeeklyHours = 1 } | Out-Null

        # Religión: 1 hora (18 horas totales para el profesor de religión)
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $religionTeacher; GroupId = $groupId; AllocationId = $subjectMap["rel"]; WeeklyHours = 1 } | Out-Null

        # Segunda Lengua Extranjera: 1 hora (18 horas totales para el profesor de 2ª lengua)
        Invoke-Api -Method POST -Uri "assignments" -Body @{ TeacherId = $l2Teacher; GroupId = $groupId; AllocationId = $subjectMap["l2"]; WeeklyHours = 1 } | Out-Null

        $assignmentCount += 9
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " ¡SEMbrado Completado con Éxito!" -ForegroundColor Green
Write-Host " Resumen de datos inyectados:" -ForegroundColor Green
Write-Host "   - 1 Colegio Concertado de Jornada Partida" -ForegroundColor Green
Write-Host "   - 22 Aulas (18 de Tutoría + 4 Especiales)" -ForegroundColor Green
Write-Host "   - 27 Profesores (18 Tutores + 9 Especialistas)" -ForegroundColor Green
Write-Host "   - 18 Grupos (1ºA a 6ºC, líneas de 3)" -ForegroundColor Green
Write-Host "   - $assignmentCount Asignaciones (25h por grupo de alumnos)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
