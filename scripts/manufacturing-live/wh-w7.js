// Warehouse W7 live check (Manufacturing Phase 4, P4-1): a part of a lot moves as a sub-lot, a movement answers the lots it used, a lot on
// quality hold is issued only on request (rework). The split and the user-endpoint refusal are driven through the public Warehouse API; the flag
// itself and the multi-lot breakdown go through the internal stock API, which only Manufacturing can call, so they are played by the dev-only
// POST /mfg/.../seed/lot-cycle. Dev / demo tenants only. Default host = SYSTEM (admin.erp.local); WH_HOST / WH_EMAIL / WH_PASSWORD switch tenant.
const http = require('http')
const repo = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
const { execFileSync } = require('child_process')
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
const near = (a, b) => Math.abs(Number(a) - b) < 1e-6
const ok2xx = (r) => r.status >= 200 && r.status < 300

const S = Date.now().toString(36).toUpperCase().slice(-4)
const prefix = `ZZW7${S}-`
const plant = `ZZ-W7-${S}`
const seed = () => execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plant], { encoding: 'utf8' })

;(async () => {
  const out = seed()
  const ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
  const whs = JSON.parse(/^warehouses (.*)$/m.exec(out)[1])
  check('DS-01 set seeded', /opening stock: 12 rows received/.test(out), out.split('\n').filter((l) => /opening|FAIL|Error/.test(l)).join(' | '))

  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const fgId = ids['TP-BAN01']
  const lots = async (id, depleted = false) => (await wh('GET', `/inventory/lots/product/${id}${depleted ? '?includeDepleted=true' : ''}`)).json() ?? []
  const movement = (body) => wh('POST', '/movements', body)

  // ── 1. a lot on quality hold in the quarantine warehouse
  const lotNo = `W7-${S}`
  let r = await movement({ productId: fgId, quantity: 10, movementType: 'ProductionReceipt', reference: `W7-${S}#1`, lotNumber: lotNo, warehouseId: whs.qc, unitCost: 100000, initialLotStatus: 'QcHold' })
  const received = r.json()
  check('receipt of 10 on QcHold answers its lot in lotId and in one lots row', ok2xx(r) && received?.lotId && received.lots?.length === 1 && received.lots[0].lotId === received.lotId && near(received.lots[0].quantity, 10), r.text.slice(0, 300))
  const parent = received?.lotId

  // ── 2. a PART moves: the part is a new sub-lot in the finished-goods warehouse, the movement keeps the requested lot
  const split = { productId: fgId, quantity: 4, movementType: 'SubcontractTransfer', reference: `W7-${S}#2`, warehouseId: whs.qc, toWarehouseId: whs.fg, lotId: parent }
  r = await movement(split)
  const moved = r.json()
  const sub = moved?.lots?.[0]
  check('partial move of 4 is accepted (it was WH_PARTIAL_LOT_MOVE before W7)', ok2xx(r), `${r.status} ${r.text.slice(0, 300)}`)
  check('movement lotId stays the lot named in the request; lots[0] is the new sub-lot with parentLotId', moved?.lotId === parent && sub?.parentLotId === parent && sub.lotNumber === `${lotNo}-S1` && near(sub.quantity, 4) && sub.warehouseId === whs.fg, JSON.stringify(moved))
  let all = await lots(fgId)
  const p1 = all.find((l) => l.lotId === parent)
  const s1 = all.find((l) => l.lotId === sub?.lotId)
  check('public lot list: parent 6 in quarantine on hold, sub-lot 4 in finished goods on hold with parentLotId', near(p1?.remainingQuantity, 6) && p1.warehouseId === whs.qc && p1.status === 'QcHold' && near(s1?.remainingQuantity, 4) && s1.warehouseId === whs.fg && s1.status === 'QcHold' && s1.parentLotId === parent, JSON.stringify([p1, s1]))
  check('sub-lot keeps cost, expiry and the received date of its parent', s1 && p1 && near(s1.unitCost, p1.unitCost) && s1.expiryDate === p1.expiryDate && s1.receivedDate === p1.receivedDate, JSON.stringify([p1, s1]))
  const inv = (await wh('GET', `/inventory/${fgId}`)).json()
  check('item total unchanged by the split (20 old + 10 = 30)', near(inv?.totalQuantity, 30), JSON.stringify(inv))
  const bal = (await wh('GET', `/inventory/${fgId}/balances`)).json() ?? []
  check('balances: parent 6 at quarantine, sub-lot 4 at finished goods', near(bal.find((b) => b.lotId === parent)?.quantity, 6) && near(bal.find((b) => b.lotId === sub?.lotId)?.quantity, 4), JSON.stringify(bal.map((b) => [b.warehouseCode, b.lotNumber, b.quantity])))
  r = await movement(split)
  const replay = r.json()
  check('the same key again is a replay with the same sub-lot, no third lot', ok2xx(r) && replay?.replayed === true && replay.lots?.[0]?.lotId === sub?.lotId && (await lots(fgId)).filter((l) => l.lotNumber.startsWith(`${lotNo}-S`)).length === 1, r.text.slice(0, 300))
  r = await movement({ ...split, quantity: 5 })
  check('another quantity under the same key is refused (idempotency conflict)', r.status === 409 && /WH_IDEMPOTENCY_CONFLICT/.test(r.text), `${r.status} ${r.text.slice(0, 200)}`)
  r = await movement({ ...split, reference: `W7-${S}#2B`, quantity: 3 })
  check('a second split gets the next number -S2', ok2xx(r) && r.json()?.lots?.[0]?.lotNumber === `${lotNo}-S2`, r.text.slice(0, 300))
  r = await movement({ ...split, reference: `W7-${S}#2C`, quantity: 50 })
  check('more than the lot holds is refused with WH_LOT_QUANTITY', r.status === 400 && /WH_LOT_QUANTITY/.test(r.text), `${r.status} ${r.text.slice(0, 200)}`)

  // ── 3. only the sub-lot is released
  r = await movement({ productId: fgId, quantity: 1, movementType: 'QcRelease', reference: `W7-${S}#3`, lotId: sub?.lotId })
  all = await lots(fgId)
  check('release of the sub-lot leaves the parent on hold', ok2xx(r) && all.find((l) => l.lotId === sub?.lotId)?.status === 'Available' && all.find((l) => l.lotId === parent)?.status === 'QcHold', `${r.status} ${r.text.slice(0, 200)}`)

  // ── 4. the user endpoint cannot issue a held lot, also when it sends the flag
  r = await movement({ productId: fgId, quantity: 1, movementType: 'ProductionIssue', reference: `W7-${S}#4`, lotId: parent, allowHeldLot: true })
  check('user endpoint with allowHeldLot=true is still refused WH_LOT_NOT_USABLE', r.status === 400 && /WH_LOT_NOT_USABLE/.test(r.text), `${r.status} ${r.text.slice(0, 200)}`)

  // ── 5. the internal API through the dev-only cycle: split, rework issue of a held lot, multi-lot breakdown
  const order = `MO-ZZ-W7-${S}`
  r = await call('POST', '/mfg/api/v1/manufacturing/seed/lot-cycle', auth, { orderNumber: order, finishedProductId: fgId, quarantineWarehouseId: whs.qc, targetWarehouseId: whs.fg, componentProductId: ids['NVL-SON01'], rawWarehouseId: whs.raw, componentQuantity: 52.5 })
  const cycle = r.json()
  check('lot cycle answered 200', r.status === 200 && Array.isArray(cycle?.steps), `${r.status} ${r.text.slice(0, 400)}`)
  for (const step of cycle?.steps ?? []) check(`cycle step ${step.name}`, step.ok === true, step.detail)
  const names = (cycle?.steps ?? []).map((x) => x.name)
  check('cycle covered split, replay, release, refusal without flag, rework issue and the multi-lot breakdown',
    ['receipt-on-hold', 'split-transfer', 'split-replay', 'release-sub-lot', 'issue-held-without-flag', 'issue-held-without-reason', 'issue-held-for-rework', 'issue-breakdown', 'issue-breakdown-replay'].every((n) => names.includes(n)), names.join(','))

  // what the cycle left, seen through the public API
  all = await lots(fgId)
  const cp = all.find((l) => l.lotNumber === `LC-${order}`)
  const cs = all.find((l) => l.lotNumber === `LC-${order}-S1`)
  check('cycle lot: 10 received, 4 split off, 2 issued for rework = 4 left, still on hold', near(cp?.remainingQuantity, 4) && cp.status === 'QcHold', JSON.stringify(cp))
  check('cycle sub-lot: 4, released (Available), in finished goods, parent = the cycle lot', near(cs?.remainingQuantity, 4) && cs.status === 'Available' && cs.warehouseId === whs.fg && cs.parentLotId === cp?.lotId, JSON.stringify(cs))
  const sonMoves = ((await wh('GET', `/movements/${ids['NVL-SON01']}?take=20`)).json()?.items ?? []).filter((m) => m.reference === `${order}#5`)
  const rows = sonMoves[0]?.lots ?? []
  check('history of the paint issue shows two lots: L-SON-A 30 and L-SON-B 22.5, lotId empty', sonMoves.length === 1 && rows.length === 2 && rows.some((l) => l.lotNumber === 'L-SON-A' && near(l.quantity, 30)) && rows.some((l) => l.lotNumber === 'L-SON-B' && near(l.quantity, 22.5)) && !sonMoves[0].lotId, JSON.stringify(sonMoves))
  const reworkMove = (((await wh('GET', `/movements/${fgId}?take=50`)).json()?.items) ?? []).find((m) => m.reference === `${order}#4`)
  check('rework issue is on the movement history with its reason', reworkMove && /Rework/.test(reworkMove.reason ?? '') && reworkMove.lotId === cp?.lotId, JSON.stringify(reworkMove))

  // ── reconciliation: the item total, the balances and the lots still agree
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  const mine = drift.filter((d) => Object.values(ids).includes(d.productId) && d.kind !== 'Reserved')
  check('reconciliation: no quantity / lot drift for the DS-01 items after splits and rework', mine.length === 0, JSON.stringify(mine))

  console.log(`\nprefix ${prefix} plant ${plant}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.message); process.exit(1) })
