# Fuentes legales — Horarios Primaria en Madrid

## Normativa principal

| Norma | Enlace | Estado |
|-------|--------|--------|
| **Decreto 61/2022** — Currículo Primaria CAM | https://gestiona.comunidad.madrid/wleg_pub/servlet/Servidor?opcion=VerHtml&nmnorma=10249 | Vigente |
| **Decreto 94/2025** — Jornada escolar | https://gestiona.comunidad.madrid/wleg_pub/secure/normativas/contenidoNormativa.jsf?nmnorma=8041 | Vigente desde ene 2026 |
| **Orden 130/2023** — Organización centros primaria | https://www.bocm.es/boletin/CM_Orden_BOCM/2023/01/30/BOCM-20230130-23.PDF | Vigente |

## Página informativa CAM

- Regulación Educación Primaria: https://www.comunidad.madrid/servicios/educacion/regulacion-educacion-primaria
- Modificación horaria Primaria (trámite): https://sede.comunidad.madrid/autorizaciones-licencias-permisos-carnes/modificacion-horaria-educacion-primaria

## Referencia para el proyecto

Las cifras horarias del Decreto 61/2022 que usa el código fueron obtenidas de:
- El SQL de referencia del proyecto: `doc/horarios-escolares/docs/database/seed-lomloe-madrid.sql`
- Las búsquedas web realizadas durante la sesión de junio de 2026

**Para re-verificar:** Si hay alguna duda sobre una cifra concreta, consultar directamente
el BOCM con el número de decreto indicado arriba, o la Consejería de Educación de Madrid.

## Nota histórica

El seed original citaba incorrectamente "Decreto 82/2022". La norma correcta es el
**Decreto 61/2022, de 13 de julio** (BOCM 14 de julio de 2022). Esta corrección se
aplicó en junio de 2026 en `DbInitializer.cs` y en la plantilla curricular.
