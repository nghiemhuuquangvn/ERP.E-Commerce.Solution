// Warehouse W3 live check on the glcheck host: production movements, cost on the movement, idempotency by reference, QC hold/release,
// subcontract transfer, reconciliation, and "Accounting books nothing for production movements".
const http = require('http')
const repoRoot = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
const fs = require('fs')
const { execFileSync } = require('child_process')
const host = process.env.WH_HOST || 'glcheck.erp.local'
const call = (method, path, headers = {}, body) =>
  new Promise((resolve, reject) => {
    const req = http.request({ host: '127.0.0.1', port: 5000, method, path, headers: { ...headers, Host: host } }, (res) => {
      let d = ''
      res.on('data', (c) => (d += c))
      res.on('end', () => resolve({ status: res.statusCode, text: d, json: () => { try { return JSON.parse(d) } catch { return null } } }))
    })
    req.on('error', reject)
    if (body !== undefined) req.write(JSON.stringify(body))
    req.end()
  })
const J = { 'Content-Type': 'application/json' }
let failed = 0
const check = (name, ok, extra = '') => { if (!ok) failed++; console.log(`${ok ? 'OK  ' : 'FAIL'} ${name} ${ok ? '' : extra}`) }
const near = (a, b) => Math.abs(Number(a) - b) < 1e-9
const ok2xx = (r) => r.status === 200 || r.status === 201 || r.status === 204
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const pw = fs.readFileSync(repoRoot + '/.env', 'utf8').match(/^SA_PASSWORD=(.*)$/m)[1].trim()
const sql = (db, q) => execFileSync('sqlcmd', ['-S', 'localhost', '-U', 'sa', '-P', pw, '-C', '-b', '-d', db, '-h', '-1', '-W', '-Q', `SET NOCOUNT ON; ${q}`]).toString().trim()

