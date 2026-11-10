// Warehouse W6 live check: DS-01 opening stock (spec 1.7) entered by scripts/seed-ds01-products.js --stock --seed, then verified through the APIs:
// totals and lots, the expired lot X, first-expiry-first suggestion (TC-MAT-010), reservation, refusal to issue the expired lot (TC-MAT-009),
// idempotent second run, flags, reconciliation. Default host = SYSTEM (admin.erp.local); WH_HOST / WH_EMAIL / WH_PASSWORD switch tenant.
const http = require('http')
const repoRoot = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
const crypto = require('crypto')
const { execFileSync } = require('child_process')
const host = process.env.WH_HOST || 'admin.erp.local'
const email = process.env.WH_EMAIL || 'superadmin@system.local'
const password = process.env.WH_PASSWORD || 'SuperAdmin@123'
const repo = repoRoot
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
const near = (a, b) => Math.abs(Number(a) - b) < 1e-6
const ok2xx = (r) => r.status >= 200 && r.status < 300

const S = Date.now().toString(36).toUpperCase().slice(-4)
const prefix = `ZZW6${S}-`
const plant = `ZZ-W6-${S}`
const seed = () => execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plant], { encoding: 'utf8' })

;(async () => {
  const out1 = seed()
  const ids = JSON.parse(/^products (.*)$/m.exec(out1)[1])
  const whs = JSON.parse(/^warehouses (.*)$/m.exec(out1)[1])
  check('first run received the 12 opening-stock rows', /opening stock: 12 rows received, 0 already there/.test(out1), out1.split('\n').filter((l) => /opening|FAIL|Error/.test(l)).join(' | '))
  const seedLine = out1.trim().split('\n').pop()
  check('Manufacturing seeder answered 200', seedLine.startsWith('200'), seedLine.slice(0, 300))
  check('its answer says stock is entered by the script', /seed-ds01-products\.js --stock/.test(seedLine))

  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const inv = async (code) => (await wh('GET', `/inventory/${ids[code]}`)).json()
  const lots = async (code) => (await wh('GET', `/inventory/lots/product/${ids[code]}`)).json() ?? []
  const bal = async (code) => (await wh('GET', `/inventory/${ids[code]}/balances`)).json() ?? []

  // ── §1.7 totals
  const expected = { 'BTP-MAT01': 120, 'BTP-CHAN01': 300, 'NVL-VIT01': 2000, 'NVL-SON01': 100, 'NVL-KEO01': 50, 'NVL-KEO02': 20, 'NVL-THUNG01': 500, 'NVL-XOP01': 1000, 'NVL-GO01': 5000, 'TP-BAN01': 20 }
  for (const [code, qty] of Object.entries(expected)) check(`stock ${code} = ${qty}`, near((await inv(code))?.totalQuantity, qty), JSON.stringify(await inv(code)))
  const fgBal = await bal('TP-BAN01')
  check('TP-BAN01 sits in the finished-goods warehouse only', fgBal.length === 1 && fgBal[0].warehouseId === whs.fg && fgBal[0].lotNumber === 'L-TP-OLD', JSON.stringify(fgBal))
  const sonLots = await lots('NVL-SON01')
  check('paint lots A 30, B 50, X 20 with the book expiry dates', ['L-SON-A', 'L-SON-B', 'L-SON-X'].every((n, i) => near(sonLots.find((l) => l.lotNumber === n)?.remainingQuantity, [30, 50, 20][i])), JSON.stringify(sonLots.map((l) => [l.lotNumber, l.remainingQuantity, l.expiryDate])))
  const x = sonLots.find((l) => l.lotNumber === 'L-SON-X')
  check('L-SON-X is expired (2026-10-31)', x && new Date(x.expiryDate) < new Date(), JSON.stringify(x))
  check('wood lot L-GO-01 5000', near((await lots('NVL-GO01')).find((l) => l.lotNumber === 'L-GO-01')?.remainingQuantity, 5000))

  // ── lot flags
  for (const code of ['TP-BAN01', 'NVL-GO01', 'NVL-SON01']) {
    const p = (await call('GET', `/prd/api/v1/products/${ids[code]}`, auth)).json()
    check(`${code} is lot-controlled`, p?.isLotControlled === true, JSON.stringify(p)?.slice(0, 200))
  }
  const k = (await call('GET', `/prd/api/v1/products/${ids['NVL-KEO01']}`, auth)).json()
  check('NVL-KEO01 is not lot-controlled', k?.isLotControlled === false)

  // ── TC-MAT-010 suggestion: A 30 + B 22.5, never X
  let r = await wh('GET', `/inventory/${ids['NVL-SON01']}/suggest-lots?quantity=52.5&warehouseId=${whs.raw}`)
  let s = r.json()
  check('suggest 52.5 of paint: A 30 + B 22.5, no X (TC-MAT-010)', s?.lots?.length === 2 && s.lots[0].lotNumber === 'L-SON-A' && near(s.lots[0].take, 30) && s.lots[1].lotNumber === 'L-SON-B' && near(s.lots[1].take, 22.5) && near(s.shortfall, 0), r.text.slice(0, 300))
  r = await wh('GET', `/inventory/${ids['NVL-SON01']}/suggest-lots?quantity=100&warehouseId=${whs.raw}`)
  check('asking 100 leaves a shortfall of 20 (the expired lot does not count: TC-MRP-015 available 80)', near(r.json()?.shortfall, 20), r.text.slice(0, 200))

  // ── reservation for a throw-away order takes A then B
  const mo = crypto.randomUUID()
  r = await wh('POST', '/inventory/reservations/source', { sourceType: 'ProductionOrder', sourceId: mo, sourceReference: `MO-ZZ-W6-${S}`, warehouseId: whs.raw, lines: [{ productId: ids['NVL-SON01'], quantity: 52.5, sourceLineId: crypto.randomUUID() }] })
  const res = r.json()
  const alloc = res?.lines?.[0]?.lots ?? []
  check('reserve 52.5: A 30 then B 22.5', ok2xx(r) && alloc.length === 2 && alloc[0].lotNumber === 'L-SON-A' && near(alloc[0].quantity, 30) && alloc[1].lotNumber === 'L-SON-B' && near(alloc[1].quantity, 22.5), r.text.slice(0, 300))
  check('the expired lot holds nothing', near((await lots('NVL-SON01')).find((l) => l.lotNumber === 'L-SON-X')?.reservedQuantity, 0))

  // ── TC-MAT-009: the expired lot cannot be issued
  r = await wh('POST', '/movements', { productId: ids['NVL-SON01'], quantity: 5, movementType: 'ProductionIssue', reference: `MO-ZZ-W6-${S}#X`, lotId: x.lotId, warehouseId: whs.raw })
  check('issue from L-SON-X refused with WH_LOT_EXPIRED (TC-MAT-009)', r.status === 400 && /WH_LOT_EXPIRED/.test(r.text), `${r.status} ${r.text.slice(0, 240)}`)
  check('and nothing left the lot', near((await lots('NVL-SON01')).find((l) => l.lotNumber === 'L-SON-X')?.remainingQuantity, 20))
  r = await wh('POST', '/movements', { productId: ids['NVL-SON01'], quantity: 5, movementType: 'ProductionScrap', reference: `MO-ZZ-W6-${S}#SX`, lotId: x.lotId, warehouseId: whs.raw })
  check('but the expired lot can be scrapped (quality must be able to write it off)', ok2xx(r), `${r.status} ${r.text.slice(0, 200)}`)

  // a held lot (TC-MAT-011)
  r = await wh('POST', '/movements', { productId: ids['NVL-SON01'], quantity: 10, movementType: 'ProductionReceipt', reference: `MO-ZZ-W6-${S}#Q`, lotNumber: 'L-SON-Q', warehouseId: whs.raw, unitCost: 60000, initialLotStatus: 'QcHold', expiryDate: '2027-06-30T00:00:00Z' })
  const q = r.json()
  r = await wh('POST', '/movements', { productId: ids['NVL-SON01'], quantity: 1, movementType: 'ProductionIssue', reference: `MO-ZZ-W6-${S}#QI`, lotId: q?.lotId, warehouseId: whs.raw })
  check('issue from a lot on QC hold refused (TC-MAT-011)', r.status === 400 && /WH_LOT_NOT_USABLE/.test(r.text), `${r.status} ${r.text.slice(0, 200)}`)

  // free the reservation again
  r = await wh('POST', '/inventory/release', { reservationId: res.reservationId, reason: 'zz w6 cleanup' })
  check('reservation released', ok2xx(r), `${r.status} ${r.text.slice(0, 160)}`)

  // ── second run changes nothing
  const out2 = seed()
  check('second run: 0 rows received, 12 already there', /opening stock: 0 rows received, 12 already there/.test(out2), out2.split('\n').filter((l) => /opening/.test(l)).join(' | '))
  const second = JSON.parse(out2.trim().split('\n').pop().replace(/^\d+ /, ''))
  const createdTotal = Object.entries(second.created ?? {}).filter(([k]) => k !== 'itemProfile').reduce((t, [, n]) => t + n, 0)
  check('second Manufacturing seed creates nothing new (item profiles are an upsert)', createdTotal === 0, JSON.stringify(second.created))
  check('stock after the second run: paint 100 + 10 held - 5 scrapped = 105', near((await inv('NVL-SON01')).totalQuantity, 105), JSON.stringify(await inv('NVL-SON01')))
  check('wood still 5000', near((await inv('NVL-GO01')).totalQuantity, 5000))

  // ── reconciliation
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  const mine = drift.filter((d) => Object.values(ids).includes(d.productId) && d.kind !== 'Reserved')
  check('reconciliation: no quantity / lot drift for the DS-01 items', mine.length === 0, JSON.stringify(mine))

  console.log(`\nprefix ${prefix} plant ${plant}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.message); process.exit(1) })
