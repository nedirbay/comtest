/// <reference types="vite/client" />

// VITE_* values are compiled into the browser bundle, where anyone can read them.
// Nothing secret may ever be added here.
interface ImportMetaEnv {
  readonly VITE_API_URL: string
  readonly VITE_SENTRY_DSN?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
