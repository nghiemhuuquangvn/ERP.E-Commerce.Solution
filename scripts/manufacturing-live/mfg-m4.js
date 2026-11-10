// M4 live check on glcheck (throw-away data: plant ZZ-MFG-M4*, products ZZ-M4-*-<suffix>, DS-01 shifts / calendar / work centers, routings, CSV imports).
const http = require('http')
const host = process.env.MFG_HOST || 'glcheck.erp.local'
const call = (method, path, headers = {}, body, raw) =>
  new Promise((resolve, reject) => {
    const req = http.request({ host: '127.0.0.1', port: 5000, method, path, headers: { ...headers, Host: host } }, (res) => {
      let d = ''
      res.on('data', (c) => (d += c))
      res.on('end', () => resolve({ status: res.statusCode, text: d, json: () => { try { return JSON.parse(d) } catch { return null } } }))
    })
    req.on('error', reject)
    if (body !== undefined) req.write(raw ? body : JSON.stringify(body))
    req.end()
  })
const J = { 'Content-Type': 'application/json' }
let failed = 0
const check = (name, ok, extra = '') => { if (!ok) failed++; console.log(`${ok ? 'OK  ' : 'FAIL'} ${name} ${ok ? '' : extra}`) }
const code = (r) => r.json()?.errorCode

