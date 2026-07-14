import { PublicClientApplication, type Configuration } from '@azure/msal-browser'

// Placeholder/example values in this environment — no live Azure AD app
// registration exists here. Set VITE_MSAL_CLIENT_ID / VITE_MSAL_AUTHORITY in
// .env for a real tenant (see .env.example).
const msalConfig: Configuration = {
  auth: {
    clientId: (import.meta.env.VITE_MSAL_CLIENT_ID as string | undefined) ?? '',
    authority:
      (import.meta.env.VITE_MSAL_AUTHORITY as string | undefined) ??
      'https://login.microsoftonline.com/common',
    redirectUri: '/login',
  },
  cache: {
    cacheLocation: 'localStorage',
  },
}

let instance: PublicClientApplication | null = null

// Constructed lazily, on first actual use, instead of at module load. MSAL's
// constructor requires window.crypto.subtle, which browsers only expose in
// secure contexts (HTTPS, or the literal "localhost" origin) — eagerly
// constructing this at import time throws and crashes the entire bundle
// (blank page, no login at all) on any plain-HTTP custom domain, even for
// users who never touch Microsoft sign-in.
export function getMsalInstance(): PublicClientApplication {
  if (!instance) {
    instance = new PublicClientApplication(msalConfig)
  }
  return instance
}
