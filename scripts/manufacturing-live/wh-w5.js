// Warehouse W5 live check: the internal stock API for Manufacturing, played as one production-order cycle (MO-A) through
// POST /mfg/.../seed/stock-cycle (real service token, real idempotency keys, real lot/serial flags), then verified through the public APIs.
// Default host = the SYSTEM tenant (admin.erp.local); WH_HOST / WH_EMAIL / WH_PASSWORD switch tenant.
const http = require('http')
const repoRoot = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
const crypto = require('crypto')
const { execFileSync } = require('child_process')
const fs = require('fs')
const host = process.env.WH_HOST || 'admin.erp.local'
const email = process.env.WH_EMAIL || 'superadmin@system.local'
const password = process.env.WH_PASSWORD || 'SuperAdmin@123'
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
const env = fs.readFileSync(repoRoot + '/.env', 'utf8')
const pw = /^SA_PASSWORD=(.*)$/m.exec(env)[1].trim()
const sql = (db, q) => execFileSync('sqlcmd', ['-S', 'localhost', '-U', 'sa', '-P', pw, '-C', '-b', '-d', db, '-h', '-1', '-W', '-Q', `SET NOCOUNT ON; ${q}`]).toString().trim()

;(async () => {
  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  if (!login?.accessToken) { console.log('FAIL login', JSON.stringify(login)); process.exit(1) }
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const units = {}
  for (const u of (await call('GET', '/mst/api/v1/masterdata/units-of-measure?pageSize=100', auth)).json().items) units[u.code] = u.id
  const newProduct = async (tag, lot, serial) => {
    const sku = `ZZ-WH-W5${tag}-${S}`
    const created = (await call('POST', '/prd/api/v1/products', auth, { name: `ZZ WH W5${tag} ${S}`, sku, productType: 1, baseUomId: units.KG })).json()
    await call('POST', `/prd/api/v1/products/${created.productId}/variants`, auth, { sku, cost: 1 })
    await call('POST', `/prd/api/v1/products/${created.productId}/publish`, auth)
    if (lot || serial) await call('PUT', `/prd/api/v1/products/${created.productId}/tracking`, auth, { isLotControlled: lot, isSerialControlled: serial })
    return { id: created.productId, sku }
  }
  const whs = (await wh('GET', '/warehouses')).json().filter((w) => w.isActive)
  const [A, B] = whs
  check('two active warehouses', whs.length >= 2)

  const raw = await newProduct('RAW', true, false)
  const fg = await newProduct('FG', true, false)
  const serial = await newProduct('SER', false, true)
  const receive = (p, qty, lot, warehouseId, expiry, cost = 4) => wh('POST', '/inventory/receive', { productId: p.id, productName: p.sku, productSku: p.sku, quantity: qty, unitCost: cost, lotNumber: lot, expiryDate: expiry, warehouseId, reference: `ZZ-W5-${S}-${p.sku}-${lot ?? 'plain'}-${qty}` })
  await receive(raw, 6, `LA-${S}`, A.id, '2030-06-01T00:00:00Z')
  await receive(raw, 4, `LB-${S}`, A.id, '2028-01-01T00:00:00Z')
  await receive(raw, 5, `LC-${S}`, B.id, '2027-06-01T00:00:00Z')
  await receive(fg, 1, `FGSEED-${S}`, B.id, undefined, 20)   // an inventory record must exist before a production receipt
  await receive(serial, 10, undefined, A.id)
  const inv = async (p) => (await wh('GET', `/inventory/${p.id}`)).json()
  const balances = async (p) => (await wh('GET', `/inventory/${p.id}/balances`)).json() ?? []
  const history = async (p) => (await wh('GET', `/movements/${p.id}?take=200`)).json()?.items ?? []
  check('seed: raw 15, finished 1, serial item 10', near((await inv(raw)).totalQuantity, 15) && near((await inv(fg)).totalQuantity, 1) && near((await inv(serial)).totalQuantity, 10))

  // internal routes are never reachable from outside
  let r = await call('GET', `/wh/internal/v1/stock/${raw.id}/availability`, auth)
  check('the Gateway does not route the internal stock API (404)', r.status === 404, `${r.status}`)
  r = await call('GET', `/internal/v1/stock/${raw.id}/availability`, auth)
  check('and not on the root either', r.status === 404 || r.status === 401, `${r.status}`)

  // ── the MO-A cycle
  const order = `MO-ZZ-${S}-A`
  r = await call('POST', '/mfg/api/v1/manufacturing/seed/stock-cycle', auth, { orderNumber: order, componentProductId: raw.id, finishedProductId: fg.id, rawWarehouseId: A.id, quarantineWarehouseId: B.id, componentQuantity: 8, firstIssueQuantity: 3, returnQuantity: 1, scrapQuantity: 0.5, finishedQuantity: 5 })
  const cycle = r.json()
  check('cycle endpoint answered', ok2xx(r) && cycle, `${r.status} ${r.text.slice(0, 400)}`)
  for (const s of cycle?.steps ?? []) console.log(`      ${s.ok ? 'ok ' : 'BAD'} ${s.name}: ${s.detail}`)
  check('every step of the cycle is ok', cycle?.ok === true, JSON.stringify((cycle?.steps ?? []).filter((s) => !s.ok)))
  const names = (cycle?.steps ?? []).map((s) => s.name)
  for (const n of ['issue-1-replay', 'issue-1-conflict', 'reservation-fulfilled', 'hold-not-allocatable', 'released-allocatable', 'block-lot', 'reverse-receipt', 'refuse-legacy-type', 'refuse-key-format', 'refuse-lot-missing'])
    check(`step present: ${n}`, names.includes(n))

  // ── verify through the public APIs
  const raw1 = await inv(raw)
  check('raw material: 15 - 8 issued + 1 returned - 0.5 scrapped = 7.5, nothing reserved', near(raw1.totalQuantity, 7.5) && near(raw1.reservedQuantity, 0), JSON.stringify(raw1).slice(0, 300))
  const fg1 = await inv(fg)
  check('finished good: receipt of 5 reversed again, 1 left', near(fg1.totalQuantity, 1), JSON.stringify(fg1).slice(0, 300))
  const hRaw = await history(raw)
  const refs = hRaw.map((m) => m.reference)
  check('raw movements carry the order keys MO#1, #2, #R1, #S1 once each', ['1', '2', 'R1', 'S1'].every((l) => refs.filter((x) => x === `${order}#${l}`).length === 1), JSON.stringify(refs))
  const issue1 = hRaw.find((m) => m.reference === `${order}#1`)
  check('issue 1 is valued at the lot cost 4: value -12, unit cost 4', issue1 && near(issue1.valueDelta, -12) && near(issue1.unitCost, 4), JSON.stringify(issue1))
  const hFg = await history(fg)
  const receipt = hFg.find((m) => m.reference === `${order}#FG1`)
  check('finished receipt booked at actual cost (8 issued x 4 / 5 = 6.4 per unit)', receipt && near(receipt.unitCost, 6.4) && near(receipt.valueDelta, 32), JSON.stringify(receipt))
  const madeRow = sql('WarehouseDB', `SELECT CONCAT(Status, '|', RemainingQuantity, '|', ReceivedQuantity) FROM InventoryLots WHERE LotNumber = 'FG-${order}'`)
  check('finished lot FG-<order> exists, emptied by the reversal (0 of 5) and released (status 0 = Available)', /^0|0(.0+)?|5(.0+)?$/.test(madeRow), madeRow)
  const resv = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${cycle?.productionOrderId}`)).json()
  check('production reservation is Fulfilled', resv?.status === 'Fulfilled' && near(resv.lines[0].consumed, 8), JSON.stringify(resv).slice(0, 300))
  check('first-expiry-first took LB (exp 2028) 4 before LA 4, never LC in warehouse B', resv?.lines?.[0]?.lots?.length === 2 && resv.lines[0].lots[0].lotNumber === `LB-${S}` && near(resv.lines[0].lots[0].quantity, 4) && resv.lines[0].lots[1].lotNumber === `LA-${S}`, JSON.stringify(resv?.lines?.[0]?.lots))

  // ── a serial-controlled component is refused at the first issue; the reservation it made is released again
  r = await call('POST', '/mfg/api/v1/manufacturing/seed/stock-cycle', auth, { orderNumber: `MO-ZZ-${S}-S`, componentProductId: serial.id, finishedProductId: fg.id, rawWarehouseId: A.id, quarantineWarehouseId: B.id, componentQuantity: 2, firstIssueQuantity: 1 })
  const sc = r.json()
  const stopped = (sc?.steps ?? []).find((s) => s.name === 'stopped')
  check('serial-controlled component: cycle stops with WH_SERIAL_NOT_SUPPORTED', sc?.ok === false && stopped && stopped.detail.startsWith('WH_SERIAL_NOT_SUPPORTED'), JSON.stringify(sc?.steps?.slice(-2)))
  check('serial item stock untouched (10)', near((await inv(serial)).totalQuantity, 10))
  const sres = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${sc?.productionOrderId}`)).json()
  if (sres?.reservationId) {
    r = await wh('POST', '/inventory/release', { reservationId: sres.reservationId, reason: 'zz w5 cleanup' })
    check('the reservation of the refused cycle is released', ok2xx(r) && near((await inv(serial)).reservedQuantity, 0), `${r.status} ${r.text.slice(0, 200)}`)
  }

  // ── two parallel identical issues under one key = one movement (public route runs the same service)
  const key = `MO-ZZ-${S}-P#1`
  const issueBody = { productId: raw.id, quantity: 0.25, movementType: 'ProductionIssue', reference: key, warehouseId: A.id }
  const [r1, r2] = await Promise.all([wh('POST', '/movements', issueBody), wh('POST', '/movements', issueBody)])
  const statuses = [r1.status, r2.status].sort()
  check('parallel identical issues: one 200, the other 200 (replay) or 409 (race)', statuses[0] < 300 && (statuses[1] < 300 || statuses[1] === 409), statuses.join(','))
  if (statuses[1] === 409) { const again = await wh('POST', '/movements', issueBody); check('the retry after the 409 answers the replay', ok2xx(again) && again.json()?.replayed === true, `${again.status} ${again.text.slice(0, 200)}`) }
  check('exactly one movement for the parallel key', (await history(raw)).filter((m) => m.reference === key).length === 1)
  check('and the stock fell only once (7.5 - 0.25)', near((await inv(raw)).totalQuantity, 7.25))

  // ── parity and reconciliation
  const bal = await balances(raw)
  check('raw balances add up to the total', near(bal.reduce((a, b) => a + Number(b.quantity), 0), 7.25), JSON.stringify(bal.map((b) => [b.warehouseCode, b.lotNumber, b.quantity])))
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  const mine = drift.filter((d) => [raw.id, fg.id, serial.id].includes(d.productId))
  check('reconciliation: no drift for the products of this check', mine.length === 0, JSON.stringify(mine))

  // ── Accounting books nothing for the production movements
  console.log('waiting for the outbox...')
  await sleep(8000)
  const ids = [...hRaw, ...(await history(raw)), ...hFg].filter((m) => String(m.movementType).startsWith('Production') || m.movementType === 'QcRelease').map((m) => m.movementId)
  const key2 = (x) => `StockMovement:${String(x).replace(/-/g, '').toLowerCase()}`
  const list = [...new Set(ids)].map((x) => `'${key2(x)}'`).join(',')
  const booked = Number(sql('AccountingDB', `SELECT COUNT(*) FROM JournalEntries WHERE SourceKey IN (${list})`))
  check(`Accounting has no journal entry for the ${new Set(ids).size} production movements`, booked === 0, `booked=${booked}`)

  console.log(`\nproducts ${raw.sku}, ${fg.sku}, ${serial.sku}; order ${order}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})()