;(async () => {
  const login = (await call('POST', '/api/v1/auth/login', J, { email: process.env.MFG_EMAIL || 'admin@glcheck.test', password: process.env.MFG_PASSWORD || 'Gl-Check-Passw0rd!', userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const csvAuth = { Authorization: `Bearer ${login.accessToken}`, 'Content-Type': 'text/csv' }
  const m = (method, path, body) => call(method, `/mfg/api/v1/manufacturing${path}`, auth, body)
  const mcsv = (path, text) => call('POST', `/mfg/api/v1/manufacturing${path}`, csvAuth, text, true)
  const S = Date.now().toString(36).toUpperCase().slice(-4)

  const units = {}
  for (const u of (await call('GET', '/mst/api/v1/masterdata/units-of-measure?pageSize=100', auth)).json().items) units[u.code] = u.id

  const whs = (await call('GET', '/wh/api/v1/warehouses', auth)).json()
  const w = (Array.isArray(whs) ? whs : whs.items).filter((x) => /^ZZ-MFG-WH/.test(x.code)).slice(0, 3)
  let r = await m('POST', '/plants', { code: `ZZ-MFG-M4${S}`, name: 'ZZ M4 plant', rawMaterialWarehouseId: w[0].id, finishedGoodsWarehouseId: w[1].id, quarantineWarehouseId: w[2].id })
  check('plant created', r.status === 201, r.text)
  const P = r.json().id

  // ── DS-01 calendar and work centers
  const mk = async (c, s, e, b) => (await m('POST', '/shifts', { plantId: P, code: c, name: c, startTime: s, endTime: e, breakMinutes: b })).json()
  const ca1 = await mk('CA1', '06:00:00', '14:00:00', 30)
  const ca2 = await mk('CA2', '14:00:00', '22:00:00', 30)
  const cal = (await m('POST', '/calendars', { plantId: P, code: 'CAL-5D', name: 'Five-day week' })).json()
  for (const d of ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']) await m('PUT', `/calendars/${cal.id}/days/${d}`, { shiftIds: [ca1.id, ca2.id] })
  await m('PUT', `/calendars/${cal.id}/days/Saturday`, { shiftIds: [ca1.id] })
  const wcv = (over = {}) => ({ name: 'WC', calendarId: cal.id, type: 'Machine', numberOfResources: 1, efficiencyPct: 100, utilizationPct: 100, laborRatePerHour: 50000, machineRatePerHour: 20000, queueTimeMinutes: 0, moveTimeMinutes: 0, ...over })
  const wc = {}
  for (const [c, over] of [['WC-ASM', { type: 'Labor', numberOfResources: 3 }], ['WC-PNT', { efficiencyPct: 80 }], ['WC-PKG', { type: 'Labor', numberOfResources: 2 }], ['WC-OUT', { type: 'Subcontract', vendorId: '11111111-2222-3333-4444-555555555555' }]]) {
    r = await m('POST', '/work-centers', { plantId: P, code: c, values: wcv(over) })
    wc[c] = r.json()
    if (r.status !== 201) console.log('work center', c, r.status, r.text.slice(0, 160))
  }
  check('4 work centers created', Object.values(wc).every((x) => x?.id))
  r = await m('POST', '/standard-operations', { plantId: P, code: 'STD-PNT', body: { name: 'Painting (standard)', defaultWorkCenterId: wc['WC-PNT'].id, setupTimeMinutes: 30, runTimeMinutes: 6, runTimeBasis: 'PerUnit', instructions: 'Two coats' } })
  check('standard operation created', r.status === 201, r.text.slice(0, 200))
  const std = r.json()

  // ── products
  const make = async (key, uom, kind) => {
    const sku = `ZZ-M4-${key}-${S}`
    const created = (await call('POST', '/prd/api/v1/products', auth, { name: `ZZ M4 ${key}`, sku, productType: 1, baseUomId: units[uom] })).json()
    await call('POST', `/prd/api/v1/products/${created.productId}/variants`, auth, { sku, cost: 1 })
    await call('POST', `/prd/api/v1/products/${created.productId}/publish`, auth)
    await m('PUT', `/item-profiles/${created.productId}?plantId=${P}`, { makeOrBuy: kind, lotSizingPolicy: 'LotForLot', standardLotSize: 1, fixedLeadTimeDays: 1, purchaseLeadTimeDays: 1, safetyStockQty: 0, requiresFqc: false, shelfLifeDays: 0 })
    return { id: created.productId, sku, key }
  }
  const it = {}
  for (const [key, uom, kind] of [['TOP', 'PCS', 'Make'], ['MAT', 'PCS', 'Make'], ['VIT', 'PCS', 'Buy'], ['SON', 'L', 'Buy'], ['KEO', 'KG', 'Buy']]) it[key] = await make(key, uom, kind)
  check('5 products ready', Object.keys(it).length === 5)

  // ── routing v1 (DS-01 TP-BAN01)
  const newRt = (key, from = '2026-01-01') => m('POST', '/routings', { plantId: P, itemId: it[key].id, effectiveFrom: from })
  const op = (rtId, body) => m('POST', `/routings/${rtId}/operations`, body)
  const rt1 = (await newRt('TOP')).json()
  check('routing v1 is a Draft', rt1.version === 1 && rt1.status === 'Draft')
  r = await op(rt1.id, { operationSeq: 10, name: 'Assembly', workCenterId: wc['WC-ASM'].id, setupTimeMinutes: 15, runTimeMinutes: 10, runTimeBasis: 'PerUnit' })
  check('operation 10 added', r.status === 201, r.text.slice(0, 200))
  r = await op(rt1.id, { operationSeq: 10, name: 'Again', workCenterId: wc['WC-ASM'].id, setupTimeMinutes: 0, runTimeMinutes: 1 })
  check('duplicate operation number refused MFG_RT_001', r.status === 400 && code(r) === 'MFG_RT_001', r.text.slice(0, 200))
  r = await op(rt1.id, { operationSeq: 15, name: 'Bad', workCenterId: wc['WC-ASM'].id, setupTimeMinutes: 0, runTimeMinutes: 0 })
  check('run time 0 refused MFG_RT_002', r.status === 400 && code(r) === 'MFG_RT_002', r.text.slice(0, 200))
  r = await op(rt1.id, { operationSeq: 15, name: 'Bad', workCenterId: wc['WC-ASM'].id, setupTimeMinutes: 0, runTimeMinutes: 5, runTimeBasis: 'PerBatch' })
  check('batch operation without a batch size refused MFG_RT_002', r.status === 400 && code(r) === 'MFG_RT_002', r.text.slice(0, 200))
  r = await op(rt1.id, { operationSeq: 15, name: 'Outside', workCenterId: wc['WC-ASM'].id, setupTimeMinutes: 0, runTimeMinutes: 5, isSubcontract: true })
  check('subcontracted operation on a normal center refused MFG_RT_004', r.status === 400 && code(r) === 'MFG_RT_004', r.text.slice(0, 200))
  r = await op(rt1.id, { operationSeq: 15, name: 'Outside', workCenterId: wc['WC-OUT'].id, setupTimeMinutes: 0, runTimeMinutes: 5, isSubcontract: true })
  check('subcontracted operation on a Subcontract center accepted', r.status === 201, r.text.slice(0, 200))
  await m('DELETE', `/routings/${rt1.id}/operations/15`)
  r = await op(rt1.id, { operationSeq: 15, name: 'Nowhere', workCenterId: '11111111-1111-1111-1111-111111111111', setupTimeMinutes: 0, runTimeMinutes: 5 })
  check('unknown work center refused MFG_RT_003', r.status === 400 && code(r) === 'MFG_RT_003', r.text.slice(0, 200))
  r = await m('POST', `/routings/${rt1.id}/operations/from-standard/${std.id}`, { operationSeq: 20 })
  check('operation 20 copied from the standard operation (30 + 6, two coats)', r.status === 201 && r.json().operations.find((o) => o.operationSeq === 20)?.setupTimeMinutes === 30 && r.json().operations.find((o) => o.operationSeq === 20).instructions === 'Two coats' && r.json().operations.find((o) => o.operationSeq === 20).workCenterCode === 'WC-PNT', r.text.slice(0, 300))
  r = await op(rt1.id, { operationSeq: 30, name: 'Packing', workCenterId: wc['WC-PKG'].id, setupTimeMinutes: 0, runTimeMinutes: 2 })
  check('3 operations', r.json().operations.length === 3)

  // lead time of the draft (DS-01: 1015 + 788 + 200 = 2003, start Wednesday 11 November)
  r = await m('GET', `/routings/${rt1.id}/lead-time?quantity=100&endDate=2026-11-13`)
  const lt = r.json()
  check('lead time x100 = 2003 calendar minutes, start 2026-11-11', r.status === 200 && lt.totalMinutes === 2003 && lt.startDate === '2026-11-11', r.text.slice(0, 300))
  const ops = Object.fromEntries((lt?.operations ?? []).map((o) => [o.operationSeq, o]))
  check('operation 20: standard 630, calendar 788; operation 10 standard 1015', ops[20]?.standardMinutes === 630 && ops[20]?.calendarMinutes === 788 && ops[10]?.standardMinutes === 1015, JSON.stringify(ops).slice(0, 300))
  check('operation 30 on Friday 13th from minute 700; operation 10 begins Wednesday 11th', ops[30]?.start?.date === '2026-11-13' && ops[30]?.start?.workingMinute === 700 && ops[10]?.start?.date === '2026-11-11', JSON.stringify([ops[30]?.start, ops[10]?.start]))
  r = await m('GET', `/routings/${rt1.id}/lead-time?quantity=0&endDate=2026-11-13`)
  check('quantity 0 refused', r.status === 400, r.text.slice(0, 160))

  // renumber (a draft copy)
  const scratch = (await m('POST', `/routings/${rt1.id}/copy`, { effectiveFrom: '2027-06-01' })).json()
  r = await m('POST', `/routings/${scratch.id}/renumber`, { orderedSeqs: [30, 10, 20] })
  check('renumber: Packing 10, Assembly 20, Painting 30', r.status === 200 && r.json().operations.map((o) => o.name).join('|') === 'Packing|Assembly|Painting (standard)', r.text.slice(0, 300))
  r = await m('POST', `/routings/${scratch.id}/renumber`, { orderedSeqs: [10, 20] })
  check('renumber that misses an operation refused', r.status === 400, r.text.slice(0, 160))
  await m('DELETE', `/routings/${scratch.id}`)

  // approve + versions
  r = await m('POST', `/routings/${rt1.id}/approve`)
  check('routing v1 approved', r.status === 200 && r.json().status === 'Approved' && r.json().approvedBy, r.text.slice(0, 200))
  r = await op(rt1.id, { operationSeq: 40, name: 'More', workCenterId: wc['WC-PKG'].id, setupTimeMinutes: 0, runTimeMinutes: 1 })
  check('approved routing refuses edits', r.status === 400 && code(r) === 'MFG_BOM_002', r.text.slice(0, 160))
  const rt2 = (await m('POST', `/routings/${rt1.id}/copy`, { effectiveFrom: '2026-12-01' })).json()
  check('copy is v3 Draft with 3 operations (v2 was a deleted draft; its number is not reused)', rt2.version === 3 && rt2.status === 'Draft' && rt2.operations.length === 3)
  r = await m('POST', `/routings/${rt2.id}/approve`)
  check('v3 approved', r.status === 200, r.text.slice(0, 200))
  r = await m('GET', `/routings/${rt1.id}`)
  check('v1 closed on 2026-11-30', r.json().effectiveTo === '2026-11-30')
  r = await m('GET', `/routings/effective?plantId=${P}&itemId=${it.TOP.id}&date=2026-12-01`)
  check('effective on 2026-12-01 is the approved copy', r.json()?.version === 3)
  r = await m('GET', `/routings/effective?plantId=${P}&itemId=${it.MAT.id}&date=2026-12-01`)
  check('an item without a routing answers 404', r.status === 404, `${r.status}`)
  const rt3 = (await m('POST', `/routings/${rt1.id}/copy`, { effectiveFrom: '2026-06-01' })).json()
  r = await m('POST', `/routings/${rt3.id}/approve`)
  check('version inside the closed period refused MFG_BOM_003', r.status === 400 && code(r) === 'MFG_BOM_003', r.text.slice(0, 200))
  await m('DELETE', `/routings/${rt3.id}`)

  // used work center cannot be deleted
  r = await m('DELETE', `/work-centers/${wc['WC-ASM'].id}`)
  check('a work center used by a routing cannot be deleted (MFG_GEN_005)', r.status === 400 && code(r) === 'MFG_GEN_005', r.text.slice(0, 200))

  // ── BOM / routing check (BR-RT-08)
  const line = (key, qty, uom, extra = {}) => ({ lineType: 'Component', componentItemId: it[key].id, quantity: qty, uomId: units[uom], scrapPct: 0, isFixedQuantity: false, isPhantom: false, issueMethod: 'Manual', ...extra })
  const bom = (await m('POST', '/boms', { plantId: P, itemId: it.TOP.id, effectiveFrom: '2026-01-01' })).json()
  await m('POST', `/boms/${bom.id}/lines`, line('VIT', 16, 'PCS', { operationSeq: 10 }))
  await m('POST', `/boms/${bom.id}/lines`, line('SON', 0.5, 'L', { operationSeq: 40 }))
  r = await m('POST', `/boms/${bom.id}/approve`)
  check('BOM with operation 40 (routing has 10-30) approves WITH a warning', r.status === 200 && r.json().status === 'Approved' && r.json().warnings?.length === 1 && /Operation 40/.test(r.json().warnings[0]), r.text.slice(0, 400))
  const bomNoRt = (await m('POST', '/boms', { plantId: P, itemId: it.MAT.id, effectiveFrom: '2026-01-01' })).json()
  await m('POST', `/boms/${bomNoRt.id}/lines`, line('KEO', 1, 'KG', { operationSeq: 99 }))
  r = await m('POST', `/boms/${bomNoRt.id}/approve`)
  check('BOM of an item with no routing: no warning', r.status === 200 && r.json().warnings?.length === 0, r.text.slice(0, 300))

  // ── CSV import: BOM
  const bomHeader = 'componentSku,quantity,uom,scrapPct,operationSeq,issueMethod,lineType,isPhantom,isFixedQuantity,alternativeGroup,alternativePriority,costAllocationPct,notes'
  const q = `plantId=${P}&itemId=${it.TOP.id}`
  const badFile = [bomHeader, `${it.VIT.sku},16,PCS,0,10,Backflush,,,,,,,`, `${it.SON.sku},0.5,L,5,20,,,,,,,,`, `NO-SUCH-ITEM,1,PCS,0,30,,,,,,,,`, `${it.KEO.sku},0.1,KG,0,10,,,,,,,,`, `${it.SON.sku},2,PCS,0,20,,,,,,,,`].join('\n')
  const before = (await m('GET', `/boms?plantId=${P}&itemId=${it.TOP.id}`)).json().length
  r = await mcsv(`/boms/import?${q}`, badFile)
  check('bad BOM file: 200 with errors on lines 4 and 6 only', r.status === 200 && r.json().createdId === null && r.json().errors.map((e) => e.line).join() === '4,6', r.text.slice(0, 400))
  const after = (await m('GET', `/boms?plantId=${P}&itemId=${it.TOP.id}`)).json().length
  check('bad file wrote nothing', after === before)
  const goodFile = [bomHeader, `${it.VIT.sku},16,PCS,0,10,Backflush,,,,,,,`, `${it.SON.sku},0.5,L,5,20,,,,,,,,Two coats`, `${it.KEO.sku};0,1;KG`.replace(/;/g, ',').replace('0,1', '0.1') + ',0,10,,,,,,,,'].join('\n')
  r = await mcsv(`/boms/import?${q}&dryRun=true`, goodFile)
  check('dry run: clean, nothing created', r.status === 200 && r.json().errors.length === 0 && r.json().createdId === null && r.json().dryRun === true && r.json().rowCount === 3, r.text.slice(0, 300))
  r = await mcsv(`/boms/import?${q}&effectiveFrom=2027-01-01`, goodFile)
  check('good BOM file creates the next Draft version with 3 lines', r.status === 200 && r.json().createdId && r.json().version === 2 && r.json().errors.length === 0, r.text.slice(0, 300))
  const imported = (await m('GET', `/boms/${r.json().createdId}`)).json()
  check('imported BOM is a Draft with the component names and Backflush', imported.status === 'Draft' && imported.lines.length === 3 && imported.lines[0].issueMethod === 'Backflush' && imported.lines[1].notes === 'Two coats', JSON.stringify(imported).slice(0, 300))
  r = await mcsv(`/boms/import?${q}`, 'componentSku,quantity\nX,1')
  check('missing column is a 400', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  r = await call('GET', '/mfg/api/v1/manufacturing/boms/import/template', auth)
  check('BOM template downloads', r.status === 200 && /componentSku/.test(r.text), `${r.status}`)

  // ── CSV import: routing
  const rtHeader = 'operationSeq,name,workCenter,standardOperation,setupMinutes,runMinutes,basis,batchSize,queueMinutes,moveMinutes,isSubcontract,instructions'
  const rq = `plantId=${P}&itemId=${it.MAT.id}`
  r = await mcsv(`/routings/import?${rq}`, [rtHeader, '10,Cut,WC-ASM,,20,3,PerUnit,,0,0,false,', '20,Nowhere,WC-NOPE,,0,1,,,,,,', '30,Batch,WC-PKG,,0,5,PerBatch,,,,,', '10,Dup,WC-PKG,,0,1,,,,,,'].join('\n'))
  check('bad routing file: errors on lines 3, 4, 5', r.status === 200 && r.json().createdId === null && r.json().errors.map((e) => e.line).join() === '3,4,5', r.text.slice(0, 400))
  check('bad routing file wrote nothing', (await m('GET', `/routings?plantId=${P}&itemId=${it.MAT.id}`)).json().length === 0)
  r = await mcsv(`/routings/import?${rq}`, [rtHeader, '10,Cut,WC-ASM,,20,3,PerUnit,,0,0,false,', '20,,WC-PNT,STD-PNT,,,,,,,,'].join('\n'))
  check('good routing file creates Draft v1', r.status === 200 && r.json().createdId && r.json().version === 1 && r.json().errors.length === 0, r.text.slice(0, 300))
  const rimp = (await m('GET', `/routings/${r.json().createdId}`)).json()
  check('imported operation 20 took its values from STD-PNT (30 + 6, Painting (standard))', rimp.operations[1]?.setupTimeMinutes === 30 && rimp.operations[1]?.name === 'Painting (standard)', JSON.stringify(rimp.operations).slice(0, 300))
  r = await call('GET', '/mfg/api/v1/manufacturing/routings/import/template', auth)
  check('routing template downloads', r.status === 200 && /operationSeq/.test(r.text))

  // ── access
  r = await call('GET', `/mfg/api/v1/manufacturing/routings?plantId=${P}`, { Host: host })
  check('no token -> 401', r.status === 401, `${r.status}`)
  r = await m('GET', `/routings?plantId=00000000-0000-0000-0000-000000000000`)
  check('unknown plant -> 404', r.status === 404, `${r.status}`)
  r = await call('POST', `/prd/internal/v1/product-lookup/by-sku`, { Host: host })
  r2 = await call('POST', `/mst/internal/v1/units-of-measure/by-code`, { Host: host })
  check('internal lookups are not routed by the Gateway', [404, 401, 405].includes(r.status) && [404, 401, 405].includes(r2.status), `${r.status} ${r2.status}`)

  console.log(`\nplant ZZ-MFG-M4${S}, products ZZ-M4-*-${S}\n${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
})()
