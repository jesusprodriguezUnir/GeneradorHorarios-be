# SPEC-001: Scaffolding Angular 20 PWA + Vercel CI/CD

**Estado:** Draft  
**Prioridad:** P0  
**Fase:** 0 — Infraestructura  
**Estimación:** 1 día  
**Depende de:** —  

---

## Contexto

Punto de partida del frontend. Debe quedar configurado con Angular 20 Zoneless, PWA, Strict mode, PrimeNG y desplegado automáticamente en Vercel en cada push a `main`.

---

## Comportamiento esperado

Un desarrollador que clone el repo y ejecute `npm install && ng serve` debe ver la app corriendo en local con routing básico, tema PrimeNG aplicado y service worker registrado.

Un push a `main` debe desencadenar deploy automático en Vercel en menos de 3 minutos.

---

## Criterios de aceptación

**ESCENARIO 1: Configuración Zoneless**
```
Given: El proyecto Angular 20 está creado
When:  Se arranca con ng serve
Then:  No hay Zone.js en el bundle (verificable en angular.json)
And:   La app funciona sin DetectChanges manual
And:   provideExperimentalZonelessChangeDetection() está en app.config.ts
```

**ESCENARIO 2: PWA instalable**
```
Given: La app está desplegada en Vercel con HTTPS
When:  Un usuario accede desde Chrome móvil
Then:  Aparece el banner "Añadir a pantalla de inicio"
And:   La app se instala y abre sin barra de navegador
And:   El manifest.json tiene nombre, iconos y theme_color correctos
```

**ESCENARIO 3: Strict mode**
```
Given: El proyecto está configurado
When:  Se ejecuta ng build
Then:  Compila sin errores con strict: true en tsconfig.json
And:   strictNullChecks, strictTemplates y strictInjectionParameters activos
```

**ESCENARIO 4: PrimeNG operativo**
```
Given: PrimeNG está instalado
When:  Se renderiza el componente raíz
Then:  El tema Aura (o Lara) de PrimeNG está aplicado
And:   Un p-button renderiza correctamente en la shell
```

**ESCENARIO 5: Deploy automático**
```
Given: El repo está conectado a Vercel
When:  Se hace push a la rama main
Then:  Vercel inicia el build automáticamente
And:   El build completa en menos de 3 minutos
And:   La URL de producción sirve la nueva versión
And:   El routing SPA funciona (no 404 en rutas internas)
```

---

## Estructura de ficheros resultante

```
src/web/
├── src/
│   ├── app/
│   │   ├── app.config.ts          # provideZonelessChangeDetection, router, primeng
│   │   ├── app.routes.ts          # rutas lazy por feature
│   │   ├── app.component.ts       # shell mínima
│   │   ├── core/
│   │   │   ├── auth/
│   │   │   ├── api/
│   │   │   └── guards/
│   │   ├── features/              # un directorio por SPEC
│   │   └── shared/
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   ├── manifest.webmanifest
│   └── ngsw-config.json
├── angular.json                   # polyfills sin zone.js
├── tsconfig.json                  # strict: true
└── vercel.json                    # SPA rewrites
```

---

## Configuración clave

### app.config.ts
```typescript
import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeng/themes/aura';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideExperimentalZonelessChangeDetection(),
    provideRouter(routes),
    provideAnimationsAsync(),
    providePrimeNG({ theme: { preset: Aura } })
  ]
};
```

### angular.json — sin Zone.js
```json
{
  "polyfills": []
}
```

### vercel.json
```json
{
  "rewrites": [{ "source": "/(.*)", "destination": "/index.html" }],
  "headers": [{
    "source": "/assets/(.*)",
    "headers": [{ "key": "Cache-Control", "value": "public, max-age=31536000, immutable" }]
  }]
}
```

---

## Fuera de alcance

- Implementación de features reales (van en sus specs)
- Configuración de Supabase en el cliente (SPEC-003)
- Autenticación (SPEC-011)

---

## Notas técnicas

- Usar `ng new` con `--minimal --standalone --style scss --routing`
- Eliminar `zone.js` de polyfills inmediatamente tras la creación
- La rama por defecto en GitHub es `main`
- Vercel se conecta al repo GitHub con auto-deploy en `main`
- Preview deploys activos en PRs (Vercel lo hace automáticamente)
