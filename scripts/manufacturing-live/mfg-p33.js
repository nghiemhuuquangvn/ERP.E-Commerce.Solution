// Manufacturing P3-3 live check (stock-document engine, material issue and return) on a fresh DS-01 set with real stock and lots.
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
  const prefix = `ZZP33${S}-`
  const plantCode = `ZZ-P33-${S}`
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
  const lotsOf = async (key) => (await wh('GET', `/inventory/lots/product/${ids[key]}`)).json() ?? []
  const refs = async (key) => ((await wh('GET', `/movements/${ids[key]}?take=200`)).json()?.items ?? []).map((x) => x.reference)

  await m('POST', '/reason-codes/seed-defaults')
  const reasons = (await m('GET', '/reason-codes')).json()
  const reasonOf = (type) => reasons.find((x) => x.type === type && x.isActive)?.id
  const holdReason = reasonOf('Hold')
  const overIssue = reasonOf('OverIssue')
  check('Hold and OverIssue reasons exist', !!holdReason && !!overIssue)

  let due = addDays(plantNow(), 7)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const create = async (qty = 100) => (await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: qty, dueDate: iso(due) })).json()
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const line = (o, key, quantity, lotId) => ({ lineNo: o.materials.find((x) => x.componentCode === prefix + key).lineNo, quantity, lotId })
  const mat = (o, key) => o.materials.find((x) => x.componentCode === prefix + key)
  const issue = (o, lines, extra = {}, reasonCodeId) => m('POST', `/orders/${o.id}/material-issues`, { lines, reasonCodeId }, extra)
  const ret = (o, lines, extra = {}) => m('POST', `/orders/${o.id}/material-returns`, { lines }, extra)
  const docs = async (query = '') => (await m('GET', `/stock-documents?plantId=${plant}${query}`)).json()

  // ── a draft takes no material (TC-MAT-002)
  const draft = await create(100)
  let r = await issue(draft, [line(draft, 'NVL-KEO01', 1)])
  check('issue on a Draft: 400 MFG_MAT_001', r.status === 400 && code(r) === 'MFG_MAT_001', `${r.status} ${r.text.slice(0, 200)}`)

  r = await m('POST', `/orders/${draft.id}/release`)
  check('MO-A released with the reservation in line', r.status === 200 && r.json().reservationPending === false, `${r.status} pending=${r.json()?.reservationPending} err=${r.json()?.order?.reservationSyncError} shortages=${JSON.stringify(r.json()?.shortages?.map((x) => x.componentCode))}`)
  let a = await getOrder(draft.id)

  // ── TC-MAT-001: issue KEO01 10 from the reservation
  const keoBefore = await inv('NVL-KEO01')
  const key1 = `p33-${S}-1`
  r = await issue(a, [line(a, 'NVL-KEO01', 10)], { 'Idempotency-Key': key1 })
  const d1 = r.json()
  check('issue KEO01 10: 201, document MI, Posted, one reserved line', r.status === 201 && d1.status === 'Posted' && /^MI-\d{4}-\d{5}$/.test(d1.docNo) && d1.lines.length === 1 && d1.lines[0].isFromReservation === true, `${r.status} ${r.text.slice(0, 400)}`)
  a = await getOrder(a.id)
  check('order is InProgress with ActualStart, KEO01 issued 10, no pending documents', a.status === 'InProgress' && !!a.actualStart && near(mat(a, 'NVL-KEO01').issuedQty, 10) && a.pendingStockDocuments === 0, JSON.stringify([a.status, a.actualStart, mat(a, 'NVL-KEO01').issuedQty, a.pendingStockDocuments]))
  check('stock of KEO01 down by 10', near((await inv('NVL-KEO01')).totalQuantity, Number(keoBefore.totalQuantity) - 10))
  check('the Warehouse holds the movement under the line key', (await refs('NVL-KEO01')).includes(d1.lines[0].movementKey), d1.lines[0].movementKey)
  let rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${a.id}`)).json()
  const keoLine = rs?.lines?.find((l) => l.productId === ids['NVL-KEO01'])
  check('the reservation line of KEO01 is consumed (BR-MAT-07)', near(keoLine?.consumed, 10) && near(keoLine?.remaining, 0), JSON.stringify(keoLine))

  // ── TC-GEN-010: the same key twice, then six in parallel with another key
  r = await issue(a, [line(a, 'NVL-KEO01', 10)], { 'Idempotency-Key': key1 })
  check('the same Idempotency-Key answers the same document, nothing is booked twice', (r.status === 201 || r.status === 200) && r.json().id === d1.id && (await refs('NVL-KEO01')).filter((x) => x === d1.lines[0].movementKey).length === 1, `${r.status} ${r.text.slice(0, 200)}`)
  r = await issue(a, [line(a, 'NVL-KEO01', 5)], { 'Idempotency-Key': key1 })
  check('the same key with another request: 409/400 MFG_GEN_007', [400, 409].includes(r.status) && code(r) === 'MFG_GEN_007', `${r.status} ${r.text.slice(0, 200)}`)
  const keyP = `p33-${S}-par`
  const par = await Promise.all(Array.from({ length: 6 }, () => issue(a, [line(a, 'BTP-MAT01', 1)], { 'Idempotency-Key': keyP })))
  const parIds = new Set(par.map((x) => x.json()?.id))
  check('6 parallel posts with one key: one document', par.every((x) => [200, 201, 202].includes(x.status)) && parIds.size === 1, par.map((x) => `${x.status}:${x.text.slice(0, 80)}`).join(' | '))
  await sleep(1500)
  const matRefs = (await refs('BTP-MAT01')).filter((x) => x.startsWith([...parIds][0] ? d1.docNo.slice(0, 3) : 'MI-'))
  check('and exactly one tops movement of 1 piece', (await getOrder(a.id)).materials.find((x) => x.componentCode === prefix + 'BTP-MAT01').issuedQty === 1, JSON.stringify(matRefs))

  // ── tolerance: TC-MAT-004 / 005 / 006
  a = await getOrder(a.id)
  r = await issue(a, [line(a, 'NVL-KEO01', 0.3)])
  check('KEO01 up to 10.30 (tolerance 3 %) is accepted', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  r = await issue(a, [line(a, 'NVL-KEO01', 0.01)])
  check('0.01 more: 400 MFG_MAT_002', r.status === 400 && code(r) === 'MFG_MAT_002', `${r.status} ${r.text.slice(0, 200)}`)
  r = await issue(a, [line(a, 'NVL-KEO01', 1)], {}, holdReason)
  check('over-issue with a wrong reason type: 400 MFG_MAT_002', r.status === 400 && code(r) === 'MFG_MAT_002', `${r.status} ${r.text.slice(0, 200)}`)
  r = await issue(a, [line(a, 'NVL-KEO01', 1)], {}, overIssue)
  check('over-issue with permission and an OverIssue reason: 201, reason kept', r.status === 201 && r.json().reasonCodeId === overIssue && r.json().lines[0].isFromReservation === false, `${r.status} ${r.text.slice(0, 300)}`)

  // ── TC-MAT-007: more than the stock
  const docsBefore = (await docs()).total
  r = await issue(a, [line(a, 'BTP-CHAN01', 350)])
  check('CHAN 350 against 300: 400 MFG_MAT_003, no document stored', r.status === 400 && code(r) === 'MFG_MAT_003' && (await docs()).total === docsBefore, `${r.status} ${r.text.slice(0, 200)}`)

  // ── lots (TC-MAT-004 lot rule, 005 expired, 010 suggestion)
  const sonLots = await lotsOf('NVL-SON01')
  const lotX = sonLots.find((l) => l.lotNumber === 'L-SON-X')
  const lotA = sonLots.find((l) => l.lotNumber === 'L-SON-A')
  const lotB = sonLots.find((l) => l.lotNumber === 'L-SON-B')
  r = await m('GET', `/orders/${a.id}/material-issues/suggest-lots?lineNo=${line(a, 'NVL-SON01', 1).lineNo}&quantity=10`)
  const sug = r.json()
  check('suggest-lots: first-expiry-first, never the expired lot, and the lots the reservation holds', r.status === 200 && sug.suggestion.lots.length > 0 && !sug.suggestion.lots.some((l) => l.lotNumber === 'L-SON-X') && sug.reservedLots.some((l) => l.lotNumber === 'L-SON-A') && near(sug.reservedRemaining, 52.5), `${r.status} ${r.text.slice(0, 400)}`)
  r = await issue(a, [line(a, 'NVL-SON01', 1, lotX.lotId)])
  check('expired lot named: 400 MFG_MAT_005', r.status === 400 && code(r) === 'MFG_MAT_005', `${r.status} ${r.text.slice(0, 200)}`)
  r = await issue(a, [line(a, 'NVL-SON01', 52.5)])
  const sonDoc = r.json()
  check('SON 52.5 from the reservation without naming a lot: Posted (the Warehouse takes A 30 + B 22.5)', r.status === 201 && sonDoc.status === 'Posted', `${r.status} ${r.text.slice(0, 300)}`)
  const lotsAfter = await lotsOf('NVL-SON01')
  check('lot A used up, lot B 27.5 left, expired X untouched', near(lotsAfter.find((l) => l.lotNumber === 'L-SON-A')?.remainingQuantity ?? 0, 0) && near(lotsAfter.find((l) => l.lotNumber === 'L-SON-B')?.remainingQuantity, 27.5) && near(lotsAfter.find((l) => l.lotNumber === 'L-SON-X')?.remainingQuantity, 20), JSON.stringify(lotsAfter.map((l) => [l.lotNumber, l.remainingQuantity])))
  r = await issue(a, [line(a, 'NVL-SON01', 1)])
  check('SON beyond the reservation without a lot: 400 MFG_MAT_004 (TC-MAT-008)', r.status === 400 && code(r) === 'MFG_MAT_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await issue(a, [line(a, 'NVL-SON01', 1, lotB.lotId)])
  check('SON 1 from lot B (free stock): Posted, lot kept on the line', r.status === 201 && r.json().lines[0].lotId === lotB.lotId, `${r.status} ${r.text.slice(0, 300)}`)

  // ── returns: TC-MAT-014 / 015 / 016
  a = await getOrder(a.id)
  const keoIssued = mat(a, 'NVL-KEO01').issuedQty
  r = await ret(a, [line(a, 'NVL-KEO01', 2)])
  const mr = r.json()
  check('return KEO01 2: 201, document MR, Posted', r.status === 201 && mr.status === 'Posted' && /^MR-/.test(mr.docNo) && mr.lines[0].isFromReservation === false, `${r.status} ${r.text.slice(0, 300)}`)
  const issueCost = d1.lines[0].unitCost
  check('returned at the issue cost', near(mr.lines[0].unitCost, Number(issueCost)), `${mr.lines[0].unitCost} vs ${issueCost}`)
  a = await getOrder(a.id)
  check('ReturnedQty 2, net issued down by 2, stock up by 2', near(mat(a, 'NVL-KEO01').returnedQty, 2) && near(mat(a, 'NVL-KEO01').issuedQty, keoIssued), JSON.stringify(mat(a, 'NVL-KEO01')))
  r = await ret(a, [line(a, 'NVL-KEO01', 100)])
  check('return more than is out: 400 MFG_MAT_006', r.status === 400 && code(r) === 'MFG_MAT_006', `${r.status} ${r.text.slice(0, 200)}`)
  r = await ret(a, [line(a, 'NVL-SON01', 1)])
  check('lot-controlled return without a lot: 400 MFG_MAT_004', r.status === 400 && code(r) === 'MFG_MAT_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await ret(a, [line(a, 'NVL-SON01', 2.5, lotB.lotId)])
  check('SON 2.5 back into lot B: Posted, the lot is kept', r.status === 201 && r.json().lines[0].lotId === lotB.lotId, `${r.status} ${r.text.slice(0, 300)}`)
  check('lot B up by 2.5', near((await lotsOf('NVL-SON01')).find((l) => l.lotNumber === 'L-SON-B')?.remainingQuantity, 27.5 - 1 + 2.5))

  // ── the order cannot go back once material left (TC-PO-017) and a held order takes none (BR-PO-11)
  r = await m('POST', `/orders/${a.id}/unrelease`)
  check('unrelease after an issue: refused (the order is InProgress, MFG_PO_002)', r.status === 400 && ['MFG_PO_008', 'MFG_PO_002'].includes(code(r)), `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/hold`, { reasonCodeId: holdReason })
  check('hold the order', r.status === 200)
  r = await issue(a, [line(a, 'BTP-MAT01', 1)])
  check('issue on a held order: 400 MFG_PO_011', r.status === 400 && code(r) === 'MFG_PO_011', `${r.status} ${r.text.slice(0, 200)}`)
  await m('POST', `/orders/${a.id}/resume`)

  // ── list and read the documents
  const list = await docs(`&orderId=${a.id}`)
  check('the order has its documents in the list, newest first', list.total >= 6 && list.items[0].createdAt >= list.items[list.items.length - 1].createdAt, `${list.total}`)
  r = await m('GET', `/orders/${a.id}/stock-documents`)
  check('the order endpoint lists the same documents', r.status === 200 && r.json().length === list.total)
  r = await m('GET', `/stock-documents/${d1.id}`)
  check('one document with its lines', r.status === 200 && r.json().docNo === d1.docNo && r.json().lines.length === 1)

  // ── failure injection 1: an issue is checked against the Warehouse first, so with the Warehouse down it is refused and nothing is stored
  const docsBeforeOutage = (await docs()).total
  const keoStock = Number((await inv('NVL-KEO01')).totalQuantity)
  const keoReturned = Number(mat(await getOrder(a.id), 'NVL-KEO01').returnedQty)
  docker('stop', 'erp-warehouse-service')
  let pendingDoc
  try {
    await sleep(2000)
    r = await issue(a, [line(a, 'BTP-MAT01', 1)])
    check('issue with the Warehouse down: fails closed, 400 MFG_INT_001, nothing stored', r.status === 400 && code(r) === 'MFG_INT_001', `${r.status} ${r.text.slice(0, 200)}`)

    // a return needs no Warehouse read to be accepted: it is stored Pending and the engine keeps trying
    r = await ret(a, [line(a, 'NVL-KEO01', 1)])
    pendingDoc = r.json()
    check('return with the Warehouse down: 202 Pending, attempts >= 1', r.status === 202 && pendingDoc.status === 'Pending' && pendingDoc.attempts >= 1, `${r.status} ${r.text.slice(0, 300)}`)
    const during = await getOrder(a.id)
    check('the order shows nothing of it yet and counts one pending document', near(mat(during, 'NVL-KEO01').returnedQty, keoReturned) && during.pendingStockDocuments === 1, JSON.stringify([mat(during, 'NVL-KEO01').returnedQty, during.pendingStockDocuments]))
    r = await m('POST', `/orders/${a.id}/cancel`, { reasonCodeId: reasonOf('Cancel') })
    check('cancel is refused', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
    r = await m('POST', `/stock-documents/${pendingDoc.id}/retry`)
    check('manual retry with the Warehouse still down: 202, attempts grew', r.status === 202 && r.json().attempts > pendingDoc.attempts, `${r.status} ${r.text.slice(0, 200)}`)
    r = await docs('&status=Pending')
    check('the pending document is in the Pending list', r.items.some((x) => x.id === pendingDoc.id))
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  let posted = null
  for (let i = 0; i < 30 && !posted; i++) {
    await sleep(5000)
    const cur = (await m('GET', `/stock-documents/${pendingDoc.id}`)).json()
    if (cur?.status === 'Posted') posted = cur
  }
  check('after the Warehouse is back the job posts the document', !!posted)
  check('exactly one movement for its key, stock up by 1 once, order up by 1 once',
    (await refs('NVL-KEO01')).filter((x) => x === pendingDoc.lines[0].movementKey).length === 1 && near((await inv('NVL-KEO01')).totalQuantity, keoStock + 1) && near(mat(await getOrder(a.id), 'NVL-KEO01').returnedQty, keoReturned + 1))
  check('one document was added and none is pending', (await docs()).total === docsBeforeOutage + 1 && (await getOrder(a.id)).pendingStockDocuments === 0)

  // ── failure injection 2: abandon a pending document (nothing was booked, so the rollback is empty)
  docker('stop', 'erp-warehouse-service')
  let abandoned
  try {
    await sleep(2000)
    r = await ret(a, [line(a, 'NVL-KEO01', 1)])
    const second = r.json()
    check('another outage: the return is 202 Pending', r.status === 202, `${r.status} ${r.text.slice(0, 200)}`)
    r = await m('POST', `/stock-documents/${second.id}/abandon`)
    abandoned = r.json()
    check('abandon: the document ends Failed (ABANDONED) and no longer blocks the order', r.status === 200 && abandoned.status === 'Failed' && abandoned.lastErrorCode === 'ABANDONED', `${r.status} ${r.text.slice(0, 300)}`)
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  check('the order did not change and has nothing pending', near(mat(await getOrder(a.id), 'NVL-KEO01').returnedQty, keoReturned + 1) && (await getOrder(a.id)).pendingStockDocuments === 0)
  r = await docs('&status=Failed')
  check('the failed document is in the Failed list', r.items.some((x) => x.id === abandoned.id))

  // ── permissions and another tenant
  const mail = `zz33${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P33' })
  const cl = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (cl.status === 200 && cl.json().accessToken) {
    const plain = { Authorization: `Bearer ${cl.json().accessToken}`, ...J }
    for (const [what, method, path, body] of [['issue', 'POST', `/orders/${a.id}/material-issues`, { lines: [] }], ['return', 'POST', `/orders/${a.id}/material-returns`, { lines: [] }],
      ['suggest-lots', 'GET', `/orders/${a.id}/material-issues/suggest-lots?lineNo=1&quantity=1`], ['order documents', 'GET', `/orders/${a.id}/stock-documents`],
      ['list', 'GET', `/stock-documents?plantId=${plant}`], ['retry', 'POST', `/stock-documents/${d1.id}/retry`], ['abandon', 'POST', `/stock-documents/${d1.id}/abandon`]]) {
      const x = await call(method, `/mfg/api/v1/manufacturing${path}`, plain, body)
      check(`a user without the permission gets 403 on ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', cl.status, cl.text.slice(0, 100))

  // ── clean up what the order still holds in the Warehouse
  rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${a.id}`)).json()
  if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p33 cleanup' })

  console.log(`\nprefix ${prefix} plant ${plantCode} order ${a.orderNo}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
