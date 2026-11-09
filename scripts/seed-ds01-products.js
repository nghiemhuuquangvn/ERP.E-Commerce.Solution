// Creates the 12 products of the Manufacturing demo set DS-01 (docs/business/Manufacturing/DemoData-DS01.md) through the Gateway,
// optionally enters the opening stock of spec 1.7 in the Warehouse (--stock) and calls the Manufacturing seeder (--seed). Usage:
//   node scripts/seed-ds01-products.js --host shop.erp.local --email admin@x --password ... [--prefix ZZ-]
//        [--stock] [--seed --raw <whId> --fg <whId> --qc <whId>]
// --stock: sets the lot flag on TP-BAN01 / NVL-GO01 / NVL-SON01, finds or creates the warehouses <prefix>WH-RM, <prefix>WH-FG, <prefix>WH-QC (unless
//          --raw/--fg/--qc are given) and receives the opening stock; running it again changes nothing (every row is keyed by its movement reference).
//          Stock is booked like any receipt (Accounting posts it) - for demo / test tenants only.
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

// Spec 1.7. Warehouse: RM unless 'FG'. Unit costs are ASSUMED (the spec gives only the standard cost of wood: 8 000 000 / m3) - assumption A-1.
const LOT_CONTROLLED = ['TP-BAN01', 'NVL-GO01', 'NVL-SON01']
const STOCK = [
  { code: 'BTP-MAT01', qty: 120, cost: 300000 },
  { code: 'BTP-CHAN01', qty: 300, cost: 40000 },
  { code: 'NVL-VIT01', qty: 2000, cost: 500 },
  { code: 'NVL-SON01', lot: 'L-SON-A', qty: 30, cost: 60000, expiry: '2026-11-30T00:00:00Z' },
  { code: 'NVL-SON01', lot: 'L-SON-B', qty: 50, cost: 60000, expiry: '2027-03-31T00:00:00Z' },
  { code: 'NVL-SON01', lot: 'L-SON-X', qty: 20, cost: 60000, expiry: '2026-10-31T00:00:00Z' }, // already expired
  { code: 'NVL-KEO01', qty: 50, cost: 40000 },
  { code: 'NVL-KEO02', qty: 20, cost: 45000 },
  { code: 'NVL-THUNG01', qty: 500, cost: 5000 },
  { code: 'NVL-XOP01', qty: 1000, cost: 2000 },
  { code: 'NVL-GO01', lot: 'L-GO-01', qty: 5000, cost: 8000000 },
  { code: 'TP-BAN01', lot: 'L-TP-OLD', qty: 20, cost: 2000000, wh: 'FG' },
]

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
  const ids = {}
  for (const [c, u] of defs) {
    const sku = prefix + c
    const r = await call('POST', '/prd/api/v1/products', auth, { name: `DS-01 ${c}`, sku, productType: 1, baseUomId: units[u] })
    if (r.status >= 300) {
      const found = (await call('GET', `/prd/api/v1/products/sku/${encodeURIComponent(sku)}`, auth)).json()
      ids[c] = found?.productId ?? found?.id
      console.log(sku, r.status, ids[c] ? 'exists' : r.text.slice(0, 120))
      continue
    }
    const id = r.json().productId
    ids[c] = id
    await call('POST', `/prd/api/v1/products/${id}/variants`, auth, { sku, cost: 1 })
    await call('POST', `/prd/api/v1/products/${id}/publish`, auth)
    console.log('created', sku)
  }

  let whIds = { raw: a.raw, fg: a.fg, qc: a.qc }
  if (a.stock) {
    // lot flag of the three lot-controlled items (idempotent)
    for (const c of LOT_CONTROLLED) {
      if (!ids[c]) { console.log('no product for', c); continue }
      const r = await call('PUT', `/prd/api/v1/products/${ids[c]}/tracking`, auth, { isLotControlled: true, isSerialControlled: false })
      console.log('lot-controlled', prefix + c, r.status)
    }

    // warehouses
    const list = (await call('GET', '/wh/api/v1/warehouses', auth)).json() ?? []
    const ensure = async (given, code, name) => {
      if (given) return given
      const found = list.find((w) => w.code === code)
      if (found) return found.id
      const r = await call('POST', '/wh/api/v1/warehouses', auth, { code, name })
      if (r.status >= 300) throw new Error(`warehouse ${code}: ${r.status} ${r.text.slice(0, 160)}`)
      console.log('warehouse created', code)
      return r.json().id
    }
    whIds = {
      raw: await ensure(a.raw, `${prefix}WH-RM`, 'DS-01 raw materials'),
      fg: await ensure(a.fg, `${prefix}WH-FG`, 'DS-01 finished goods'),
      qc: await ensure(a.qc, `${prefix}WH-QC`, 'DS-01 quarantine'),
    }

    // opening stock: a row is entered once - the reference is looked for in the item's movement history first
    // (a plain receipt is not idempotent in the Warehouse; only a purchase-order receipt is)
    let created = 0, skipped = 0
    for (const s of STOCK) {
      const productId = ids[s.code]
      if (!productId) { console.log('no product for', s.code); continue }
      const reference = `DS01-${prefix}${s.code}-${s.lot ?? 'plain'}`
      const history = (await call('GET', `/wh/api/v1/movements/${productId}?take=200`, auth)).json()?.items ?? []
      if (history.some((m) => m.reference === reference)) { skipped++; continue }
      const r = await call('POST', '/wh/api/v1/inventory/receive', auth, {
        productId, productName: `DS-01 ${s.code}`, productSku: prefix + s.code, quantity: s.qty, unitCost: s.cost,
        lotNumber: s.lot, expiryDate: s.expiry, warehouseId: s.wh === 'FG' ? whIds.fg : whIds.raw, reference,
      })
      if (r.status >= 300) throw new Error(`receive ${s.code} ${s.lot ?? ''}: ${r.status} ${r.text.slice(0, 200)}`)
      created++
    }
    console.log(`opening stock: ${created} rows received, ${skipped} already there`)
    console.log('warehouses', JSON.stringify(whIds))
  }
  console.log('products', JSON.stringify(ids))

  if (a.seed) {
    const r = await call('POST', '/mfg/api/v1/manufacturing/seed/ds01', auth, { plantCode: a.plant ?? 'PL01', skuPrefix: prefix, warehouses: { raw: whIds.raw, finished: whIds.fg, quarantine: whIds.qc, scrap: a.scrap ?? null } })
    console.log(r.status, r.text)
  }
})().catch((e) => { console.error(e.message); process.exit(1) })
