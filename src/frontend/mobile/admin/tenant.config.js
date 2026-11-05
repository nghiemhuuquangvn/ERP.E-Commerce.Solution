// The per-tenant part of the build, as a pure function so it can be tested (test/tenantConfig.test.ts) and used by app.config.js.
// One code base, one APK per tenant: the API address (the tenant is the request HOST), the app name and the Android package change.
// Defaults are the platform tenant SYSTEM on the platform host.

const DEFAULT_CODE = 'SYSTEM'
const DEFAULT_API_URL = 'http://admin.erp.local/api'
const DEFAULT_BRAND = '#0369a1'

/** `SYSTEM` -> com.erp.admin; `glcheck` -> com.erp.admin.glcheck (Android package parts: letters, digits, underscore; start with a letter). */
function androidPackage(code) {
  if (code.toUpperCase() === DEFAULT_CODE) return 'com.erp.admin'
  const part = code.toLowerCase().replace(/[^a-z0-9_]/g, '_').replace(/^([^a-z])/, 't$1')
  return `com.erp.admin.${part}`
}

function resolveTenantConfig(env = process.env) {
  const rawCode = (env.TENANT_CODE || DEFAULT_CODE).trim()
  if (!/^[A-Za-z][A-Za-z0-9_-]{0,31}$/.test(rawCode)) {
    throw new Error(`TENANT_CODE "${rawCode}" is not a valid tenant code (letters, digits, - and _; starts with a letter).`)
  }
  const system = rawCode.toUpperCase() === DEFAULT_CODE
  const code = system ? DEFAULT_CODE : rawCode
  const apiUrl = (env.EXPO_PUBLIC_API_URL || DEFAULT_API_URL).replace(/\/+$/, '')
  const brand = env.BRAND_COLOR || DEFAULT_BRAND
  if (!/^#[0-9a-fA-F]{6}$/.test(brand)) throw new Error(`BRAND_COLOR "${brand}" must be a #rrggbb colour.`)
  return {
    code,
    apiUrl,
    appName: env.APP_NAME || (system ? 'ERP Admin' : `ERP Admin - ${code}`),
    brand,
    androidPackage: androidPackage(code),
    production: env.APP_VARIANT === 'production',
    // Push is off unless asked for AND the Firebase file is given (a push token cannot be had without it).
    googleServicesFile: env.GOOGLE_SERVICES_FILE || '',
    pushEnabled: env.PUSH_ENABLED === 'true' && !!env.GOOGLE_SERVICES_FILE,
  }
}

module.exports = { resolveTenantConfig, androidPackage, DEFAULT_CODE, DEFAULT_API_URL }
