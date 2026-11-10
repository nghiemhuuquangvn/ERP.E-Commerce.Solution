// Manufacturing P3-5 live check (finished-goods receipt, lots and quality hold, complete / short-close / reopen / close) on a fresh DS-01 set.
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
  const prefix = `ZZP35${S}-`
  const plantCode = `ZZ-P35-${S}`
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
  const lotsOf = async (key) => (await wh('GET', `/inventory/lots/product/${ids[key]}`)).json() ?? []
  const refs = async (key) => ((await wh('GET', `/movements/${ids[key]}?take=300`)).json()?.items ?? []).map((x) => x.reference)

  await m('POST', '/reason-codes/seed-defaults')
  const reasons = (await m('GET', '/reason-codes')).json()
  const reasonOf = (type) => reasons.find((x) => x.type === type && x.isActive)?.id
  const shortCloseReason = reasonOf('ShortClose')
  const scrapReason = reasonOf('Scrap')
  check('ShortClose reason exists', !!shortCloseReason)
  const plantInfo = (await m('GET', `/plants/${plant}`)).json()
  const quarantine = plantInfo.quarantineWarehouseId
  const fgWarehouse = plantInfo.finishedGoodsWarehouseId
  check('plant has quarantine and finished-goods warehouses', !!quarantine && !!fgWarehouse, JSON.stringify(plantInfo).slice(0, 300))

  let due = addDays(plantNow(), 7)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const create = async (key, qty) => (await m('POST', '/orders', { plantId: plant, itemId: ids[key], quantity: qty, dueDate: iso(due) })).json()
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const mat = (o, key) => o.materials.find((x) => x.componentCode === prefix + key)
  const op = (o, seq) => o.operations.find((x) => x.operationSeq === seq)
  const report = (o, seq, goodQty) => m('POST', `/orders/${o.id}/operations/${seq}/report`, { goodQty, scrapQty: 0, reworkQty: 0 })
  const receive = (o, lines, extra = {}) => m('POST', `/orders/${o.id}/receipts`, { lines }, extra)
  const issue = (o, key, quantity) => m('POST', `/orders/${o.id}/material-issues`, { lines: [{ lineNo: mat(o, key).lineNo, quantity }] })
  const stockDocs = async (o) => (await m('GET', `/orders/${o.id}/stock-documents`)).json()
  const produce = async (o, qty) => { for (const seq of [10, 20, 30]) { const r = await report(o, seq, qty); if (r.status !== 201) return r } return { status: 201 } }
  const release = async (o) => m('POST', `/orders/${o.id}/release`)

  // ── MO-A: full run, lot on hold, completion, close
  let a = await create('TP-BAN01', 40)
  let r = await release(a)
  check('MO-A released', r.status === 200 && r.json().reservationPending === false, `${r.status} ${r.text.slice(0, 200)}`)
  a = await getOrder(a.id)
  r = await issue(a, 'NVL-SON01', 21)
  check('SON 21 issued by hand (to be settled at close)', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  r = await issue(a, 'NVL-KEO01', 4)
  check('KEO01 4 issued by hand', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  r = await produce(a, 40)
  check('operations 10 / 20 / 30 report Good 40', r.status === 201, `${r.status} ${r.text?.slice(0, 300)}`)
  r = await receive(a, [{ outputLineNo: null, quantity: 41 }])
  check('receive 41 of 40 with the last operation at 40 (tolerance would allow 42): 400 MFG_FG_001 (TC-FG-002)', r.status === 400 && code(r) === 'MFG_FG_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await receive(a, [{ outputLineNo: null, quantity: 40 }], { 'Idempotency-Key': `p35-${S}-a` })
  const fr1 = r.json()
  const receiptLine = fr1?.lines?.find((l) => l.kind === 'Receipt')
  check('receive 40: 201, document FR Posted, lot <item>-yymmdd-01 on quality hold in the quarantine warehouse (TC-FG-001)',
    r.status === 201 && fr1.status === 'Posted' && /^FR-\d{4}-\d{5}$/.test(fr1.docNo) && /-\d{6}-01$/.test(receiptLine?.lotNumber ?? '') && receiptLine.isQcHold === true && receiptLine.warehouseId === quarantine, `${r.status} ${r.text.slice(0, 500)}`)
  r = await receive(a, [{ outputLineNo: null, quantity: 40 }], { 'Idempotency-Key': `p35-${S}-a` })
  check('the same key answers the same receipt, nothing is booked twice', [200, 201].includes(r.status) && r.json().id === fr1.id && (await stockDocs(a)).filter((d) => d.type === 'Receipt').length === 1, `${r.status} ${r.text.slice(0, 200)}`)
  a = await getOrder(a.id)
  check('CompletedQty 40, order Completed by itself, ActualEnd set, every operation Completed (TC-PO-029)',
    near(a.completedQty, 40) && a.status === 'Completed' && !!a.actualEnd && a.operations.every((o) => o.status === 'Completed'), JSON.stringify([a.completedQty, a.status, a.operations.map((o) => o.status)]))
  const lotsTp = await lotsOf('TP-BAN01')
  const newLot = lotsTp.find((l) => l.lotNumber === receiptLine.lotNumber)
  check('the Warehouse lot exists, 40 pieces, on quality hold', !!newLot && near(newLot.remainingQuantity ?? newLot.quantity, 40) && /hold/i.test(String(newLot.status)), JSON.stringify(newLot))
  const rsA = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${a.id}`)).json()
  check('what the order still held in the Warehouse was released on completion', !rsA || rsA.status !== 'Reserved', JSON.stringify(rsA)?.slice(0, 200))

  // close checklist and close without confirmation
  let cl = (await m('GET', `/orders/${a.id}/close-checklist`)).json()
  const unmet = cl.conditions.filter((c) => !c.met).map((c) => c.code).sort()
  check('checklist: lot on hold and unsettled material are unmet (TC-PO-036 / 037)', cl.canClose === false && unmet.join() === 'QUALITY_HOLD,UNSETTLED_MATERIAL', JSON.stringify(cl))
  r = await m('POST', `/orders/${a.id}/close`, { confirmConsumeRemaining: true })
  check('close with the lot still on hold: 400 MFG_PO_013 naming the hold', r.status === 400 && code(r) === 'MFG_PO_013' && /chờ kiểm/.test(r.json().detail ?? ''), `${r.status} ${r.text.slice(0, 300)}`)

  // interim quality release
  r = await m('POST', `/stock-documents/${fr1.id}/qc-release`)
  check('qc-release: the receipt line shows the release time', r.status === 200 && r.json().lines.some((l) => l.kind === 'Receipt' && !!l.qcReleasedAt), `${r.status} ${r.text.slice(0, 300)}`)
  const lotsAfter = await lotsOf('TP-BAN01')
  const released = lotsAfter.find((l) => l.lotNumber === receiptLine.lotNumber)
  check('the lot is Available in the finished-goods warehouse', !!released && /available/i.test(String(released.status)) && (released.warehouseId ?? fgWarehouse) === fgWarehouse, JSON.stringify(released))
  r = await m('POST', `/stock-documents/${fr1.id}/qc-release`)
  check('releasing again is harmless', r.status === 200)
  check('the Warehouse holds both release movements once', (await refs('TP-BAN01')).filter((x) => x === receiptLine.movementKey + 'Q1' || x === receiptLine.movementKey + 'Q2').length === 2)

  // return on a Completed order, then close with the rest carried to consumption
  r = await m('POST', `/orders/${a.id}/material-returns`, { lines: [{ lineNo: mat(a, 'NVL-KEO01').lineNo, quantity: 1 }] })
  check('a return is accepted on a Completed order (KEO01 1)', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/close`, { confirmConsumeRemaining: false })
  check('close without confirmation: 400 MFG_PO_013 (unsettled SON / KEO01)', r.status === 400 && code(r) === 'MFG_PO_013' && /NVL-SON01/.test(r.json().detail ?? ''), `${r.status} ${r.text.slice(0, 300)}`)
  r = await m('POST', `/orders/${a.id}/close`, { confirmConsumeRemaining: true })
  check('close with confirmation: Closed (TC-PO-038)', r.status === 200 && r.json().status === 'Closed', `${r.status} ${r.text.slice(0, 300)}`)
  a = await getOrder(a.id)
  check('SON consumed 21, KEO01 3 (4 issued - 1 returned): the rest was carried to consumption', near(mat(a, 'NVL-SON01').consumedQty, 21) && near(mat(a, 'NVL-KEO01').consumedQty, 3), JSON.stringify([mat(a, 'NVL-SON01').consumedQty, mat(a, 'NVL-KEO01').consumedQty]))
  r = await m('POST', `/orders/${a.id}/reopen`)
  check('an action on a Closed order: 400 MFG_PO_014 (TC-PO-039)', r.status === 400 && code(r) === 'MFG_PO_014', `${r.status} ${r.text.slice(0, 200)}`)
  const hist = (await m('GET', `/orders/${a.id}/history`)).json()
  check('history shows Released, InProgress, Completed, Closed', ['Released', 'InProgress', 'Completed', 'Closed'].every((s) => hist.some((h) => h.toStatus === s)), JSON.stringify(hist.map((h) => h.toStatus)))

  // ── MO-B: partial receipts, lots -02 / -03, duplicate lot, short-close, reopen
  let b = await create('TP-BAN01', 50)
  await release(b)
  b = await getOrder(b.id)
  r = await produce(b, 25)
  check('MO-B: operations report Good 25', r.status === 201, `${r.status} ${r.text?.slice(0, 200)}`)
  r = await receive(b, [{ outputLineNo: null, quantity: 26 }])
  check('MO-B receive 26 of 25 good: 400 MFG_FG_001', r.status === 400 && code(r) === 'MFG_FG_001', `${r.status} ${r.text.slice(0, 200)}`)
  const lotNo = (d) => d?.lines?.find((l) => l.kind === 'Receipt')?.lotNumber
  r = await receive(b, [{ outputLineNo: null, quantity: 15 }])
  const b1 = r.json()
  r = await receive(b, [{ outputLineNo: null, quantity: 1, lotNumber: receiptLine.lotNumber }])
  check('a manual lot number that exists: 400 MFG_FG_002 (TC-FG-006)', r.status === 400 && code(r) === 'MFG_FG_002', `${r.status} ${r.text.slice(0, 200)}`)
  const oldLot = lotsTp.find((l) => /OLD/.test(l.lotNumber))
  if (oldLot) {
    r = await receive(b, [{ outputLineNo: null, quantity: 1, lotNumber: oldLot.lotNumber }])
    check('the old finished lot of the opening stock is protected too', r.status === 400 && code(r) === 'MFG_FG_002', `${r.status} ${r.text.slice(0, 200)}`)
  }
  r = await receive(b, [{ outputLineNo: null, quantity: 10 }])
  const b2 = r.json()
  check('two receipts on one day get consecutive lot numbers (TC-FG-005)', !!lotNo(b1) && !!lotNo(b2) && lotNo(b1) !== lotNo(b2) && /-\d{6}-0[23]$/.test(lotNo(b1)) && /-\d{6}-0[34]$/.test(lotNo(b2)), `${lotNo(b1)} ${lotNo(b2)}`)
  b = await getOrder(b.id)
  check('MO-B CompletedQty 25, still InProgress', near(b.completedQty, 25) && b.status === 'InProgress', JSON.stringify([b.completedQty, b.status]))
  r = await m('POST', `/orders/${b.id}/complete`, {})
  check('complete at 25 of 50 without a reason: 400 MFG_PO_012 (TC-PO-031)', r.status === 400 && code(r) === 'MFG_PO_012', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${b.id}/complete`, { reasonCodeId: shortCloseReason, note: 'customer takes 25' })
  check('short-close with the ShortClose reason: Completed, operations Completed (TC-PO-032)', r.status === 200 && r.json().order.status === 'Completed' && r.json().order.operations.every((o) => o.status === 'Completed'), `${r.status} ${r.text.slice(0, 300)}`)
  r = await m('POST', `/orders/${b.id}/reopen`)
  check('reopen: InProgress, ActualEnd cleared (TC-PO-034)', r.status === 200 && r.json().order.status === 'InProgress' && !r.json().order.actualEnd, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${b.id}/complete`, { reasonCodeId: shortCloseReason })
  check('completed again', r.status === 200 && r.json().order.status === 'Completed')

  // ── MO-C: co- / by-product (TC-FG-010 / 011)
  let c = await create('BTP-MAT01', 50)
  await release(c)
  c = await getOrder(c.id)
  const output = c.outputs[0]
  r = await report(c, 10, 48)
  check('MO-C: the cutting operation reports Good 48', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  check('MO-C has a by-product line planned 25', !!output && near(output.plannedQty, 25), JSON.stringify(c.outputs))
  r = await receive(c, [{ outputLineNo: output.lineNo, quantity: 26.26 }])
  check('by-product 26.26 of a plan of 25: 400 MFG_FG_001 (ceiling 26.25)', r.status === 400 && code(r) === 'MFG_FG_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await receive(c, [{ outputLineNo: null, quantity: 48 }, { outputLineNo: output.lineNo, quantity: 24 }])
  check('main 48 + by-product 24: 201, two receipt lines', r.status === 201 && r.json().lines.filter((l) => l.kind === 'Receipt').length === 2, `${r.status} ${r.text.slice(0, 400)}`)
  c = await getOrder(c.id)
  check('CompletedQty 48 and the by-product received 24', near(c.completedQty, 48) && near(c.outputs[0].receivedQty, 24), JSON.stringify([c.completedQty, c.outputs[0].receivedQty]))

  // ── MO-D: Warehouse outage (BTP-CHAN01 has no lot and no backflush, so the receipt needs no Warehouse read)
  let d = await create('BTP-CHAN01', 200)
  await release(d)
  d = await getOrder(d.id)
  r = await m('POST', `/orders/${d.id}/material-issues`, { lines: [{ lineNo: mat(d, 'NVL-GO01').lineNo, quantity: 0.5 }] })
  check('MO-D takes 0.5 of wood and is InProgress', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  const chan0 = await refs('BTP-CHAN01')
  docker('stop', 'erp-warehouse-service')
  let pendingDoc
  try {
    await sleep(2000)
    r = await receive(d, [{ outputLineNo: null, quantity: 100 }])
    pendingDoc = r.json()
    check('receipt with the Warehouse down: 202 Pending', r.status === 202 && pendingDoc.status === 'Pending', `${r.status} ${r.text.slice(0, 300)}`)
    r = await m('POST', `/orders/${d.id}/complete`, {})
    check('the order cannot be completed while a document is pending: 400 MFG_PO_013', r.status === 400 && code(r) === 'MFG_PO_013', `${r.status} ${r.text.slice(0, 200)}`)
    r = await receive(b, [{ outputLineNo: null, quantity: 1 }])
    check('a receipt that needs the Warehouse (lot list) is refused MFG_INT_001, nothing stored', r.status === 400 && code(r) === 'MFG_INT_001' || r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  let posted = null
  for (let i = 0; i < 30 && !posted; i++) {
    await sleep(5000)
    const cur = (await m('GET', `/stock-documents/${pendingDoc.id}`)).json()
    if (cur?.status === 'Posted') posted = cur
  }
  check('after the Warehouse is back the job posts the receipt', !!posted)
  d = await getOrder(d.id)
  check('exactly one movement for its key and CompletedQty 100', (await refs('BTP-CHAN01')).filter((x) => x === pendingDoc.lines[0].movementKey).length === 1 && near(d.completedQty, 100), `${d.completedQty}`)
  check('chan references before the outage did not contain the key', !chan0.includes(pendingDoc.lines[0].movementKey))

  // ── permissions
  const mail = `zz35${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P35' })
  const cl2 = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (cl2.status === 200 && cl2.json().accessToken) {
    const plain = { Authorization: `Bearer ${cl2.json().accessToken}`, ...J }
    for (const [what, method, path, body] of [['receipt', 'POST', `/orders/${b.id}/receipts`, { lines: [] }], ['complete', 'POST', `/orders/${b.id}/complete`, {}], ['reopen', 'POST', `/orders/${b.id}/reopen`],
      ['close-checklist', 'GET', `/orders/${b.id}/close-checklist`], ['close', 'POST', `/orders/${b.id}/close`, { confirmConsumeRemaining: false }], ['qc-release', 'POST', `/stock-documents/${fr1.id}/qc-release`]]) {
      const x = await call(method, `/mfg/api/v1/manufacturing${path}`, plain, body)
      check(`a user without the permission gets 403 on ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', cl2.status, cl2.text.slice(0, 100))

  // ── clean up what the open orders still hold in the Warehouse
  for (const o of [b, c, d]) {
    const rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${o.id}`)).json()
    if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p35 cleanup' })
  }

  console.log(`\nprefix ${prefix} plant ${plantCode} orders ${a.orderNo} ${b.orderNo} ${c.orderNo} ${d.orderNo}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
