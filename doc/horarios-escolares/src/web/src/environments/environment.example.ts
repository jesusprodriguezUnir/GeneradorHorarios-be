// Copia este fichero como environment.ts y environment.prod.ts
// NO commitear los ficheros reales (están en .gitignore)

export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  supabase: {
    url: 'https://TU-PROYECTO.supabase.co',
    anonKey: 'TU-ANON-KEY'
  }
};
