// Manufacturing P3-1 live check (production order core): a fresh DS-01 set (products + stock + plant) from scripts/seed-ds01-products.js, then orders through the
// Gateway: create with the book numbers, schedule relative to today, regenerate, refusals, list / history, outbox, permissions, parallel numbers.
// Default host = glcheck; MFG_HOST / MFG_EMAIL / MFG_PASSWORD switch tenant (SYSTEM: admin.erp.local / superadmin@system.local / SuperAdmin@123).
const http = require('http')
const repoRoot = require('path').resolve(__dirname, '..', '..').split(String.fromCharCode(92)).join('/')
const fs = require('fs')
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
const env = fs.readFileSync(`${repo}/.env`, 'utf8')
const envVar = (k, d) => (new RegExp(`^${k}=(.*)$`, 'm').exec(env)?.[1] ?? d).trim().replace(/^['"]|['"]$/g, '')
const pg = (sql) => execFileSync('docker', ['run', '--rm', '-e', `PGPASSWORD=${envVar('PG_PASSWORD', '')}`, 'postgres:16-alpine', 'psql', '-h', 'host.docker.internal', '-p', envVar('PG_PORT', '5432'),
  '-U', envVar('PG_USER', 'postgres'), '-d', 'ManufacturingDB', '-t', '-A', '-c', sql], { encoding: 'utf8' }).trim()

// plant time is UTC+7; dates are computed in plant time
const plantNow = () => new Date(Date.now() + 7 * 3600e3)
const iso = (d) => d.toISOString().slice(0, 10)
const addDays = (d, n) => new Date(d.getTime() + n * 86400e3)
const localDate = (utcIso) => iso(new Date(new Date(utcIso).getTime() + 7 * 3600e3))

;(async () => {
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const prefix = `ZZP31${S}-`
  const plantCode = `ZZ-P31-${S}`
  const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
  const seedAnswer = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, ''))
  const ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
  const plant = seedAnswer.plantId
  check('DS-01 set seeded (plant, products, stock)', !!plant && !!ids['TP-BAN01'], out.slice(-400))

  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body) => call(method, `/mfg/api/v1/manufacturing${path}`, auth, body)
  const code = (r) => r.json()?.errorCode

  // due date: the first Friday at least 7 days ahead, so the order is not late
  let due = addDays(plantNow(), 7)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const dueIso = iso(due)

  // ── MO-A
  let r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 100, dueDate: dueIso })
  const a = r.json()
  check('create MO-A answers 201', r.status === 201, `${r.status} ${r.text.slice(0, 300)}`)
  check('number MO-yyMM-nnnnn, Draft, priority 3', /^MO-\d{4}-\d{5}$/.test(a?.orderNo) && a.status === 'Draft' && a.priority === 3, JSON.stringify([a?.orderNo, a?.status, a?.priority]))
  const need = (c) => a?.materials?.find((x) => x.componentCode === prefix + c)?.requiredQty
  check('7 materials with the book numbers (MAT 100, CHAN 400, VIT 1600, SON 52.5, KEO01 10, THUNG 100, XOP 200)',
    a?.materials?.length === 7 && need('BTP-MAT01') === 100 && need('BTP-CHAN01') === 400 && need('NVL-VIT01') === 1600 && near(need('NVL-SON01'), 52.5) && need('NVL-KEO01') === 10 && need('NVL-THUNG01') === 100 && need('NVL-XOP01') === 200,
    JSON.stringify(a?.materials?.map((x) => [x.componentCode, x.requiredQty])))
  check('no KEO02 and no phantom kit line', !a?.materials?.some((x) => /KEO02|KIT-PKG01/.test(x.componentCode)))
  check('VIT / THUNG / XOP are backflush, SON is manual', ['NVL-VIT01', 'NVL-THUNG01', 'NVL-XOP01'].every((c) => a.materials.find((x) => x.componentCode === prefix + c).issueMethod === 'Backflush') && a.materials.find((x) => x.componentCode === prefix + 'NVL-SON01').issueMethod === 'Manual')
  check('3 operations 10 / 20 / 30, all Pending, no outputs', a?.operations?.map((o) => o.operationSeq).join() === '10,20,30' && a.operations.every((o) => o.status === 'Pending') && a.outputs.length === 0)
  const ops = Object.fromEntries((a?.operations ?? []).map((o) => [o.operationSeq, o]))
  check('painting: 630 standard minutes, 788 calendar minutes at 80 % efficiency', near(ops[20]?.standardMinutes, 630) && ops[20]?.calendarMinutes === 788, JSON.stringify(ops[20]))
  check('schedule: start two working days before the due Friday, op20 the day after, op30 on the due date',
    localDate(a.plannedStart) === iso(addDays(due, -2)) && localDate(ops[20].plannedStart) === iso(addDays(due, -1)) && localDate(ops[30].plannedStart) === dueIso && localDate(a.plannedEnd) === dueIso && a.isLate === false,
    JSON.stringify([a?.plannedStart, a?.plannedEnd, a?.isLate]))
  check('output warehouse is the plant finished-goods warehouse', !!a?.outputWarehouseId)

  // ── regenerate a Draft
  r = await m('PUT', `/orders/${a.id}`, { quantity: 120, dueDate: dueIso })
  const b = r.json()
  check('Draft change to 120 regenerates the snapshot (SON 63, CHAN 480, op10 1215 minutes)',
    r.status === 200 && near(b.materials.find((x) => x.componentCode === prefix + 'NVL-SON01').requiredQty, 63) && b.materials.find((x) => x.componentCode === prefix + 'BTP-CHAN01').requiredQty === 480 && near(b.operations.find((o) => o.operationSeq === 10).standardMinutes, 1215) && b.materials.length === 7,
    `${r.status} ${r.text.slice(0, 300)}`)
  r = await m('PUT', `/orders/${a.id}`, { quantity: 120, dueDate: iso(addDays(due, 7)) })
  check('a new due date moves the schedule by a week', r.status === 200 && localDate(r.json().plannedStart) === iso(addDays(due, 5)), `${r.status} ${localDate(r.json()?.plannedStart)}`)
  await m('PUT', `/orders/${a.id}`, { quantity: 100, dueDate: dueIso })

  // ── refusals
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 100, dueDate: iso(addDays(plantNow(), -1)) })
  check('due date in the past: 400 MFG_PO_003', r.status === 400 && code(r) === 'MFG_PO_003', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 0, dueDate: dueIso })
  check('quantity 0: 400 MFG_PO_001', r.status === 400 && code(r) === 'MFG_PO_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 10.5, dueDate: dueIso })
  check('a fraction of a piece: 400', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 10, dueDate: dueIso, priority: 9 })
  check('priority 9: 400', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['NVL-SON01'], quantity: 10, dueDate: dueIso })
  check('a bought item cannot be produced: 400 MFG_ITM_001', r.status === 400 && code(r) === 'MFG_ITM_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 100, dueDate: dueIso, bomId: a.bomId, routingId: a.bomId })
  check('a routing id that is not a routing: 404', r.status === 404, `${r.status} ${r.text.slice(0, 160)}`)

  // ── order without routing (BTP-CHAN01 has a BOM and no routing)
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['BTP-CHAN01'], quantity: 200, dueDate: dueIso })
  const chan = r.json()
  check('an item without routing: 201, no operations, start on a working day before the due date', r.status === 201 && chan.operations.length === 0 && chan.routingId === null && localDate(chan.plannedStart) <= dueIso, `${r.status} ${r.text.slice(0, 200)}`)
  check('BTP-CHAN01 x 200: GO 1.1 m3 (0.005 x 200 x 1.1)', near(chan?.materials?.[0]?.requiredQty, 1.1), JSON.stringify(chan?.materials))

  // ── a late order
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 100, dueDate: iso(plantNow()) })
  check('due today: created and late, planned end after the due date', r.status === 201 && r.json().isLate === true && localDate(r.json().plannedEnd) > iso(plantNow()), `${r.status} ${r.text.slice(0, 200)}`)

  // ── list, one order, history
  r = await m('GET', `/orders?plantId=${plant}&pageSize=2&page=1`)
  const page = r.json()
  check('list answers {items,total,page,pageSize} and pages', r.status === 200 && page.items.length === 2 && page.total >= 3 && page.page === 1 && page.pageSize === 2, r.text.slice(0, 200))
  r = await m('GET', `/orders?plantId=${plant}&isLate=true`)
  check('filter isLate returns only late orders', r.json().items.length >= 1 && r.json().items.every((x) => x.isLate), r.text.slice(0, 200))
  r = await m('GET', `/orders?plantId=${plant}&search=${encodeURIComponent(a.orderNo)}`)
  check('search by order number finds it', r.json().items.length === 1 && r.json().items[0].id === a.id, r.text.slice(0, 200))
  r = await m('GET', `/orders?plantId=${plant}&status=Released`)
  check('no Released order yet', r.json().total === 0)
  r = await m('GET', `/orders/${a.id}`)
  check('get one order with its lines', r.status === 200 && r.json().materials.length === 7 && r.json().operations.length === 3, r.text.slice(0, 160))
  r = await m('GET', `/orders/${a.id}/history`)
  check('history of a new order is empty', r.status === 200 && r.json().length === 0, r.text.slice(0, 160))

  // ── parallel numbering (20 creates at once)
  const results = await Promise.all(Array.from({ length: 20 }, () => m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 10, dueDate: dueIso })))
  const numbers = results.map((x) => x.json()?.orderNo)
  check('20 parallel creates: all 201 and 20 distinct numbers', results.every((x) => x.status === 201) && new Set(numbers).size === 20, `${results.map((x) => x.status).join()} ${numbers.length}`)

  // ── outbox
  await sleep(9000)
  const outbox = pg(`SELECT count(*) FILTER (WHERE "ProcessedOnUtc" IS NOT NULL) || '/' || count(*) FROM "OutboxMessages" WHERE "Type" LIKE '%ProductionOrderCreatedEvent%' AND "Content" LIKE '%${a.id}%'`)
  check('the ProductionOrderCreated event of MO-A was written to the outbox and published', outbox === '1/1', outbox)
  const unpublished = pg(`SELECT count(*) FROM "OutboxMessages" WHERE "ProcessedOnUtc" IS NULL AND "OccurredOnUtc" < now() - interval '1 minute'`)
  check('no message waits longer than a minute', unpublished === '0', unpublished)

  // ── permissions and tenants
  const mail = `zz31${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P31' })
  const cl = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (cl.status === 200 && cl.json().accessToken) {
    const plain = { Authorization: `Bearer ${cl.json().accessToken}`, ...J }
    let x = await call('POST', '/mfg/api/v1/manufacturing/orders', plain, { plantId: plant, itemId: ids['TP-BAN01'], quantity: 1, dueDate: dueIso })
    check('a user without order:create gets 403 on create', x.status === 403, `${x.status}`)
    x = await call('GET', `/mfg/api/v1/manufacturing/orders?plantId=${plant}`, plain)
    check('a user without order:view gets 403 on the list', x.status === 403, `${x.status}`)
  } else console.log('SKIP no-permission user:', cl.status, cl.text.slice(0, 100))

  // ── serial flag last (the product lookup is cached for a minute): a serial-controlled component is refused, then the flag is removed again
  await call('PUT', `/prd/api/v1/products/${ids['NVL-KEO01']}/tracking`, auth, { isLotControlled: false, isSerialControlled: true })
  await sleep(61000)
  r = await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: 10, dueDate: dueIso })
  check('a serial-controlled component: 400 MFG_MAT_011', r.status === 400 && code(r) === 'MFG_MAT_011', `${r.status} ${r.text.slice(0, 200)}`)
  await call('PUT', `/prd/api/v1/products/${ids['NVL-KEO01']}/tracking`, auth, { isLotControlled: false, isSerialControlled: false })

  console.log(`\nprefix ${prefix} plant ${plantCode} order ${a.orderNo}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); process.exit(1) })
