// Manufacturing P4-3 live check: inspection plans (BR-QC-01, TC-QC-001 / 002) and FQC / IPQC inspections (TC-QC-003..015, TC-NCR-001 marker) with the real Warehouse.
// Stops and starts the erp-warehouse-service container once (failure injection).
// Through the Gateway. Default host = glcheck; MFG_HOST / MFG_EMAIL / MFG_PASSWORD switch tenant (SYSTEM: admin.erp.local / superadmin@system.local / SuperAdmin@123).
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
const docker = (...args) => execFileSync('docker', args, { encoding: 'utf8' }).trim()
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const plantNow = () => new Date(Date.now() + 7 * 3600e3)
const near = (a, b) => Math.abs(Number(a) - b) < 1e-6
const addDays = (d, n) => new Date(d.getTime() + n * 86400e3)
const iso = (d) => d.toISOString().slice(0, 10)

;(async () => {
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body, extra = {}) => call(method, `/mfg/api/v1/manufacturing${path}`, { ...auth, ...extra }, body)
  const code = (r) => r.json()?.errorCode

  const prefix = `ZZP43${S}-`
  const plantCode = `ZZ-P43-${S}`
  const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
  const ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
  const plant = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, '')).plantId
  check('DS-01 set seeded', !!plant && !!ids['TP-BAN01'], out.slice(-300))

  // ═══ 1. inspection plans: TC-QC-001 / 002 ═════════════════════════════════════════════════════════════
  console.log('\n── inspection plans ──')
  const planCode = `FQC-${S}`
  let r
  r = await m('POST', '/inspection-plans', { plantId: plant, itemId: ids['TP-BAN01'], code: planCode, type: 'Fqc', samplingMethod: 'Fixed', sampleValue: 5, allowedMinorDefects: 1 })
  const v1 = r.json()
  check('a plan series is created as version 1, Draft', r.status === 201 && v1.version === 1 && v1.status === 'Draft' && v1.code === planCode && v1.type === 'Fqc', `${r.status} ${r.text.slice(0, 300)}`)
  r = await m('POST', '/inspection-plans', { plantId: plant, itemId: ids['BTP-MAT01'], code: planCode, type: 'Fqc', samplingMethod: 'Full', allowedMinorDefects: 0 })
  check('the code of a series cannot be used twice in a plant: 400 MFG_GEN_006', r.status === 400 && code(r) === 'MFG_GEN_006', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/inspection-plans/${v1.id}/characteristics`, { name: 'Length', kind: 'Measurement', minValue: 752, maxValue: 748, unit: 'mm', isCritical: true })
  check('TC-QC-001 a measurement with Min above Max is refused', r.status === 400 && /minimum/i.test(r.text), `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/inspection-plans/${v1.id}/approve`)
  check('a plan without characteristics cannot be approved', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  for (const c of [
    { name: 'Length', kind: 'Measurement', minValue: 748, maxValue: 752, unit: 'mm', isCritical: true },
    { name: 'Colour', kind: 'Attribute', isCritical: true },
    { name: 'Scratches', kind: 'Attribute', isCritical: false },
    { name: 'Label', kind: 'Attribute', isCritical: false },
  ]) {
    r = await m('POST', `/inspection-plans/${v1.id}/characteristics`, c)
    if (r.status !== 201) check(`characteristic ${c.name} added`, false, `${r.status} ${r.text.slice(0, 200)}`)
  }
  let cur = (await m('GET', `/inspection-plans/${v1.id}`)).json()
  check('four characteristics numbered 10, 20, 30, 40', cur.characteristics.map((x) => x.seq).join() === '10,20,30,40', JSON.stringify(cur.characteristics.map((x) => x.seq)))
  r = await m('POST', `/inspection-plans/${v1.id}/approve`)
  check('approve: 200, Approved by the signed-in user', r.status === 200 && r.json().status === 'Approved' && !!r.json().approvedBy, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/inspection-plans/${v1.id}/characteristics`, { name: 'Extra', kind: 'Attribute', isCritical: false })
  check('an approved plan cannot be edited: 400 MFG_BOM_002', r.status === 400 && code(r) === 'MFG_BOM_002', `${r.status} ${r.text.slice(0, 200)}`)

  // versions: a later version ends the open one the day before; an overlapping one is refused
  const tomorrow = iso(addDays(new Date(), 1))
  r = await m('POST', `/inspection-plans/${v1.id}/copy`, { effectiveFrom: tomorrow })
  const v2 = r.json()
  check('copy: version 2, Draft, same code and characteristics', r.status === 201 && v2.version === 2 && v2.status === 'Draft' && v2.code === planCode && v2.characteristics.length === 4, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/inspection-plans/${v2.id}/approve`)
  cur = (await m('GET', `/inspection-plans/${v1.id}`)).json()
  check('approving version 2 ends version 1 the day before', r.status === 200 && cur.effectiveTo === iso(new Date()), `${r.status} ${cur.effectiveTo}`)
  r = await m('POST', `/inspection-plans/${v1.id}/copy`, { effectiveFrom: '2020-01-01' })
  const v3 = r.json()
  r = await m('POST', `/inspection-plans/${v3.id}/approve`)
  check('a version that overlaps another approved one is refused: 400 MFG_BOM_003', r.status === 400 && code(r) === 'MFG_BOM_003', `${r.status} ${r.text.slice(0, 200)}`)

  // pointers on the item profile
  const prof = (await m('GET', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`)).json()
  const values = { ...prof.values, requiresFqc: true }
  r = await m('PUT', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`, { ...values, fqcInspectionPlanId: v1.id })
  check('a profile points at the FQC plan of its item', r.status === 200 && r.json().values.fqcInspectionPlanId === v1.id, `${r.status} ${r.text.slice(0, 300)}`)
  const otherItem = (await m('POST', '/inspection-plans', { plantId: plant, itemId: ids['BTP-MAT01'], code: `FQM-${S}`, type: 'Fqc', samplingMethod: 'Full', allowedMinorDefects: 0 })).json()
  r = await m('PUT', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`, { ...values, fqcInspectionPlanId: otherItem.id })
  check('a plan of another item is refused', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  const ipqcPlan = (await m('POST', '/inspection-plans', { plantId: plant, itemId: ids['TP-BAN01'], code: `IPQ-${S}`, type: 'Ipqc', samplingMethod: 'Percent', sampleValue: 10, allowedMinorDefects: 0 })).json()
  r = await m('PUT', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`, { ...values, fqcInspectionPlanId: ipqcPlan.id })
  check('an IPQC plan cannot be the final inspection plan', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)

  // a plan that a profile points at cannot be deleted; a free draft can
  r = await m('PUT', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`, { ...values, fqcInspectionPlanId: v3.id })
  check('the profile can point at a draft version too', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('DELETE', `/inspection-plans/${v3.id}`)
  check('a plan used by a profile cannot be deleted: 400 MFG_GEN_005', r.status === 400 && code(r) === 'MFG_GEN_005', `${r.status} ${r.text.slice(0, 200)}`)
  await m('PUT', `/item-profiles/${ids['TP-BAN01']}?plantId=${plant}`, { ...values, fqcInspectionPlanId: v1.id })
  r = await m('DELETE', `/inspection-plans/${v3.id}`)
  check('a free draft is deleted: 204', r.status === 204, `${r.status} ${r.text.slice(0, 200)}`)

  // lists and filters
  let list = (await m('GET', `/inspection-plans?plantId=${plant}&type=Ipqc`)).json()
  check('list filters by type', list.length === 1 && list[0].code === `IPQ-${S}`, JSON.stringify(list).slice(0, 300))
  list = (await m('GET', `/inspection-plans?plantId=${plant}&itemId=${ids['TP-BAN01']}&status=Approved`)).json()
  check('list filters by item and status (versions 1 and 2)', list.length === 2 && list.every((x) => x.status === 'Approved'), JSON.stringify(list).slice(0, 300))

  // ═══ 2. final inspections (FQC): TC-QC-003..013 ═════════════════════════════════════════════════════════
  console.log('\n── final inspections ──')
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const plantRow = (await m('GET', `/plants/${plant}`)).json()
  const quarantine = plantRow.quarantineWarehouseId
  await m('POST', '/reason-codes/seed-defaults')
  const scrapReason = (await m('GET', '/reason-codes')).json().find((x) => x.type === 'Scrap' && x.isActive)?.id
  let due = addDays(plantNow(), 10)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const create = async (qty) => (await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: qty, dueDate: iso(due), outputWarehouseId: null })).json()
  const release = async (o) => { await m('POST', `/orders/${o.id}/release`); return (await m('GET', `/orders/${o.id}`)).json() }
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const report = (o, seq, good, scrap = 0) => m('POST', `/orders/${o.id}/operations/${seq}/report`, { goodQty: good, scrapQty: scrap, reworkQty: 0, scrapReasonId: scrap > 0 ? scrapReason : null, reworkReasonId: null, machineId: null, note: null })
  const produce = async (o, qty) => { for (const seq of [10, 20, 30]) { const r = await report(o, seq, qty); if (r.status !== 201) return r } return { status: 201 } }
  const receive = (o, qty) => m('POST', `/orders/${o.id}/receipts`, { lines: [{ outputLineNo: null, quantity: qty, lotNumber: null }], note: null })
  const inspections = async (o) => (await m('GET', `/orders/${o.id}/inspections`)).json()
  const lotsOf = async () => (await wh('GET', `/inventory/lots/product/${ids['TP-BAN01']}`)).json() ?? []
  const lotFor = async (number) => (await lotsOf()).find((l) => l.lotNumber === number)
  const refs = async () => ((await wh('GET', `/movements/${ids['TP-BAN01']}?take=300`)).json()?.items ?? []).map((x) => x.reference)
  const op = (o, seq) => o.operations.find((x) => x.operationSeq === seq)
  const valuesOf = (d, { length = 750, failing = [] } = {}) => d.characteristics.flatMap((c) => c.kind === 'Measurement'
    ? Array.from({ length: d.units }, (_, i) => ({ characteristicId: c.id, sampleNo: i + 1, value: length }))
    : [{ characteristicId: c.id, sampleNo: 1, passed: !failing.includes(c.name) }])
  const submit = (id, d, opts, extra = {}) => m('POST', `/inspections/${id}/submit`, { sampleQty: d.inspection.sampleQty, results: valuesOf(d, opts), ...extra })
  const detail = async (id) => (await m('GET', `/inspections/${id}`)).json()

  // A: the whole way - pass releases the lot
  let A = await release(await create(10))
  await produce(A, 10)
  r = await receive(A, 10)
  const fa = r.json()
  const lotA = fa.lines.find((l) => l.kind === 'Receipt').lotNumber
  let insA = (await inspections(A))[0]
  check('a receipt of the item with an approved plan creates one Pending FQC with the plan\'s sample (5 of 10)', r.status === 201 && insA?.type === 'Fqc' && insA.status === 'Pending' && insA.lotNumber === lotA && near(insA.lotQty, 10) && near(insA.sampleQty, 5) && /^QI-\d{4}-\d{5}$/.test(insA.docNo), JSON.stringify(insA))
  check('the lot is on QcHold in the quarantine warehouse', (await lotFor(lotA))?.status === 'QcHold' && (await lotFor(lotA))?.warehouseId === quarantine, JSON.stringify(await lotFor(lotA)))
  r = await m('POST', `/stock-documents/${fa.id}/qc-release`)
  check('the interim release is refused for an inspected line: MFG_QC_004', r.status === 400 && code(r) === 'MFG_QC_004', `${r.status} ${r.text.slice(0, 200)}`)
  let dA = await detail(insA.id)
  r = await submit(insA.id, dA, {}, { sampleQty: 4 })
  check('TC-QC-006 a sample size other than the plan\'s: 400 MFG_QC_001', r.status === 400 && code(r) === 'MFG_QC_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/inspections/${insA.id}/submit`, { sampleQty: 5, results: valuesOf(dA).slice(0, -1) })
  check('TC-QC-011 a missing value: 400 MFG_QC_003, nothing decided', r.status === 400 && code(r) === 'MFG_QC_003' && (await detail(insA.id)).inspection.status === 'Pending', `${r.status} ${r.text.slice(0, 200)}`)
  r = await submit(insA.id, dA)
  const passedA = r.json()
  check('TC-QC-007 all in range and attributes passing: Passed and released', r.status === 200 && passedA.inspection.status === 'Passed' && !!passedA.inspection.releasedAt && passedA.inspection.ncrRequired === false, `${r.status} ${r.text.slice(0, 300)}`)
  const lotRowA = await lotFor(lotA)
  check('the lot is Available in the finished-goods warehouse', lotRowA?.status === 'Available' && lotRowA?.warehouseId === A.outputWarehouseId, JSON.stringify(lotRowA))
  const lineKey = fa.lines.find((l) => l.kind === 'Receipt').movementKey
  const refsA = await refs()
  check('the release used the idempotent keys Q1 and Q2 once each', refsA.filter((x) => x === lineKey + 'Q1').length === 1 && refsA.filter((x) => x === lineKey + 'Q2').length === 1, JSON.stringify(refsA.filter((x) => x.startsWith(lineKey))))
  r = await submit(insA.id, dA)
  check('TC-QC-012 a submitted inspection cannot be changed: 400 MFG_QC_002', r.status === 400 && code(r) === 'MFG_QC_002', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('PUT', `/inspections/${insA.id}/results`, { sampleQty: 5, results: valuesOf(dA) })
  check('a draft cannot be saved after the submit: 400 MFG_QC_002', r.status === 400 && code(r) === 'MFG_QC_002', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${A.id}/receipts/${fa.id}/reverse`, { note: 'try after the inspection' })
  check('a receipt whose inspection has a result cannot be reversed: 400 MFG_FG_004 naming the inspection', r.status === 400 && code(r) === 'MFG_FG_004' && r.text.includes(insA.docNo), `${r.status} ${r.text.slice(0, 300)}`)
  A = await getOrder(A.id)
  const checklist = (await m('GET', `/orders/${A.id}/close-checklist`)).json()
  check('the order is Completed and its quality-hold condition is met after the pass', A.status === 'Completed' && checklist.conditions.find((c) => c.code === 'QUALITY_HOLD')?.met === true, JSON.stringify(checklist.conditions?.map((c) => [c.code, c.met])))

  // B: a critical failure keeps the lot on hold; a re-inspection decides
  let B = await release(await create(10))
  await produce(B, 10)
  const fb = (await receive(B, 10)).json()
  const lotB = fb.lines.find((l) => l.kind === 'Receipt').lotNumber
  const insB = (await inspections(B))[0]
  let dB = await detail(insB.id)
  r = await submit(insB.id, dB, { length: 753 })
  check('TC-QC-008 one value out of range: Failed, NCR required', r.status === 200 && r.json().inspection.status === 'Failed' && r.json().inspection.ncrRequired === true, `${r.status} ${r.text.slice(0, 300)}`)
  check('TC-NCR-001 (marker) the failed lot stays on QcHold in the quarantine warehouse', (await lotFor(lotB))?.status === 'QcHold' && (await lotFor(lotB))?.warehouseId === quarantine, JSON.stringify(await lotFor(lotB)))
  r = await m('POST', `/stock-documents/${fb.id}/qc-release`)
  check('the interim release is refused: MFG_QC_004', r.status === 400 && code(r) === 'MFG_QC_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${B.id}/receipts/${fb.id}/reverse`, { note: 'try' })
  check('the failed receipt cannot be reversed: 400 MFG_FG_004', r.status === 400 && code(r) === 'MFG_FG_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/inspections/${insB.id}/reinspect`)
  const reB = r.json()
  check('TC-QC-013 re-inspection: a new Pending document pointing at the failed one', r.status === 201 && reB.inspection.status === 'Pending' && reB.inspection.reinspectionOfId === insB.id && reB.inspection.docNo !== insB.docNo, `${r.status} ${r.text.slice(0, 300)}`)
  r = await m('POST', `/inspections/${insB.id}/reinspect`)
  check('a failed inspection is re-inspected only once: 400', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  r = await submit(reB.inspection.id, reB)
  check('the re-inspection passes and releases the lot', r.status === 200 && r.json().inspection.status === 'Passed' && (await lotFor(lotB))?.status === 'Available', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/inspections/${reB.inspection.id}/reinspect`)
  check('a passed inspection cannot be re-inspected: 400 MFG_QC_002', r.status === 400 && code(r) === 'MFG_QC_002', `${r.status} ${r.text.slice(0, 200)}`)
  check('the order lists both inspections', (await inspections(B)).length === 2)

  // C / D: minor defects against the tolerance of 1
  for (const [tag, failing, want, tc] of [['C', ['Scratches'], 'Passed', 'TC-QC-009 one minor defect within the tolerance'], ['D', ['Scratches', 'Label'], 'Failed', 'TC-QC-010 two minor defects beyond the tolerance']]) {
    let X = await release(await create(10))
    await produce(X, 10)
    await receive(X, 10)
    const ix = (await inspections(X))[0]
    r = await submit(ix.id, await detail(ix.id), { failing })
    check(`${tc}: ${want}`, r.status === 200 && r.json().inspection.status === want, `${r.status} ${r.text.slice(0, 300)}`)
  }

  // E: a Pending inspection goes with a reversed receipt
  let E = await release(await create(10))
  await produce(E, 10)
  const fe = (await receive(E, 10)).json()
  r = await m('POST', `/orders/${E.id}/receipts/${fe.id}/reverse`, { note: 'wrong receipt' })
  const insE = (await inspections(E))[0]
  check('a receipt with a Pending inspection can be reversed and the inspection is cancelled', r.status === 201 && insE?.status === 'Cancelled', `${r.status} ${JSON.stringify(insE)}`)

  // F: failure injection - the Warehouse stops while a passing inspection is submitted
  let F = await release(await create(10))
  await produce(F, 10)
  const ff = (await receive(F, 10)).json()
  const lotF = ff.lines.find((l) => l.kind === 'Receipt').lotNumber
  const keyF = ff.lines.find((l) => l.kind === 'Receipt').movementKey
  const insF = (await inspections(F))[0]
  const dF = await detail(insF.id)
  docker('stop', 'erp-warehouse-service')
  try {
    await sleep(2500)
    r = await submit(insF.id, dF)
    check('Warehouse down at submit: 400 MFG_INT_001, nothing decided', r.status === 400 && code(r) === 'MFG_INT_001' && (await detail(insF.id)).inspection.status === 'Pending', `${r.status} ${r.text.slice(0, 200)}`)
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  let up = false
  for (let i = 0; i < 24 && !up; i++) { await sleep(5000); up = (await wh('GET', `/inventory/${ids['TP-BAN01']}`)).status === 200 }
  check('the Warehouse is back', up)
  r = await submit(insF.id, dF)
  check('the second submit passes and releases the lot', r.status === 200 && r.json().inspection.status === 'Passed' && (await lotFor(lotF))?.status === 'Available', `${r.status} ${r.text.slice(0, 200)}`)
  check('exactly one QcRelease movement for the line key', (await refs()).filter((x) => x === keyF + 'Q2').length === 1)

  // ═══ 3. in-process inspections (IPQC): TC-QC-014 / 015 ═══════════════════════════════════════════════
  console.log('\n── in-process inspections ──')
  r = await m('POST', '/inspection-plans', { plantId: plant, itemId: ids['TP-BAN01'], code: `IPQ2-${S}`, type: 'Ipqc', samplingMethod: 'Fixed', sampleValue: 3, allowedMinorDefects: 0 })
  const ipq = r.json()
  await m('POST', `/inspection-plans/${ipq.id}/characteristics`, { name: 'Appearance', kind: 'Attribute', isCritical: true })
  await m('POST', `/inspection-plans/${ipq.id}/approve`)
  const routings = (await m('GET', `/routings?plantId=${plant}&itemId=${ids['TP-BAN01']}&status=Approved`)).json()
  r = await m('POST', `/routings/${routings[0].id}/copy`, { effectiveFrom: iso(plantNow()) })
  const rt2 = r.json()
  const op10 = rt2.operations.find((o) => o.operationSeq === 10)
  r = await m('PUT', `/routings/${rt2.id}/operations/10`, { operationSeq: 10, standardOperationId: op10.standardOperationId, name: op10.name, workCenterId: op10.workCenterId, setupTimeMinutes: op10.setupTimeMinutes, runTimeMinutes: op10.runTimeMinutes, runTimeBasis: op10.runTimeBasis, batchSize: op10.batchSize, queueTimeMinutes: op10.queueTimeMinutes, moveTimeMinutes: op10.moveTimeMinutes, isSubcontract: op10.isSubcontract, instructions: op10.instructions, ipqcInspectionPlanId: ipq.id })
  check('an operation of a routing points at an IPQC plan', r.status === 200 && r.json().operations.find((o) => o.operationSeq === 10).ipqcInspectionPlanId === ipq.id, `${r.status} ${r.text.slice(0, 300)}`)
  r = await m('POST', `/routings/${rt2.id}/approve`)
  check('the new routing version is approved', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)

  let G = await release(await create(10))
  check('the order snapshot carries the plan of operation 10', op(G, 10).ipqcInspectionPlanId === ipq.id && op(G, 20).ipqcInspectionPlanId == null, JSON.stringify(G.operations.map((o) => [o.operationSeq, o.ipqcInspectionPlanId])))
  r = await report(G, 10, 6)
  let insG = await inspections(G)
  G = await getOrder(G.id)
  check('TC-QC-015 a Good report creates one Pending IPQC with the Good quantity; the output is held', r.status === 201 && insG.length === 1 && insG[0].type === 'Ipqc' && insG[0].status === 'Pending' && near(insG[0].lotQty, 6) && insG[0].operationSeq === 10 && near(op(G, 10).ipqcHeldQty, 6), `${r.status} ${JSON.stringify(insG)} ${JSON.stringify(op(G, 10))}`)
  check('operation 20 has no input yet (still Pending)', op(G, 20).status === 'Pending')
  r = await report(G, 10, 4)
  insG = (await inspections(G)).sort((x, y) => x.createdAt.localeCompare(y.createdAt))
  const [first, second] = insG
  r = await submit(first.id, await detail(first.id))
  check('the first inspection (6) passes', r.status === 200 && r.json().inspection.status === 'Passed', `${r.status} ${r.text.slice(0, 200)}`)
  G = await getOrder(G.id)
  check('operation 20 is Ready and 4 stay held (Good 10, passed 6)', op(G, 20).status === 'Ready' && near(op(G, 10).ipqcHeldQty, 4) && near(op(G, 10).ipqcPassedQty, 6), JSON.stringify(op(G, 10)))
  r = await report(G, 20, 7)
  check('TC-QC-014 operation 20 can take only what passed: 7 refused with MFG_OP_004', r.status === 400 && code(r) === 'MFG_OP_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await report(G, 20, 6)
  check('6 are accepted', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)
  r = await submit(second.id, await detail(second.id), { failing: ['Appearance'] })
  check('the second inspection fails: NCR required, the 4 stay held', r.status === 200 && r.json().inspection.status === 'Failed' && r.json().inspection.ncrRequired === true && near(op(await getOrder(G.id), 10).ipqcHeldQty, 4))
  r = await report(G, 20, 1)
  check('the held quantity is still not input: 1 more refused (MFG_OP_004)', r.status === 400 && code(r) === 'MFG_OP_004', `${r.status} ${r.text.slice(0, 200)}`)
  const reG = (await m('POST', `/inspections/${second.id}/reinspect`)).json()
  r = await submit(reG.inspection.id, reG)
  check('the re-inspection passes: the hold is lifted and 4 more can be taken', r.status === 200 && near(op(await getOrder(G.id), 10).ipqcHeldQty, 0), `${r.status} ${r.text.slice(0, 200)}`)
  r = await report(G, 20, 4)
  check('operation 20 takes the 4', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)

  // reversals of reports with an inspection
  let H = await release(await create(10))
  const h1 = (await report(H, 10, 3)).json().report
  let insH = (await inspections(H))[0]
  r = await m('POST', `/orders/${H.id}/operation-reports/${h1.id}/reverse`, { note: 'wrong count' })
  insH = (await inspections(H))[0]
  H = await getOrder(H.id)
  check('reversing a report whose IPQC is Pending: 201, the inspection is cancelled and the hold lifted', r.status === 201 && insH.status === 'Cancelled' && near(op(H, 10).ipqcHeldQty, 0) && near(op(H, 10).goodQty, 0), `${r.status} ${JSON.stringify(insH)} ${JSON.stringify(op(H, 10))}`)
  const h2 = (await report(H, 10, 3)).json().report
  const insH2 = (await inspections(H)).find((i) => i.status === 'Pending')
  await submit(insH2.id, await detail(insH2.id))
  r = await m('POST', `/orders/${H.id}/operation-reports/${h2.id}/reverse`, { note: 'try' })
  check('reversing a report whose IPQC has a result: 400 MFG_REV_003', r.status === 400 && code(r) === 'MFG_REV_003', `${r.status} ${r.text.slice(0, 200)}`)

  // consistency
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  const mine = drift.filter((x) => Object.values(ids).includes(x.productId) && x.kind !== 'Reserved')
  check('reconciliation: no quantity / lot drift for the DS-01 items', mine.length === 0, JSON.stringify(mine).slice(0, 300))
  for (const o of [A, B, E, F, G, H]) {
    const rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${o.id}`)).json()
    if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p43 cleanup' })
  }

  // permissions
  const mail = `zz43${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P43' })
  const plain = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (plain.status === 200 && plain.json().accessToken) {
    const pa = { Authorization: `Bearer ${plain.json().accessToken}`, ...J }
    for (const [what, method, path] of [['view plans', 'GET', `/inspection-plans?plantId=${plant}`], ['create a plan', 'POST', '/inspection-plans'], ['approve a plan', 'POST', `/inspection-plans/${ipqcPlan.id}/approve`]]) {
      const x = await call(method, `/mfg/api/v1/manufacturing${path}`, pa, method === 'POST' ? {} : undefined)
      check(`a user without the permission gets 403 when trying to ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', plain.status, plain.text.slice(0, 100))

  console.log(`\nprefix ZZP43${S}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
