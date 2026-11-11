// Manufacturing P3-6 contract check (node scripts/manufacturing-live/mfg-p36-contract-check.js; needs the stack up, the Gateway on 127.0.0.1:5000 and Docker for the Warehouse-stopped part): drives an order through every call the Admin screens make and asserts that the JSON has every key the TypeScript
// interfaces of src/frontend/admin/src/api/manufacturing/mfg.api.ts declare (and that enum values are members of the declared unions).
// Default host = glcheck; MFG_HOST / MFG_EMAIL / MFG_PASSWORD switch tenant (SYSTEM: admin.erp.local / superadmin@system.local / SuperAdmin@123).
const http = require('http')
const fs = require('fs')
const { execFileSync } = require('child_process')
const host = process.env.MFG_HOST || 'glcheck.erp.local'
const email = process.env.MFG_EMAIL || 'admin@glcheck.test'
const password = process.env.MFG_PASSWORD || 'Gl-Check-Passw0rd!'
const repo = require('path').resolve(__dirname, '..', '..')
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

// ── the TypeScript contract, read from the source ────────────────────────────────────────────────────────
const tsSource = fs.readFileSync(`${repo}/src/frontend/admin/src/api/manufacturing/mfg.api.ts`, 'utf8')
const interfaces = {}
for (const m of tsSource.matchAll(/export interface (\w+)(?:<[^>]*>)?(?: extends (\w+))? \{/g)) {
  let depth = 1, i = m.index + m[0].length
  const start = i
  while (depth > 0 && i < tsSource.length) { const c = tsSource[i++]; if (c === '{') depth++; else if (c === '}') depth-- }
  const body = tsSource.slice(start, i - 1)
  // top-level members only
  const fields = []
  let d = 0, token = ''
  for (const c of body) {
    if (c === '{') d++
    if (c === '}') d--
    if (d === 0 && (c === ';' || c === '\n')) { if (token.trim()) fields.push(token.trim()); token = ''; continue }
    token += c
  }
  if (token.trim()) fields.push(token.trim())
  // a line may carry several members separated by ';' (already split) or by ',' inside one-line interfaces: split those at depth 0 too
  const members = []
  for (const f of fields) {
    let dd = 0, cur = ''
    for (const c of f) { if ('{[('.includes(c)) dd++; if ('}])'.includes(c)) dd--; if (c === ';' && dd === 0) { members.push(cur); cur = ''; continue } cur += c }
    members.push(cur)
  }
  const required = [], optional = []
  for (const mem of members) {
    const mm = /^\s*(\w+)(\?)?:/.exec(mem)
    if (mm) (mm[2] ? optional : required).push(mm[1])
  }
  interfaces[m[1]] = { required, optional, parent: m[2] }
}
const unions = {}
for (const m of tsSource.matchAll(/export type (\w+) = ((?:'[^']+'(?: \| )?)+)/g)) unions[m[1]] = [...m[2].matchAll(/'([^']+)'/g)].map((x) => x[1])

function shape(label, name, value) {
  const spec = interfaces[name]
  if (!spec) { check(`${label}: interface ${name} found in mfg.api.ts`, false); return }
  if (value === null || typeof value !== 'object' || Array.isArray(value)) { check(`${label} matches ${name}`, false, `got ${JSON.stringify(value)?.slice(0, 120)}`); return }
  const required = [...spec.required, ...(spec.parent && interfaces[spec.parent] ? interfaces[spec.parent].required : [])]
  const missing = required.filter((k) => !(k in value))
  const declared = new Set([...required, ...spec.optional])
  const extra = Object.keys(value).filter((k) => !declared.has(k))
  check(`${label} has every key of ${name}`, missing.length === 0, `missing ${missing.join(', ')}`)
  if (extra.length) console.log(`     (info) ${label}: keys the server sends and ${name} does not declare: ${extra.join(', ')}`)
}
const shapes = (label, name, list) => { check(`${label} is a list`, Array.isArray(list), JSON.stringify(list)?.slice(0, 120)); if (Array.isArray(list) && list.length) shape(`${label}[0]`, name, list[0]) }
const member = (label, unionName, value) => check(`${label} "${value}" is a member of ${unionName}`, (unions[unionName] ?? []).includes(value), JSON.stringify(unions[unionName]))

;(async () => {
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const prefix = `ZZP36${S}-`
  const plantCode = `ZZ-P36-${S}`
  console.log(`contract: ${Object.keys(interfaces).length} interfaces, ${Object.keys(unions).length} unions read from mfg.api.ts`)
  const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
  const seedAnswer = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, ''))
  const ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
  const plant = seedAnswer.plantId
  check('DS-01 set seeded', !!plant && !!ids['TP-BAN01'], out.slice(-300))

  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body, extra = {}) => call(method, `/mfg/api/v1/manufacturing${path}`, { ...auth, ...extra }, body)
  const code = (r) => r.json()?.errorCode

  await m('POST', '/reason-codes/seed-defaults')
  const reasonsRes = await m('GET', '/reason-codes')
  const reasons = reasonsRes.json()
  shapes('reason-codes', 'ReasonCode', reasons)
  const reasonOf = (type) => reasons.find((x) => x.type === type && x.isActive)?.id
  for (const t of ['Cancel', 'Hold', 'ShortClose', 'OverIssue', 'Scrap', 'Rework']) check(`an active ${t} reason exists (the dialogs need it)`, !!reasonOf(t))

  const plantsRes = await m('GET', '/plants?activeOnly=true')
  shapes('plants', 'Plant', plantsRes.json())

  // employees the way the Admin asks for them
  const emps = await call('GET', '/mst/api/v1/masterdata/employees?status=0&pageSize=200', auth)
  const empList = emps.json()
  check('employees: envelope with items (id, employeeNumber, fullName)', emps.status === 200 && Array.isArray(empList?.items) && empList.items.length >= 2 && ['id', 'employeeNumber', 'fullName'].every((k) => k in empList.items[0]), emps.text.slice(0, 200))
  const empIds = (empList?.items ?? []).map((e) => e.id)

  let due = addDays(plantNow(), 7)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const create = async (key, qty, extra = {}) => (await m('POST', '/orders', { plantId: plant, itemId: ids[key], quantity: qty, dueDate: iso(due), ...extra })).json()
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const mat = (o, key) => o.materials.find((x) => x.componentCode === prefix + key)

  // ── order A: create, list, availability, release, history
  const createRes = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 40, dueDate: iso(due), priority: 2, notes: 'p36' })
  check('create answers 201', createRes.status === 201, createRes.text.slice(0, 200))
  let a = createRes.json()
  shape('created order', 'Order', a)
  member('order status', 'OrderStatus', a.status)
  shapes('order materials', 'OrderMaterial', a.materials)
  shapes('order operations', 'OrderOperation', a.operations)
  member('operation status', 'OperationStatus', a.operations[0].status)
  member('material issueMethod', 'IssueMethod', a.materials[0].issueMethod)
  check('pendingStockDocuments is a number on a fresh order', typeof a.pendingStockDocuments === 'number')

  const listRes = await m('GET', `/orders?plantId=${plant}&status=Draft&isLate=false&priority=2&search=${encodeURIComponent(a.orderNo)}&dueFrom=${iso(addDays(plantNow(), -1))}&dueTo=${iso(addDays(due, 1))}&page=1&pageSize=25`)
  const list = listRes.json()
  check('list: envelope items / total / page / pageSize', listRes.status === 200 && Array.isArray(list.items) && ['total', 'page', 'pageSize'].every((k) => k in list), listRes.text.slice(0, 200))
  check('list: the filters found the order', list.items.some((x) => x.id === a.id), listRes.text.slice(0, 300))
  shapes('list items', 'OrderListItem', list.items)
  const empty = (await m('GET', `/orders?plantId=${plant}&status=Closed&page=1&pageSize=25`)).json()
  check('list: a filter with no match answers an empty page', Array.isArray(empty.items) && empty.items.length === 0 && empty.total === 0, JSON.stringify(empty))

  const avail = (await m('GET', `/orders/${a.id}/availability`)).json()
  shape('availability', 'OrderAvailability', avail)
  shapes('availability materials', 'MaterialAvailability', avail.materials)

  let upd = await m('PUT', `/orders/${a.id}`, { quantity: 40, dueDate: iso(due), priority: 3, bomId: null, routingId: null, outputWarehouseId: null, notes: 'edited' })
  check('PUT a Draft with the body the form sends (nulls for the optional fields)', upd.status === 200 && upd.json().notes === 'edited', `${upd.status} ${upd.text.slice(0, 200)}`)

  let r = await m('POST', `/orders/${a.id}/release`)
  check('release answers 200 with order / shortages / reservationPending', r.status === 200, r.text.slice(0, 200))
  shape('release answer', 'OrderActionResult', r.json())
  shape('release answer order', 'Order', r.json().order)
  a = await getOrder(a.id)
  member('released status', 'OrderStatus', a.status)

  // ── material: suggest, issue (idempotent), return
  const son = mat(a, 'NVL-SON01')
  const sug = await m('GET', `/orders/${a.id}/material-issues/suggest-lots?lineNo=${son.lineNo}&quantity=${son.requiredQty}`)
  check('suggest-lots answers 200', sug.status === 200, sug.text.slice(0, 200))
  shape('suggest-lots', 'MaterialLotSuggestion', sug.json())
  shape('suggest-lots.suggestion', 'LotSuggestion', sug.json().suggestion)
  if (sug.json().suggestion.lots.length) shape('suggested lot', 'SuggestedLot', sug.json().suggestion.lots[0])
  check('the suggestion names real lots for the lot-controlled paint', sug.json().suggestion.lots.length > 0 || sug.json().reservedRemaining >= son.requiredQty, sug.text.slice(0, 300))

  const key = `p36-${S}-issue`
  r = await m('POST', `/orders/${a.id}/material-issues`, { lines: [{ lineNo: mat(a, 'NVL-KEO01').lineNo, quantity: 4, lotId: null }], reasonCodeId: null, note: null }, { 'Idempotency-Key': key })
  check('issue answers 201 with a Posted document', r.status === 201 && r.json().status === 'Posted', `${r.status} ${r.text.slice(0, 300)}`)
  const issueDoc = r.json()
  shape('issue document', 'StockDocument', issueDoc)
  shapes('issue lines', 'StockDocumentLine', issueDoc.lines)
  member('document type', 'StockDocumentType', issueDoc.type)
  member('document status', 'StockDocumentStatus', issueDoc.status)
  member('line status', 'StockLineStatus', issueDoc.lines[0].status)
  member('line kind', 'StockLineKind', issueDoc.lines[0].kind)
  r = await m('POST', `/orders/${a.id}/material-issues`, { lines: [{ lineNo: mat(a, 'NVL-KEO01').lineNo, quantity: 4, lotId: null }], reasonCodeId: null, note: null }, { 'Idempotency-Key': key })
  check('the same Idempotency-Key and body answer the same document', [200, 201].includes(r.status) && r.json().id === issueDoc.id, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/material-issues`, { lines: [{ lineNo: mat(a, 'NVL-KEO01').lineNo, quantity: 5, lotId: null }], reasonCodeId: null, note: null }, { 'Idempotency-Key': key })
  check('the same key with another body is refused (409 MFG_GEN_007)', r.status === 409 || code(r) === 'MFG_GEN_007', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/material-returns`, { lines: [{ lineNo: mat(a, 'NVL-KEO01').lineNo, quantity: 1, lotId: null }], reasonCodeId: null, note: null })
  check('return answers 201', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  shape('return document', 'StockDocument', r.json())
  r = await m('POST', `/orders/${a.id}/material-issues`, { lines: [{ lineNo: mat(a, 'NVL-KEO01').lineNo, quantity: 99999, lotId: null }], reasonCodeId: null, note: null })
  check('an impossible issue is a 400 with an errorCode the dialog can react to', r.status === 400 && /^MFG_/.test(code(r) ?? ''), `${r.status} ${r.text.slice(0, 200)}`)
  const docsRes = await m('GET', `/orders/${a.id}/stock-documents`)
  shapes('order stock-documents', 'StockDocument', docsRes.json())

  // ── operations: list, time logs, report
  const opsRes = await m('GET', `/orders/${a.id}/operations`)
  const ops = opsRes.json()
  shapes('operations', 'OperationDetail', ops)
  shape('operations[0].operation', 'OrderOperation', ops[0].operation)
  let log
  for (const e of empIds) {
    r = await m('POST', `/orders/${a.id}/operations/10/start`, { employeeId: e, machineId: null })
    if (r.status === 201) { log = r.json(); break }
  }
  check('start answers 201 with a time log', !!log, r.text.slice(0, 200))
  shape('time log', 'TimeLog', log)
  check('time log state is Running', log.state === 'Running')
  r = await m('POST', `/orders/${a.id}/operations/10/time-logs/${log.id}/pause`)
  check('pause answers 200 / Paused', r.status === 200 && r.json().state === 'Paused', r.text.slice(0, 200))
  r = await m('POST', `/orders/${a.id}/operations/10/time-logs/${log.id}/resume`)
  check('resume answers 200 / Running', r.status === 200 && r.json().state === 'Running', r.text.slice(0, 200))
  r = await m('POST', `/orders/${a.id}/operations/10/time-logs/${log.id}/stop`)
  check('stop answers 200 / Stopped with a duration', r.status === 200 && r.json().state === 'Stopped' && r.json().durationMinutes != null, r.text.slice(0, 200))
  let manual
  for (const e of empIds.slice().reverse()) {
    const t = (min) => new Date(Date.now() + min * 60000).toISOString()
    r = await m('POST', `/orders/${a.id}/operations/10/time-logs`, { employeeId: e, machineId: null, startAt: t(-300), endAt: t(-240) })
    if (r.status === 201) { manual = r.json(); break }
  }
  check('manual time answers 201', !!manual, r.text.slice(0, 200))
  if (manual) shape('manual time log', 'TimeLog', manual)

  const scrapReason = reasonOf('Scrap')
  r = await m('POST', `/orders/${a.id}/operations/10/report`, { goodQty: 30, scrapQty: 2, reworkQty: 0, scrapReasonId: scrapReason, reworkReasonId: null, machineId: null, note: null }, { 'Idempotency-Key': `p36-${S}-r1` })
  check('operation report answers 201 (the backflush is in the Warehouse)', r.status === 201 && r.json().isPending === false, `${r.status} ${r.text.slice(0, 300)}`)
  shape('report answer', 'OperationReportAnswer', r.json())
  shape('report answer.report', 'OperationReport', r.json().report)
  member('report status', 'OperationReportStatus', r.json().report.status)
  if (r.json().backflush) shape('report answer.backflush', 'StockDocument', r.json().backflush)
  r = await m('POST', `/orders/${a.id}/operations/10/report`, { goodQty: 10, scrapQty: 0, reworkQty: 0, scrapReasonId: null, reworkReasonId: null, machineId: null, note: null })
  check('second report completes operation 10', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  for (const seq of [20, 30]) {
    r = await m('POST', `/orders/${a.id}/operations/${seq}/report`, { goodQty: 40, scrapQty: 0, reworkQty: 0, scrapReasonId: null, reworkReasonId: null, machineId: null, note: null })
    check(`operation ${seq} reports Good 40`, r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  }
  const reps = (await m('GET', `/orders/${a.id}/operation-reports`)).json()
  shapes('operation-reports', 'OperationReport', reps)
  r = await m('POST', `/orders/${a.id}/operations/30/reopen`)
  check('manual reopen of the last operation answers an operation (or a business refusal)', r.status === 200 ? true : r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  if (r.status === 200) shape('reopened operation', 'OrderOperation', r.json())

  // ── receipt: the lot fields the receipts tab shows
  r = await m('POST', `/orders/${a.id}/receipts`, { lines: [{ outputLineNo: null, quantity: 40, lotNumber: null }], note: null }, { 'Idempotency-Key': `p36-${S}-fr` })
  check('receipt answers 201', r.status === 201, `${r.status} ${r.text.slice(0, 400)}`)
  const fr = r.json()
  shape('receipt document', 'StockDocument', fr)
  const rl = fr.lines.find((l) => l.kind === 'Receipt')
  check('receipt line carries lotNumber, expiryDate, manufactureDate, isQcHold and a null material id', !!rl && !!rl.lotNumber && 'expiryDate' in rl && 'manufactureDate' in rl && rl.isQcHold === true && (rl.orderMaterialId === null || rl.orderMaterialId === undefined) && rl.outputLineNo === null, JSON.stringify(rl))
  shapes('receipt lines', 'StockDocumentLine', fr.lines)
  a = await getOrder(a.id)
  check('the order shows CompletedQty 40 and is Completed (auto-complete)', near(a.completedQty, 40) && a.status === 'Completed', JSON.stringify([a.completedQty, a.status]))

  const pageRes = await m('GET', `/stock-documents?plantId=${plant}&type=Receipt&status=Posted&page=1&pageSize=100`)
  const docPage = pageRes.json()
  check('stock-documents list: envelope items / total / page / pageSize', pageRes.status === 200 && Array.isArray(docPage.items) && ['total', 'page', 'pageSize'].every((k) => k in docPage), pageRes.text.slice(0, 200))
  shapes('stock-documents items', 'StockDocument', docPage.items)
  check('the receipt is in the plant list', docPage.items.some((d) => d.id === fr.id))

  r = await m('POST', `/stock-documents/${fr.id}/qc-release`)
  check('qc-release answers the document with the release time', r.status === 200 && r.json().lines.some((l) => l.kind === 'Receipt' && !!l.qcReleasedAt), `${r.status} ${r.text.slice(0, 300)}`)
  shape('qc-release document', 'StockDocument', r.json())

  // ── reversal (P4-2): the counter-report and the Reversal document the Admin buttons send and show
  {
    const x = await create('TP-BAN01', 10)
    await m('POST', `/orders/${x.id}/release`)
    r = await m('POST', `/orders/${x.id}/operations/10/report`, { goodQty: 5, scrapQty: 0, reworkQty: 0, scrapReasonId: null, reworkReasonId: null, machineId: null, note: null })
    const rep = r.json()?.report
    r = await m('POST', `/orders/${x.id}/operation-reports/${rep?.id}/reverse`, { note: 'contract check' })
    check('reversing a report answers 201', r.status === 201 && r.json().isPending === false, `${r.status} ${r.text.slice(0, 300)}`)
    shape('report reversal answer', 'OperationReportAnswer', r.json())
    shape('report reversal answer.report', 'OperationReport', r.json().report)
    if (r.json().backflush) {
      shape('report reversal answer.backflush', 'StockDocument', r.json().backflush)
      shapes('reversal lines', 'StockDocumentLine', r.json().backflush.lines)
      member('reversal document type', 'StockDocumentType', r.json().backflush.type)
      member('reversal line action', 'ReversalAction', r.json().backflush.lines[0].reversalAction)
    }
    for (const seq of [10, 20, 30]) await m('POST', `/orders/${x.id}/operations/${seq}/report`, { goodQty: 10, scrapQty: 0, reworkQty: 0, scrapReasonId: null, reworkReasonId: null, machineId: null, note: null })
    const frx = (await m('POST', `/orders/${x.id}/receipts`, { lines: [{ outputLineNo: null, quantity: 4, lotNumber: null }], note: null })).json()
    r = await m('POST', `/orders/${x.id}/receipts/${frx.id}/reverse`, { note: 'contract check' })
    check('reversing a receipt answers 201', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
    shape('receipt reversal document', 'StockDocument', r.json())
    shapes('receipt reversal lines', 'StockDocumentLine', r.json().lines)
    check('the reversal line carries its reversal action', r.json().lines.some((l) => l.reversalAction === 'Scrap'), JSON.stringify(r.json().lines[0]))
    const xdocs = (await m('GET', `/orders/${x.id}/stock-documents`)).json()
    check('the reversed receipt reads isReversed in the order document list', xdocs.find((d) => d.id === frx.id)?.isReversed === true)
    shapes('order documents with reversal', 'StockDocument', xdocs)
    const lotLine = xdocs.find((d) => d.id === frx.id)?.lines.find((l) => l.kind === 'Receipt')
    if (lotLine?.lots?.length) shapes('line lots', 'StockLineLot', lotLine.lots)
    const xreps = (await m('GET', `/orders/${x.id}/operation-reports`)).json()
    check('the report list shows the original reversed and the reversal with its link', xreps.some((q) => q.isReversed === true) && xreps.some((q) => q.reversalOfId === rep.id), JSON.stringify(xreps.map((q) => [q.docNo, q.isReversed, q.reversalOfId])))
  }

  // ── close
  let cl = await m('GET', `/orders/${a.id}/close-checklist?confirmConsumeRemaining=false`)
  shape('close-checklist', 'CloseChecklist', cl.json())
  shapes('close conditions', 'CloseCondition', cl.json().conditions)
  cl = await m('GET', `/orders/${a.id}/close-checklist?confirmConsumeRemaining=true`)
  check('checklist with confirmConsumeRemaining=true can close', cl.json().canClose === true, cl.text.slice(0, 400))
  r = await m('POST', `/orders/${a.id}/close`, { confirmConsumeRemaining: true })
  check('close answers the closed order', r.status === 200 && r.json().status === 'Closed', `${r.status} ${r.text.slice(0, 200)}`)
  shape('closed order', 'Order', r.json())
  const hist = await m('GET', `/orders/${a.id}/history`)
  shapes('history', 'OrderHistory', hist.json())

  // ── order B: hold / resume / change quantity / unrelease / cancel and the late flag
  let b = await create('TP-BAN01', 20)
  await m('POST', `/orders/${b.id}/release`)
  r = await m('POST', `/orders/${b.id}/hold`, { reasonCodeId: reasonOf('Hold'), note: 'p36' })
  check('hold answers 200 / OnHold', r.status === 200 && r.json().order.status === 'OnHold', `${r.status} ${r.text.slice(0, 200)}`)
  shape('hold answer', 'OrderActionResult', r.json())
  check('OnHold order reports statusBeforeHold', r.json().order.statusBeforeHold === 'Released', JSON.stringify(r.json().order.statusBeforeHold))
  r = await m('POST', `/orders/${b.id}/resume`)
  check('resume answers 200 / Released', r.status === 200 && r.json().order.status === 'Released', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${b.id}/change-quantity`, { quantity: 24, note: 'p36' })
  check('change-quantity answers 200 with the new quantity', r.status === 200 && near(r.json().order.quantity, 24), `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${b.id}/unrelease`)
  check('unrelease answers 200 / Draft', r.status === 200 && r.json().order.status === 'Draft', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${b.id}/cancel`, { reasonCodeId: null, note: null })
  check('cancel without a reason: 400 MFG_PO_009 (the dialog never sends this, the server still guards)', r.status === 400 && code(r) === 'MFG_PO_009', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${b.id}/cancel`, { reasonCodeId: reasonOf('Cancel'), note: 'p36' })
  check('cancel with the reason: 200 / Cancelled', r.status === 200 && r.json().order.status === 'Cancelled', `${r.status} ${r.text.slice(0, 200)}`)
  const lateList = (await m('GET', `/orders?plantId=${plant}&isLate=true&page=1&pageSize=5`)).json()
  check('isLate=true is accepted by the list', Array.isArray(lateList.items))

  // ── short close: the screen reacts to MFG_PO_012
  let c = await create('TP-BAN01', 40)
  await m('POST', `/orders/${c.id}/release`)
  c = await getOrder(c.id)
  for (const seq of [10, 20, 30]) await m('POST', `/orders/${c.id}/operations/${seq}/report`, { goodQty: 20, scrapQty: 0, reworkQty: 0 })
  await m('POST', `/orders/${c.id}/receipts`, { lines: [{ outputLineNo: null, quantity: 20, lotNumber: null }], note: null })
  r = await m('POST', `/orders/${c.id}/complete`, { reasonCodeId: null, note: null })
  check('complete at 20 of 40 without a reason: 400 MFG_PO_012 (opens the short-close dialog)', r.status === 400 && code(r) === 'MFG_PO_012', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${c.id}/complete`, { reasonCodeId: reasonOf('ShortClose'), note: 'p36' })
  check('complete with the ShortClose reason: 200 / Completed', r.status === 200 && r.json().order.status === 'Completed', `${r.status} ${r.text.slice(0, 200)}`)
  shape('complete answer', 'OrderActionResult', r.json())
  r = await m('POST', `/orders/${c.id}/reopen`)
  check('reopen answers an OrderActionResult', r.status === 200 && r.json().order.status === 'InProgress', `${r.status} ${r.text.slice(0, 200)}`)

  // ── the pending path: the Warehouse stops
  let d = await create('BTP-CHAN01', 100)
  await m('POST', `/orders/${d.id}/release`)
  d = await getOrder(d.id)
  const goLine = mat(d, 'NVL-GO01').lineNo
  r = await m('POST', `/orders/${d.id}/material-issues`, { lines: [{ lineNo: goLine, quantity: 0.1, lotId: null }], reasonCodeId: null, note: null })
  check('chan order takes 0.1 of wood (starts the order)', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  let pendingA, pendingB
  docker('stop', 'erp-warehouse-service')
  try {
    await sleep(2000)
    // an issue / return reads the Warehouse first and is refused while it is down (R227); a plain receipt (no lot, no backflush) is accepted and waits
    r = await m('POST', `/orders/${d.id}/material-issues`, { lines: [{ lineNo: goLine, quantity: 0.1, lotId: null }], reasonCodeId: null, note: null })
    check('an issue with the Warehouse down is refused 400 MFG_INT_001 and nothing is stored', r.status === 400 && code(r) === 'MFG_INT_001', `${r.status} ${r.text.slice(0, 200)}`)
    r = await m('POST', `/orders/${d.id}/receipts`, { lines: [{ outputLineNo: null, quantity: 30, lotNumber: null }], note: null })
    check('a receipt with the Warehouse down answers 202 with a Pending document', r.status === 202 && r.json().status === 'Pending', `${r.status} ${r.text.slice(0, 300)}`)
    pendingA = r.json()
    shape('pending document', 'StockDocument', pendingA)
    check('a Pending document reports attempts and a next attempt', typeof pendingA.attempts === 'number' && 'nextAttemptAt' in pendingA)
    const dd = await getOrder(d.id)
    check('the order reports pendingStockDocuments 1', dd.pendingStockDocuments === 1, `${dd.pendingStockDocuments}`)
    r = await m('POST', `/stock-documents/${pendingA.id}/retry`)
    check('retry while the Warehouse is down answers 202 (still waiting)', r.status === 202 || r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
    r = await m('POST', `/stock-documents/${pendingA.id}/abandon`)
    check('abandon: 200 and the document is Failed', r.status === 200 && r.json().status === 'Failed', `${r.status} ${r.text.slice(0, 300)}`)
    shape('abandoned document', 'StockDocument', r.json())
    r = await m('POST', `/orders/${d.id}/receipts`, { lines: [{ outputLineNo: null, quantity: 20, lotNumber: null }], note: null })
    pendingB = r.json()
    check('a second receipt is Pending too', r.status === 202, `${r.status} ${r.text.slice(0, 200)}`)
    const attention = (await m('GET', `/stock-documents?plantId=${plant}&status=Pending&page=1&pageSize=100`)).json()
    check('the plant list (Pending) shows it', attention.items.some((x) => x.id === pendingB.id))
    const failedList = (await m('GET', `/stock-documents?plantId=${plant}&status=Failed&page=1&pageSize=100`)).json()
    check('the plant list (Failed) shows the abandoned one', failedList.items.some((x) => x.id === pendingA.id))
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  let posted = null
  for (let i = 0; i < 30 && !posted; i++) {
    await sleep(5000)
    const cur = (await m('GET', `/stock-documents/${pendingB.id}`)).json()
    if (cur?.status === 'Posted') posted = cur
  }
  check('after the Warehouse is back the job posts the pending document', !!posted)
  d = await getOrder(d.id)
  check('pendingStockDocuments is back to 0', d.pendingStockDocuments === 0, `${d.pendingStockDocuments}`)

  // ── permissions: a plain account sees 403 on every call the screens make
  const mail = `zz36${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P36' })
  const cl2 = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (cl2.status === 200 && cl2.json().accessToken) {
    const plain = { Authorization: `Bearer ${cl2.json().accessToken}`, ...J }
    for (const [what, method, path, body] of [
      ['orders list', 'GET', `/orders?plantId=${plant}&page=1&pageSize=5`], ['order', 'GET', `/orders/${d.id}`], ['create', 'POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 1, dueDate: iso(due) }],
      ['release', 'POST', `/orders/${d.id}/release`], ['hold', 'POST', `/orders/${d.id}/hold`, { reasonCodeId: null }], ['issue', 'POST', `/orders/${d.id}/material-issues`, { lines: [] }],
      ['return', 'POST', `/orders/${d.id}/material-returns`, { lines: [] }], ['receipt', 'POST', `/orders/${d.id}/receipts`, { lines: [] }], ['start', 'POST', `/orders/${d.id}/operations/10/start`, { employeeId: empIds[0] }],
      ['stock-documents', 'GET', `/stock-documents?plantId=${plant}&page=1&pageSize=5`], ['retry', 'POST', `/stock-documents/${issueDoc.id}/retry`], ['abandon', 'POST', `/stock-documents/${issueDoc.id}/abandon`],
      ['qc-release', 'POST', `/stock-documents/${fr.id}/qc-release`], ['close', 'POST', `/orders/${a.id}/close`, { confirmConsumeRemaining: false }]]) {
      const x = await call(method, `/mfg/api/v1/manufacturing${path}`, plain, body)
      check(`a user without the permission gets 403 on ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', cl2.status, cl2.text.slice(0, 100))

  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  // ── quality (P4-3): plans, the FQC the receipt created, its document and the order's Quality tab (last: it links a plan to the item, so later receipts would be inspected)
  {
    const planCode = 'FQC-' + S
    let r = await m('POST', '/inspection-plans', { plantId: plant, itemId: ids['TP-BAN01'], code: planCode, type: 'Fqc', samplingMethod: 'Fixed', sampleValue: 3, allowedMinorDefects: 1 })
    shape('inspection plan (created)', 'InspectionPlan', r.json())
    member('plan type', 'InspectionType', r.json().type)
    member('plan sampling', 'SamplingMethod', r.json().samplingMethod)
    member('plan status', 'VersionStatus', r.json().status)
    const planId = r.json().id
    for (const c of [{ name: 'Length', kind: 'Measurement', minValue: 748, maxValue: 752, unit: 'mm', isCritical: true }, { name: 'Colour', kind: 'Attribute', isCritical: true }]) r = await m('POST', `/inspection-plans/${planId}/characteristics`, c)
    shape('inspection plan (with characteristics)', 'InspectionPlan', r.json())
    shapes('plan characteristics', 'InspectionCharacteristic', r.json().characteristics)
    member('characteristic kind', 'CharacteristicKind', r.json().characteristics[0].kind)
    await m('POST', `/inspection-plans/${planId}/approve`)
    shapes('inspection plan list', 'InspectionPlanListItem', (await m('GET', `/inspection-plans?plantId=${plant}`)).json())
    const prof = (await m('GET', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`)).json()
    await m('PUT', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`, { ...prof.values, requiresFqc: true, fqcInspectionPlanId: planId })

    const x = await create('TP-BAN01', 5)
    await m('POST', `/orders/${x.id}/release`)
    for (const seq of [10, 20, 30]) await m('POST', `/orders/${x.id}/operations/${seq}/report`, { goodQty: 5, scrapQty: 0, reworkQty: 0, scrapReasonId: null, reworkReasonId: null, machineId: null, note: null })
    const rcp = await m('POST', `/orders/${x.id}/receipts`, { lines: [{ outputLineNo: null, quantity: 5, lotNumber: null }], note: null })
    check('the receipt of the inspected item answers 201', rcp.status === 201, `${rcp.status} ${rcp.text.slice(0, 200)}`)
    const own = (await m('GET', `/orders/${x.id}/inspections`)).json()
    shapes('order inspections', 'InspectionListItem', own)
    check('one FQC was created with the receipt', own.length === 1 && own[0].type === 'Fqc' && own[0].status === 'Pending', JSON.stringify(own))
    member('inspection status', 'InspectionStatus', own[0].status)
    member('inspection type', 'InspectionType', own[0].type)
    const list = (await m('GET', `/inspections?plantId=${plant}&status=Pending&type=Fqc&page=1&pageSize=25`)).json()
    check('inspection list: envelope items / total / page / pageSize', Array.isArray(list.items) && ['total', 'page', 'pageSize'].every((k) => k in list), JSON.stringify(list).slice(0, 200))
    shape('inspection list envelope', 'PagedInspections', list)
    shapes('inspection list items', 'InspectionListItem', list.items)
    const doc = (await m('GET', `/inspections/${own[0].id}`)).json()
    shape('inspection document', 'Inspection', doc)
    shape('inspection document.inspection', 'InspectionListItem', doc.inspection)
    shapes('inspection characteristics', 'InspectionCharacteristic', doc.characteristics)
    check('the sample comes from the plan and needs one value per unit', doc.inspection.sampleQty === 3 && doc.units === 3, JSON.stringify([doc.inspection.sampleQty, doc.units]))
    const length = doc.characteristics.find((c) => c.kind === 'Measurement')
    const colour = doc.characteristics.find((c) => c.kind === 'Attribute')
    const values = [1, 2, 3].map((no) => ({ characteristicId: length.id, sampleNo: no, value: 750 })).concat([{ characteristicId: colour.id, sampleNo: 1, passed: true }])
    r = await m('PUT', `/inspections/${own[0].id}/results`, { sampleQty: 3, results: values.slice(0, 2) })
    check('a draft is saved and the inspection stays Pending', r.status === 200 && r.json().inspection.status === 'Pending' && r.json().results.length === 2, `${r.status} ${r.text.slice(0, 300)}`)
    shapes('draft results', 'InspectionResult', r.json().results)
    r = await m('POST', `/inspections/${own[0].id}/submit`, { sampleQty: 3, results: values })
    check('submit answers 200 and the document is Passed with the release time', r.status === 200 && r.json().inspection.status === 'Passed' && !!r.json().inspection.releasedAt, `${r.status} ${r.text.slice(0, 300)}`)
    shape('submitted inspection', 'Inspection', r.json())
    const again = await m('POST', `/inspections/${own[0].id}/submit`, { sampleQty: 3, results: values })
    check('a submitted inspection cannot be submitted again: MFG_QC_002', again.status === 400 && code(again) === 'MFG_QC_002', `${again.status} ${again.text.slice(0, 200)}`)
    const od = (await m('GET', `/orders/${x.id}`)).json()
    shapes('order operations carry the in-process fields', 'OrderOperation', od.operations)
    check('order operations carry ipqcHeldQty / ipqcPassedQty', od.operations.every((o) => 'ipqcHeldQty' in o && 'ipqcPassedQty' in o), JSON.stringify(od.operations[0]))
    const rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${x.id}`)).json()
    if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p36 cleanup' })
  }

  // ── clean up what open orders still hold in the Warehouse
  for (const o of [c, d]) {
    const rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${o.id}`)).json()
    if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p36 cleanup' })
  }

  console.log(`\nprefix ${prefix} plant ${plantCode} orders ${a.orderNo} ${b.orderNo} ${c.orderNo} ${d.orderNo}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
