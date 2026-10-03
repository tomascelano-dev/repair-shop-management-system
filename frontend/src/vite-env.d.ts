/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** API base URL, e.g. https://api.mitaller.com/api/v1. Defaults to /api/v1 (same origin / dev proxy). */
  readonly VITE_API_BASE?: string
  /** Shows the demo users on the login screen (true in development). */
  readonly VITE_SHOW_DEMO_USERS?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
