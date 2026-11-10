// Manufacturing P3-7 end-to-end check: TC-E2E-001 (manual order, whole cycle), TC-E2E-005 (hold and change of quantity in the middle), TC-GEN-006 (50 parallel
// order numbers) and the two owner decisions of P3-7 (R234: no manual issue of a backflush material, R219: no deactivation of a plant with unfinished orders).
// Each scenario runs on its own fresh DS-01 set. Through the Gateway, with the real Warehouse. Default host = glcheck; MFG_HOST / MFG_EMAIL / MFG_PASSWORD switch
// tenant (SYSTEM: admin.erp.local / superadmin@system.local / SuperAdmin@123).
// Adaptations of the spec text (docs/business/Manufacturing/Phase3-Acceptance.md): step 3 is a second MANUAL order (automatic child orders are T4.12),
// step 9 "FQC Pass" is the interim quality release (FQC documents are Phase 4), step 11 returns material on the COMPLETED order (accepted by the owner).
const http = require('http')
const { execFileSync } = require('child_process')
const host = process.env.MFG_HOST || 'glcheck.erp.local'
const email = process.env.MFG_EMAIL || 'admin@glcheck.test'
const password = process.env.MFG_PASSWORD || 'Gl-Check-Passw0rd!'
const repo = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
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
const numbers = {}   // document numbers produced, for the summary

function seed(prefix, plantCode) {
  const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
  const answer = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, ''))
  return { ids: JSON.parse(/^products (.*)$/m.exec(out)[1]), plant: answer.plantId, out }
}

