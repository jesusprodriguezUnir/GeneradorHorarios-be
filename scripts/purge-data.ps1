# Script para vaciar los datos del colegio mediante la API
# Deja el colegio, los usuarios y las asignaturas intactos

. "$PSScriptRoot/_common.ps1"

Write-Host "Iniciando purga de datos..." -ForegroundColor Yellow

# 1. Borrar todas las Asignaciones
Write-Host "`n[1/4] Borrando asignaciones..." -ForegroundColor Cyan
$summaries = Invoke-Api -Method GET -Uri "assignments"
$assignmentCount = 0
if ($null -ne $summaries) {
    foreach ($s in $summaries) {
        foreach ($a in $s.assignments) {
            Write-Host "Borrando asignación: $($a.subjectName) -> $($a.groupDisplay) ($($a.teacherName))" -ForegroundColor DarkGray
            Invoke-Api -Method DELETE -Uri "assignments/$($a.id)"
            $assignmentCount++
        }
    }
}
Write-Host "Completado: $assignmentCount asignaciones eliminadas." -ForegroundColor Green

# 2. Borrar todos los Grupos
Write-Host "`n[2/4] Borrando grupos de clase..." -ForegroundColor Cyan
$groups = Invoke-Api -Method GET -Uri "groups"
$groupCount = 0
if ($null -ne $groups) {
    foreach ($g in $groups) {
        Write-Host "Borrando grupo: $($g.displayName)" -ForegroundColor DarkGray
        Invoke-Api -Method DELETE -Uri "groups/$($g.id)"
        $groupCount++
    }
}
Write-Host "Completado: $groupCount grupos eliminados." -ForegroundColor Green

# 3. Borrar todos los Profesores (excepto Laura Fernández para no romper el AppUser)
Write-Host "`n[3/4] Borrando profesores..." -ForegroundColor Cyan
$teachers = Invoke-Api -Method GET -Uri "teachers"
$teacherCount = 0
if ($null -ne $teachers) {
    foreach ($t in $teachers) {
        if ($t.email -eq "laura.fernandez@ceip-miguel-hernandez.es") {
            Write-Host "Preservando profesor del usuario de pruebas: $($t.fullName) ($($t.email))" -ForegroundColor Cyan
            continue
        }
        Write-Host "Borrando profesor: $($t.fullName) ($($t.email))" -ForegroundColor DarkGray
        Invoke-Api -Method DELETE -Uri "teachers/$($t.id)"
        $teacherCount++
    }
}
Write-Host "Completado: $teacherCount profesores eliminados." -ForegroundColor Green

# 4. Borrar todas las Aulas
Write-Host "`n[4/4] Borrando aulas..." -ForegroundColor Cyan
$classrooms = Invoke-Api -Method GET -Uri "classrooms"
$classroomCount = 0
if ($null -ne $classrooms) {
    foreach ($c in $classrooms) {
        Write-Host "Borrando aula: $($c.name) [Type: $($c.classroomType)]" -ForegroundColor DarkGray
        Invoke-Api -Method DELETE -Uri "classrooms/$($c.id)"
        $classroomCount++
    }
}
Write-Host "Completado: $classroomCount aulas eliminadas." -ForegroundColor Green

Write-Host "`n¡Purga de datos finalizada con éxito!" -ForegroundColor Green
