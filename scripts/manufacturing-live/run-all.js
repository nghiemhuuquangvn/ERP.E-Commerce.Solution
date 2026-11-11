// Runs the Manufacturing live checks one after the other against one tenant of the running stack and prints a table. Dev / demo tenants only.
//   node scripts/manufacturing-live/run-all.js [--tenant glcheck|system] [--only mfg-p35,mfg-p37-e2e] [--pause 65]
// The Gateway rate-limits sign-in, so the runner waits (default 65 s) between scripts. Some scripts stop and start the erp-warehouse-service container on purpose.
// Every script prints OK / FAIL lines and ends with "ALL OK" or "n FAILED"; the exit code is non-zero when any script failed.
const { spawnSync } = require('child_process')
const path = require('path')

const arg = (name, fallback) => { const i = process.argv.indexOf(`--${name}`); return i > 0 ? process.argv[i + 1] : fallback }
const tenant = arg('tenant', 'glcheck')
const pause = Number(arg('pause', '65'))
const only = arg('only', '')?.split(',').filter(Boolean)

const TENANTS = {
  glcheck: { host: 'glcheck.erp.local', email: 'admin@glcheck.test', password: 'Gl-Check-Passw0rd!' },
  system: { host: 'admin.erp.local', email: 'superadmin@system.local', password: 'SuperAdmin@123' },
}
const t = TENANTS[tenant]
if (!t) { console.error(`unknown tenant ${tenant}; use glcheck or system`); process.exit(2) }

// order: engineering and settings first, then the order cycle P3-1 .. P3-7, then the Warehouse integration checks
const SCRIPTS = ['mfg-m4', 'mfg-m5', 'mfg-m6', 'mfg-p31', 'mfg-p32', 'mfg-p33', 'mfg-p34', 'mfg-p35', 'mfg-p36-contract-check', 'mfg-p37-e2e', 'wh-w3', 'wh-w5', 'wh-w6', 'wh-w7']
// these two need warehouses named ZZ-MFG-WH* that only glcheck has
const GLCHECK_ONLY = ['mfg-m4', 'mfg-m6']
const selected = SCRIPTS.filter((s) => (!only?.length || only.includes(s)) && !(tenant !== 'glcheck' && GLCHECK_ONLY.includes(s)))
const env = { ...process.env, MFG_HOST: t.host, MFG_EMAIL: t.email, MFG_PASSWORD: t.password, WH_HOST: t.host, WH_EMAIL: t.email, WH_PASSWORD: t.password }
const sleep = (s) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, s * 1000)

const rows = []
selected.forEach((name, i) => {
  if (i > 0) sleep(pause)
  const started = Date.now()
  console.log(`\n=== ${name} on ${tenant} ===`)
  const run = spawnSync('node', [path.join(__dirname, `${name}.js`)], { env, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, timeout: 25 * 60 * 1000 })
  const lines = (run.stdout ?? '').split('\n')
  const failures = lines.filter((l) => l.startsWith('FAIL'))
  const verdict = lines.reverse().find((l) => /ALL OK|\d+ FAILED/.test(l)) ?? ''
  const ok = run.status === 0 && /ALL OK/.test(verdict)
  failures.slice(0, 8).forEach((l) => console.log(l.slice(0, 300)))
  if (!ok && run.stderr) console.log(run.stderr.split('\n').slice(0, 6).join('\n'))
  rows.push({ script: name, result: ok ? 'ALL OK' : `FAILED (${failures.length} FAIL lines, exit ${run.status})`, seconds: Math.round((Date.now() - started) / 1000) })
  console.log(`${name}: ${ok ? 'ALL OK' : 'FAILED'}`)
})

console.log('\n── Summary ──')
console.table(rows)
process.exit(rows.every((r) => r.result === 'ALL OK') ? 0 : 1)
