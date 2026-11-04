import Constants from 'expo-constants'

export interface TenantConfig {
  /** Tenant code the build was made for (SYSTEM = the platform tenant). */
  code: string
  /** Gateway address of that tenant's host, ending in /api. The Gateway takes the tenant from the HOST, so this is the only tenant switch. */
  apiUrl: string
  appName: string
}

const FALLBACK: TenantConfig = {
  code: 'SYSTEM',
  apiUrl: (process.env.EXPO_PUBLIC_API_URL ?? 'http://admin.erp.local/api').replace(/\/+$/, ''),
  appName: 'ERP Admin',
}

/** Written into `extra.tenant` by app.config.js from TENANT_CODE / EXPO_PUBLIC_API_URL / APP_NAME at build time. */
export const tenantConfig: TenantConfig = (Constants.expoConfig?.extra as { tenant?: TenantConfig } | undefined)?.tenant ?? FALLBACK