;(async () => {
  const login = (await call('POST', '/api/v1/auth/login', J, { email: process.env.WH_EMAIL || 'admin@glcheck.test', password: process.env.WH_PASSWORD || 'Gl-Check-Passw0rd!', userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const units = {}
  for (const u of (await call('GET', '/mst/api/v1/masterdata/units-of-measure?pageSize=100', auth)).json().items) units[u.code] = u.id

  const newProduct = async (tag) => {
    const sku = `ZZ-WH-W3${tag}-${S}`
    const created = (await call('POST', '/prd/api/v1/products', auth, { name: `ZZ WH W3${tag} ${S}`, sku, productType: 1, baseUomId: units.KG })).json()
    await call('POST', `/prd/api/v1/products/${created.productId}/variants`, auth, { sku, cost: 1 })
    await call('POST', `/prd/api/v1/products/${created.productId}/publish`, auth)
    return { id: created.productId, sku }
  }
  const whs = (await wh('GET', '/warehouses')).json().filter((w) => w.isActive)
  const [A, B] = whs
  check('two active warehouses', whs.length >= 2)

  const prod = await newProduct('')
  const p = prod.id
  const inv = async () => (await wh('GET', `/inventory/${p}`)).json()
  const balances = async () => (await wh('GET', `/inventory/${p}/balances`)).json() ?? []
  const lotsOf = async () => (await wh('GET', `/inventory/lots/product/${p}`)).json() ?? []
  const history = async () => (await wh('GET', `/movements/${p}?take=200`)).json()?.items ?? []
  const sum = (rows, f) => rows.reduce((a, r) => a + Number(r[f]), 0)
  const mov = (type, qty, ref, extra = {}) => wh('POST', '/movements', { productId: p, quantity: qty, movementType: type, reference: ref, ...extra })

  // seed: 10 kg in lot LA at cost 4, warehouse A
  let r = await wh('POST', '/inventory/receive', { productId: p, productName: prod.sku, productSku: prod.sku, quantity: 10, unitCost: 4, lotNumber: `LA-${S}`, expiryDate: '2030-06-01T00:00:00Z', warehouseId: A.id, reference: `ZZ-W3-${S}-RCV` })
  check('seed: receive 10 kg lot LA at cost 4 into A', ok2xx(r), `${r.status} ${r.text.slice(0, 200)}`)
  const LA = (await lotsOf()).find((l) => l.lotNumber === `LA-${S}`)

  // ── production issue from the named lot
  r = await mov('ProductionIssue', 2.5, `ZZ-MO-${S}#1`, { lotId: LA.lotId })
  const issue = r.json()
  check('production issue 2.5 kg from lot LA accepted', ok2xx(r) && issue?.replayed === false, `${r.status} ${r.text.slice(0, 240)}`)
  check('issue answered cost 4 and value -10', near(issue?.unitCost, 4) && near(issue?.valueDelta, -10), JSON.stringify(issue))
  let i = await inv(); let rows = await balances()
  check('stock 7.5 and the balances agree', near(i.totalQuantity, 7.5) && near(sum(rows, 'quantity'), 7.5), JSON.stringify(i).slice(0, 160))

  // ── same reference again = replay
  r = await mov('ProductionIssue', 2.5, `ZZ-MO-${S}#1`, { lotId: LA.lotId })
  check('same reference again: replayed, same movement id', ok2xx(r) && r.json()?.replayed === true && r.json()?.movementId === issue.movementId, `${r.status} ${r.text.slice(0, 200)}`)
  i = await inv()
  check('stock still 7.5 after the replay', near(i.totalQuantity, 7.5))

  // ── two identical requests at once: one movement only
  const ref2 = `ZZ-MO-${S}#2`
  const both = await Promise.all([1, 2].map(() => mov('ProductionIssue', 1, ref2, { lotId: LA.lotId })))
  console.log('parallel outcomes', both.map((x) => x.status).join(','))
  check('parallel identical requests: nothing answers 5xx', both.every((x) => x.status < 500), both.map((x) => `${x.status} ${x.text.slice(0, 80)}`).join(' | '))
  await sleep(500)
  const rowsForRef = (await history()).filter((m) => m.reference === ref2)
  check('parallel identical requests: exactly one movement row', rowsForRef.length === 1, JSON.stringify(rowsForRef.length))
  i = await inv()
  check('stock is 6.5 (7.5 - 1, issued once)', near(i.totalQuantity, 6.5), JSON.stringify(i).slice(0, 160))

  // ── refusals
  r = await mov('ProductionIssue', 1, '', { lotId: LA.lotId })
  check('a production movement without a reference is refused (400)', r.status === 400, `${r.status} ${r.text.slice(0, 160)}`)
  r = await mov('NoSuchType', 1, `ZZ-MO-${S}#9`)
  check('an unknown movement type is refused (400)', r.status === 400, `${r.status} ${r.text.slice(0, 160)}`)
  r = await mov('ProductionIssue', 99, `ZZ-MO-${S}#10`, { lotId: LA.lotId })
  check('issuing more than the stock is refused (4xx)', r.status >= 400 && r.status < 500, `${r.status} ${r.text.slice(0, 160)}`)

  // ── receipt into a lot on hold; the hold keeps it out of allocation
  r = await mov('ProductionReceipt', 5, `ZZ-MO-${S}#3`, { warehouseId: A.id, lotNumber: `FG-${S}`, expiryDate: '2027-01-01T00:00:00Z', unitCost: 9, initialLotStatus: 'QcHold' })
  const receipt = r.json()
  check('production receipt of 5 kg into lot FG on QcHold', ok2xx(r) && near(receipt?.valueDelta, 45) && !!receipt?.lotId, `${r.status} ${r.text.slice(0, 240)}`)
  const FG = (await lotsOf()).find((l) => l.lotNumber === `FG-${S}`)
  check('the lot is QcHold, Production source, warehouse A', FG?.status === 'QcHold' && FG?.warehouseId === A.id, JSON.stringify(FG))

  const reserve = async (n, qty) => {
    const orderId = '3f1d0000-0000-4000-8000-' + (Date.now() + n).toString(16).padStart(12, '0').slice(-12)
    const rr = await wh('POST', '/inventory/reserve', { orderId, items: [{ productId: p, quantity: qty }], holdUntil: new Date(Date.now() + 3600e3).toISOString(), correlationId: `zz-w3-${S}-${n}` })
    return { rr, id: rr.json()?.reservationId }
  }
  let held = await reserve(1, 6)
  check('reserve 6 kg', ok2xx(held.rr), `${held.rr.status} ${held.rr.text.slice(0, 200)}`)
  let lots = await lotsOf()
  check('the held lot got nothing (although it expires first)', near(lots.find((l) => l.lotNumber === `FG-${S}`).reservedQuantity, 0), JSON.stringify(lots.map((l) => [l.lotNumber, l.reservedQuantity])))
  await wh('POST', '/inventory/release', { reservationId: held.id, reason: 'zz w3' })

  // ── quality release
  r = await mov('QcRelease', 0, `ZZ-QC-${S}#1`, { lotId: FG.lotId })
  check('quality release accepted, value 0', ok2xx(r) && near(r.json()?.valueDelta, 0) && near(r.json()?.quantity, 5), `${r.status} ${r.text.slice(0, 200)}`)
  check('the lot reads Available', (await lotsOf()).find((l) => l.lotNumber === `FG-${S}`).status === 'Available')
  held = await reserve(2, 6)
  lots = await lotsOf()
  check('after the release the lot is allocated first-expiry-first (5 on FG, 1 on LA)', near(lots.find((l) => l.lotNumber === `FG-${S}`).reservedQuantity, 5) && near(lots.find((l) => l.lotNumber === `LA-${S}`).reservedQuantity, 1), JSON.stringify(lots.map((l) => [l.lotNumber, l.reservedQuantity])))
  await wh('POST', '/inventory/release', { reservationId: held.id, reason: 'zz w3' })
  lots = await lotsOf(); rows = await balances()
  check('release leaves no reservation anywhere', near(sum(rows, 'reservedQuantity'), 0) && lots.every((l) => near(l.reservedQuantity, 0)), JSON.stringify(rows.map((x) => x.reservedQuantity)))

  // ── return, scrap
  r = await mov('ProductionReturn', 1, `ZZ-MO-${S}#4`, { lotId: LA.lotId, unitCost: 4 })
  check('production return of 1 kg into lot LA', ok2xx(r) && near(r.json()?.valueDelta, 4), `${r.status} ${r.text.slice(0, 200)}`)
  r = await mov('ProductionScrap', 0.5, `ZZ-MO-${S}#5`, { lotId: FG.lotId })
  check('production scrap of 0.5 kg from lot FG at its cost (9 -> -4.5)', ok2xx(r) && near(r.json()?.valueDelta, -4.5), `${r.status} ${r.text.slice(0, 200)}`)
  i = await inv(); rows = await balances()
  check('stock 12 (6.5 + 5 + 1 - 0.5) = sum of balances', near(i.totalQuantity, 12) && near(sum(rows, 'quantity'), 12), JSON.stringify(i).slice(0, 160))

  // ── subcontract transfer of lot-less stock between warehouses
  const prod2 = await newProduct('S')
  await wh('POST', '/inventory/receive', { productId: prod2.id, productName: prod2.sku, productSku: prod2.sku, quantity: 3, unitCost: 2, warehouseId: A.id, reference: `ZZ-W3S-${S}` })
  r = await wh('POST', '/movements', { productId: prod2.id, quantity: 1, movementType: 'SubcontractTransfer', reference: `ZZ-SC-${S}#1`, warehouseId: A.id, toWarehouseId: B.id })
  check('subcontract transfer of 1 kg A -> B accepted, total unchanged', ok2xx(r) && near(r.json()?.valueDelta, 0), `${r.status} ${r.text.slice(0, 200)}`)
  const rows2 = (await wh('GET', `/inventory/${prod2.id}/balances`)).json() ?? []
  const inv2 = (await wh('GET', `/inventory/${prod2.id}`)).json()
  check('A keeps 2, B holds 1, item total 3', near(rows2.find((x) => x.warehouseCode === A.code)?.quantity, 2) && near(rows2.find((x) => x.warehouseCode === B.code)?.quantity, 1) && near(inv2.totalQuantity, 3), JSON.stringify(rows2.map((x) => [x.warehouseCode, x.quantity])))

  // ── history shows cost and value, also for a manual sale of an older type
  r = await mov('Sold', 1, `ZZ-W3-${S}-SOLD`, { reason: 'zz w3' })
  check('manual sale of 1 kg (older type) accepted', ok2xx(r), `${r.status} ${r.text.slice(0, 200)}`)
  const soldId = r.json()?.movementId
  const h = await history()
  const hIssue = h.find((m) => m.movementId === issue.movementId)
  check('history: the production issue shows warehouse, lot, cost 4, value -10, delta -2.5', hIssue && hIssue.warehouseId === A.id && hIssue.lotId === LA.lotId && near(hIssue.unitCost, 4) && near(hIssue.valueDelta, -10) && near(hIssue.stockDelta, -2.5), JSON.stringify(hIssue))
  const hSold = h.find((m) => m.movementId === soldId)
  check('history: the manual sale (older type) now also carries cost and delta', hSold && hSold.unitCost != null && near(hSold.stockDelta, -1), JSON.stringify(hSold))

  // ── reconciliation
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  check('reconciliation: no quantity or lot drift anywhere', !drift.some((d) => d.kind === 'Quantity' || d.kind === 'Lot'), JSON.stringify(drift.filter((d) => d.kind !== 'Reserved')).slice(0, 300))

  // ── Accounting books nothing for the production movements
  console.log('waiting for the outbox...')
  await sleep(8000)
  const prodIds = (await history()).filter((m) => String(m.movementType).startsWith('Production') || m.movementType === 'QcRelease').map((m) => m.movementId)
  const key = (x) => `StockMovement:${String(x).replace(/-/g, '').toLowerCase()}`
  const inList = prodIds.map((x) => `'${key(x)}'`).join(',')
  const booked = Number(sql('AccountingDB', `SELECT COUNT(*) FROM JournalEntries WHERE SourceKey IN (${inList})`))
  check(`Accounting has no journal entry for the ${prodIds.length} production movements`, booked === 0, `booked=${booked}`)
  const soldBooked = Number(sql('AccountingDB', `SELECT COUNT(*) FROM JournalEntries WHERE SourceKey = '${key(soldId)}'`))
  check('the manual sale of an older type IS booked (so the check above is meaningful)', soldBooked === 1, `soldBooked=${soldBooked}`)

  console.log(`\nproducts ${prod.sku}, ${prod2.sku}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
})()
