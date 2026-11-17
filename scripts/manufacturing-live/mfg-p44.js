// Manufacturing P4-4 live check: non-conformance reports (TC-NCR-001..007), dispositions Rework / Scrap / UseAsIs and rework orders (TC-RW-001..004) with the real Warehouse.
// Stops and starts the erp-warehouse-service container once (failure injection: UseAsIs while the Warehouse is down).
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
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const code = (r) => r.json()?.errorCode

  const prefix = `ZZP44${S}-`
  const plantCode = `ZZ-P44-${S}`
  const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
  const ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
  const plant = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, '')).plantId
  check('DS-01 set seeded', !!plant && !!ids['TP-BAN01'], out.slice(-300))
  const item = ids['TP-BAN01']

  // ── set-up: an approved FQC plan on the finished good ─────────────────────────────────────────────────
  let r = await m('POST', '/inspection-plans', { plantId: plant, itemId: item, code: `FQC-${S}`, type: 'Fqc', samplingMethod: 'Fixed', sampleValue: 5, allowedMinorDefects: 1 })
  const plan = r.json()
  for (const c of [
    { name: 'Length', kind: 'Measurement', minValue: 748, maxValue: 752, unit: 'mm', isCritical: true },
    { name: 'Colour', kind: 'Attribute', isCritical: true },
    { name: 'Scratches', kind: 'Attribute', isCritical: false },
  ]) await m('POST', `/inspection-plans/${plan.id}/characteristics`, c)
  r = await m('POST', `/inspection-plans/${plan.id}/approve`)
  check('the FQC plan is approved', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  const prof = (await m('GET', `/item-profiles/${item}?plantId=${plant}`)).json()
  r = await m('PUT', `/item-profiles/${item}?plantId=${plant}`, { ...prof.values, requiresFqc: true, fqcInspectionPlanId: plan.id })
  check('the item profile points at it', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)

  const plantRow = (await m('GET', `/plants/${plant}`)).json()
  const quarantine = plantRow.quarantineWarehouseId
  await m('POST', '/reason-codes/seed-defaults')
  const scrapReason = (await m('GET', '/reason-codes')).json().find((x) => x.type === 'Scrap' && x.isActive)?.id
  let due = addDays(plantNow(), 10)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const create = async (qty) => (await m('POST', '/orders', { plantId: plant, itemId: item, quantity: qty, dueDate: iso(due), outputWarehouseId: null })).json()
  const release = async (o) => { await m('POST', `/orders/${o.id}/release`); return (await m('GET', `/orders/${o.id}`)).json() }
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const report = (o, seq, good, scrap = 0) => m('POST', `/orders/${o.id}/operations/${seq}/report`, { goodQty: good, scrapQty: scrap, reworkQty: 0, scrapReasonId: scrap > 0 ? scrapReason : null, reworkReasonId: null, machineId: null, note: null })
  const produce = async (o, qty) => { for (const seq of [10, 20, 30]) { const x = await report(o, seq, qty); if (x.status !== 201) return x } return { status: 201 } }
  const receive = (o, qty, lot = null) => m('POST', `/orders/${o.id}/receipts`, { lines: [{ outputLineNo: null, quantity: qty, lotNumber: lot }], note: null })
  const inspections = async (o) => (await m('GET', `/orders/${o.id}/inspections`)).json()
  const lotsOf = async () => (await wh('GET', `/inventory/lots/product/${item}`)).json() ?? []
  const lotFor = async (number) => (await lotsOf()).find((l) => l.lotNumber === number)
  const lotQty = (l) => (l ? Number(l.remainingQuantity ?? l.quantity) : 0)   // a used-up lot may be missing from the list
  const refs = async () => ((await wh('GET', `/movements/${item}?take=500`)).json()?.items ?? []).map((x) => x.reference)
  const op = (o, seq) => o.operations.find((x) => x.operationSeq === seq)
  const valuesOf = (d, { length = 750, failing = [] } = {}) => d.characteristics.flatMap((c) => c.kind === 'Measurement'
    ? Array.from({ length: d.units }, (_, i) => ({ characteristicId: c.id, sampleNo: i + 1, value: length }))
    : [{ characteristicId: c.id, sampleNo: 1, passed: !failing.includes(c.name) }])
  const detail = async (id) => (await m('GET', `/inspections/${id}`)).json()
  const submit = async (id, opts) => { const d = await detail(id); return m('POST', `/inspections/${id}/submit`, { sampleQty: d.inspection.sampleQty, results: valuesOf(d, opts) }) }
  const ncrOf = async (id) => (await m('GET', `/ncrs/${id}`)).json()
  const checklist = async (o) => (await m('GET', `/orders/${o.id}/close-checklist`)).json()

  // ═══ 1. the whole way: a failed lot of 100 -> Rework 10 + Scrap 5 + UseAsIs 85 ════════════════════════════
  console.log('\n── final inspection fails: NCR, dispositions, rework ──')
  let A = await release(await create(100))
  r = await produce(A, 100)
  check('100 pieces produced', r.status === 201, `${r.status} ${r.text?.slice(0, 200)}`)
  const fa = (await receive(A, 100)).json()
  const lotA = fa.lines.find((l) => l.kind === 'Receipt').lotNumber
  const insA = (await inspections(A))[0]
  r = await submit(insA.id, { length: 753 })
  const failedA = r.json()
  check('TC-QC-008 the final inspection fails (753 mm)', r.status === 200 && failedA.inspection.status === 'Failed' && failedA.inspection.ncrRequired === true, `${r.status} ${r.text.slice(0, 300)}`)
  check('the failed inspection answers the NCR it created', !!failedA.inspection.ncrId && /^NCR-\d{4}-\d{5}$/.test(failedA.inspection.ncrNo ?? ''), JSON.stringify(failedA.inspection).slice(0, 400))
  let ncrA = await ncrOf(failedA.inspection.ncrId)
  const n = ncrA.ncr
  check('TC-NCR-001 NCR Open for the whole lot of 100, the right lot and order', n.status === 'Open' && near(n.nonconformingQty, 100) && n.lotNumber === lotA && n.orderId === A.id && n.sourceType === 'Inspection' && !n.isInProcess && !n.isRecordOnly, JSON.stringify(n))
  check('the order lists it', ((await m('GET', `/orders/${A.id}/ncrs`)).json() ?? []).some((x) => x.id === n.id))
  const lotRowA = await lotFor(lotA)
  check('the lot is still on QcHold in the quarantine warehouse', lotRowA?.status === 'QcHold' && lotRowA?.warehouseId === quarantine, JSON.stringify(lotRowA))
  const open = (await checklist(A)).conditions.find((c) => c.code === 'QUALITY_HOLD')
  check('an open NCR keeps the quality-hold condition of the order unmet', open?.met === false, JSON.stringify(open))
  r = await m('POST', `/orders/${A.id}/receipts/${fa.id}/reverse`, { note: 'try after the failure' })
  check('the failed receipt cannot be reversed: 400 MFG_FG_004', r.status === 400 && code(r) === 'MFG_FG_004', `${r.status} ${r.text.slice(0, 200)}`)

  // dispositions
  r = await m('PUT', `/ncrs/${n.id}/dispositions`, { lines: [{ type: 'Rework', qty: 10 }, { type: 'Scrap', qty: 5, reasonCodeId: scrapReason }, { type: 'UseAsIs', qty: 80 }] })
  check('TC-NCR-002 lines that do not add up to 100: 400 MFG_NCR_001', r.status === 400 && code(r) === 'MFG_NCR_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('PUT', `/ncrs/${n.id}/dispositions`, { lines: [{ type: 'Rework', qty: 10 }, { type: 'Scrap', qty: 5 }, { type: 'UseAsIs', qty: 85 }] })
  check('a Scrap line without its reason is refused', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('PUT', `/ncrs/${n.id}/dispositions`, { lines: [{ type: 'Rework', qty: 10 }, { type: 'Scrap', qty: 5, reasonCodeId: scrapReason }, { type: 'UseAsIs', qty: 85, note: 'customer accepts' }] })
  ncrA = r.json()
  check('TC-NCR-003 Rework 10 + Scrap 5 + UseAsIs 85 is accepted (InDisposition, the UseAsIs line approved)', r.status === 200 && ncrA.ncr.status === 'InDisposition' && ncrA.dispositions.length === 3 && !!ncrA.dispositions.find((d) => d.type === 'UseAsIs').approvedBy, `${r.status} ${r.text.slice(0, 400)}`)

  // Scrap 5
  r = await m('POST', `/ncrs/${n.id}/dispositions/2/execute`, {})
  check('TC-NCR-005 the Scrap line is executed', r.status === 200 && r.json().ncr.dispositions.find((d) => d.lineNo === 2).isExecuted === true, `${r.status} ${r.text.slice(0, 300)}`)
  check('the Warehouse booked the scrap under <NCR>#2 and the lot holds 95', (await refs()).filter((x) => x === `${n.ncrNo}#2`).length === 1 && near(lotQty(await lotFor(lotA)), 95), JSON.stringify(await lotFor(lotA)))
  r = await m('POST', `/ncrs/${n.id}/dispositions/2/execute`, {})
  check('a line is executed only once: 400 MFG_NCR_004', r.status === 400 && code(r) === 'MFG_NCR_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('PUT', `/ncrs/${n.id}/dispositions`, { lines: [{ type: 'Scrap', qty: 100, reasonCodeId: scrapReason }] })
  check('the lines cannot be replaced once one has started: 400 MFG_NCR_004', r.status === 400 && code(r) === 'MFG_NCR_004', `${r.status} ${r.text.slice(0, 200)}`)

  // UseAsIs 85 with the Warehouse down: nothing is marked, the second try moves the stock once
  docker('stop', 'erp-warehouse-service')
  try {
    await sleep(2500)
    r = await m('POST', `/ncrs/${n.id}/dispositions/3/execute`, {})
    check('Warehouse down: 400 MFG_INT_001, the line stays unexecuted', r.status === 400 && code(r) === 'MFG_INT_001' && (await ncrOf(n.id)).dispositions.find((d) => d.lineNo === 3).isExecuted === false, `${r.status} ${r.text.slice(0, 200)}`)
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  let up = false
  for (let i = 0; i < 24 && !up; i++) { await sleep(5000); up = (await wh('GET', `/inventory/${item}`)).status === 200 }
  check('the Warehouse is back', up)
  r = await m('POST', `/ncrs/${n.id}/dispositions/3/execute`, {})
  const useAsIs = r.json()?.ncr?.dispositions?.find((d) => d.lineNo === 3)
  check('TC-NCR-006 UseAsIs 85 is executed after the outage', r.status === 200 && useAsIs?.isExecuted === true && !!useAsIs.resultLotNumber, `${r.status} ${r.text.slice(0, 300)}`)
  const refsU = await refs()
  check('exactly one transfer and one release for the line', refsU.filter((x) => x === `${n.ncrNo}#3Q1`).length === 1 && refsU.filter((x) => x === `${n.ncrNo}#3Q2`).length === 1, JSON.stringify(refsU.filter((x) => x.startsWith(n.ncrNo))))
  const subLot = await lotFor(useAsIs?.resultLotNumber)
  check('the 85 are a sub-lot, Available in the finished-goods warehouse', subLot?.status === 'Available' && subLot?.warehouseId === A.outputWarehouseId && near(lotQty(subLot), 85) && subLot?.parentLotId === lotRowA.lotId, JSON.stringify(subLot))
  check('the 10 left of the original lot are still on hold in quarantine', near(lotQty(await lotFor(lotA)), 10) && (await lotFor(lotA)).status === 'QcHold', JSON.stringify(await lotFor(lotA)))

  // Rework 10
  r = await m('POST', `/ncrs/${n.id}/dispositions/1/execute`, {})
  const rw = r.json()
  check('the Rework line creates a rework order', r.status === 200 && !!rw.reworkOrderId && !!rw.reworkOrderNo, `${r.status} ${r.text.slice(0, 300)}`)
  let R = await getOrder(rw.reworkOrderId)
  check('TC-RW-001 a Draft rework order for 10 of the item, source Ncr, parent = the original order, no BOM', R.orderType === 'Rework' && R.status === 'Draft' && near(R.quantity, 10) && R.sourceType === 'Ncr' && R.sourceRefId === n.id && R.parentOrderId === A.id && R.isRework === true && R.bomId == null && R.itemId === item, JSON.stringify({ t: R.orderType, s: R.status, q: R.quantity, st: R.sourceType, b: R.bomId }))
  check('its one material is the held lot, Manual issue, in the quarantine warehouse', R.materials.length === 1 && R.materials[0].sourceLotId === lotRowA.lotId && R.materials[0].sourceLotNumber === lotA && R.materials[0].issueMethod === 'Manual' && R.materials[0].warehouseId === quarantine && near(R.materials[0].requiredQty, 10), JSON.stringify(R.materials))
  r = await m('POST', `/ncrs/${n.id}/dispositions/1/execute`, {})
  check('the rework line is not executed twice: 400 MFG_NCR_004', r.status === 400 && code(r) === 'MFG_NCR_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('PUT', `/orders/${R.id}`, { quantity: 12, dueDate: iso(due), priority: 3 })
  check('a rework order cannot be edited as a draft: 400', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  R = await release(R)
  check('TC-RW-002 release: no shortage, no reservation for the held lot', R.status === 'Released' && near(R.materials[0].reservedQty, 0), JSON.stringify({ s: R.status, rs: R.materials[0].reservedQty }))
  const lineNoR = R.materials[0].lineNo
  r = await m('POST', `/orders/${R.id}/material-issues`, { lines: [{ lineNo: lineNoR, quantity: 10, lotId: '00000000-0000-0000-0000-0000000000aa' }] })
  check('issuing another lot to a rework order: 400 MFG_MAT_005', r.status === 400 && code(r) === 'MFG_MAT_005', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${R.id}/material-issues`, { lines: [{ lineNo: lineNoR, quantity: 10, lotId: null }] })
  check('the held lot is issued to the rework order (201, Posted)', r.status === 201 && (r.json().document ?? r.json()).status === 'Posted', `${r.status} ${r.text.slice(0, 300)}`)
  check('the original lot is used up', near(lotQty(await lotFor(lotA)), 0), JSON.stringify(await lotFor(lotA)))
  r = await receive(R, 10, 'MY-OWN-LOT')
  check('a typed lot number is refused on a rework receipt', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  r = await receive(R, 10)
  const frR = r.json()
  const reworkLot = frR?.document?.lines?.find((l) => l.kind === 'Receipt')?.lotNumber ?? frR?.lines?.find((l) => l.kind === 'Receipt')?.lotNumber
  check('TC-RW-003 the reworked goods come in as <lot>-R1', r.status === 201 && reworkLot === `${lotA}-R1`, `${r.status} ${r.text.slice(0, 300)}`)
  const rLot = await lotFor(reworkLot)
  check('the -R1 lot is on QcHold in quarantine and has its own pending FQC', rLot?.status === 'QcHold' && rLot?.warehouseId === quarantine && (await inspections(R)).some((i) => i.type === 'Fqc' && i.status === 'Pending' && i.lotNumber === reworkLot), JSON.stringify(await inspections(R)).slice(0, 300))
  R = await getOrder(R.id)
  check('the rework order completed itself (all 10 received)', R.status === 'Completed' && near(R.completedQty, 10) && R.isRework === true, `${R.status} ${R.completedQty}`)
  ncrA = await ncrOf(n.id)
  check('TC-NCR-007 every line executed: the NCR is Closed', ncrA.ncr.status === 'Closed' && !!ncrA.ncr.closedAt && ncrA.dispositions.every((d) => d.isExecuted), JSON.stringify(ncrA.ncr.status))
  const cl = (await checklist(A)).conditions.find((c) => c.code === 'QUALITY_HOLD')
  check('the quality-hold condition of the original order is met again', cl?.met === true, JSON.stringify(cl))
  const insR = (await inspections(R)).find((i) => i.status === 'Pending')
  r = await submit(insR.id, {})
  check('the rework lot passes its FQC and is released', r.status === 200 && r.json().inspection.status === 'Passed' && (await lotFor(reworkLot))?.status === 'Available' && (await lotFor(reworkLot))?.warehouseId === R.outputWarehouseId, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${A.id}/close`, { confirmConsumeRemaining: true })
  check('the original order can now be closed', r.status === 200 && r.json().status === 'Closed', `${r.status} ${r.text.slice(0, 300)}`)
  check('TC-RW-004 the rework order is flagged (isRework) for costing', R.isRework === true && R.orderType === 'Rework')
  r = await m('GET', `/inspections/${insA.id}`)
  check('the failed inspection shows its NCR as Closed', r.json().inspection.ncrId === n.id && r.json().inspection.ncrStatus === 'Closed', r.text.slice(0, 300))

  // ═══ 2. a re-inspection meets the NCR ═════════════════════════════════════════════════════════════════
  console.log('\n── re-inspection and NCR ──')
  let B = await release(await create(5))
  await produce(B, 5)
  const fb = (await receive(B, 5)).json()
  const lotB = fb.lines.find((l) => l.kind === 'Receipt').lotNumber
  const insB = (await inspections(B))[0]
  r = await submit(insB.id, { length: 753 })
  const ncrB = r.json().inspection.ncrId
  const reB = (await m('POST', `/inspections/${insB.id}/reinspect`)).json()
  check('TC-QC-013 a re-inspection of a failed inspection whose NCR has no started line: allowed', !!reB.inspection?.id && reB.inspection.status === 'Pending', JSON.stringify(reB).slice(0, 200))
  check('the NCR it replaced is Cancelled', (await ncrOf(ncrB)).ncr.status === 'Cancelled')
  r = await submit(reB.inspection.id, {})
  check('the re-inspection passes and releases the lot', r.status === 200 && (await lotFor(lotB))?.status === 'Available', `${r.status} ${r.text.slice(0, 200)}`)

  let C = await release(await create(5))
  await produce(C, 5)
  const fc = (await receive(C, 5)).json()
  const insC = (await inspections(C))[0]
  r = await submit(insC.id, { length: 753 })
  const ncrC = r.json().inspection.ncrId
  await m('PUT', `/ncrs/${ncrC}/dispositions`, { lines: [{ type: 'Scrap', qty: 2, reasonCodeId: scrapReason }, { type: 'Scrap', qty: 3, reasonCodeId: scrapReason }] })
  await m('POST', `/ncrs/${ncrC}/dispositions/1/execute`, {})
  r = await m('POST', `/inspections/${insC.id}/reinspect`)
  check('a re-inspection after a line started is refused: 400 MFG_NCR_004', r.status === 400 && code(r) === 'MFG_NCR_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/ncrs/${ncrC}/dispositions/2/execute`, {})
  const closedC = r.json()?.ncr?.ncr
  check('executing the last line closes the NCR and settles the held lot', r.status === 200 && closedC?.status === 'Closed' && (await checklist(await getOrder(C.id))).conditions.find((c) => c.code === 'QUALITY_HOLD')?.met === true, `${r.status} ${r.text.slice(0, 200)}`)
  check('the lot was scrapped in the Warehouse (nothing left)', near(lotQty(await lotFor(fc.lines.find((l) => l.kind === 'Receipt').lotNumber)), 0))

  // ═══ 3. in-process: a failed IPQC ═════════════════════════════════════════════════════════════════════
  console.log('\n── in-process inspection fails ──')
  r = await m('POST', '/inspection-plans', { plantId: plant, itemId: item, code: `IPQ-${S}`, type: 'Ipqc', samplingMethod: 'Fixed', sampleValue: 3, allowedMinorDefects: 0 })
  const ipq = r.json()
  await m('POST', `/inspection-plans/${ipq.id}/characteristics`, { name: 'Appearance', kind: 'Attribute', isCritical: true })
  await m('POST', `/inspection-plans/${ipq.id}/approve`)
  const routings = (await m('GET', `/routings?plantId=${plant}&itemId=${item}&status=Approved`)).json()
  r = await m('POST', `/routings/${routings[0].id}/copy`, { effectiveFrom: iso(plantNow()) })
  const rt2 = r.json()
  const op10 = rt2.operations.find((o) => o.operationSeq === 10)
  r = await m('PUT', `/routings/${rt2.id}/operations/10`, { operationSeq: 10, standardOperationId: op10.standardOperationId, name: op10.name, workCenterId: op10.workCenterId, setupTimeMinutes: op10.setupTimeMinutes, runTimeMinutes: op10.runTimeMinutes, runTimeBasis: op10.runTimeBasis, batchSize: op10.batchSize, queueTimeMinutes: op10.queueTimeMinutes, moveTimeMinutes: op10.moveTimeMinutes, isSubcontract: op10.isSubcontract, instructions: op10.instructions, ipqcInspectionPlanId: ipq.id })
  check('operation 10 points at an IPQC plan', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/routings/${rt2.id}/approve`)
  check('the new routing version is approved', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)

  let G = await release(await create(10))
  r = await report(G, 10, 6)
  const reportG = r.json().report
  const insG = (await inspections(G)).find((i) => i.type === 'Ipqc')
  r = await m('POST', `/inspections/${insG.id}/submit`, { sampleQty: insG.sampleQty, results: (await detail(insG.id)).characteristics.map((c) => ({ characteristicId: c.id, sampleNo: 1, passed: false })) })
  const ncrG = r.json()?.inspection?.ncrId
  check('a failed IPQC creates an NCR', r.status === 200 && !!ncrG, `${r.status} ${r.text.slice(0, 300)}`)
  let g = await ncrOf(ncrG)
  check('it is for the held 6 of operation 10, no lot, in process', g.ncr.isInProcess === true && near(g.ncr.nonconformingQty, 6) && g.ncr.operationSeq === 10 && g.ncr.lotId == null, JSON.stringify(g.ncr))
  r = await m('PUT', `/ncrs/${ncrG}/dispositions`, { lines: [{ type: 'Rework', qty: 6 }] })
  check('rework of in-process output is refused: 400 MFG_NCR_003', r.status === 400 && code(r) === 'MFG_NCR_003', `${r.status} ${r.text.slice(0, 200)}`)
  const before = (await refs()).length
  r = await m('PUT', `/ncrs/${ncrG}/dispositions`, { lines: [{ type: 'Scrap', qty: 2, reasonCodeId: scrapReason }, { type: 'UseAsIs', qty: 4 }] })
  check('Scrap 2 + UseAsIs 4 is accepted', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  await m('POST', `/ncrs/${ncrG}/dispositions/1/execute`, {})
  G = await getOrder(G.id)
  check('Scrap: the 2 became scrap on the operation and on the order, 4 stay held', near(op(G, 10).goodQty, 4) && near(op(G, 10).scrapQty, 2) && near(op(G, 10).ipqcHeldQty, 4) && near(G.scrappedQty, 2), JSON.stringify(op(G, 10)))
  r = await m('POST', `/ncrs/${ncrG}/dispositions/2/execute`, {})
  G = await getOrder(G.id)
  check('UseAsIs: the 4 are released, the NCR is Closed and operation 20 is Ready', r.status === 200 && r.json().ncr.ncr.status === 'Closed' && near(op(G, 10).ipqcHeldQty, 0) && near(op(G, 10).ipqcPassedQty, 4) && op(G, 20).status === 'Ready', `${r.status} ${JSON.stringify(op(G, 10))} ${op(G, 20).status}`)
  check('work in process is not in stock: no Warehouse movement for either line', (await refs()).length === before)
  r = await report(G, 20, 5)
  check('operation 20 can take only the 4 that were accepted: 5 refused (MFG_OP_004)', r.status === 400 && code(r) === 'MFG_OP_004', `${r.status} ${r.text.slice(0, 200)}`)
  r = await report(G, 20, 4)
  check('4 are accepted', r.status === 201, `${r.status} ${r.text.slice(0, 200)}`)

  // ═══ 4. an NCR raised by hand from a report (record only) ═════════════════════════════════════════════
  console.log('\n── manual NCR from an operation report ──')
  r = await m('POST', '/ncrs', { orderId: G.id, reportId: reportG.id, quantity: 70, description: 'Too many' })
  check('a quantity above what the report counted is refused', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', '/ncrs', { orderId: G.id, reportId: reportG.id, quantity: 3, description: 'Scratched panels' })
  const man = r.json()
  check('a manual NCR is created, record only', r.status === 201 && man.ncr.isRecordOnly === true && man.ncr.sourceType === 'OperationReport' && near(man.ncr.nonconformingQty, 3) && man.ncr.status === 'Open', `${r.status} ${r.text.slice(0, 300)}`)
  const beforeMan = (await refs()).length
  await m('PUT', `/ncrs/${man.ncr.id}/dispositions`, { lines: [{ type: 'Scrap', qty: 1, reasonCodeId: scrapReason }, { type: 'UseAsIs', qty: 2 }] })
  await m('POST', `/ncrs/${man.ncr.id}/dispositions/1/execute`, {})
  r = await m('POST', `/ncrs/${man.ncr.id}/dispositions/2/execute`, {})
  check('both lines are ticked off, the NCR closes and nothing moves in the Warehouse', r.status === 200 && r.json().ncr.ncr.status === 'Closed' && (await refs()).length === beforeMan, `${r.status} ${r.text.slice(0, 200)}`)

  // ═══ 5. lists and filters ═════════════════════════════════════════════════════════════════════════════
  let list = (await m('GET', `/ncrs?plantId=${plant}&openOnly=true`)).json()
  check('the open list has none of the closed / cancelled reports', list.items.every((x) => x.status === 'Open' || x.status === 'InDisposition'), JSON.stringify(list.items.map((x) => x.status)))
  list = (await m('GET', `/ncrs?plantId=${plant}&status=Closed`)).json()
  check('the status filter finds the closed reports (original, scrapped lot, in-process, manual)', list.total >= 4, `${list.total}`)
  list = (await m('GET', `/ncrs?plantId=${plant}&source=OperationReport`)).json()
  check('the source filter finds the manual one', list.total === 1 && list.items[0].id === man.ncr.id, JSON.stringify(list.items.map((x) => x.ncrNo)))

  // consistency
  const drift = (await wh('GET', '/inventory/reconciliation?take=1000')).json() ?? []
  const mine = drift.filter((x) => Object.values(ids).includes(x.productId) && x.kind !== 'Reserved')
  check('reconciliation: no quantity / lot drift for the DS-01 items', mine.length === 0, JSON.stringify(mine).slice(0, 300))
  for (const o of [A, B, C, G, R]) {
    const rs = (await wh('GET', `/inventory/reservations/source/ProductionOrder/${o.id}`)).json()
    if (rs?.reservationId && rs.status === 'Reserved') await wh('POST', '/inventory/release', { reservationId: rs.reservationId, reason: 'zz p44 cleanup' })
  }

  // permissions
  const mail = `zz44${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P44' })
  const plain = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (plain.status === 200 && plain.json().accessToken) {
    const pa = { Authorization: `Bearer ${plain.json().accessToken}`, ...J }
    for (const [what, method, path] of [['list NCRs', 'GET', `/ncrs?plantId=${plant}`], ['read an NCR', 'GET', `/ncrs/${n.id}`], ['raise an NCR', 'POST', '/ncrs'], ['decide dispositions', 'PUT', `/ncrs/${n.id}/dispositions`], ['execute a line', 'POST', `/ncrs/${n.id}/dispositions/1/execute`]]) {
      const x = await call(method, `/mfg/api/v1/manufacturing${path}`, pa, method === 'GET' ? undefined : {})
      check(`a user without the permission gets 403 when trying to ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', plain.status, plain.text.slice(0, 100))

  console.log(`\nprefix ZZP44${S}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
