import fs from 'fs'
import path from 'path'
import en from '../i18n/locales/en.json'
import vi from '../i18n/locales/vi.json'

const ROOT = path.resolve(__dirname, '..')

function flat(obj: Record<string, unknown>, prefix = ''): string[] {
  return Object.entries(obj).flatMap(([k, v]) => (v && typeof v === 'object' ? flat(v as Record<string, unknown>, `${prefix}${k}.`) : [`${prefix}${k}`]))
}
function walk(dir: string): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name)
    if (e.isDirectory()) return walk(full)
    return /\.tsx?$/.test(e.name) && !/\.test\.tsx?$/.test(e.name) ? [full] : []
  })
}

describe('translations', () => {
  const enKeys = new Set(flat(en))
  const viKeys = new Set(flat(vi))

  it('English and Vietnamese have the same keys', () => {
    expect([...enKeys].filter((k) => !viKeys.has(k))).toEqual([])
    expect([...viKeys].filter((k) => !enKeys.has(k))).toEqual([])
  })

  it('every key used with t() exists in both languages', () => {
    const used = ['app', 'components'].flatMap((d) => walk(path.join(ROOT, d))).flatMap((f) => {
      const text = fs.readFileSync(f, 'utf8')
      return [...text.matchAll(/\bt\(\s*'([\w.]+)'/g)].map((m) => m[1])
    })
    const missing = used.filter((k) => !enKeys.has(k) || !viKeys.has(k))
    expect([...new Set(missing)]).toEqual([])
  })
})
