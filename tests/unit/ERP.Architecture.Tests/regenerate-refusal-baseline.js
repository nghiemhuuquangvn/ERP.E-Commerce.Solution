// node tests/unit/ERP.Architecture.Tests/regenerate-refusal-baseline.js  (repo root) - rewrites tests/unit/ERP.Architecture.Tests/RefusalBaseline.txt from the current sources.
// Run it only after REDUCING throws (the ratchet test refuses growth).
const fs = require('fs'), path = require('path')
const root = 'src/services'
const out = []
function walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name)
    if (e.isDirectory()) { if (!['obj', 'bin', 'Migrations', 'node_modules'].includes(e.name)) walk(p) }
    else if (e.name.endsWith('.cs')) {
      const n = (fs.readFileSync(p, 'utf8').match(/throw new (InvalidOperationException|ArgumentException)\(/g) || []).length
      if (n) out.push(path.relative(root, p).split(path.sep).join('/') + '|' + n)
    }
  }
}
walk(root)
out.sort()
const head = '# Files that still throw InvalidOperationException / ArgumentException for expected refusals (path|count).\n# The central mapping answers them as HTTP 400 (R99); new code throws BusinessRuleException / DuplicateException / EntityNotFoundException.\n# A count may only go DOWN; after tidying a file run: node tests/unit/ERP.Architecture.Tests/regenerate-refusal-baseline.js.\n'
fs.writeFileSync('tests/unit/ERP.Architecture.Tests/RefusalBaseline.txt', head + out.join('\n') + '\n')
console.log(out.length, 'files', out.reduce((a, l) => a + +l.split('|')[1], 0), 'throws')
