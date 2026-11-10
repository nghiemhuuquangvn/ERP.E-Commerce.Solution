// Traceability of the Manufacturing test cases: which `TC-xxx-nnn` of docs/business/M/03_PRODUCTION_TEST_CASES.md is cited by a unit test, an Admin test or a live script.
//   node scripts/manufacturing-live/tc-trace.js [--groups PO,MAT,OP,FG] [--md out.md] [--uncited]
// A citation is text (a comment, a test name or a check message that contains the id); a case can be proven by a test that does not name it, so the uncited
// list is a to-do list for a person, not a verdict.
const fs = require('fs')
const path = require('path')
const root = path.resolve(__dirname, '..', '..')
const arg = (name) => { const i = process.argv.indexOf(`--${name}`); return i > 0 ? process.argv[i + 1] : undefined }
const groups = arg('groups')?.split(',')

const rows = []
for (const line of fs.readFileSync(path.join(root, 'docs/business/M/03_PRODUCTION_TEST_CASES.md'), 'utf8').split('\n')) {
  const m = /^\| (TC-([A-Z]+)-(\d{3})) \|/.exec(line)
  if (!m) continue
  const cols = line.split('|').map((s) => s.trim())
  rows.push({ id: m[1], group: m[2], title: cols[2] ?? '', priority: cols.find((c) => /^P[0-3]$/.test(c)) ?? '', level: cols[cols.length - 3] ?? '' })
}

function walk(dir, out = []) {
  if (!fs.existsSync(dir)) return out
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (['bin', 'obj', 'node_modules', 'dist'].includes(e.name)) continue
    const p = path.join(dir, e.name)
    if (e.isDirectory()) walk(p, out)
    else if (/\.(cs|ts|tsx|js)$/.test(e.name)) out.push(p)
  }
  return out
}
const kindOf = (p) => (p.includes('manufacturing-live') ? 'live' : p.includes('frontend') ? 'admin' : 'unit')
const sources = [...walk(path.join(root, 'tests/unit/ERP.MFG.UnitTests')), ...walk(path.join(root, 'tests/unit/ERP.Architecture.Tests')), ...walk(path.join(root, 'src/frontend/admin/src')), ...walk(path.join(root, 'scripts/manufacturing-live'))]
const cited = new Map()
for (const file of sources) {
  const text = fs.readFileSync(file, 'utf8')
  const seen = new Set()
  // TC_PO_001 (C# method names), TC-PO-001, and ranges such as "TC-PO-012 / 013" or "TC-PO-029..031" after a full id
  for (const m of text.matchAll(/TC[-_]([A-Z]+)[-_](\d{3})((?:\s*(?:\/|,|\.\.|-|and|và)\s*\d{3})*)/g)) {
    const ids = [Number(m[2])]
    for (const extra of m[3].matchAll(/\d{3}/g)) ids.push(Number(extra[0]))
    if (/\.\./.test(m[3]) && ids.length === 2) for (let n = ids[0] + 1; n < ids[1]; n++) ids.push(n)
    for (const n of ids) seen.add(`TC-${m[1]}-${String(n).padStart(3, '0')}`)
  }
  for (const id of seen) {
    if (!cited.has(id)) cited.set(id, [])
    cited.get(id).push(`${kindOf(file)}:${path.basename(file)}`)
  }
}

const picked = rows.filter((r) => !groups || groups.includes(r.group))
const lines = ['| TC | Pri | Title | Cited by |', '|---|---|---|---|']
for (const r of picked) {
  const by = [...new Set(cited.get(r.id) ?? [])]
  if (process.argv.includes('--uncited') && by.length) continue
  lines.push(`| ${r.id} | ${r.priority} | ${r.title.replace(/\|/g, '/').slice(0, 90)} | ${by.length ? by.slice(0, 4).join(', ') : '**not cited**'} |`)
}
const total = picked.length
const hit = picked.filter((r) => cited.has(r.id)).length
const p1 = picked.filter((r) => r.priority === 'P1')
const p1hit = p1.filter((r) => cited.has(r.id)).length
const summary = `${total} cases${groups ? ` in ${groups.join(', ')}` : ''}: ${hit} cited; P1: ${p1.length}, cited ${p1hit}, not cited ${p1.length - p1hit}`
console.log(summary)
const out = arg('md')
if (out) fs.writeFileSync(out, `${summary}\n\n${lines.join('\n')}\n`)
else console.log(lines.join('\n'))
