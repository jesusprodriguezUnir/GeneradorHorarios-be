# Lectivo — Manual de Usuario

Bienvenido al manual oficial de **Lectivo**, la plataforma web inteligente diseñada para simplificar y automatizar la compleja tarea de generar horarios escolares en centros de educación infantil y primaria de acuerdo con la normativa LOMLOE (Comunidad de Madrid).

Este manual está diseñado para guiarte en todo el proceso de configuración y generación de horarios, tanto si eres parte del equipo directivo del centro como si eres docente.

---

## 1. Introducción y Conceptos Básicos

### ¿Qué es Lectivo?
Lectivo es un generador inteligente de horarios escolares basado en un motor de búsqueda combinatoria (*backtracking* estructurado). A partir de la definición de tu centro, plantilla de profesores, grupos de alumnos y restricciones horarias, la aplicación genera un horario completo y sin colisiones en menos de 15 minutos, reduciendo semanas de trabajo a solo unos clics.

### Conceptos Clave
* **Etapa Educativa**: Divisiones del centro escolar (Infantil, Primaria, Secundaria). Cada etapa puede tener una estructura de jornada diferente (continua o partida).
* **Ciclo Educativo**: Agrupación de niveles (por ejemplo, en Primaria: 1º Ciclo = 1º y 2º, 2º Ciclo = 3º y 4º, 3º Ciclo = 5º y 6º).
* **Asignación (Assignment)**: Relación que une a un profesor con un grupo de alumnos y una asignatura concreta por un número determinado de horas semanales.
* **Restricción Dura (Hard Constraint)**: Regla inquebrantable (ej. "un profesor no puede estar en dos sitios a la vez").
* **Restricción Blanda (Soft Constraint)**: Preferencias deseables (ej. "intentar que las Matemáticas no se impartan a última hora").

---

## 2. Acceso al Sistema y Roles de Usuario

Lectivo utiliza un sistema de simulación de acceso rápido adaptado para el piloto y desarrollo del producto:

### Roles de Usuario

| Rol | Características | Usuario Demo Preconfigurado |
|---|---|---|
| **Jefatura de Estudios** (Admin) | Control total de la configuración del centro, edición de profesores, asignación de asignaturas y ejecución del motor de generación. | `elena.castro@ceip-miguel-hernandez.es` |
| **Docente** (Teacher) | Acceso limitado exclusivamente a la visualización de su horario individual generado y publicado, optimizado para móvil. | `laura.fernandez@ceip-miguel-hernandez.es` |

> [!NOTE]
> Para cambiar entre usuarios durante el piloto, accede a la interfaz de Angular y haz clic en el selector de perfiles de la barra superior.

---

## 3. Configuración Inicial del Centro

Antes de poder generar horarios, la jefatura de estudios debe configurar la estructura básica del colegio. Este proceso se gestiona en la pantalla de **Configuración**.

```
[Configuración] ──> [1. Plantilla LOMLOE] ──> [2. Profesores] ──> [3. Aulas] ──> [4. Grupos]
```

### Paso 1: Configurar la Jornada Escolar
Permite ajustar los parámetros globales del colegio. En la cabecera del panel de configuración podrás definir:
* **Tipo de jornada**: Continua (ej. mañana) o partida (mañana y tarde).
* **Franja horaria**: Hora de inicio de las clases (por ejemplo, 09:00).
* **Estructura de slots**: Número de horas de clase al día, duración de cada hora (por ejemplo, 60 minutos) y número de días laborables por semana (generalmente 5 días, de lunes a viernes).
* **Recreos**: Define tras qué sesión ocurre el recreo (por ejemplo, tras la 2ª sesión) y su duración (30 minutos). El motor de horarios omitirá estas franjas automáticamente.

### Paso 2: Importar la Plantilla LOMLOE Oficial
Para evitar introducir manualmente todas las asignaturas y sus límites de horas oficiales, pulsa el botón **Clonar Plantilla Oficial LOMLOE**.
Esto creará de forma automática asignaturas como:
* Lengua Castellana y Literatura
* Matemáticas
* Lengua Extranjera (Inglés)
* Educación Física (requiere aula especial: Gimnasio)
* Educación Artística (Plástica y Música)
* Ciencias de la Naturaleza / Ciencias Sociales
* Religión / Valores Cívicos
* Tutoría / Atención Educativa

