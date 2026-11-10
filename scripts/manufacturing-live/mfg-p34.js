// Manufacturing P3-4 live check (operations: time logs and reports; backflush) on a fresh DS-01 set with real stock.
// Default host = glcheck; MFG_HOST / MFG_EMAIL / MFG_PASSWORD switch tenant (SYSTEM: admin.erp.local / superadmin@system.local / SuperAdmin@123).
const http = require('http')
const repoRoot = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
const { execFileSync } = require('child_process')
const host = process.env.MFG_HOST || 'glcheck.erp.local'
const email = process.env.MFG_EMAIL || 'admin@glcheck.test'
const password = process.env.MFG_PASSWORD || 'Gl-Check-Passw0rd!'
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
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const docker = (...args) => execFileSync('docker', args, { encoding: 'utf8' }).trim()
const plantNow = () => new Date(Date.now() + 7 * 3600e3)
const iso = (d) => d.toISOString().slice(0, 10)
const addDays = (d, n) => new Date(d.getTime() + n * 86400e3)

;(async () => {
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const prefix = `ZZP34${S}-`
  const plantCode = `ZZ-P34-${S}`
  const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
  const seedAnswer = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, ''))
  const ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
  const plant = seedAnswer.plantId
  check('DS-01 set seeded (plant, products, lots, stock)', !!plant && !!ids['TP-BAN01'], out.slice(-400))

  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body, extra = {}) => call(method, `/mfg/api/v1/manufacturing${path}`, { ...auth, ...extra }, body)
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const code = (r) => r.json()?.errorCode
  const inv = async (key) => (await wh('GET', `/inventory/${ids[key]}`)).json()
  const refs = async (key) => ((await wh('GET', `/movements/${ids[key]}?take=300`)).json()?.items ?? []).map((x) => x.reference)

  await m('POST', '/reason-codes/seed-defaults')
  const reasons = (await m('GET', '/reason-codes')).json()
  const reasonOf = (type) => reasons.find((x) => x.type === type && x.isActive)?.id
  const holdReason = reasonOf('Hold')
  const scrapReason = reasonOf('Scrap')
  const reworkReason = reasonOf('Rework')
  check('Hold, Scrap and Rework reasons exist', !!holdReason && !!scrapReason && !!reworkReason)

  const emps = (await call('GET', '/mst/api/v1/masterdata/employees?page=1&pageSize=60', auth)).json()
  const empList = (emps.items ?? emps).filter((x) => x.isActive !== false)
  const empIds = empList.map((x) => x.id)
  let e1, e2
  check('employees from MasterData', empIds.length >= 2)

  let due = addDays(plantNow(), 7)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const create = async (qty = 100) => (await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: qty, dueDate: iso(due) })).json()
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const mat = (o, key) => o.materials.find((x) => x.componentCode === prefix + key)
  const op = (o, seq) => o.operations.find((x) => x.operationSeq === seq)
  const start = (o, seq, employeeId) => m('POST', `/orders/${o.id}/operations/${seq}/start`, { employeeId })
  const report = (o, seq, body, extra = {}) => m('POST', `/orders/${o.id}/operations/${seq}/report`, body, extra)
  const logAction = (o, seq, logId, what) => m('POST', `/orders/${o.id}/operations/${seq}/time-logs/${logId}/${what}`)
  const opsOf = async (o) => (await m('GET', `/orders/${o.id}/operations`)).json()
  const reportsOf = async (o) => (await m('GET', `/orders/${o.id}/operation-reports`)).json()
  const vit = async () => Number((await inv('NVL-VIT01')).totalQuantity)

  // ── MO-A released
  const draft = await create(100)
  let r = await m('POST', `/orders/${draft.id}/release`)
  check('MO-A released with the reservation in line', r.status === 200 && r.json().reservationPending === false, `${r.status} ${r.text.slice(0, 200)}`)
  let a = await getOrder(draft.id)
  check('op10 Ready, op20 / op30 Pending', op(a, 10).status === 'Ready' && op(a, 20).status === 'Pending' && op(a, 30).status === 'Pending')
  const vit0 = await vit()

  // ── time logs (TC-OP-001..006)
  r = await start(a, 20, e1)
  check('start on a Pending operation: 400 MFG_OP_001 (TC-OP-002)', r.status === 400 && code(r) === 'MFG_OP_001', `${r.status} ${r.text.slice(0, 200)}`)
  // employees left with an open session by an earlier aborted run are skipped (BR-OP-05)
  for (const cand of empIds) { r = await start(a, 10, cand); if (r.status === 201) { e1 = cand; break } }
  const log1 = r.json()
  check('start op10 by NV1: 201, Running (TC-OP-001)', r.status === 201 && log1.state === 'Running', `${r.status} ${r.text.slice(0, 300)}`)
  a = await getOrder(a.id)
  check('order InProgress with ActualStart, op10 Running', a.status === 'InProgress' && !!a.actualStart && op(a, 10).status === 'Running', JSON.stringify([a.status, op(a, 10).status]))
  for (const cand of empIds.filter((x) => x !== e1)) { r = await start(a, 10, cand); if (r.status === 201) { e2 = cand; break } }
  const log2 = r.json()
  check('a second employee shares the operation (TC-OP-003)', r.status === 201 && log2.state === 'Running', `${r.status} ${r.text.slice(0, 200)}`)
  r = await start(a, 10, e1)
  check('NV1 again: 400 MFG_OP_006 (TC-OP-004)', r.status === 400 && code(r) === 'MFG_OP_006', `${r.status} ${r.text.slice(0, 200)}`)
  r = await logAction(a, 10, log2.id, 'pause')
  check('NV2 pauses: Paused, op10 still Running', r.status === 200 && r.json().state === 'Paused' && op(await getOrder(a.id), 10).status === 'Running', `${r.status} ${r.text.slice(0, 200)}`)
  r = await logAction(a, 10, log2.id, 'resume')
  check('NV2 resumes', r.status === 200 && r.json().state === 'Running')
  r = await logAction(a, 10, log2.id, 'stop')
  check('NV2 stops: duration set, op10 still Running (NV1 works)', r.status === 200 && r.json().state === 'Stopped' && r.json().durationMinutes !== null && op(await getOrder(a.id), 10).status === 'Running', `${r.status} ${r.text.slice(0, 200)}`)
  const spread = Math.floor(Math.random() * 20000)
  const t = (offsetMin) => new Date(Date.now() + (offsetMin < -200 ? offsetMin - 1000 - spread : offsetMin) * 60e3).toISOString()
  r = await m('POST', `/orders/${a.id}/operations/10/time-logs`, { employeeId: e2, startAt: t(-120), endAt: t(-180) })
  check('manual time with End before Start: 400 MFG_OP_005 (TC-OP-006)', r.status === 400 && code(r) === 'MFG_OP_005', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/operations/10/time-logs`, { employeeId: e2, startAt: t(60), endAt: t(120) })
  check('manual time in the future: 400 MFG_OP_005', r.status === 400 && code(r) === 'MFG_OP_005', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/operations/10/time-logs`, { employeeId: e2, startAt: t(-300), endAt: t(-240) })
  check('manual time of 60 minutes accepted', r.status === 201 && near(r.json().durationMinutes, 60) && r.json().isManual === true, `${r.status} ${r.text.slice(0, 200)}`)

  // ── report op10: Good 60, Scrap 2 -> VIT 992 (TC-OP-007, TC-BF-001)
  r = await report(a, 10, { goodQty: 0, scrapQty: 0, reworkQty: 0 })
  check('report of zeros: 400 MFG_OP_002 (TC-OP-008)', r.status === 400 && code(r) === 'MFG_OP_002', `${r.status} ${r.text.slice(0, 200)}`)
  r = await report(a, 10, { goodQty: 10, scrapQty: 2, reworkQty: 0 })
  check('scrap without a reason: 400 MFG_OP_003 (TC-OP-010)', r.status === 400 && code(r) === 'MFG_OP_003', `${r.status} ${r.text.slice(0, 200)}`)
  const reportsBefore = (await reportsOf(a)).length
  r = await report(a, 10, { goodQty: 60, scrapQty: 2, reworkQty: 0, scrapReasonId: scrapReason })
  const rep1 = r.json()
  check('report op10 Good 60 Scrap 2: 201, Posted, backflush document BF Posted', r.status === 201 && rep1.report.status === 'Posted' && /^OR-\d{4}-\d{5}$/.test(rep1.report.docNo) && /^BF-\d{4}-\d{5}$/.test(rep1.backflush?.docNo) && rep1.backflush.status === 'Posted', `${r.status} ${r.text.slice(0, 500)}`)
  a = await getOrder(a.id)
  check('op10 Good 60 / Scrap 2, op20 Ready, order scrapped 2', near(op(a, 10).goodQty, 60) && near(op(a, 10).scrapQty, 2) && op(a, 20).status === 'Ready' && near(a.scrappedQty, 2), JSON.stringify([op(a, 10), op(a, 20).status, a.scrappedQty]))
  check('VIT consumed 992, issued 0 (TC-BF-001)', near(mat(a, 'NVL-VIT01').consumedQty, 992) && near(mat(a, 'NVL-VIT01').issuedQty, 0), JSON.stringify(mat(a, 'NVL-VIT01')))
  check('VIT stock down by 992 and the Warehouse holds the BF movement', near(await vit(), vit0 - 992) && (await refs('NVL-VIT01')).includes(rep1.backflush.lines[0].movementKey), rep1.backflush.lines[0].movementKey)
  check('one more report row', (await reportsOf(a)).length === reportsBefore + 1)

  // ── limits (TC-OP-012)
  r = await report(a, 20, { goodQty: 61, scrapQty: 0, reworkQty: 0 })
  check('op20 cannot take 61 when 60 are good (TC-OP-012)', r.status === 400 && code(r) === 'MFG_OP_004', `${r.status} ${r.text.slice(0, 200)}`)

  // ── second report completes op10: 608, total 1600 (TC-BF-002, TC-OP-014), idempotent
  const key = `p34-${S}-1`
  r = await report(a, 10, { goodQty: 38, scrapQty: 0, reworkQty: 0 }, { 'Idempotency-Key': key })
  const rep2 = r.json()
  check('report op10 Good 38: 201, backflush takes the remaining 608 (TC-BF-002)', r.status === 201 && near(rep2.backflush?.lines?.reduce((s, l) => s + Number(l.qty), 0), 608), `${r.status} ${r.text.slice(0, 400)}`)
  r = await report(a, 10, { goodQty: 38, scrapQty: 0, reworkQty: 0 }, { 'Idempotency-Key': key })
  check('the same key answers the same report, nothing is booked twice', [200, 201].includes(r.status) && r.json().report.id === rep2.report.id && near(await vit(), vit0 - 1600), `${r.status} ${r.text.slice(0, 200)} vit=${await vit()}`)
  r = await report(a, 10, { goodQty: 39, scrapQty: 0, reworkQty: 0 }, { 'Idempotency-Key': key })
  check('the same key with another body: MFG_GEN_007', [400, 409].includes(r.status) && code(r) === 'MFG_GEN_007', `${r.status} ${r.text.slice(0, 200)}`)
  a = await getOrder(a.id)
  check('VIT consumed 1 600 and op10 Completed (98 + 2 = 100)', near(mat(a, 'NVL-VIT01').consumedQty, 1600) && op(a, 10).status === 'Completed' && !!op(a, 10).actualEnd, JSON.stringify([mat(a, 'NVL-VIT01').consumedQty, op(a, 10).status]))
  const ops = await opsOf(a)
  check('completing the operation closed NV1\'s open time log', ops.find((o) => o.operation.operationSeq === 10).timeLogs.every((l) => l.state === 'Stopped'), JSON.stringify(ops[0].timeLogs.map((l) => l.state)))
  r = await report(a, 10, { goodQty: 1, scrapQty: 0, reworkQty: 0 })
  check('a completed operation takes no more: 400 MFG_OP_009 (TC-OP-016)', r.status === 400 && code(r) === 'MFG_OP_009', `${r.status} ${r.text.slice(0, 200)}`)

  // ── op20 and op30 (TC-BF-003 phantom)
  r = await report(a, 20, { goodQty: 98, scrapQty: 0, reworkQty: 0 })
  check('op20 takes the 98 good pieces and no backflush document is needed', r.status === 201 && r.json().backflush === null, `${r.status} ${r.text.slice(0, 300)}`)
  a = await getOrder(a.id)
  check('op20 Completed, op30 Ready', op(a, 20).status === 'Completed' && op(a, 30).status === 'Ready', JSON.stringify([op(a, 20).status, op(a, 30).status]))
  const thung0 = Number((await inv('NVL-THUNG01')).totalQuantity)
  const xop0 = Number((await inv('NVL-XOP01')).totalQuantity)
  r = await report(a, 30, { goodQty: 50, scrapQty: 0, reworkQty: 0 })
  check('op30 Good 50: backflush THUNG 50 and XOP 100 (TC-BF-003)', r.status === 201 && r.json().backflush?.lines?.length === 2, `${r.status} ${r.text.slice(0, 400)}`)
  a = await getOrder(a.id)
  check('consumed THUNG 50 / XOP 100, stock down accordingly', near(mat(a, 'NVL-THUNG01').consumedQty, 50) && near(mat(a, 'NVL-XOP01').consumedQty, 100) && near(Number((await inv('NVL-THUNG01')).totalQuantity), thung0 - 50) && near(Number((await inv('NVL-XOP01')).totalQuantity), xop0 - 100))

  // ── hold pauses running work (BR-PO-11)
  r = await start(a, 30, e2)
  const log3 = r.json()
  check('start op30 by NV2', r.status === 201 && log3.state === 'Running', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/hold`, { reasonCodeId: holdReason })
  check('hold the order', r.status === 200)
  const heldOps = await opsOf(a)
  check('the running log and the operation are Paused', heldOps.find((o) => o.operation.operationSeq === 30).timeLogs.find((l) => l.id === log3.id).state === 'Paused' && heldOps.find((o) => o.operation.operationSeq === 30).operation.status === 'Paused')
  r = await report(a, 30, { goodQty: 1, scrapQty: 0, reworkQty: 0 })
  check('report on a held order: 400 MFG_PO_011', r.status === 400 && code(r) === 'MFG_PO_011', `${r.status} ${r.text.slice(0, 200)}`)
  r = await logAction(a, 30, log3.id, 'resume')
  check('resume of the log on a held order: 400 MFG_PO_011', r.status === 400 && code(r) === 'MFG_PO_011', `${r.status} ${r.text.slice(0, 200)}`)
  await m('POST', `/orders/${a.id}/resume`)
  check('resuming the order leaves the log Paused', (await opsOf(a)).find((o) => o.operation.operationSeq === 30).timeLogs.find((l) => l.id === log3.id).state === 'Paused')
  r = await logAction(a, 30, log3.id, 'resume')
  check('the log resumes, op30 Running', r.status === 200 && r.json().state === 'Running')
  await logAction(a, 30, log3.id, 'stop')

  // ── complete by hand and reopen (TC-OP-017), cancel refused after work
  r = await m('POST', `/orders/${a.id}/operations/30/complete`)
  check('supervisor completes op30 by hand', r.status === 200 && r.json().status === 'Completed', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/operations/30/reopen`)
  check('and reopens it: Ready', r.status === 200 && r.json().status === 'Ready', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/unrelease`)
  check('unrelease after work: refused', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)

  // ── MO-B: shortage (TC-BF-005), negative stock (TC-BF-006)
  const bDraft = await create(100)
  r = await m('POST', `/orders/${bDraft.id}/release`)
  let b = await getOrder(bDraft.id)
  const vitB = await vit()
  check('MO-B released; only what is left of VIT (400) can be reserved', r.status === 200 && near(vitB, vit0 - 1600) && near(mat(b, 'NVL-VIT01').reservedQty, vitB), `${r.status} vit=${vitB} ${JSON.stringify(mat(b, 'NVL-VIT01'))}`)
  const reportsB0 = (await reportsOf(b)).length
  const docsB0 = (await m('GET', `/stock-documents?plantId=${plant}&orderId=${b.id}`)).json().total
  r = await report(b, 10, { goodQty: 100, scrapQty: 0, reworkQty: 0 })
  check('Good 100 needs 1 600 VIT, 400 in stock: 400 MFG_MAT_008 (TC-BF-005)', r.status === 400 && code(r) === 'MFG_MAT_008', `${r.status} ${r.text.slice(0, 300)}`)
  b = await getOrder(b.id)
  check('nothing stored: op10 Good 0, no report, no document, order still Released', near(op(b, 10).goodQty, 0) && (await reportsOf(b)).length === reportsB0 && (await m('GET', `/stock-documents?plantId=${plant}&orderId=${b.id}`)).json().total === docsB0 && b.status === 'Released')

  const settings = (await m('GET', `/plants/${plant}/settings`)).json()
  r = await m('PUT', `/plants/${plant}/settings`, { ...settings.values, allowNegativeStockOnBackflush: true })
  check('AllowNegativeStockOnBackflush switched on', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  r = await report(b, 10, { goodQty: 100, scrapQty: 0, reworkQty: 0 })
  check('the report now posts and VIT goes below zero (TC-BF-006)', r.status === 201 && r.json().backflush?.lines?.some((l) => l.isFromReservation === false), `${r.status} ${r.text.slice(0, 400)}`)
  check('VIT stock is 400 - 1600 = -1200', near(await vit(), vitB - 1600), `${await vit()}`)
  check('the negative line is flagged on the document', (await m('GET', `/orders/${b.id}/stock-documents`)).json().some((d) => d.type === 'Backflush' && d.lines.some((l) => l.isFromReservation === false)))
  r = await m('PUT', `/plants/${plant}/settings`, { ...settings.values, allowNegativeStockOnBackflush: false })
  check('AllowNegativeStockOnBackflush switched off again', r.status === 200)

  // ── failure injection: the Warehouse is down
  b = await getOrder(b.id)
  docker('stop', 'erp-warehouse-service')
  try {
    await sleep(2000)
    r = await report(b, 20, { goodQty: 100, scrapQty: 0, reworkQty: 0 })
    check('report on an operation without backflush needs no Warehouse: 201', r.status === 201 && r.json().backflush === null, `${r.status} ${r.text.slice(0, 300)}`)
    const keep = (await reportsOf(b)).length
    r = await report(b, 30, { goodQty: 5, scrapQty: 0, reworkQty: 0 })
    check('report with a backflush while the Warehouse is down: 400 MFG_INT_001, nothing stored', r.status === 400 && code(r) === 'MFG_INT_001' && (await reportsOf(b)).length === keep, `${r.status} ${r.text.slice(0, 300)}`)
    r = await start(b, 30, e1)
    check('time logs work too: start op30', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
    if (r.status === 201) await logAction(b, 30, r.json().id, 'stop')
  } finally {
    docker('start', 'erp-warehouse-service')
  }

  // ── permissions
  const mail = `zz34${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P34' })
  const cl = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (cl.status === 200 && cl.json().accessToken) {
    const plain = { Authorization: `Bearer ${cl.json().accessToken}`, ...J }
    for (const [what, method, path, body] of [['operations list', 'GET', `/orders/${a.id}/operations`], ['start', 'POST', `/orders/${a.id}/operations/10/start`, { employeeId: e1 }],
      ['report', 'POST', `/orders/${a.id}/operations/10/report`, { goodQty: 1 }], ['complete', 'POST', `/orders/${a.id}/operations/10/complete`], ['reopen', 'POST', `/orders/${a.id}/operations/10/reopen`],
      ['reports list', 'GET', `/orders/${a.id}/operation-reports`], ['pause', 'POST', `/orders/${a.id}/operations/10/time-logs/${log1.id}/pause`]]) {
      const x = await call(method, `/mfg/api/v1/manufacturing${path}`, plain, body)
      check(`a user without the permission gets 403 on ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', cl.status, cl.text.slice(0, 100))

  // ── clean up what the orders still hold in the Warehouse
  for (const o of [a, b]) {
    const rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${o.id}`)).json()
    if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p34 cleanup' })
  }

  console.log(`\nprefix ${prefix} plant ${plantCode} orders ${a.orderNo} ${b.orderNo}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
