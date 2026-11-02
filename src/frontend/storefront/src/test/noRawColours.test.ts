import { describe, it, expect } from 'vitest'

// U4 (R69): UI colours come from the role tokens (text-ink, bg-surface, border-line, brand-action, danger-soft ...), never from the raw
// Tailwind palette or the old numeric aliases (brand-100/500/600). This test keeps the migration from slipping back.
const files = import.meta.glob(['../**/*.{vue,ts}', '!../**/*.test.ts', '!../test/**', '!../styles/tokens.ts'], { query: '?raw', import: 'default', eager: true }) as Record<string, string>

const RAW = /(?<![\w-])(?:[a-z0-9[\]-]+:)*(?:text|placeholder|fill|stroke|accent|bg|from|to|via|border(?:-[trblxy])?|divide|ring|outline)-(?:gray|slate|zinc|neutral|red|rose|green|emerald|lime|yellow|amber|orange|blue|sky|indigo|purple|violet|pink|fuchsia|teal|cyan|stone|primary|brand)-\d{2,3}(?![\w-])/g
const WHITE = /(?<![\w-])(?:[a-z0-9[\]-]+:)*(?:bg|text|border)-(?:white|black)(?![\w/-])/g

describe('colour tokens', () => {
  it('no source file uses a raw palette class or a numeric alias', () => {
    const offenders = Object.entries(files).flatMap(([file, text]) => (text.match(RAW) ?? []).map((m) => `${file}: ${m}`))
    expect(offenders).toEqual([])
  })

  it('no source file uses bg-white / text-white / bg-black (use surface / on-brand / ink)', () => {
    const offenders = Object.entries(files).flatMap(([file, text]) => (text.match(WHITE) ?? []).map((m) => `${file}: ${m}`))
    expect(offenders).toEqual([])
  })
})
