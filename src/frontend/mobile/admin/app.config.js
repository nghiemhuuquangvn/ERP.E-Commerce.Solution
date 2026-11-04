// Static values live in app.json. This file adds what depends on the build: the tenant (TENANT_CODE, default SYSTEM) and the profile.
// A tenant build changes the app name, the Android package (so several tenants install side by side) and the API address.
// Android refuses plain http:// by default; the internal "preview" build talks to the LAN test stack, so it allows cleartext
// traffic. "production" (APP_VARIANT=production) does not and must use https.
const { resolveTenantConfig } = require('./tenant.config')

module.exports = ({ config }) => {
  const tenant = resolveTenantConfig(process.env)
  return {
    ...config,
    name: tenant.appName,
    slug: 'erp-admin',
    android: { ...config.android, package: tenant.androidPackage },
    ios: { ...config.ios, bundleIdentifier: tenant.androidPackage },
    splash: { ...config.splash, backgroundColor: tenant.brand },
    plugins: [
      ...(config.plugins ?? []),
      ...(tenant.production ? [] : [['expo-build-properties', { android: { usesCleartextTraffic: true } }]]),
    ],
    extra: { ...config.extra, tenant: { code: tenant.code, apiUrl: tenant.apiUrl, appName: tenant.appName } },
  }
}
