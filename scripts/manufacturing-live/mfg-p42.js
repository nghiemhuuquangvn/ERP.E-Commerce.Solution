// Manufacturing P4-2 live check: reversal of operation reports (BR-OP-07, TC-REV-001..003) and of finished-goods receipts (BR-FG-07, TC-REV-004..007), with the real Warehouse.
// Two fresh DS-01 sets: A for the report reversals (a full order of 100), B for the receipts and the failure injection (small orders, so their reservations share the opening stock).
// Through the Gateway. Default host = glcheck; MFG_HOST / MFG_EMAIL / MFG_PASSWORD switch tenant (SYSTEM: admin.erp.local / superadmin@system.local / SuperAdmin@123).
// Stops and starts the erp-warehouse-service container once (failure injection).
const http = require('http')
const { execFileSync } = require('child_process')
const host = process.env.MFG_HOST || 'glcheck.erp.local'
const email = process.env.MFG_EMAIL || 'admin@glcheck.test'
const password = process.env.MFG_PASSWORD || 'Gl-Check-Passw0rd!'
const repo = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
const docker = (...args) => execFileSync('docker', args, { encoding: 'utf8' }).trim()
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
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const plantNow = () => new Date(Date.now() + 7 * 3600e3)
const iso = (d) => d.toISOString().slice(0, 10)
const addDays = (d, n) => new Date(d.getTime() + n * 86400e3)