;(async () => {
  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body, extra = {}) => call(method, `/mfg/api/v1/manufacturing${path}`, { ...auth, ...extra }, body)
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const code = (r) => r.json()?.errorCode

  await m('POST', '/reason-codes/seed-defaults')
  const reasons = (await m('GET', '/reason-codes')).json()
  const reasonOf = (type) => reasons.find((x) => x.type === type && x.isActive)?.id
  const [scrapReason, holdReason, cancelReason] = ['Scrap', 'Hold', 'Cancel'].map(reasonOf)
  check('Scrap, Hold and Cancel reasons exist', !!scrapReason && !!holdReason && !!cancelReason)
  const emps = (await call('GET', '/mst/api/v1/masterdata/employees?status=0&pageSize=200', auth)).json()
  const empIds = (emps?.items ?? []).map((e) => e.id)
  check('employees from MasterData', empIds.length >= 2)

  let due = addDays(plantNow(), 10)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)

  // helpers bound to one DS-01 set
  const tools = (set, prefix) => {
    const { ids, plant } = set
    const create = async (key, qty, outputWarehouseId = null) => (await m('POST', '/orders', { plantId: plant, itemId: ids[key], quantity: qty, dueDate: iso(due), outputWarehouseId })).json()
    const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
    const mat = (o, key) => o.materials.find((x) => x.componentCode === prefix + key)
    const op = (o, seq) => o.operations.find((x) => x.operationSeq === seq)
    const inv = async (key) => (await wh('GET', `/inventory/${ids[key]}`)).json()
    const total = async (key) => Number((await inv(key)).totalQuantity)
    const lotsOf = async (key) => (await wh('GET', `/inventory/lots/product/${ids[key]}`)).json() ?? []
    // the public lot list leaves out a lot that is used up: a missing lot has nothing left
    const lotQty = (l) => (l ? Number(l.remainingQuantity ?? l.quantity) : 0)
    const docs = async (o) => (await m('GET', `/orders/${o.id}/stock-documents`)).json()
    const issue = (o, lines, extra = {}) => m('POST', `/orders/${o.id}/material-issues`, { lines, reasonCodeId: null, note: null }, extra)
    const ret = (o, lines) => m('POST', `/orders/${o.id}/material-returns`, { lines, reasonCodeId: null, note: null })
    const report = (o, seq, goodQty, scrapQty = 0, extra = {}) =>
      m('POST', `/orders/${o.id}/operations/${seq}/report`, { goodQty, scrapQty, reworkQty: 0, scrapReasonId: scrapQty > 0 ? scrapReason : null, reworkReasonId: null, machineId: null, note: null }, extra)
    const receive = (o, lines, extra = {}) => m('POST', `/orders/${o.id}/receipts`, { lines, note: null }, extra)
    const reservation = async (o) => (await wh('GET', `/inventory/reservations/source/ProductionOrder/${o.id}`)).json()
    const release = (o) => m('POST', `/orders/${o.id}/release`)
    const settled = async (o) => { for (let i = 0; i < 12; i++) { const x = await getOrder(o.id); if (!x.reservationSyncPending) return x; await sleep(2500) } return getOrder(o.id) }
    return { ids, plant, create, getOrder, mat, op, inv, total, lotsOf, lotQty, docs, issue, ret, report, receive, reservation, release, settled }
  }

  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  // TC-E2E-001
  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  console.log('\n── TC-E2E-001 ──')
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const prefixA = `ZZP37A${S}-`
  const plantCodeA = `ZZ-P37A-${S}`
  const setA = seed(prefixA, plantCodeA)
  check('DS-01 set A seeded', !!setA.plant && !!setA.ids['TP-BAN01'], setA.out.slice(-300))
  const T = tools(setA, prefixA)
  const plantInfo = (await m('GET', `/plants/${T.plant}`)).json()
  const quarantine = plantInfo.quarantineWarehouseId
  const fgWarehouse = plantInfo.finishedGoodsWarehouseId

  // 1 create MO-A
  let a = await T.create('TP-BAN01', 100)
  check('1. MO-A created (Draft, 100)', !!a?.id && a.status === 'Draft' && near(a.quantity, 100), JSON.stringify(a)?.slice(0, 200))
  numbers.moA = a.orderNo

  // 2 release with the CHAN shortage
  let r = await T.release(a)
  const shortages = r.json()?.shortages ?? []
  const chanShort = shortages.find((s) => s.componentCode === prefixA + 'BTP-CHAN01')
  check('TC-PO-011 / TC-PO-012 2. release: Released with ONE shortage, BTP-CHAN01 need 400 / allocatable 300 / short 100',
    r.status === 200 && shortages.length === 1 && !!chanShort && near(chanShort.need, 400) && near(chanShort.allocatable, 300) && near(chanShort.shortage, 100), `${r.status} ${r.text.slice(0, 400)}`)
  a = await T.getOrder(a.id)
  check('2. Released, operation 10 Ready, 20 and 30 Pending', a.status === 'Released' && T.op(a, 10).status === 'Ready' && T.op(a, 20).status === 'Pending' && T.op(a, 30).status === 'Pending')

  // 3 child order: a second manual order for 100 legs, produced and received
  // the legs are a semi-finished product used in the raw-material warehouse, so the child order receives them there (the output warehouse of an order can be chosen)
  let child = await T.create('BTP-CHAN01', 100, plantInfo.rawMaterialWarehouseId)
  numbers.child = child.orderNo
  await T.release(child)
  child = await T.getOrder(child.id)
  const childManual = child.materials.filter((x) => x.issueMethod === 'Manual')
  r = await T.issue(child, childManual.map((x) => ({ lineNo: x.lineNo, quantity: x.requiredQty, lotId: null })))
  check('3. child order CHAN 100: its manual material issued', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  let childOk = true
  for (const o of child.operations) { r = await T.report(child, o.operationSeq, 100); childOk = childOk && r.status === 201 }
  check('3. child order: operations (if any) report Good 100', childOk, `${r.status} ${r.text.slice(0, 200)}`)
  r = await T.receive(child, [{ outputLineNo: null, quantity: 100, lotNumber: null }])
  check('3. child order: receipt 100 posted, order Completed by itself', r.status === 201 && (await T.getOrder(child.id)).status === 'Completed', `${r.status} ${r.text.slice(0, 300)}`)
  check('3. legs in stock are now 400', near(await T.total('BTP-CHAN01'), 400), `${await T.total('BTP-CHAN01')}`)

  // 4 issues; R234 and the tolerance first
  const stock0 = { vit: await T.total('NVL-VIT01'), thung: await T.total('NVL-THUNG01'), xop: await T.total('NVL-XOP01'), keo: await T.total('NVL-KEO01'), mat: await T.total('BTP-MAT01'), tp: await T.total('TP-BAN01') }
  r = await T.issue(a, [{ lineNo: T.mat(a, 'NVL-VIT01').lineNo, quantity: 100, lotId: null }])
  check('4. R234: a backflush material (screws) cannot be issued by hand: 400 MFG_MAT_013, nothing stored', r.status === 400 && code(r) === 'MFG_MAT_013' && (await T.docs(a)).length === 0, `${r.status} ${r.text.slice(0, 200)}`)
  r = await T.issue(a, [{ lineNo: T.mat(a, 'NVL-KEO01').lineNo, quantity: 10.31, lotId: null }])
  check('4. glue 10.31 passes the 3 % tolerance of 10: 400 MFG_MAT_002', r.status === 400 && code(r) === 'MFG_MAT_002', `${r.status} ${r.text.slice(0, 200)}`)
  const sonLine = T.mat(a, 'NVL-SON01')
  const sug = (await m('GET', `/orders/${a.id}/material-issues/suggest-lots?lineNo=${sonLine.lineNo}&quantity=52.5`)).json()
  const lotNames = (sug?.suggestion?.lots ?? []).map((l) => l.lotNumber).concat((sug?.reservedLots ?? []).map((l) => l.lotNumber)).filter(Boolean)
  check('4. suggest-lots never offers the expired lot X', !lotNames.some((n) => /L-SON-X/.test(n)), JSON.stringify(lotNames))
  r = await T.issue(a, [
    { lineNo: T.mat(a, 'BTP-MAT01').lineNo, quantity: 100, lotId: null }, { lineNo: T.mat(a, 'BTP-CHAN01').lineNo, quantity: 400, lotId: null },
    { lineNo: T.mat(a, 'NVL-KEO01').lineNo, quantity: 10, lotId: null }, { lineNo: sonLine.lineNo, quantity: 52.5, lotId: null }], { 'Idempotency-Key': `p37-${S}-mi` })
  const mi = r.json()
  check('4. issue MAT01 100, CHAN 400, KEO01 10, SON 52.50: 201, document MI-yymm-nnnnn Posted', r.status === 201 && mi.status === 'Posted' && /^MI-\d{4}-\d{5}$/.test(mi.docNo), `${r.status} ${r.text.slice(0, 400)}`)
  numbers.mi = mi.docNo
  check('4. CHAN was taken 300 from the reservation + 100 from free stock (two lines)', mi.lines.filter((l) => l.itemCode === prefixA + 'BTP-CHAN01').map((l) => `${l.isFromReservation}:${l.qty}`).sort().join() === 'false:100,true:300', JSON.stringify(mi.lines.map((l) => [l.itemCode, l.isFromReservation, l.qty])))
  const sonLots = await T.lotsOf('NVL-SON01')
  const lot = (n) => sonLots.find((l) => l.lotNumber === n)
  const sonAfter = (await T.lotsOf('NVL-SON01'))
  check('4. paint came first-expiry-first: lot A 30 -> 0, lot B 50 -> 27.5, the expired lot X untouched at 20',
    near(T.lotQty(sonAfter.find((l) => String(l.lotNumber).endsWith('L-SON-A'))), 0) && near(T.lotQty(sonAfter.find((l) => String(l.lotNumber).endsWith('L-SON-B'))), 27.5) && near(T.lotQty(sonAfter.find((l) => String(l.lotNumber).endsWith('L-SON-X'))), 20),
    JSON.stringify(sonAfter.map((l) => [l.lotNumber, T.lotQty(l)])))
  a = await T.getOrder(a.id)
  check('4. order InProgress with ActualStart', a.status === 'InProgress' && !!a.actualStart)
  check('4. issued: MAT01 100, CHAN 400, KEO01 10, SON 52.5', near(T.mat(a, 'BTP-MAT01').issuedQty, 100) && near(T.mat(a, 'BTP-CHAN01').issuedQty, 400) && near(T.mat(a, 'NVL-KEO01').issuedQty, 10) && near(T.mat(a, 'NVL-SON01').issuedQty, 52.5))

  // 5 op10 Good 98 Scrap 2 (with a time log)
  let log
  for (const e of empIds) { r = await m('POST', `/orders/${a.id}/operations/10/start`, { employeeId: e, machineId: null }); if (r.status === 201) { log = r.json(); break } }
  check('5. an employee starts operation 10', !!log && log.state === 'Running', `${r.status} ${r.text.slice(0, 200)}`)
  if (log) {
    r = await m('POST', `/orders/${a.id}/operations/10/time-logs/${log.id}/pause`); check('5. pause', r.status === 200 && r.json().state === 'Paused')
    r = await m('POST', `/orders/${a.id}/operations/10/time-logs/${log.id}/resume`); check('5. resume', r.status === 200 && r.json().state === 'Running')
  }
  r = await T.report(a, 10, 98, 2, { 'Idempotency-Key': `p37-${S}-op10` })
  check('5. op10 Good 98 Scrap 2: 201, report OR-yymm-nnnnn Posted', r.status === 201 && r.json().report.status === 'Posted' && /^OR-\d{4}-\d{5}$/.test(r.json().report.docNo), `${r.status} ${r.text.slice(0, 300)}`)
  numbers.or10 = r.json()?.report?.docNo
  // completing the operation closed its open time log by itself
  const logsAfter = (await m('GET', `/orders/${a.id}/operations`)).json().find((x) => x.operation.operationSeq === 10).timeLogs
  check('5. completing the operation closed the open time log (Stopped, duration booked)', logsAfter.length >= 1 && logsAfter.every((x) => x.state === 'Stopped' && x.durationMinutes != null), JSON.stringify(logsAfter.map((x) => [x.state, x.durationMinutes])))
  a = await T.getOrder(a.id)
  check('5. screws consumed 1600 = (98 + 2) x 16, warehouse stock 2000 -> 400', near(T.mat(a, 'NVL-VIT01').consumedQty, 1600) && near(await T.total('NVL-VIT01'), stock0.vit - 1600), `${T.mat(a, 'NVL-VIT01').consumedQty} ${await T.total('NVL-VIT01')}`)
  check('5. op10 Completed (98 + 2 = 100), op20 Ready, scrapped 2', T.op(a, 10).status === 'Completed' && T.op(a, 20).status === 'Ready' && near(a.scrappedQty, 2), JSON.stringify([T.op(a, 10).status, T.op(a, 20).status, a.scrappedQty]))
  const bf = (await T.docs(a)).find((d) => d.type === 'Backflush')
  check('5. a Backflush document BF-yymm-nnnnn is Posted', !!bf && bf.status === 'Posted' && /^BF-\d{4}-\d{5}$/.test(bf.docNo), JSON.stringify(bf)?.slice(0, 200))
  numbers.bf = bf?.docNo

  // 6, 7
  r = await T.report(a, 20, 98); check('6. op20 Good 98', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  r = await T.report(a, 30, 98); check('7. op30 Good 98', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  a = await T.getOrder(a.id)
  check('7. cartons 98 and foam 196 backflushed, warehouse stock 500 -> 402 and 1000 -> 804',
    near(T.mat(a, 'NVL-THUNG01').consumedQty, 98) && near(T.mat(a, 'NVL-XOP01').consumedQty, 196) && near(await T.total('NVL-THUNG01'), stock0.thung - 98) && near(await T.total('NVL-XOP01'), stock0.xop - 196),
    JSON.stringify([T.mat(a, 'NVL-THUNG01').consumedQty, T.mat(a, 'NVL-XOP01').consumedQty]))
  check('7. all three operations Completed', a.operations.every((o) => o.status === 'Completed'), JSON.stringify(a.operations.map((o) => o.status)))

  // 8 receive 98 into quality
  r = await T.receive(a, [{ outputLineNo: null, quantity: 98, lotNumber: null }], { 'Idempotency-Key': `p37-${S}-fr` })
  const fr = r.json()
  const frLine = fr?.lines?.find((l) => l.kind === 'Receipt')
  check('8. receive 98: 201, FR-yymm-nnnnn Posted, lot <item>-yymmdd-01 on quality hold in the quarantine warehouse',
    r.status === 201 && fr.status === 'Posted' && /^FR-\d{4}-\d{5}$/.test(fr.docNo) && /-\d{6}-01$/.test(frLine?.lotNumber ?? '') && frLine.isQcHold === true && frLine.warehouseId === quarantine, `${r.status} ${r.text.slice(0, 500)}`)
  numbers.fr = fr?.docNo
  a = await T.getOrder(a.id)
  check('TC-PO-044 8. CompletedQty 98 (scrapped 2 since step 5), the order is still InProgress (98 < 100)', near(a.completedQty, 98) && a.status === 'InProgress', JSON.stringify([a.completedQty, a.status]))

  // 9 quality release
  r = await m('POST', `/stock-documents/${fr.id}/qc-release`)
  check('9. quality release (interim FQC pass): the receipt line shows the release', r.status === 200 && r.json().lines.some((l) => l.kind === 'Receipt' && !!l.qcReleasedAt), `${r.status} ${r.text.slice(0, 300)}`)
  const tpLots = await T.lotsOf('TP-BAN01')
  const newLot = tpLots.find((l) => l.lotNumber === frLine.lotNumber)
  check('9. the lot is Available in the finished-goods warehouse with 98 pieces', !!newLot && /available/i.test(String(newLot.status)) && (newLot.warehouseId ?? fgWarehouse) === fgWarehouse && near(T.lotQty(newLot), 98), JSON.stringify(newLot))
  check('9. finished goods stock +98 (20 old + 98)', near(await T.total('TP-BAN01'), stock0.tp + 98))

  // 10 complete
  r = await m('POST', `/orders/${a.id}/complete`, { reasonCodeId: null, note: null })
  check('10. complete at 98 of 100 (>= 98): Completed without a reason', r.status === 200 && r.json().order.status === 'Completed' && !!r.json().order.actualEnd, `${r.status} ${r.text.slice(0, 300)}`)
  const rsA = await T.reservation(a)
  check('10. nothing of the order is still reserved in the Warehouse', !rsA || rsA.status !== 'Reserved', JSON.stringify(rsA)?.slice(0, 200))

  // 11 return on the Completed order
  const lotB = sonLots.find((l) => String(l.lotNumber).endsWith('L-SON-B'))
  r = await T.ret(a, [{ lineNo: sonLine.lineNo, quantity: 1, lotId: lotB.lotId }])
  check('11. return of paint 1.00 on the COMPLETED order is accepted (owner decision): MR-yymm-nnnnn Posted', r.status === 201 && r.json().status === 'Posted' && /^MR-\d{4}-\d{5}$/.test(r.json().docNo), `${r.status} ${r.text.slice(0, 300)}`)
  numbers.mr = r.json()?.docNo
  check('11. lot B back to 28.5', near(T.lotQty((await T.lotsOf('NVL-SON01')).find((l) => String(l.lotNumber).endsWith('L-SON-B'))), 28.5))
  r = await T.issue(a, [{ lineNo: T.mat(a, 'NVL-KEO01').lineNo, quantity: 1, lotId: null }])
  check('11. an ISSUE on the Completed order is still refused: 400 MFG_MAT_001', r.status === 400 && code(r) === 'MFG_MAT_001', `${r.status} ${r.text.slice(0, 200)}`)

  // 12 close
  let cl = (await m('GET', `/orders/${a.id}/close-checklist`)).json()
  check('12. checklist: unsettled issued material is unmet', cl.canClose === false && cl.conditions.some((c) => !c.met), JSON.stringify(cl).slice(0, 400))
  r = await m('POST', `/orders/${a.id}/close`, { confirmConsumeRemaining: false })
  check('12. close without confirmation: 400 MFG_PO_013', r.status === 400 && code(r) === 'MFG_PO_013', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/close`, { confirmConsumeRemaining: true })
  check('12. close with confirmation: Closed', r.status === 200 && r.json().status === 'Closed', `${r.status} ${r.text.slice(0, 300)}`)
  a = await T.getOrder(a.id)
  check('12. consumed: screws 1600, cartons 98, foam 196, paint 51.50, glue 10, tops 100, legs 400',
    near(T.mat(a, 'NVL-VIT01').consumedQty, 1600) && near(T.mat(a, 'NVL-THUNG01').consumedQty, 98) && near(T.mat(a, 'NVL-XOP01').consumedQty, 196) && near(T.mat(a, 'NVL-SON01').consumedQty, 51.5)
      && near(T.mat(a, 'NVL-KEO01').consumedQty, 10) && near(T.mat(a, 'BTP-MAT01').consumedQty, 100) && near(T.mat(a, 'BTP-CHAN01').consumedQty, 400),
    JSON.stringify(a.materials.map((x) => [x.componentCode.replace(prefixA, ''), x.consumedQty])))
  r = await m('POST', `/orders/${a.id}/reopen`); check('12. an action on the Closed order: 400 MFG_PO_014', r.status === 400 && code(r) === 'MFG_PO_014')
  r = await T.ret(a, [{ lineNo: sonLine.lineNo, quantity: 1, lotId: lotB.lotId }]); check('12. a return on the Closed order: 400 MFG_PO_014', r.status === 400 && code(r) === 'MFG_PO_014', `${r.status} ${r.text.slice(0, 200)}`)

  // final state
  const hist = (await m('GET', `/orders/${a.id}/history`)).json()
  check('history has Released, InProgress, Completed and Closed (>= 4 rows)', hist.length >= 4 && ['Released', 'InProgress', 'Completed', 'Closed'].every((s) => hist.some((h) => h.toStatus === s)), JSON.stringify(hist.map((h) => h.toStatus)))
  const all = await T.docs(a)
  check('every document has a number, all numbers differ, none is Pending', all.length >= 5 && new Set(all.map((d) => d.docNo)).size === all.length && all.every((d) => /^(MI|MR|BF|FR)-\d{4}-\d{5}$/.test(d.docNo) && d.status === 'Posted'), JSON.stringify(all.map((d) => [d.docNo, d.status])))
  check('warehouse: screws 400, cartons 402, foam 804, glue 40, tops 20', near(await T.total('NVL-VIT01'), 400) && near(await T.total('NVL-THUNG01'), 402) && near(await T.total('NVL-XOP01'), 804) && near(await T.total('NVL-KEO01'), 40) && near(await T.total('BTP-MAT01'), 20))
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  const mine = drift.filter((d) => Object.values(setA.ids).includes(d.productId) && d.kind !== 'Reserved')
  check('Warehouse reconciliation is clean for the items of this scenario', mine.length === 0, JSON.stringify(mine).slice(0, 300))
  // close the child order too (settle its wood)
  r = await m('POST', `/orders/${child.id}/close`, { confirmConsumeRemaining: true })
  check('the child order closes too', r.status === 200 && r.json().status === 'Closed', `${r.status} ${r.text.slice(0, 200)}`)

  // general and security rules that no scenario step shows
  console.log('\n── TC-GEN-002 / 005, TC-ITM-001, TC-SEC-002 / 003 ──')
  r = await m('DELETE', `/stock-documents/${mi.id}`)
  check('TC-GEN-002 a posted document cannot be deleted: 404 / 405, and it is still there', [404, 405].includes(r.status) && (await m('GET', `/stock-documents/${mi.id}`)).status === 200, `${r.status}`)
  r = await m('DELETE', `/orders/${a.id}`)
  check('TC-GEN-002 an order cannot be deleted: 404 / 405', [404, 405].includes(r.status), `${r.status}`)
  let conflictDraft = await T.create('TP-BAN01', 3)
  const updates = await Promise.all(Array.from({ length: 6 }, (_, i) => m('PUT', `/orders/${conflictDraft.id}`, { quantity: 3, dueDate: iso(due), priority: (i % 5) + 1, bomId: null, routingId: null, outputWarehouseId: null, notes: 'p37 race ' + i })))
  const statuses = updates.map((x) => x.status)
  check('TC-GEN-005 six parallel changes of one order: every answer is 200 or 409 (never a 500), at least one succeeds', statuses.every((x) => x === 200 || x === 409) && statuses.includes(200), statuses.join(','))
  await m('POST', `/orders/${conflictDraft.id}/cancel`, { reasonCodeId: cancelReason, note: 'p37' })
  r = await m('POST', '/orders', { plantId: T.plant, itemId: T.ids['NVL-SON01'], quantity: 1, dueDate: iso(due) })
  check('TC-ITM-001 an order for a Buy item (paint) is refused: 400 MFG_ITM_001', r.status === 400 && code(r) === 'MFG_ITM_001', `${r.status} ${r.text.slice(0, 200)}`)
  const mail = `zz37${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P37' })
  const plain = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (plain.status === 200 && plain.json().accessToken) {
    const pa = { Authorization: `Bearer ${plain.json().accessToken}`, ...J }
    const boms = (await m('GET', `/boms?plantId=${T.plant}`)).json() ?? []
    const bomId = boms[0]?.id
    let x = await call('POST', `/mfg/api/v1/manufacturing/boms/${bomId}/approve`, pa)
    check('TC-SEC-002 a user without the approve permission cannot approve a BOM: 403', x.status === 403, `${x.status}`)
    x = await call('POST', `/mfg/api/v1/manufacturing/orders/${a.id}/material-issues`, pa, { lines: [] })
    check('TC-SEC-003 a user without the issue permission cannot issue material: 403', x.status === 403, `${x.status}`)
  } else console.log('SKIP no-permission user:', plain.status, plain.text.slice(0, 100))

  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  // TC-GEN-006: 50 parallel creates on the plant of set A
  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  console.log('\n── TC-GEN-006 (50 parallel order numbers) ──')
  const results = await Promise.all(Array.from({ length: 50 }, () => m('POST', '/orders', { plantId: T.plant, itemId: T.ids['TP-BAN01'], quantity: 1, dueDate: iso(due) })))
  const created = results.filter((x) => x.status === 201).map((x) => x.json())
  check('50 parallel creates all answer 201', created.length === 50, results.map((x) => x.status).filter((s) => s !== 201).join(',') + ' ' + results.find((x) => x.status !== 201)?.text.slice(0, 200))
  check('50 different numbers MO-yymm-nnnnn', new Set(created.map((o) => o.orderNo)).size === 50 && created.every((o) => /^MO-\d{4}-\d{5}$/.test(o.orderNo)), created.map((o) => o.orderNo).slice(0, 5).join(','))
  let cancelled = 0
  for (const o of created) { const x = await m('POST', `/orders/${o.id}/cancel`, { reasonCodeId: cancelReason, note: 'p37 parallel test' }); if (x.status === 200) cancelled++ }
  check('the 50 test orders are cancelled again', cancelled === 50, `${cancelled}`)

  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  // TC-E2E-005
  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  console.log('\n── TC-E2E-005 ──')
  const prefixB = `ZZP37B${S}-`
  const plantCodeB = `ZZ-P37B-${S}`
  const setB = seed(prefixB, plantCodeB)
  check('DS-01 set B seeded', !!setB.plant, setB.out.slice(-300))
  const U = tools(setB, prefixB)
  let e = await U.create('TP-BAN01', 100)
  numbers.moE = e.orderNo
  await U.release(e)
  e = await U.getOrder(e.id)
  r = await U.issue(e, [{ lineNo: U.mat(e, 'NVL-KEO01').lineNo, quantity: 5, lotId: null }])
  check('MO-E (100) released, 5 of glue issued: InProgress', r.status === 201 && (await U.getOrder(e.id)).status === 'InProgress', `${r.status} ${r.text.slice(0, 200)}`)
  r = await U.report(e, 10, 40)
  check('op10 Good 40: screws consumed 640', r.status === 201 && near(U.mat(await U.getOrder(e.id), 'NVL-VIT01').consumedQty, 640), `${r.status} ${r.text.slice(0, 200)}`)
  const before = await U.getOrder(e.id)

  r = await m('POST', `/orders/${e.id}/hold`, { reasonCodeId: null, note: null })
  check('hold without a reason: 400 MFG_PO_009', r.status === 400 && code(r) === 'MFG_PO_009', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${e.id}/hold`, { reasonCodeId: holdReason, note: 'p37' })
  check('hold with a reason: OnHold, remembers InProgress', r.status === 200 && r.json().order.status === 'OnHold' && r.json().order.statusBeforeHold === 'InProgress', `${r.status} ${r.text.slice(0, 200)}`)
  r = await U.report(e, 10, 10); check('TC-OP-018 a report while OnHold: 400 MFG_PO_011', r.status === 400 && code(r) === 'MFG_PO_011', `${r.status} ${r.text.slice(0, 200)}`)
  r = await U.issue(e, [{ lineNo: U.mat(e, 'NVL-KEO01').lineNo, quantity: 1, lotId: null }]); check('an issue while OnHold: 400 MFG_PO_011', r.status === 400 && code(r) === 'MFG_PO_011', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${e.id}/resume`); check('resume: back to InProgress', r.status === 200 && r.json().order.status === 'InProgress', `${r.status} ${r.text.slice(0, 200)}`)

  r = await m('POST', `/orders/${e.id}/change-quantity`, { quantity: 120, note: 'customer asked for more' })
  check('change quantity to 120: 200', r.status === 200 && near(r.json().order.quantity, 120), `${r.status} ${r.text.slice(0, 300)}`)
  e = await U.settled(e)
  check('120: required SON 63, glue 12, screws 1920, tops 120; issued glue stays 5',
    near(U.mat(e, 'NVL-SON01').requiredQty, 63) && near(U.mat(e, 'NVL-KEO01').requiredQty, 12) && near(U.mat(e, 'NVL-VIT01').requiredQty, 1920) && near(U.mat(e, 'BTP-MAT01').requiredQty, 120) && near(U.mat(e, 'NVL-KEO01').issuedQty, 5),
    JSON.stringify(e.materials.map((x) => [x.componentCode.replace(prefixB, ''), x.requiredQty, x.issuedQty])))
  check('120: the reservation follows - paint reserved 63, glue reserved 12 - 5 = 7, reservation in line', near(U.mat(e, 'NVL-SON01').reservedQty, 63) && near(U.mat(e, 'NVL-KEO01').reservedQty, 7) && e.reservationSyncPending === false, JSON.stringify(e.materials.map((x) => [x.componentCode.replace(prefixB, ''), x.reservedQty])))
  check('120: the schedule is recomputed', e.plannedStart !== before.plannedStart || e.plannedEnd !== before.plannedEnd, `${before.plannedStart} ${before.plannedEnd} -> ${e.plannedStart} ${e.plannedEnd}`)
  const hist2 = (await m('GET', `/orders/${e.id}/history`)).json()
  check('history shows the hold, the resume and the quantity change', hist2.some((h) => h.toStatus === 'OnHold') && hist2.filter((h) => h.fromStatus === 'OnHold' && h.toStatus === 'InProgress').length >= 1 && hist2.some((h) => /120/.test(h.note ?? '')), JSON.stringify(hist2.map((h) => [h.fromStatus, h.toStatus, h.note])))

  // finish at 120
  r = await U.report(e, 10, 80); check('op10 Good 80 more (120 in total, within 126)', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  r = await U.report(e, 20, 120); check('op20 Good 120', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  r = await U.report(e, 30, 120); check('op30 Good 120', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  e = await U.getOrder(e.id)
  check('screws 1920, cartons 120, foam 240 in total', near(U.mat(e, 'NVL-VIT01').consumedQty, 1920) && near(U.mat(e, 'NVL-THUNG01').consumedQty, 120) && near(U.mat(e, 'NVL-XOP01').consumedQty, 240), JSON.stringify(e.materials.map((x) => [x.componentCode.replace(prefixB, ''), x.consumedQty])))
  r = await U.issue(e, [
    { lineNo: U.mat(e, 'NVL-KEO01').lineNo, quantity: 7, lotId: null }, { lineNo: U.mat(e, 'NVL-SON01').lineNo, quantity: 63, lotId: null }, { lineNo: U.mat(e, 'BTP-MAT01').lineNo, quantity: 120, lotId: null }, { lineNo: U.mat(e, 'BTP-CHAN01').lineNo, quantity: 300, lotId: null }])
  check('the rest of the material the stock allows is issued (legs: the 300 in stock)', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  r = await U.receive(e, [{ outputLineNo: null, quantity: 20, lotNumber: null }])
  check('receive 20', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  const fr1 = r.json()
  r = await m('POST', `/orders/${e.id}/change-quantity`, { quantity: 10, note: 'too low' })
  check('change quantity below what is completed (20): 400 MFG_PO_010', r.status === 400 && code(r) === 'MFG_PO_010', `${r.status} ${r.text.slice(0, 200)}`)
  r = await U.receive(e, [{ outputLineNo: null, quantity: 100, lotNumber: null }])
  check('receive the other 100', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  const fr2 = r.json()
  e = await U.getOrder(e.id)
  check('Completed at 120 by itself (auto-complete), CompletedQty 120', e.status === 'Completed' && near(e.completedQty, 120), JSON.stringify([e.status, e.completedQty]))
  const rsE = await U.reservation(e)
  check('the reservation is released', !rsE || rsE.status !== 'Reserved', JSON.stringify(rsE)?.slice(0, 200))
  for (const f of [fr1, fr2]) await m('POST', `/stock-documents/${f.id}/qc-release`)
  r = await m('POST', `/orders/${e.id}/close`, { confirmConsumeRemaining: true })
  check('MO-E closes after the quality release', r.status === 200 && r.json().status === 'Closed', `${r.status} ${r.text.slice(0, 300)}`)
  numbers.frE = [fr1?.docNo, fr2?.docNo].join(' ')

  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  // R219: a plant with unfinished orders cannot be deactivated (plant B has only Closed orders so far)
  // ═════════════════════════════════════════════════════════════════════════════════════════════════════
  console.log('\n── R219 ──')
  const draft = await U.create('TP-BAN01', 5)
  r = await m('POST', `/plants/${U.plant}/deactivate`)
  check('R219: deactivate a plant with a Draft order: 400 MFG_PLT_001 naming the order', r.status === 400 && code(r) === 'MFG_PLT_001' && (r.json().detail ?? '').includes(draft.orderNo), `${r.status} ${r.text.slice(0, 300)}`)
  check('R219: the plant is still active', (await m('GET', `/plants/${U.plant}`)).json().isActive === true)
  await m('POST', `/orders/${draft.id}/cancel`, { reasonCodeId: cancelReason, note: 'p37' })
  r = await m('POST', `/plants/${U.plant}/deactivate`)
  check('R219: with only Closed / Cancelled orders it can be deactivated (a throw-away plant is left inactive)', r.status === 200 && r.json().isActive === false, `${r.status} ${r.text.slice(0, 200)}`)

  console.log('\nDocuments produced:', JSON.stringify(numbers))
  console.log(`\nprefixes ${prefixA} ${prefixB} plants ${plantCodeA} ${plantCodeB}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); process.exit(1) })
