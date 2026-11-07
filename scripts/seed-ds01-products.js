// Creates the 12 products of the Manufacturing demo set DS-01 (docs/business/Manufacturing/DemoData-DS01.md) through the Gateway,
// then (optionally) calls the Manufacturing seeder. Usage:
//   node scripts/seed-ds01-products.js --host shop.erp.local --email admin@x --password ... [--prefix ZZ-] [--seed --raw <whId> --fg <whId> --qc <whId>]
// Needs Manufacturing__EnableDemoSeed=true on the service (compose: MFG_DEMO_SEED=true) for --seed. Gateway on 127.0.0.1:5000 unless --gateway host:port.
const http = require('http')
const a = Object.fromEntries(process.argv.slice(2).reduce((acc, v, i, all) => (v.startsWith('--') ? [...acc, [v.slice(2), all[i + 1] && !all[i + 1].startsWith('--') ? all[i + 1] : true]] : acc), []))
const [gh, gp] = (a.gateway ?? '127.0.0.1:5000').split(':')
const call = (method, path, headers = {}, body) => new Promise((resolve, reject) => {
  const req = http.request({ host: gh, port: Number(gp), method, path, headers: { ...headers, Host: a.host } }, (res) => {
    let d = ''; res.on('data', (c) => (d += c)); res.on('end', () => resolve({ status: res.statusCode, text: d, json: () => { try { return JSON.parse(d) } catch { return null } } }))
  })
  req.on('error', reject); if (body !== undefined) req.write(JSON.stringify(body)); req.end()
})
const J = { 'Content-Type': 'application/json' }
;(async () => {
  if (!a.host || !a.email || !a.password) throw new Error('--host, --email and --password are required')
  const prefix = a.prefix === true ? '' : (a.prefix ?? '')
  const login = (await call('POST', '/api/v1/auth/login', J, { email: a.email, password: a.password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const units = {}
  for (const u of (await call('GET', '/mst/api/v1/masterdata/units-of-measure?pageSize=100', auth)).json().items) units[u.code] = u.id
  if (!units.M3) { await call('POST', '/mst/api/v1/masterdata/units-of-measure', auth, { code: 'M3', name: 'Cubic metre', symbol: 'm3', unitType: 1, baseUnitId: null, conversionFactor: 1, decimalPlaces: 3 }); console.log('unit M3 created') }
  for (const u of (await call('GET', '/mst/api/v1/masterdata/units-of-measure?pageSize=100&search=M3', auth)).json().items) units[u.code] = u.id
  const defs = [['TP-BAN01', 'PCS'], ['BTP-MAT01', 'PCS'], ['BTP-CHAN01', 'PCS'], ['KIT-PKG01', 'SET'], ['NVL-GO01', 'M3'], ['NVL-VIT01', 'PCS'], ['NVL-SON01', 'L'], ['NVL-KEO01', 'KG'], ['NVL-KEO02', 'KG'], ['NVL-THUNG01', 'PCS'], ['NVL-XOP01', 'PCS'], ['PP-MUN01', 'KG']]
  for (const [c, u] of defs) {
    const sku = prefix + c
    const r = await call('POST', '/prd/api/v1/products', auth, { name: `DS-01 ${c}`, sku, productType: 1, baseUomId: units[u] })
    if (r.status >= 300) { console.log(sku, r.status, r.text.slice(0, 120)); continue }
    const id = r.json().productId
    await call('POST', `/prd/api/v1/products/${id}/variants`, auth, { sku, cost: 1 })
    await call('POST', `/prd/api/v1/products/${id}/publish`, auth)
    console.log('created', sku)
  }
  if (a.seed) {
    const r = await call('POST', '/mfg/api/v1/manufacturing/seed/ds01', auth, { plantCode: a.plant ?? 'PL01', skuPrefix: prefix, warehouses: { raw: a.raw, finished: a.fg, quarantine: a.qc, scrap: a.scrap ?? null } })
    console.log(r.status, r.text)
  }
})().catch((e) => { console.error(e.message); process.exit(1) })