> [!TIP]
> Puedes ajustar las horas por defecto de cada asignatura asignadas al centro, siempre que se mantengan dentro del rango de mínimos y máximos establecidos por la consejería de educación.

### Paso 3: Registrar Profesores y Especialidades
Añade a los docentes del centro en la pestaña **Profesores**:
1. Pulsa **Nuevo Profesor**.
2. Rellena el nombre, correo electrónico y tipo de plaza (definitivo, interino).
3. **Especialidades**: Marca las especialidades que el docente está habilitado para impartir (por ejemplo, "Inglés", "Música", "Educación Física").
4. **Límites de Carga**: Asigna el máximo de horas semanales que puede impartir (por defecto 25h) y el número máximo de horas consecutivas diarias que puede dar clase sin descanso.
5. Elige un color identificativo para facilitar la visualización del horario.

### Paso 4: Dar de Alta Aulas y Espacios
Registra las aulas en la pestaña **Aulas**:
* **Aulas Regulares**: Aulas asignadas por defecto a un grupo específico (ej. "Aula 1ºA").
* **Aulas Especiales / Compartidas**: Espacios comunes requeridos por asignaturas específicas (ej. "Gimnasio", "Aula de Música", "Laboratorio"). Marca la opción **Compartida** para asegurar que el motor controle que no se solapen dos grupos diferentes en el mismo espacio.

### Paso 5: Crear los Grupos de Alumnos
En la pestaña **Grupos**, da de alta cada unidad escolar:
* Define el nivel (1º a 6º de Primaria) y la letra del grupo (ej. "A", "B").
* Selecciona al tutor/a del grupo (fundamental para que el motor asigne las horas de Tutoría).
* Asigna su **Aula habitual** para que el motor coloque allí todas las clases normales del grupo.

---

## 4. Gestión de Asignaciones (Assignments)

Una vez creados los recursos, la jefatura debe definir **quién imparte qué y a quién**. Esto se realiza mediante el cuadro de **Asignaciones**:

1. En la tabla de asignaciones, selecciona un Grupo (ej. "3ºA").
2. Elige una Asignatura (ej. "Matemáticas").
3. Selecciona un Profesor del listado de docentes disponibles.
4. Define el número de **Horas semanales** de esa asignatura para el grupo (ej. 5 horas semanales, lo que equivaldrá a 1 hora diaria).
5. Pulsa **Guardar Asignación**.

> [!WARNING]
> Si la asignatura requiere de un especialista (por ejemplo, Educación Física), el selector de profesores filtrará automáticamente y solo te permitirá seleccionar docentes que tengan marcada la especialidad de "Educación Física" en su ficha.

---

## 5. El Asistente de Generación (Wizard de 4 Pasos)

Con todos los datos cargados, haz clic en la sección **Generador** del menú lateral para acceder al asistente inteligente estructurado en 4 etapas:

```
[1. Diagnóstico] ──> [2. Disponibilidad] ──> [3. Reglas Especiales] ──> [4. Generación]
```

### Paso 1: Diagnóstico de Viabilidad
En esta pantalla el sistema realiza un chequeo previo (*pre-flight validation*). El sistema sumará las horas de las asignaciones y las comparará con la capacidad de los profesores y los slots de las aulas. 
* Si hay una inconsistencia matemática (ej. un profesor tiene asignadas 28 horas lectivas pero su contrato indica un máximo de 25), el sistema mostrará un mensaje de error detallado indicando qué debes ajustar antes de poder continuar.

### Paso 2: Disponibilidades del Profesorado
Permite configurar el calendario de exclusiones de los docentes:
* Selecciona un profesor de la lista.
* Haz clic sobre las celdas del horario semanal para bloquearlas (se pintarán en rojo). Las celdas bloqueadas representan horas en las que el profesor **no está disponible** (por ejemplo, reducción de jornada los viernes a última hora, o reuniones externas los martes por la mañana).
* El motor evitará a toda costa colocar clases en las celdas bloqueadas.

