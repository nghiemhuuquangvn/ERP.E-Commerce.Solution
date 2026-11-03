// node deployment/docker/lan-test/check-from-lan.js [host] [reference-host]
// Prints this PC's LAN addresses and checks, through each of them, that nginx answers for the test shop's custom host (default shop.glcheck.lan)
// AND that the Gateway resolves it to the SAME tenant as the reference host (default glcheck.shop.erp.local): the public product list must be
// identical. (The local docker-compose sets Tenancy__FallbackTenantCode=SYSTEM, so an unknown host does not answer 404 here but with the platform
// tenant's data - a plain "200" would prove nothing, the comparison does.)
// It proves nginx on the LAN address and the tenant domain. It can NOT prove the Windows Firewall rule (traffic from this PC to its own address
// skips it) nor the router's DNS record: open http://<host> in the phone's browser for those.
const http = require('http')
const os = require('os')

const host = process.argv[2] || 'shop.glcheck.lan'
const reference = process.argv[3] || 'glcheck.shop.erp.local'
const PATH = '/api/v1/products/featured'

const get = (ip, hostHeader) =>
  new Promise((resolve) => {
    const req = http.get({ host: ip, port: 80, path: PATH, headers: { host: hostHeader }, timeout: 5000 }, (res) => {
      let body = ''
      res.on('data', (c) => (body += c))
      res.on('end', () => resolve({ status: res.statusCode, body }))
    })
    req.on('timeout', () => { req.destroy(); resolve({ status: 'timeout', body: '' }) })
    req.on('error', (e) => resolve({ status: e.code ?? 'error', body: '' }))
  })

const addresses = Object.values(os.networkInterfaces()).flat().filter((i) => i && i.family === 'IPv4' && !i.internal).map((i) => i.address)
if (addresses.length === 0) { console.log('No LAN IPv4 address found: is the PC on the Wi-Fi?'); process.exit(1) }

;(async () => {
  const ref = await get('127.0.0.1', reference)
  if (ref.status !== 200) { console.log(`The reference host ${reference} does not answer (${ref.status}): is the Docker stack running?`); process.exit(1) }
  let ok = true
  for (const ip of addresses) {
    const mine = await get(ip, host)
    const pass = mine.status === 200 && mine.body === ref.body
    ok = ok && pass
    console.log(`${pass ? 'OK  ' : 'FAIL'} http://${ip}  Host ${host} -> ${mine.status}${mine.status === 200 ? (pass ? ', same shop as ' + reference : ', but NOT the same shop as ' + reference + ' (tenant domain missing or not yet cached)') : ''}`)
  }
  console.log(ok ? `\nThe stack answers on the LAN. Router DNS: ${host} -> the address of your Wi-Fi above (usually 192.168.x.x).` : '\nNot ready: Docker running? tenant domain registered (wait up to a minute for the Gateway cache)?')
  process.exit(ok ? 0 : 1)
})()
