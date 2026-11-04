import fs from 'fs'
import path from 'path'

// U5 (R69): UI colours come from the role tokens (text-ink, bg-surface, border-line, brand-action, danger-soft ...), never from the raw
// Tailwind palette or the old numeric aliases (brand-100/500/600, primary-50/500/600). This test keeps the migration from slipping back.
const ROOT = path.resolve(__dirname, '..')

function walk(dir: string): string[] {
  if (!fs.existsSync(dir)) return []
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name)
    if (e.isDirectory()) return walk(full)
    return /\.(ts|tsx)$/.test(e.name) && !/\.test\.tsx?$/.test(e.name) && !e.name.endsWith('.d.ts') ? [full] : []
  })
}

const files = ['app', 'components', 'lib'].flatMap((d) => walk(path.join(ROOT, d))).map((f) => [path.relative(ROOT, f), fs.readFileSync(f, 'utf8')] as const)

const RAW = /(?<![\w-])(?:[a-z0-9[\]-]+:)*(?:text|placeholder|fill|stroke|accent|bg|from|to|via|border(?:-[trblxy])?|divide|ring|outline)-(?:gray|slate|zinc|neutral|red|rose|green|emerald|lime|yellow|amber|orange|blue|sky|indigo|purple|violet|pink|fuchsia|teal|cyan|stone|primary|brand)-\d{2,3}(?![\w-])/g
const WHITE = /(?<![\w-])(?:[a-z0-9[\]-]+:)*(?:bg|text|border)-(?:white|black)(?![\w/-])/g
const HEX = /#[0-9a-fA-F]{6}\b/g

describe('colour tokens', () => {
  it('finds the source files', () => {
    expect(files.length).toBeGreaterThan(5)
  })

  it('no source file uses a raw palette class or a numeric alias', () => {
    expect(files.flatMap(([f, text]) => (text.match(RAW) ?? []).map((m) => `${f}: ${m}`))).toEqual([])
  })

  it('no source file uses bg-white / text-white / bg-black (use surface / on-brand / ink)', () => {
    expect(files.flatMap(([f, text]) => (text.match(WHITE) ?? []).map((m) => `${f}: ${m}`))).toEqual([])
  })

  it('no source file carries a hex colour (use theme/theme.ts)', () => {
    expect(files.flatMap(([f, text]) => (text.match(HEX) ?? []).map((m) => `${f}: ${m}`))).toEqual([])
  })
})
