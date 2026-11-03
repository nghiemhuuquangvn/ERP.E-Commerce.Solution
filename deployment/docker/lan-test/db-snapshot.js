// node deployment/docker/lan-test/db-snapshot.js save <file.json>        row counts of every table of every service database (read-only)
// node deployment/docker/lan-test/db-snapshot.js diff <before.json>      what changed since that snapshot (tables whose row count differs)
// Used around the R68 device test: snapshot, one real sale on the phone, diff = exactly the rows that one sale wrote across the services.
// Needs sqlcmd and SA_PASSWORD in the repo's .env (the local docker-compose SQL Server). Never writes to a database.
const { execFileSync } = require('child_process')
const fs = require('fs')
const path = require('path')

const env = fs.readFileSync(path.resolve(__dirname, '../../../.env'), 'utf8')
const pw = /^SA_PASSWORD=(.*)$/m.exec(env)[1].trim()
const run = (db, sql) => execFileSync('sqlcmd', ['-S', 'localhost', '-U', 'sa', '-P', pw, '-C', '-I', '-h', '-1', '-W', '-s', '|', '-d', db, '-Q', 'SET NOCOUNT ON; ' + sql], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 }).trim()

function snapshot() {
  const dbs = run('master', "SELECT name FROM sys.databases WHERE database_id > 4 AND name NOT IN ('ReportServer','ReportServerTempDB') ORDER BY name").split(/\s+/).filter(Boolean)
  const out = {}
  for (const db of dbs) {
    const rows = run(db, "SELECT t.name, SUM(p.row_count) FROM sys.dm_db_partition_stats p JOIN sys.tables t ON t.object_id = p.object_id WHERE p.index_id IN (0, 1) GROUP BY t.name")
    for (const line of rows.split(/\r?\n/)) {
      const [table, count] = line.split('|')
      if (table && count !== undefined && !Number.isNaN(Number(count))) out[`${db}.${table}`] = Number(count)
    }
  }
  return out
}

const [mode, file] = process.argv.slice(2)
if (mode === 'save' && file) {
  const s = snapshot()
  fs.writeFileSync(file, JSON.stringify(s, null, 1))
  console.log(`saved ${Object.keys(s).length} tables to ${file}`)
} else if (mode === 'diff' && file) {
  const before = JSON.parse(fs.readFileSync(file, 'utf8'))
  const after = snapshot()
  const changed = Object.keys({ ...before, ...after }).filter((k) => (before[k] ?? 0) !== (after[k] ?? 0)).sort()
  if (changed.length === 0) console.log('No table changed.')
  for (const k of changed) console.log(`${k.padEnd(60)} ${before[k] ?? 0} -> ${after[k] ?? 0}  (${(after[k] ?? 0) - (before[k] ?? 0) > 0 ? '+' : ''}${(after[k] ?? 0) - (before[k] ?? 0)})`)
} else {
  console.log('usage: node db-snapshot.js save <file.json> | diff <before.json>')
  process.exit(1)
}