### Paso 3: Reglas Especiales y Asignaturas Intensivas
Configura preferencias pedagógicas para mejorar la calidad del horario:
* **Materias Intensivas**: Marca asignaturas como Matemáticas o Lengua para que el motor intente colocarlas preferentemente en las primeras horas del día, cuando el rendimiento cognitivo de los alumnos es mayor.
* **Bloques Consecutivos (Splittable)**: Activa la preferencia para que asignaturas con alta carga práctica (como Educación Física o Talleres Artísticos) se impartan en bloques de 2 horas seguidas en lugar de divididas en días sueltos.

### Paso 4: Generación y Progreso en Tiempo Real
1. Revisa el resumen de la configuración.
2. Haz clic en el botón **Generar horario**.
3. El sistema cambiará a la pantalla de ejecución. A través de una conexión de red en tiempo real (SignalR), verás una barra de progreso que indica el porcentaje de sesiones asignadas en tiempo real.
4. **Resultados**:
   * **Completo (Status: Complete)**: El horario se generó sin ningún conflicto. Se te redirigirá a la rejilla de resultados.
   * **Parcial (Status: Partial)**: Debido a restricciones demasiado estrictas, el motor no pudo colocar el 100% de las sesiones antes de alcanzar el límite de tiempo de 30 segundos. La rejilla se abrirá mostrando las sesiones colocadas (sin conflictos entre ellas) y un listado detallado de **Conflictos** con explicaciones en lenguaje natural para ayudarte a corregirlo.

---

## 6. El Editor de Horarios y Gestión de Conflictos

### Interpretación de Conflictos
Si el horario es parcial, en la parte inferior verás un panel informativo de colisiones:
* *Ejemplo*: "El profesor Juan Gómez tiene un conflicto de solapamiento en el slot del lunes a las 09:00 porque ya está asignado al grupo 5ºB."
* *Ejemplo*: "El Gimnasio está doblemente reservado en la sesión del miércoles a las 11:30."

### Edición Manual por Drag & Drop
Puedes realizar ajustes manuales directamente sobre la rejilla visual del horario:
1. Haz clic sobre una asignatura colocada y arrástrala hacia una celda vacía o sobre otra asignatura para intercambiarlas.
2. Si realizas un movimiento inválido (que viole una restricción dura, como solapar a un profesor), la celda de destino se marcará en rojo con una advertencia visual inmediata.
3. El editor te permite tomar decisiones de compromiso cuando es imposible resolverlo automáticamente.

### Publicación
Una vez que el horario esté a tu gusto y libre de colisiones críticas, haz clic en el botón **Publicar Horario** en la barra superior. Esto activará el horario oficial y actualizará el acceso de toda la comunidad educativa.

---

## 7. Vista del Profesor (Mi Horario)

Cuando un docente inicia sesión con su correo (por ejemplo, Laura Fernández):
* El sistema le muestra directamente la pantalla **Mi Horario**.
* Esta pantalla es un panel limpio que muestra exclusivamente su horario de clases de lunes a viernes.
* **Diseño Responsivo Móvil**: Pensado para que los profesores consulten su horario en tiempo real desde el teléfono móvil dentro del aula. En pantallas pequeñas, el horario se transforma en un carrusel interactivo día a día (Lunes, Martes, Miércoles...) para facilitar la navegación táctil sin necesidad de hacer zoom horizontal en la rejilla.

---

## 8. Exportación e Impresión

Lectivo cuenta con una optimización específica de maquetación para impresión física:
* Haz clic en el icono de **Imprimir / Guardar en PDF** del menú lateral de la rejilla.
* Se abrirá el diálogo nativo de impresión de tu sistema operativo.
* El diseño de la aplicación aplicará estilos limpios en papel:
  - Ocultará el menú lateral y las barras de control.
  - Ajustará el ancho de la rejilla para ocupar exactamente el espacio A4 en modo apaisado (horizontal).
  - Mantendrá los colores de las asignaturas legibles sobre fondo blanco y forzará textos negros para una impresión óptima en impresoras de inyección o láser.
