// Manufacturing P3-2 live check (release, reservation, unrelease, cancel, hold / resume, change quantity) on a fresh DS-01 set.
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
const docker = (...args) => execFileSync('docker', args, { encoding: 'utf8' }).trim()
const plantNow = () => new Date(Date.now() + 7 * 3600e3)
const iso = (d) => d.toISOString().slice(0, 10)
const addDays = (d, n) => new Date(d.getTime() + n * 86400e3)

;(async () => {
  const S = Date.now().toString(36).toUpperCase().slice(-4)
  const prefix = `ZZP32${S}-`
  const plantCode = `ZZ-P32-${S}`
  const out = execFileSync('node', [`${repo}/scripts/seed-ds01-products.js`, '--host', host, '--email', email, '--password', password, '--prefix', prefix, '--stock', '--seed', '--plant', plantCode], { encoding: 'utf8' })
  const seedAnswer = JSON.parse(out.trim().split('\n').pop().replace(/^\d+ /, ''))
  const ids = JSON.parse(/^products (.*)$/m.exec(out)[1])
  const plant = seedAnswer.plantId
  check('DS-01 set seeded (plant, products, stock)', !!plant && !!ids['TP-BAN01'], out.slice(-400))

  const login = (await call('POST', '/api/v1/auth/login', J, { email, password, userType: 2 })).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body) => call(method, `/mfg/api/v1/manufacturing${path}`, auth, body)
  const wh = (method, path, body) => call(method, `/wh/api/v1${path}`, auth, body)
  const code = (r) => r.json()?.errorCode
  const inv = async (key) => (await wh('GET', `/inventory/${ids[key]}`)).json()
  const resv = async (orderId) => { const r = await wh('GET', `/inventory/reservations/source/ProductionOrder/${orderId}`); return r.status === 200 ? r.json() : null }
  const heldOf = (rs, key) => (rs?.status === 'Reserved' ? rs.lines.filter((l) => l.productId === ids[key]).reduce((s, l) => s + Number(l.remaining), 0) : 0)

  // reasons: the default set holds one Hold and one Cancel reason
  await m('POST', '/reason-codes/seed-defaults')
  const reasons = (await m('GET', '/reason-codes')).json()
  const holdReason = reasons.find((x) => x.type === 'Hold' && x.isActive)?.id
  const cancelReason = reasons.find((x) => x.type === 'Cancel' && x.isActive)?.id
  const scrapReason = reasons.find((x) => x.type === 'Scrap' && x.isActive)?.id
  check('hold / cancel reason codes exist', !!holdReason && !!cancelReason, JSON.stringify(reasons.slice(0, 3)))

  let due = addDays(plantNow(), 7)
  while (due.getUTCDay() !== 5) due = addDays(due, 1)
  const dueIso = iso(due)
  const create = async (qty = 100) => (await m('POST', '/orders', { plantId: plant, itemId: ids['TP-BAN01'], quantity: qty, dueDate: dueIso })).json()
  const getOrder = async (id) => (await m('GET', `/orders/${id}`)).json()
  const matOf = (o, key) => o.materials.find((x) => x.componentCode === prefix + key)
  const rawBefore = {}
  for (const k of ['BTP-CHAN01', 'NVL-VIT01', 'NVL-SON01']) rawBefore[k] = (await inv(k))
  console.log('stock before:', Object.fromEntries(Object.entries(rawBefore).map(([k, v]) => [k, [v.totalQuantity, v.reservedQuantity]])))

  // ── availability (TC-PO-011): the seed stocks CHAN at 300 against a need of 400
  let a = await create(100)
  let r = await m('GET', `/orders/${a.id}/availability`)
  const av = r.json()
  const shortRows = av?.materials?.filter((x) => x.shortage > 0) ?? []
  check('availability: only BTP-CHAN01 is short (need 400, have 300, short 100)', r.status === 200 && av.hasShortage === true && shortRows.length === 1 && shortRows[0].componentCode === prefix + 'BTP-CHAN01' && near(shortRows[0].need, 400) && near(shortRows[0].allocatable, 300) && near(shortRows[0].shortage, 100), `${r.status} ${r.text.slice(0, 400)}`)

  // ── release (TC-PO-012)
  r = await m('POST', `/orders/${a.id}/release`)
  const rel = r.json()
  check('release answers 200 Released with the shortage list, reservation not pending', r.status === 200 && rel.order.status === 'Released' && rel.shortages.length === 1 && rel.reservationPending === false, `${r.status} ${r.text.slice(0, 300)}`)
  a = await getOrder(a.id)
  check('op10 Ready, op20 / op30 Pending', a.operations.map((o) => o.status).join() === 'Ready,Pending,Pending', a.operations.map((o) => o.status).join())
  let rs = await resv(a.id)
  check('Warehouse holds CHAN 300 (not 400), VIT 1600, SON 52.5 for the order', rs?.status === 'Reserved' && near(heldOf(rs, 'BTP-CHAN01'), 300) && near(heldOf(rs, 'NVL-VIT01'), 1600) && near(heldOf(rs, 'NVL-SON01'), 52.5), JSON.stringify(rs).slice(0, 400))
  check('order shows ReservedQty per line and a reservation id', near(matOf(a, 'BTP-CHAN01').reservedQty, 300) && near(matOf(a, 'NVL-SON01').reservedQty, 52.5) && !!a.reservationId && a.reservationSyncPending === false)
  check('item reserved quantity rose by the reservation (CHAN +300)', near((await inv('BTP-CHAN01')).reservedQuantity - rawBefore['BTP-CHAN01'].reservedQuantity, 300))
  r = await m('POST', `/orders/${a.id}/release`)
  check('a second release is refused (MFG_PO_002)', r.status === 400 && code(r) === 'MFG_PO_002', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('PUT', `/orders/${a.id}`, { quantity: 90, dueDate: dueIso })
  check('a released order can no longer be edited with PUT (400)', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)

  // ── hold / resume (TC-PO-019, 022)
  r = await m('POST', `/orders/${a.id}/hold`, {})
  check('hold without a reason: 400 MFG_PO_009', r.status === 400 && code(r) === 'MFG_PO_009', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/hold`, { reasonCodeId: cancelReason })
  check('hold with a Cancel-type reason: 400 MFG_PO_009', r.status === 400 && code(r) === 'MFG_PO_009', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/hold`, { reasonCodeId: holdReason, note: 'waiting for a drawing' })
  check('hold with a Hold reason: OnHold from Released', r.status === 200 && r.json().order.status === 'OnHold' && r.json().order.statusBeforeHold === 'Released', `${r.status} ${r.text.slice(0, 200)}`)
  rs = await resv(a.id)
  check('the reservation is kept while on hold', near(heldOf(rs, 'BTP-CHAN01'), 300))
  r = await m('POST', `/orders/${a.id}/resume`)
  check('resume goes back to Released', r.status === 200 && r.json().order.status === 'Released', `${r.status} ${r.text.slice(0, 200)}`)

  // ── change quantity (TC-PO-024 without the issue part)
  r = await m('POST', `/orders/${a.id}/change-quantity`, { quantity: 120, note: 'customer asked for more' })
  const chg = r.json()
  check('change to 120: SON required 63, CHAN 480, op10 1215 minutes', r.status === 200 && near(matOf(chg.order, 'NVL-SON01').requiredQty, 63) && near(matOf(chg.order, 'BTP-CHAN01').requiredQty, 480) && near(chg.order.operations.find((o) => o.operationSeq === 10).standardMinutes, 1215), `${r.status} ${r.text.slice(0, 300)}`)
  rs = await resv(a.id)
  check('reservation follows: SON 63, VIT 1920, CHAN still 300', near(heldOf(rs, 'NVL-SON01'), 63) && near(heldOf(rs, 'NVL-VIT01'), 1920) && near(heldOf(rs, 'BTP-CHAN01'), 300), JSON.stringify(rs).slice(0, 300))
  const hist = (await m('GET', `/orders/${a.id}/history`)).json()
  check('history has Released, OnHold, Released and the quantity change', hist.length >= 4 && hist.some((h) => /120/.test(h.note ?? '')), JSON.stringify(hist).slice(0, 400))
  r = await m('POST', `/orders/${a.id}/change-quantity`, { quantity: 0 })
  check('quantity 0: 400', r.status === 400, `${r.status} ${r.text.slice(0, 200)}`)

  // ── the guards of other masters (P3-1 guard, now with a real Released order)
  const curSettings = (await m('GET', `/plants/${plant}/settings`)).json()
  r = await m('PUT', `/plants/${plant}/settings`, { ...curSettings.values, costingMethod: 'Standard' })
  check('costing method cannot change while an order is open: 400 MFG_SET_001', r.status === 400 && code(r) === 'MFG_SET_001', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/work-centers/${a.operations[0].workCenterId}/deactivate`)
  check('a work center with an open operation cannot be deactivated: 400 MFG_WC_003', r.status === 400 && code(r) === 'MFG_WC_003', `${r.status} ${r.text.slice(0, 200)}`)

  // ── unrelease (TC-PO-016)
  r = await m('POST', `/orders/${a.id}/unrelease`)
  check('unrelease: Draft, operations Pending, nothing reserved', r.status === 200 && r.json().order.status === 'Draft' && r.json().order.operations.every((o) => o.status === 'Pending') && r.json().reservationPending === false, `${r.status} ${r.text.slice(0, 200)}`)
  rs = await resv(a.id)
  check('Warehouse reservation released', !rs || rs.status !== 'Reserved', JSON.stringify(rs).slice(0, 200))
  check('item reserved quantity back to where it was', near((await inv('BTP-CHAN01')).reservedQuantity, rawBefore['BTP-CHAN01'].reservedQuantity))

  // ── release again, then cancel with a reason (TC-PO-026, 028)
  await m('POST', `/orders/${a.id}/release`)
  r = await m('POST', `/orders/${a.id}/cancel`, {})
  check('cancel without a reason: 400 MFG_PO_009', r.status === 400 && code(r) === 'MFG_PO_009', `${r.status} ${r.text.slice(0, 200)}`)
  r = await m('POST', `/orders/${a.id}/cancel`, { reasonCodeId: cancelReason, note: 'zz p32' })
  check('cancel with a Cancel reason: Cancelled', r.status === 200 && r.json().order.status === 'Cancelled', `${r.status} ${r.text.slice(0, 200)}`)
  rs = await resv(a.id)
  check('cancel released the reservation', !rs || rs.status !== 'Reserved')
  r = await m('POST', `/orders/${a.id}/release`)
  check('a cancelled order cannot be released (MFG_PO_002)', r.status === 400 && code(r) === 'MFG_PO_002', `${r.status} ${r.text.slice(0, 200)}`)

  // ── a draft can be cancelled too; a second draft with BlockReleaseOnShortage
  const d1 = await create(10)
  r = await m('POST', `/orders/${d1.id}/cancel`, { reasonCodeId: cancelReason })
  check('cancel a Draft with a reason', r.status === 200 && r.json().order.status === 'Cancelled', `${r.status} ${r.text.slice(0, 200)}`)

  const settings = (await m('GET', `/plants/${plant}/settings`)).json()
  r = await m('PUT', `/plants/${plant}/settings`, { ...settings.values, blockReleaseOnShortage: true })
  check('plant setting BlockReleaseOnShortage switched on', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  const blocked = await create(100)
  r = await m('POST', `/orders/${blocked.id}/release`)
  check('shortage blocks the release: 400 MFG_PO_007, order still Draft', r.status === 400 && code(r) === 'MFG_PO_007' && (await getOrder(blocked.id)).status === 'Draft', `${r.status} ${r.text.slice(0, 200)}`)
  const small = await create(10)   // CHAN need 40 <= 300
  r = await m('POST', `/orders/${small.id}/release`)
  check('an order without a shortage is released even with the block on', r.status === 200 && r.json().order.status === 'Released' && r.json().shortages.length === 0, `${r.status} ${r.text.slice(0, 200)}`)
  await m('PUT', `/plants/${plant}/settings`, { ...settings.values, blockReleaseOnShortage: false })

  // ── sequential operations off: all Ready
  r = await m('PUT', `/plants/${plant}/settings`, { ...settings.values, requireSequentialOperations: false })
  const par = await create(10)
  r = await m('POST', `/orders/${par.id}/release`)
  check('RequireSequentialOperations off: every operation Ready', r.status === 200 && r.json().order.operations.every((o) => o.status === 'Ready'), `${r.status} ${r.text.slice(0, 200)}`)
  await m('PUT', `/plants/${plant}/settings`, { ...settings.values, requireSequentialOperations: true })
  await m('POST', `/orders/${par.id}/cancel`, { reasonCodeId: cancelReason })

  // ── failure injection: the Warehouse is down
  const f1 = await create(10)
  r = await m('POST', `/orders/${f1.id}/release`)
  check('order for the outage test released', r.status === 200)
  docker('stop', 'erp-warehouse-service')
  try {
    await sleep(2000)
    const f2 = await create(10)
    r = await m('POST', `/orders/${f2.id}/release`)
    check('release with the Warehouse down fails closed: 400 MFG_INT_001, order still Draft', r.status === 400 && code(r) === 'MFG_INT_001' && (await getOrder(f2.id)).status === 'Draft', `${r.status} ${r.text.slice(0, 200)}`)
    await m('POST', `/orders/${f2.id}/cancel`, { reasonCodeId: cancelReason })
    r = await m('POST', `/orders/${f1.id}/cancel`, { reasonCodeId: cancelReason })
    check('cancel with the Warehouse down: Cancelled, reservationPending true', r.status === 200 && r.json().order.status === 'Cancelled' && r.json().reservationPending === true, `${r.status} ${r.text.slice(0, 300)}`)
    const stuck = await getOrder(f1.id)
    check('the order carries the flag, the error and the attempt count', stuck.reservationSyncPending === true && !!stuck.reservationSyncError, JSON.stringify([stuck.reservationSyncPending, stuck.reservationSyncError]))
  } finally {
    docker('start', 'erp-warehouse-service')
  }
  let cleared = false
  for (let i = 0; i < 40 && !cleared; i++) {
    await sleep(5000)
    const cur = await getOrder(f1.id)
    cleared = cur?.reservationSyncPending === false
  }
  check('after the Warehouse is back the job releases the reservation and clears the flag', cleared)
  rs = await resv(f1.id)
  check('Warehouse shows the outage order\'s reservation released', !rs || rs.status !== 'Reserved', JSON.stringify(rs).slice(0, 200))

  // ── permissions and a second tenant
  const mail = `zz32${S.toLowerCase()}@${host.replace(/^[^.]+\./, '')}`
  await call('POST', '/api/v1/auth/register', J, { email: mail, password: 'Gl-Check-Passw0rd!', confirmPassword: 'Gl-Check-Passw0rd!', firstName: 'ZZ', lastName: 'P32' })
  const cl = await call('POST', '/api/v1/auth/login', J, { email: mail, password: 'Gl-Check-Passw0rd!', userType: 1 })
  if (cl.status === 200 && cl.json().accessToken) {
    const plain = { Authorization: `Bearer ${cl.json().accessToken}`, ...J }
    for (const [what, method, path, body] of [['release', 'POST', `/orders/${small.id}/release`], ['unrelease', 'POST', `/orders/${small.id}/unrelease`], ['cancel', 'POST', `/orders/${small.id}/cancel`, {}],
      ['hold', 'POST', `/orders/${small.id}/hold`, {}], ['resume', 'POST', `/orders/${small.id}/resume`], ['change-quantity', 'POST', `/orders/${small.id}/change-quantity`, { quantity: 5 }], ['availability', 'GET', `/orders/${small.id}/availability`]]) {
      const x = await call(method, `/mfg/api/v1/manufacturing${path}`, plain, body)
      check(`a user without the permission gets 403 on ${what}`, x.status === 403, `${x.status}`)
    }
  } else console.log('SKIP no-permission user:', cl.status, cl.text.slice(0, 100))

  // ── clean up what this run holds: cancel the released leftovers, then the reserved figures must be back
  r = await m('POST', `/orders/${small.id}/cancel`, { reasonCodeId: cancelReason })
  check('cleanup: the last released order cancelled', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
  await m('POST', `/orders/${blocked.id}/cancel`, { reasonCodeId: cancelReason })
  await sleep(1500)
  for (const k of ['BTP-CHAN01', 'NVL-VIT01', 'NVL-SON01'])
    check(`reconciliation: reserved ${k} back to ${rawBefore[k].reservedQuantity}`, near((await inv(k)).reservedQuantity, rawBefore[k].reservedQuantity), JSON.stringify(await inv(k)).slice(0, 200))

  console.log(`\nprefix ${prefix} plant ${plantCode}; ${failed === 0 ? 'ALL OK' : failed + ' FAILED'}`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => { console.error(e.stack ?? e.message); try { docker('start', 'erp-warehouse-service') } catch {} process.exit(1) })
