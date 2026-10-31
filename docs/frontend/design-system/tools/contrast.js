const lum = (hex) => {
  const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
  return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
}
const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05) }
// Run: node docs/frontend/design-system/tools/contrast.js  (add a row for every new colour pair; results feed accessibility.md section 1)
const pairs = [
  ['white on Back-office hover #075985', '#ffffff', '#075985'],
  ['Back-office action #0369a1 on soft #e0f2fe', '#0369a1', '#e0f2fe'],
  ['Customer action #e11d48 on soft #ffe4e6', '#e11d48', '#ffe4e6'],
  ['Customer link #be123c on soft #ffe4e6', '#be123c', '#ffe4e6'],
  ['control border #6b7280 on white', '#6b7280', '#ffffff'],
  ['sidebar text #d1d5db on #111827', '#d1d5db', '#111827'],
  ['white on Back-office 600 (button)', '#ffffff', '#0284c7'],
  ['white on Back-office 700 (action)', '#ffffff', '#0369a1'],
  ['white on Back-office 500', '#ffffff', '#0ea5e9'],
  ['Back-office 700 text on white', '#0369a1', '#ffffff'],
  ['Back-office 600 text on white', '#0284c7', '#ffffff'],
  ['Back-office 900 on 100 (soft badge)', '#0c4a6e', '#e0f2fe'],
  ['white on Customer 600 (button)', '#ffffff', '#e11d48'],
  ['white on Customer 700 (hover)', '#ffffff', '#be123c'],
  ['white on Customer 500', '#ffffff', '#f43f5e'],
  ['Customer 600 text on white', '#e11d48', '#ffffff'],
  ['Customer 700 text on white', '#be123c', '#ffffff'],
  ['Customer 900 on 100 (soft badge)', '#881337', '#ffe4e6'],
  ['text-primary #111827 on white', '#111827', '#ffffff'],
  ['text-primary on page bg #f9fafb', '#111827', '#f9fafb'],
  ['text-secondary #4b5563 on white', '#4b5563', '#ffffff'],
  ['text-muted #6b7280 on white', '#6b7280', '#ffffff'],
  ['text-muted #6b7280 on page bg #f9fafb', '#6b7280', '#f9fafb'],
  ['placeholder #9ca3af on white', '#9ca3af', '#ffffff'],
  ['white on success #15803d', '#ffffff', '#15803d'],
  ['success text #166534 on #dcfce7', '#166534', '#dcfce7'],
  ['white on warning #b45309', '#ffffff', '#b45309'],
  ['warning text #92400e on #fef3c7', '#92400e', '#fef3c7'],
  ['white on danger #dc2626', '#ffffff', '#dc2626'],
  ['white on danger hover #b91c1c', '#ffffff', '#b91c1c'],
  ['danger text #991b1b on #fee2e2', '#991b1b', '#fee2e2'],
  ['info text #075985 on #e0f2fe', '#075985', '#e0f2fe'],
  ['border #d1d5db on white (UI 3:1)', '#d1d5db', '#ffffff'],
  ['border strong #9ca3af on white (UI 3:1)', '#9ca3af', '#ffffff'],
]
for (const [name, fg, bg] of pairs) {
  const r = ratio(fg, bg)
  console.log(`${r.toFixed(2).padStart(5)} | ${r >= 7 ? 'AAA' : r >= 4.5 ? 'AA ' : r >= 3 ? 'AA-large/UI' : 'FAIL'} | ${name}`)
}