;(async () => {
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  let prefix, plantCode, ids, plant, quarantine
  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body, extra = {}) => call(method, `/mfg/api/v1/manufacturing${path}`, { ...auth, ...extra }, body)
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const code = (r) => r.json()?.errorCode

  // every group of orders runs on its own DS-01 set: the opening stock of the set is what the group's reservations share
  const reseed = async (tag) => {
    prefix = `ZZP42${S}${tag}-`
    plantCode = `ZZ-P42-${S}${tag}`
    const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
    ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
    plant = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, '')).plantId
    check(`DS-01 set ${tag} seeded`, !!plant && !!ids['TP-BAN01'], out.slice(-300))
    quarantine = (await m('GET', `/plants/${plant}`)).json().quarantineWarehouseId
  }

  await m('POST', '/reason-codes/seed-defaults')
  const reasons = (await m('GET', '/reason-codes')).json()
  const scrapReason = reasons.find((x) => x.type === 'Scrap' && x.isActive)?.id

  let due = addDays(plantNow(), 10)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)

  const create = async (key, qty) => (await m('POST', '/orders', { plantId: plant, itemId: ids[key], quantity: qty, dueDate: iso(due), outputWarehouseId: null })).json()
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const mat = (o, key) => o.materials.find((x) => x.componentCode === prefix + key)
  const op = (o, seq) => o.operations.find((x) => x.operationSeq === seq)
  const total = async (key) => Number((await wh('GET', `/inventory/${ids[key]}`)).json().totalQuantity)
  const lotsOf = async (key) => (await wh('GET', `/inventory/lots/product/${ids[key]}`)).json() ?? []
  const lotQty = (l) => (l ? Number(l.remainingQuantity ?? l.quantity) : 0)
  const report = (o, seq, good, scrap = 0, extra = {}) =>
    m('POST', `/orders/${o.id}/operations/${seq}/report`, { goodQty: good, scrapQty: scrap, reworkQty: 0, scrapReasonId: scrap > 0 ? scrapReason : null, reworkReasonId: null, machineId: null, note: null }, extra)
  const produce = async (o, qty) => { for (const seq of [10, 20, 30]) { const r = await report(o, seq, qty); if (r.status !== 201) return r } return { status: 201 } }
  const receive = (o, qty, extra = {}) => m('POST', `/orders/${o.id}/receipts`, { lines: [{ outputLineNo: null, quantity: qty, lotNumber: null }], note: null }, extra)
  const reverseReport = (o, id, note = 'wrong count', extra = {}) => m('POST', `/orders/${o.id}/operation-reports/${id}/reverse`, { note }, extra)
  const reverseReceipt = (o, id, note = 'wrong receipt', extra = {}) => m('POST', `/orders/${o.id}/receipts/${id}/reverse`, { note }, extra)
  const reports = async (o) => (await m('GET', `/orders/${o.id}/operation-reports`)).json()
  const docs = async (o) => (await m('GET', `/orders/${o.id}/stock-documents`)).json()
  const refs = async (key) => ((await wh('GET', `/movements/${ids[key]}?take=200`)).json()?.items ?? []).map((x) => x.reference)
  const release = async (o) => { await m('POST', `/orders/${o.id}/release`); return getOrder(o.id) }

  // ═══ 1. operation reports: TC-REV-001 / 002 ═══════════════════════════════════════════════════════════
  console.log('\n── operation reports ──')
  await reseed('A')
  let a = await release(await create('TP-BAN01', 100))
  const vit0 = await total('NVL-VIT01')
  let r = await report(a, 10, 60, 2)
  const r1 = r.json()?.report
  check('op10 reported Good 60 Scrap 2: backflush takes 992 screws', r.status === 201 && near(vit0 - (await total('NVL-VIT01')), 992), `${r.status} ${r.text.slice(0, 200)}`)
  a = await getOrder(a.id)
  check('operation 20 is Ready after the report', op(a, 20).status === 'Ready')

  r = await reverseReport(a, r1.id, ' ')
  check('a blank reason is refused (MFG_VALIDATION), nothing stored', r.status === 400 && code(r) === 'MFG_VALIDATION', `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReport(a, r1.id, 'counted the same pieces twice')
  const rev1 = r.json()
  check('TC-REV-001 reversing a report nobody used: 201, a Posted counter-report and a Posted RV document returning 992',
    r.status === 201 && rev1.isPending === false && rev1.report.reversalOfId === r1.id && rev1.report.status === 'Posted' && rev1.backflush?.type === 'Reversal' && rev1.backflush.status === 'Posted'
    && /^RV-\d{4}-\d{5}$/.test(rev1.backflush.docNo) && rev1.backflush.lines.length === 1 && near(rev1.backflush.lines[0].qty, 992) && rev1.backflush.lines[0].reversalAction === 'Return', `${r.status} ${r.text.slice(0, 500)}`)
  a = await getOrder(a.id)
  check('TC-REV-001 operation 10 Good 0 Scrap 0, ScrappedQty 0, screws consumed 0, stock back to its opening level, operation 20 Pending again',
    near(op(a, 10).goodQty, 0) && near(op(a, 10).scrapQty, 0) && near(a.scrappedQty, 0) && near(mat(a, 'NVL-VIT01').consumedQty, 0) && near(await total('NVL-VIT01'), vit0) && op(a, 20).status === 'Pending',
    JSON.stringify([op(a, 10), a.scrappedQty, mat(a, 'NVL-VIT01').consumedQty, a.operations.map((x) => x.status)]))
  const list1 = await reports(a)
  check('the original report is flagged reversed and the reversal report lists its original', list1.find((x) => x.id === r1.id)?.isReversed === true && list1.find((x) => x.id === rev1.report.id)?.reversalOfId === r1.id)
  const bfDoc = (await docs(a)).find((x) => x.id === r1.backflushDocumentId)
  check('the backflush document of the reversed report is flagged reversed', bfDoc?.isReversed === true, JSON.stringify(bfDoc)?.slice(0, 200))
  r = await reverseReport(a, r1.id)
  check('a second reversal of the same report: 400 MFG_REV_001', r.status === 400 && code(r) === 'MFG_REV_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReport(a, rev1.report.id)
  check('reversing a reversal: 400 MFG_REV_001', r.status === 400 && code(r) === 'MFG_REV_001', `${r.status} ${r.text.slice(0, 200)}`)

  // TC-REV-002: the next operation used the output
  const r2 = (await report(a, 10, 60)).json()?.report
  const r3 = (await report(a, 20, 50)).json()?.report
  const movements = (await refs('NVL-VIT01')).length
  r = await reverseReport(a, r2.id)
  check('TC-REV-002 op10 Good 60 used by op20 (50): 400 MFG_OP_007 with the figures, nothing sent to the Warehouse', r.status === 400 && code(r) === 'MFG_OP_007' && /50/.test(r.text) && (await refs('NVL-VIT01')).length === movements, `${r.status} ${r.text.slice(0, 300)}`)
  const key = `p42-${S}-k1`
  r = await reverseReport(a, r3.id, 'wrong count on op20', { 'Idempotency-Key': key })
  const rev3 = r.json()
  check('a report without backflush (op20) is reversed: 201 and no RV document', r.status === 201 && rev3.report.reversalOfId === r3.id && !rev3.backflush, `${r.status} ${r.text.slice(0, 300)}`)
  r = await reverseReport(a, r3.id, 'wrong count on op20', { 'Idempotency-Key': key })
  check('the same Idempotency-Key answers the same reversal', r.status === 201 && r.json().report.id === rev3.report.id, `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReport(a, r3.id, 'another reason', { 'Idempotency-Key': key })
  check('the same key with another reason: 400 MFG_GEN_007', r.status === 400 && code(r) === 'MFG_GEN_007', `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReport(a, r2.id)
  check('after op20 is reversed, op10 Good 60 can go (the screws come back again)', r.status === 201 && near(await total('NVL-VIT01'), vit0), `${r.status} ${r.text.slice(0, 200)}`)

  // ═══ 2. receipts: TC-REV-003..007 (orders of 20, so that their reservations share one set) ══════════
  console.log('\n── finished-goods receipts ──')
  await reseed('B')
  let b = await release(await create('TP-BAN01', 20))
  const thungBefore = await total('NVL-THUNG01')
  r = await produce(b, 20)
  check('the three operations of MO-B report Good 20', r.status === 201, `${r.status} ${r.text?.slice(0, 200)}`)
  check('op30 backflush took the phantom kit (20 cartons)', near(thungBefore - (await total('NVL-THUNG01')), 20), `${thungBefore} ${await total('NVL-THUNG01')}`)
  const report30 = (await reports(b)).find((x) => x.operationSeq === 30)
  const tp0 = await total('TP-BAN01')
  r = await receive(b, 8)
  const fr1 = r.json()
  check('receipt of 8 posted, lot on QcHold in the quarantine warehouse', r.status === 201 && fr1.status === 'Posted' && fr1.lines[0].isQcHold && fr1.lines[0].warehouseId === quarantine, `${r.status} ${r.text.slice(0, 300)}`)
  const lot1 = fr1.lines[0].lotNumber
  check('the posted receipt line keeps the lot the Warehouse used', fr1.lines[0].lots?.length === 1 && fr1.lines[0].lots[0].lotNumber === lot1 && near(fr1.lines[0].lots[0].qty, 8), JSON.stringify(fr1.lines[0].lots))
  r = await reverseReport(b, report30.id)
  check('TC-REV-003 op30 Good 20, 8 received: 400 MFG_OP_007', r.status === 400 && code(r) === 'MFG_OP_007', `${r.status} ${r.text.slice(0, 300)}`)

  r = await reverseReceipt(b, fr1.id, 'wrong item')
  const rv1 = r.json()
  check('TC-REV-004 reversing the receipt: 201, a Posted RV document that scraps the lot', r.status === 201 && rv1.type === 'Reversal' && rv1.status === 'Posted' && rv1.reversalOfId === fr1.id
    && rv1.lines.some((l) => l.kind === 'Receipt' && l.reversalAction === 'Scrap' && near(l.qty, 8)), `${r.status} ${r.text.slice(0, 500)}`)
  b = await getOrder(b.id)
  const lotAfter = (await lotsOf('TP-BAN01')).find((l) => l.lotNumber === lot1)
  check('TC-REV-004 CompletedQty 0, the lot is empty in the Warehouse, finished-goods stock back to its opening level',
    near(b.completedQty, 0) && lotQty(lotAfter) === 0 && near(await total('TP-BAN01'), tp0), JSON.stringify([b.completedQty, lotAfter, await total('TP-BAN01'), tp0]))
  check('the receipt document is flagged reversed', (await docs(b)).find((x) => x.id === fr1.id)?.isReversed === true)
  r = await reverseReceipt(b, fr1.id)
  check('a second reversal of the same receipt: 400 MFG_REV_001', r.status === 400 && code(r) === 'MFG_REV_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReport(b, report30.id)
  check('after the receipt is reversed the op30 report can go: 201, the cartons are back', r.status === 201 && near(await total('NVL-THUNG01'), thungBefore), `${r.status} ${r.text.slice(0, 200)}`)

  // TC-REV-005: released from quality hold
  let c = await release(await create('TP-BAN01', 20))
  await produce(c, 20)
  const fr2 = (await receive(c, 8)).json()
  r = await m('POST', `/stock-documents/${fr2.id}/qc-release`)
  check('interim quality release of the lot', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReceipt(c, fr2.id)
  check('TC-REV-005 a receipt whose lot was released from quality hold cannot be reversed: 400 MFG_FG_004', r.status === 400 && code(r) === 'MFG_FG_004', `${r.status} ${r.text.slice(0, 300)}`)

  // TC-REV-006: the lot was partly used
  const fr3 = (await receive(c, 12)).json()
  const lot3 = (await lotsOf('TP-BAN01')).find((l) => l.lotNumber === fr3.lines[0].lotNumber)
  r = await wh('POST', '/movements', { productId: ids['TP-BAN01'], quantity: 4, movementType: 'ProductionScrap', reference: `ZZP42-${S}#USE`, lotId: lot3.lotId, warehouseId: quarantine })
  check('4 of the 12 were used in the Warehouse', r.status >= 200 && r.status < 300, `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReceipt(c, fr3.id)
  check('TC-REV-006 a lot that was partly used cannot be reversed: 400 MFG_FG_004 naming the quantity left', r.status === 400 && code(r) === 'MFG_FG_004' && /\b8\b/.test(r.text), `${r.status} ${r.text.slice(0, 300)}`)
  check('nothing was stored by the refusals (no RV document on that order)', !(await docs(c)).some((x) => x.type === 'Reversal'))

  // TC-REV-007: a Completed order reopens
  let e = await release(await create('TP-BAN01', 20))
  await produce(e, 20)
  const e1 = (await receive(e, 8)).json()
  await receive(e, 12)
  e = await getOrder(e.id)
  check('two receipts (8 + 12) complete the order by themselves', e.status === 'Completed' && near(e.completedQty, 20), `${e.status} ${e.completedQty}`)
  const someReport = (await reports(e))[0]
  r = await reverseReport(e, someReport.id)
  check('a report on a Completed order cannot be reversed until the order is reopened: 400 MFG_OP_001', r.status === 400 && code(r) === 'MFG_OP_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await reverseReceipt(e, e1.id, 'wrong lot')
  e = await getOrder(e.id)
  check('TC-REV-007 reversing the 8: CompletedQty 12 and the order is InProgress again', r.status === 201 && near(e.completedQty, 12) && e.status === 'InProgress', `${r.status} ${e.status} ${e.completedQty} ${r.text.slice(0, 200)}`)
  const hist = (await m('GET', `/orders/${e.id}/history`)).json()
  check('history has the reopen row', JSON.stringify(hist).includes('Reopened: receipt reversed'), JSON.stringify(hist)?.slice(-300))
  r = await receive(e, 8)
  check('the freed quantity can be received again', r.status === 201 && near((await getOrder(e.id)).completedQty, 20), `${r.status} ${r.text.slice(0, 200)}`)

  // ═══ 3. failure injection ═════════════════════════════════════════════════════════════════════════════
  console.log('\n── failure injection (Warehouse stopped) ──')
  let d = await release(await create('TP-BAN01', 20))
  const vitBefore = await total('NVL-VIT01')
  const d1 = (await report(d, 10, 6)).json()?.report
  check('MO-D op10 Good 6 consumed 96 screws', near(vitBefore - (await total('NVL-VIT01')), 96))
  let g = await release(await create('TP-BAN01', 10))
  await produce(g, 10)
  const frg = (await receive(g, 4)).json()
  const vitConsumed = await total('NVL-VIT01')   // after MO-G took its screws too
  let pendingRev
  docker('stop', 'erp-warehouse-service')
  try {
    await sleep(2500)
    r = await reverseReport(d, d1.id, 'warehouse is down')
    pendingRev = r.json()
    check('report reversal with the Warehouse down: 202, reversal report and RV document Pending', r.status === 202 && pendingRev.isPending === true && pendingRev.report.status === 'Pending' && pendingRev.backflush?.status === 'Pending', `${r.status} ${r.text.slice(0, 400)}`)
    check('nothing is applied while it waits (operation 10 still Good 6)', near(op(await getOrder(d.id), 10).goodQty, 6))
    r = await report(d, 10, 1)
    check('a new report on that order waits: 400 MFG_REV_002', r.status === 400 && code(r) === 'MFG_REV_002', `${r.status} ${r.text.slice(0, 200)}`)
    r = await receive(d, 1)
    check('a new receipt on that order waits too: 400 MFG_REV_002', r.status === 400 && code(r) === 'MFG_REV_002', `${r.status} ${r.text.slice(0, 200)}`)
    r = await reverseReceipt(g, frg.id)
    check('a receipt reversal needs the Warehouse to check the lot: 400 MFG_INT_001, nothing stored', r.status === 400 && code(r) === 'MFG_INT_001', `${r.status} ${r.text.slice(0, 200)}`)
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  let done = null
  for (let i = 0; i < 36 && !done; i++) {
    await sleep(5000)
    const cur = (await reports(d)).find((x) => x.id === pendingRev.report.id)
    if (cur?.status === 'Posted') done = cur
  }
  check('after the Warehouse is back the job posts the reversal', !!done)
  check('a receipt reversal that was refused left nothing behind', !(await docs(g)).some((x) => x.type === 'Reversal'))
  d = await getOrder(d.id)
  const rvRefs = (await refs('NVL-VIT01')).filter((x) => x === pendingRev.backflush.lines[0].movementKey)
  check('exactly one movement for its key, screws back, operation 10 Good 0, original flagged reversed',
    rvRefs.length === 1 && near(await total('NVL-VIT01'), vitConsumed + 96) && near(op(d, 10).goodQty, 0) && (await reports(d)).find((x) => x.id === d1.id)?.isReversed === true, `${rvRefs.length} ${await total('NVL-VIT01')} ${vitConsumed}`)
  r = await reverseReceipt(g, frg.id, 'now the Warehouse is up')
  check('the receipt reversal works once the Warehouse is back', r.status === 201 && lotQty((await lotsOf('TP-BAN01')).find((l) => l.lotNumber === frg.lines[0].lotNumber)) === 0, `${r.status} ${r.text.slice(0, 200)}`)

  // ═══ 4. permissions and consistency ═══════════════════════════════════════════════════════════════════
  console.log('\n── permissions and consistency ──')
  const mail = `zz42${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P42' })
  const plain = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (plain.status === 200 && plain.json().accessToken) {
    const pa = { Authorization: `Bearer ${plain.json().accessToken}`, ...J }
    for (const [what, path] of [['operation-report reversal', `/orders/${a.id}/operation-reports/${r1.id}/reverse`], ['receipt reversal', `/orders/${b.id}/receipts/${fr1.id}/reverse`]]) {
      const x = await call('POST', `/mfg/api/v1/manufacturing${path}`, pa, { note: 'try' })
      check(`a user without the permission gets 403 on ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', plain.status, plain.text.slice(0, 100))
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  const mine = drift.filter((x) => Object.values(ids).includes(x.productId) && x.kind !== 'Reserved')
  check('reconciliation: no quantity / lot drift for the DS-01 items of set B after reversals', mine.length === 0, JSON.stringify(mine).slice(0, 300))

  // clean up what the open orders still hold in the Warehouse
  for (const o of [a, b, c, d, e, g]) {
    const rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${o.id}`)).json()
    if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p42 cleanup' })
  }

  console.log(`\nprefix ZZP42${S}A/B; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
