/* eslint-disable @typescript-eslint/no-var-requires */
const { resolveTenantConfig, androidPackage } = require('../tenant.config')
const appConfig = require('../app.config')
const base = require('../app.json').expo

describe('tenant build configuration', () => {
  it('defaults to the platform tenant SYSTEM', () => {
    const c = resolveTenantConfig({})
    expect(c.code).toBe('SYSTEM')
    expect(c.androidPackage).toBe('com.erp.admin')
    expect(c.appName).toBe('ERP Admin')
    expect(c.apiUrl).toBe('http://admin.erp.local/api')
  })

  it('a tenant build gets its own package, name and address so several install side by side', () => {
    const c = resolveTenantConfig({ TENANT_CODE: 'glcheck', EXPO_PUBLIC_API_URL: 'http://glcheck.erp.local/api/' })
    expect(c.code).toBe('glcheck')
    expect(c.androidPackage).toBe('com.erp.admin.glcheck')
    expect(c.appName).toBe('ERP Admin - glcheck')
    expect(c.apiUrl).toBe('http://glcheck.erp.local/api')
  })

  it('treats system in any case as the platform tenant', () => {
    expect(resolveTenantConfig({ TENANT_CODE: 'system' }).code).toBe('SYSTEM')
  })

  it('makes a valid Android package part from any tenant code', () => {
    expect(androidPackage('Shop-One')).toBe('com.erp.admin.shop_one')
    expect(androidPackage('9lives')).toBe('com.erp.admin.t9lives')
  })

  it('refuses a bad tenant code or brand colour instead of building a wrong app', () => {
    expect(() => resolveTenantConfig({ TENANT_CODE: 'a b' })).toThrow(/not a valid tenant code/)
    expect(() => resolveTenantConfig({ BRAND_COLOR: 'blue' })).toThrow(/#rrggbb/)
  })

  it('app.config.js writes the tenant into extra and allows cleartext only outside production', () => {
    const prev = { ...process.env }
    try {
      process.env.TENANT_CODE = 'glcheck'
      process.env.EXPO_PUBLIC_API_URL = 'http://glcheck.erp.local/api'
      delete process.env.APP_VARIANT
      let cfg = appConfig({ config: base })
      expect(cfg.extra.tenant).toEqual({ code: 'glcheck', apiUrl: 'http://glcheck.erp.local/api', appName: 'ERP Admin - glcheck' })
      expect(cfg.android.package).toBe('com.erp.admin.glcheck')
      expect(JSON.stringify(cfg.plugins)).toContain('usesCleartextTraffic')
      process.env.APP_VARIANT = 'production'
      cfg = appConfig({ config: base })
      expect(JSON.stringify(cfg.plugins)).not.toContain('usesCleartextTraffic')
    } finally {
      process.env = prev
    }
  })
})
