// Manufacturing P4-3 live check: inspection plans (BR-QC-01, TC-QC-001 / 002) and, in the second part, FQC / IPQC inspections (TC-QC-003..015) with the real Warehouse.
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
  let r = await m('POST', '/inspection-plans', { plantId: plant, itemId: ids['TP-BAN01'], code: planCode, type: 'Fqc', samplingMethod: 'Fixed', sampleValue: 5, allowedMinorDefects: 1 })
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
})().catch((e) => { console.error(e.stack ?? e.message); process.exit(1) })
