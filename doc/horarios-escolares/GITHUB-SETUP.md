# Instrucciones para subir a GitHub

## 1. Crear el repositorio en GitHub

Ve a https://github.com/new y crea un repositorio con:
- Nombre: `horarios-escolares`
- Visibilidad: Private
- Sin README (ya tenemos el nuestro)
- Sin .gitignore (ya tenemos el nuestro)

## 2. Inicializar y subir desde esta carpeta

```bash
cd horarios-escolares

git init
git add .
git commit -m "feat: scaffolding inicial — constitución, specs SDD y estructura del proyecto"

git remote add origin https://github.com/TU-USUARIO/horarios-escolares.git
git branch -M main
git push -u origin main
```

## 3. Conectar Vercel (frontend)

1. Ve a https://vercel.com/new
2. Importa el repo `horarios-escolares`
3. Framework preset: **Angular**
4. Root directory: `src/web`
5. Build command: `ng build --configuration production`
6. Output directory: `dist/web/browser`
7. Añade las variables de entorno de `src/web/src/environments/environment.example.ts`

## 4. Conectar Railway (backend)

1. Ve a https://railway.app/new
2. Deploy from GitHub repo → selecciona `horarios-escolares`
3. Root directory: `src/api`
4. Railway detecta el Dockerfile automáticamente
5. Añade las variables de entorno de `src/api/appsettings.Development.example.json`

## 5. Configurar Cloudflare DNS

Una vez tengas las URLs de Vercel y Railway:

```
horarioscolegios.es     CNAME → cname.vercel-dns.com      (Proxy: ON)
www.horarioscolegios.es CNAME → cname.vercel-dns.com      (Proxy: ON)
api.horarioscolegios.es CNAME → tu-app.railway.app         (Proxy: OFF)
```

## 6. Crear proyecto en Supabase

1. Ve a https://supabase.com/dashboard/new
2. Crea un proyecto nuevo
3. Ejecuta en el SQL Editor:
   - El schema completo de `docs/specs/SPEC-003-supabase-schema.md`
   - El seed de `docs/database/seed-lomloe-madrid.sql`
4. En Authentication → Settings:
   - Activa Magic Link (email)
   - Activa Google OAuth (necesitas credenciales en Google Cloud Console)
5. Copia las credenciales (URL, anon key, JWT secret) a las variables de entorno

## Estructura final del repo

```
horarios-escolares/
├── .gitignore
├── README.md
├── docs/
│   ├── constitution/
│   │   └── CONSTITUTION.md           ← Constitución v1.1 (Angular 20)
│   ├── specs/
│   │   ├── INDEX.md                  ← Índice de todas las specs
│   │   ├── SPEC-001 a SPEC-063       ← Feature specs completas
│   ├── adr/
│   │   └── ADR-001-003-*.md          ← Architecture Decision Records
│   └── database/
│       └── seed-lomloe-madrid.sql    ← Datos semilla LOMLOE
├── src/
│   ├── api/                          ← .NET 8 API
│   │   ├── Domain/
│   │   ├── Features/
│   │   ├── Infrastructure/
│   │   ├── Program.cs
│   │   ├── Dockerfile
│   │   ├── railway.toml
│   │   └── appsettings.Development.example.json
│   └── web/                          ← Angular 20 PWA (pendiente ng new)
│       └── src/environments/
│           └── environment.example.ts
└── tests/
    └── Domain.Tests/
        └── Constraints/
            └── ConstraintsTests.cs
```
