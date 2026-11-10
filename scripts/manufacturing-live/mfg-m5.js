// M5 live check through the Admin SPA container (nginx, port 3000) with the tenant host: bundle contains the new screens, and the exact requests the screens make work through the SPA proxy.
const http = require('http')
const call = (method, path, headers = {}, body) => new Promise((resolve, reject) => {
  const req = http.request({ host: '127.0.0.1', port: 3000, method, path, headers: { ...headers, Host: process.env.MFG_HOST || 'glcheck.erp.local' } }, (res) => {
    let d = ''; res.on('data', (c) => (d += c)); res.on('end', () => resolve({ status: res.statusCode, text: d, json: () => { try { return JSON.parse(d) } catch { return null } } }))
  })
  req.on('error', reject); if (body !== undefined) req.write(body); req.end()
})
let failed = 0
const check = (n, ok, x = '') => { if (!ok) failed++; console.log(`${ok ? 'OK  ' : 'FAIL'} ${n} ${ok ? '' : x}`) }
;(async () => {
  const index = await call('GET', '/manufacturing/boms')
  check('SPA serves the app shell on /manufacturing/boms', index.status === 200 && /<div id="root"/.test(index.text), String(index.status))
  const js = [...index.text.matchAll(/src="(\/assets\/[^"]+\.js)"/g)].map((m) => m[1])
  let bundle = ''
  for (const f of js) bundle += (await call('GET', f)).text
  for (const needle of ['/manufacturing/routings', '/manufacturing/where-used', 'orderedSeqs', '/mfg/api/v1/manufacturing', 'Production settings']) check(`bundle contains "${needle}"`, bundle.includes(needle))

  const J = { 'Content-Type': 'application/json' }
  const login = (await call('POST', '/api/v1/auth/login', J, JSON.stringify({ email: process.env.MFG_EMAIL || 'admin@glcheck.test', password: process.env.MFG_PASSWORD || 'Gl-Check-Passw0rd!', userType: 2 }))).json()
  const auth = { Authorization: `Bearer ${login.accessToken}`, ...J }
  const m = (method, path, body) => call(method, `/mfg/api/v1/manufacturing${path}`, auth, body === undefined ? undefined : JSON.stringify(body))
  let r = await m('GET', '/plants?activeOnly=false')
  check('plants through the SPA proxy', r.status === 200 && Array.isArray(r.json()), `${r.status}`)
  const plants = r.json() ?? []
  const p = plants.find((x) => x.isActive) ?? plants[0]
  check('a plant exists to open the screens on', !!p)
  r = await m('GET', `/plants/${p.id}/settings`); check('settings screen request', r.status === 200 && r.json().values.maxBomLevels > 0, `${r.status}`)
  r = await m('GET', `/plants/${p.id}/settings/audit?take=100`); check('audit request', r.status === 200, `${r.status}`)
  r = await m('GET', `/boms?plantId=${p.id}`); check('BOM list request', r.status === 200 && Array.isArray(r.json()), `${r.status}`)
  r = await m('GET', `/routings?plantId=${p.id}`); check('routing list request', r.status === 200, `${r.status}`)
  r = await m('GET', `/reason-codes`); check('reason codes request', r.status === 200, `${r.status}`)
  r = await m('GET', `/item-profiles?plantId=${p.id}&page=1&pageSize=20`); check('item profiles request', r.status === 200 && Array.isArray(r.json().items), `${r.status}`)
  const cal = (await m('GET', `/calendars?plantId=${p.id}`)).json() ?? []
  if (cal[0]) { r = await m('GET', `/calendars/${cal[0].id}/month?year=2026&month=11`); check('month view request', r.status === 200 && r.json().length === 30, `${r.status}`) }
  const csvAuth = { Authorization: `Bearer ${login.accessToken}`, 'Content-Type': 'text/csv' }
  const item = (await m('GET', `/boms?plantId=${p.id}`)).json()?.[0]
  if (item) {
    r = await call('POST', `/mfg/api/v1/manufacturing/boms/import?plantId=${p.id}&itemId=${item.itemId}&dryRun=true`, csvAuth, 'componentSku,quantity,uom\nNO-SUCH-ITEM,1,PCS\n')
    check('CSV dry run through the SPA proxy names the bad line', r.status === 200 && r.json().errors?.[0]?.line === 2, r.text.slice(0, 200))
  }
  r = await call('GET', '/mfg/api/v1/manufacturing/boms/import/template', auth)
  check('template downloads with the token', r.status === 200 && /componentSku/.test(r.text), `${r.status}`)
  console.log(failed === 0 ? 'ALL OK' : failed + ' FAILED')
})()
