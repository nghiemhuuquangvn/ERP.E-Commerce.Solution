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

export const msalInstance = new PublicClientApplication(msalConfig)
