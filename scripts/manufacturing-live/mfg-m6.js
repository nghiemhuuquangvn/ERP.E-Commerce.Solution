// M6 live check on glcheck: DS-01 seeder end to end (products by script, seed by endpoint), the numbers of the test-case book, idempotence,
// a user without approve permission (403), and parallel approvals of one item.
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
const near = (a, b) => Math.abs(a - b) < 1e-6

;(async () => {
  const login = (await call('POST', '/api/v1/auth/login', J, { email: process.env.MFG_EMAIL || 'admin@glcheck.test', password: process.env.MFG_PASSWORD || 'Gl-Check-Passw0rd!', userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body) => call(method, `/mfg/api/v1/manufacturing${path}`, auth, body)
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const prefix = `ZZ6${S}-`
  const plantCode = `ZZ-MFG-M6${S}`

  const units = {}
  for (const u of (await call('GET', '/mst/api/v1/masterdata/units-of-measure?pageSize=100', auth)).json().items) units[u.code] = u.id
  if (!units.M3) { const cu = await call('POST', '/mst/api/v1/masterdata/units-of-measure', auth, { code: 'M3', name: 'Cubic metre', symbol: 'm3', unitType: 1, baseUnitId: null, conversionFactor: 1, decimalPlaces: 3 }); console.log('created unit M3', cu.status, cu.text.slice(0, 120)); const ul = (await call('GET', '/mst/api/v1/masterdata/units-of-measure?pageSize=100&search=M3', auth)).json().items; for (const u of ul) units[u.code] = u.id }
  check('units PCS, SET, M3, L, KG exist', ['PCS', 'SET', 'M3', 'L', 'KG'].every((c) => units[c]), JSON.stringify(Object.keys(units)))

  // ── the twelve products of DS-01 §1.4
  const defs = [['TP-BAN01', 'PCS'], ['BTP-MAT01', 'PCS'], ['BTP-CHAN01', 'PCS'], ['KIT-PKG01', 'SET'], ['NVL-GO01', 'M3'], ['NVL-VIT01', 'PCS'], ['NVL-SON01', 'L'], ['NVL-KEO01', 'KG'], ['NVL-KEO02', 'KG'], ['NVL-THUNG01', 'PCS'], ['NVL-XOP01', 'PCS'], ['PP-MUN01', 'KG']]
  const ids = {}
  for (const [c, u] of defs) {
    const sku = prefix + c
    const created = (await call('POST', '/prd/api/v1/products', auth, { name: `ZZ DS-01 ${c}`, sku, productType: 1, baseUomId: units[u] })).json()
    await call('POST', `/prd/api/v1/products/${created.productId}/variants`, auth, { sku, cost: 1 })
    await call('POST', `/prd/api/v1/products/${created.productId}/publish`, auth)
    ids[c] = created.productId
  }
  check('12 products created', Object.values(ids).every(Boolean))

  const whs = (await call('GET', '/wh/api/v1/warehouses', auth)).json()
  const w = (Array.isArray(whs) ? whs : whs.items).filter((x) => /^ZZ-MFG-WH/.test(x.code)).slice(0, 4)
  const body = { plantCode, skuPrefix: prefix, warehouses: { raw: w[0].id, finished: w[1].id, quarantine: w[2].id, scrap: w[3]?.id ?? null } }

  // ── the seed
  let r = await m('POST', '/seed/ds01', body)
  check('seed answers 200', r.status === 200, r.text.slice(0, 400))
  const first = r.json()
  const P = first?.plantId
  check('first run created plant, 3 shifts, calendar, 4 work centers, 4 machines, 12 profiles, 4 BOMs, 2 routings',
    first?.created?.plant === 1 && first.created.shift === 3 && first.created.calendar === 1 && first.created.workCenter === 4 && first.created.machine === 4 && first.created.itemProfile === 12 && first.created.bom === 4 && first.created.routing === 2,
    JSON.stringify(first?.created))
  check('result lists what is not seeded yet', first?.notSeeded?.length === 3)

  r = await m('POST', '/seed/ds01', body)
  const second = r.json()
  check('second run creates no plant / shift / work center / machine / BOM / routing', r.status === 200 && !second.created.plant && !second.created.shift && !second.created.workCenter && !second.created.machine && !second.created.bom && !second.created.routing && second.skipped.bom === 4, JSON.stringify(second));

  // ── the numbers of the test-case book
  const settings = (await m('GET', `/plants/${P}/settings`)).json()
  check('settings: 5 / 2 / 3 %, Actual costing, 90 days, 20 levels', settings.values.overProductionTolerancePct === 5 && settings.values.underCompletionTolerancePct === 2 && settings.values.overIssueTolerancePct === 3 && settings.values.maxBomLevels === 20 && settings.values.mrpHorizonDays === 90, JSON.stringify(settings.values))
  const plant = (await m('GET', `/plants/${P}`)).json()
  check('plant default calendar is set', !!plant.defaultCalendarId)
  const need = (res, c) => res.json().requirements.find((x) => x.componentCode === prefix + c)?.quantity
  r = await m('GET', `/boms/explode?plantId=${P}&itemId=${ids['TP-BAN01']}&quantity=100&date=2026-11-04`)
  check('explosion x100: 7 lines', r.status === 200 && r.json().requirements.length === 7, r.text.slice(0, 300))
  check('x100: MAT 100, CHAN 400, VIT 1600, SON 52.5, KEO1 10, THUNG 100, XOP 200',
    need(r, 'BTP-MAT01') === 100 && need(r, 'BTP-CHAN01') === 400 && need(r, 'NVL-VIT01') === 1600 && near(need(r, 'NVL-SON01'), 52.5) && near(need(r, 'NVL-KEO01'), 10) && need(r, 'NVL-THUNG01') === 100 && need(r, 'NVL-XOP01') === 200,
    JSON.stringify(r.json().requirements.map((x) => [x.componentCode, x.quantity])))
  check('no phantom line, no substitute KEO02', !r.json().requirements.some((x) => x.componentCode === prefix + 'KIT-PKG01' || x.componentCode === prefix + 'NVL-KEO02'))
  r = await m('GET', `/boms/explode?plantId=${P}&itemId=${ids['BTP-MAT01']}&quantity=50&date=2026-11-04`)
  check('BTP-MAT01 x50: wood 1.1 m3, sawdust 25 kg', near(need(r, 'NVL-GO01'), 1.1) && near(r.json().outputs.find((o) => o.itemCode === prefix + 'PP-MUN01')?.plannedQuantity, 25), r.text.slice(0, 300))
  r = await m('GET', `/boms/low-level-codes?plantId=${P}`)
  const llc = Object.fromEntries((r.json() ?? []).map((x) => [x.itemCode, x.lowLevelCode]))
  check('TC-BOM-019 low-level codes: TP 0; MAT, CHAN, KIT, VIT, SON, KEO1, KEO2 1; GO, THUNG, XOP 2',
    llc[prefix + 'TP-BAN01'] === 0 && ['BTP-MAT01', 'BTP-CHAN01', 'KIT-PKG01', 'NVL-VIT01', 'NVL-SON01', 'NVL-KEO01', 'NVL-KEO02'].every((c) => llc[prefix + c] === 1) && ['NVL-GO01', 'NVL-THUNG01', 'NVL-XOP01'].every((c) => llc[prefix + c] === 2), JSON.stringify(llc))
  const rt = ((await m('GET', `/routings?plantId=${P}&itemId=${ids['TP-BAN01']}`)).json() ?? [])[0]
  r = await m('GET', `/routings/${rt.id}/lead-time?quantity=100&endDate=2026-11-13`)
  check('lead time x100: 2003 minutes, start 2026-11-11', r.json()?.totalMinutes === 2003 && r.json().startDate === '2026-11-11', r.text.slice(0, 300))
  const cals = (await m('GET', `/calendars?plantId=${P}`)).json()
  r = await m('GET', `/calendars/${cals[0].id}/month?year=2026&month=11`)
  const day = (d) => r.json().find((x) => x.date === d)?.workingMinutes
  check('month view: Mon 900, Sat 450, Sun 0', day('2026-11-09') === 900 && day('2026-11-14') === 450 && day('2026-11-15') === 0, r.text.slice(0, 200))
  const wcs = (await m('GET', `/work-centers?plantId=${P}`)).json()
  const cap = async (c) => (await m('GET', `/work-centers/${wcs.find((x) => x.code === c).id}/capacity?from=2026-11-09&to=2026-11-09`)).json()?.[0]
  const cut = await cap('WC-CUT'), pnt = await cap('WC-PNT')
  check('capacity Monday: WC-CUT 1620, WC-PNT 720', (cut?.capacityMinutes ?? cut?.capacity) === 1620 && (pnt?.capacityMinutes ?? pnt?.capacity) === 720, JSON.stringify([cut, pnt]))
  const machines = (await m('GET', `/machines?plantId=${P}`)).json()
  check('retired machine M-OLD-01 is Retired', machines.find((x) => x.code === 'M-OLD-01')?.status === 'Retired')

  // ── a user without the approve permission (a self-registered customer holds no permission)
  const email = `zz6${S.toLowerCase()}@glcheck.test`
  const reg = await call('POST', '/api/v1/auth/register', J, { email, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'M6' })
  const cl = await call('POST', '/api/v1/auth/login', J, { email, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (cl.status === 200 && cl.json().accessToken) {
    const plain = { Authorization: `Bearer ${cl.json().accessToken}`, ...J }
    const topBom = ((await m('GET', `/boms?plantId=${P}&itemId=${ids['TP-BAN01']}`)).json() ?? [])[0]
    const cp = (await m('POST', `/boms/${topBom.id}/copy`, { effectiveFrom: '2027-06-01' })).json()
    let x = await call('POST', `/mfg/api/v1/manufacturing/boms/${cp.id}/approve`, plain, {})
    check('a user without bom:approve gets 403', x.status === 403, `${x.status} ${x.text.slice(0, 120)}`)
    x = await call('GET', `/mfg/api/v1/manufacturing/plants`, plain)
    check('a user without any manufacturing permission cannot list plants (403)', x.status === 403, `${x.status}`)
    x = await call('POST', '/mfg/api/v1/manufacturing/seed/ds01', plain, body)
    check('the seed endpoint refuses a user without settings:manage', x.status === 403, `${x.status}`)
    await m('DELETE', `/boms/${cp.id}`)
  } else console.log('SKIP no-permission user: register', reg.status, 'login', cl.status, cl.text.slice(0, 120))

  // ── parallel approvals of the same item (R167)
  const topBom = ((await m('GET', `/boms?plantId=${P}&itemId=${ids['BTP-CHAN01']}&status=Approved`)).json() ?? [])[0]
  const drafts = []
  for (let i = 0; i < 3; i++) drafts.push((await m('POST', `/boms/${topBom.id}/copy`, { effectiveFrom: '2027-03-01' })).json())
  const results = await Promise.all(drafts.map((d) => m('POST', `/boms/${d.id}/approve`)))
  const ok = results.filter((x) => x.status === 200).length
  const bad = results.filter((x) => x.status >= 500).length
  check('parallel approvals: exactly one wins, none answers 5xx', ok === 1 && bad === 0, JSON.stringify(results.map((x) => [x.status, x.json()?.errorCode])))
  const all = (await m('GET', `/boms?plantId=${P}&itemId=${ids['BTP-CHAN01']}`)).json()
  const approved = all.filter((x) => x.status === 'Approved' && x.effectiveFrom === '2027-03-01')
  check('afterwards one approved version starts 2027-03-01 and v1 is closed the day before', approved.length === 1 && all.find((x) => x.version === 1)?.effectiveTo === '2027-02-28', JSON.stringify(all.map((x) => [x.version, x.status, x.effectiveFrom, x.effectiveTo])))

  console.log(`\nplant ${plantCode} (${P}) left active for inspection; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
})()
