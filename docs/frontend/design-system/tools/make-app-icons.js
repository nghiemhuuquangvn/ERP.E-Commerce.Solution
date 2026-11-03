// node docs/frontend/design-system/tools/make-app-icons.js
// Placeholder app icons / splash for the two Expo apps (R68), drawn from a Lucide glyph on the app's brand colour and rendered with headless
// Chrome over the DevTools protocol (no extra dependency). Replace the PNGs with the real logo when there is one; this script only exists
// so the build is not blocked.   customer: ShoppingBag on the rose brand colour   staff: Store on the blue brand colour
const { spawn } = require('child_process')
const fs = require('fs')
const os = require('os')
const path = require('path')

const root = path.resolve(__dirname, '../../../..')
const CHROME = process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe'
const PORT = 9433
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

const GLYPHS = {
  ShoppingBag: '<path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z"/><path d="M3 6h18"/><path d="M16 10a4 4 0 0 1-8 0"/>',
  Store: '<path d="m2 7 4.41-4.41A2 2 0 0 1 7.83 2h8.34a2 2 0 0 1 1.42.59L22 7"/><path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8"/><path d="M15 22v-4a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2v4"/><path d="M2 7h20"/><path d="M22 7v3a2 2 0 0 1-2 2a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 16 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 12 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 8 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 4 12a2 2 0 0 1-2-2V7"/>',
}
const APPS = [
  { dir: 'src/frontend/mobile/customer', glyph: 'ShoppingBag' },
  { dir: 'src/frontend/mobile/staff', glyph: 'Store' },
]

const brand = (app) => /"brandAction":\s*"(#[0-9a-fA-F]{6})"/.exec(fs.readFileSync(path.join(root, app.dir, 'theme/theme.ts'), 'utf8'))[1]

;(async () => {
  const profile = path.join(os.tmpdir(), 'chrome-icons-' + Date.now())
  const proc = spawn(CHROME, ['--headless=new', '--disable-gpu', '--no-sandbox', `--remote-debugging-port=${PORT}`, `--user-data-dir=${profile}`, 'about:blank'], { stdio: 'ignore' })
  let list
  for (let i = 0; i < 40; i++) { try { list = await (await fetch(`http://127.0.0.1:${PORT}/json`)).json(); if (list.length) break } catch {} await sleep(300) }
  const ws = new WebSocket(list.find((t) => t.type === 'page').webSocketDebuggerUrl)
  await new Promise((r) => (ws.onopen = r))
  let id = 0
  const pending = new Map()
  ws.onmessage = (m) => { const d = JSON.parse(m.data); if (d.id && pending.has(d.id)) { pending.get(d.id)(d); pending.delete(d.id) } }
  const send = (method, params = {}) => new Promise((r) => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })) })

  /** @param bg colour or 'transparent'; @param scale glyph size as a fraction of the canvas */
  async function render(file, size, bg, glyph, scale) {
    const g = size * scale
    const html = `<!doctype html><html><body style="margin:0;width:${size}px;height:${size}px;background:${bg};display:flex;align-items:center;justify-content:center"><svg xmlns="http://www.w3.org/2000/svg" width="${g}" height="${g}" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round">${GLYPHS[glyph]}</svg></body></html>`
    await send('Emulation.setDeviceMetricsOverride', { width: size, height: size, deviceScaleFactor: 1, mobile: false })
    await send('Emulation.setDefaultBackgroundColorOverride', { color: { r: 0, g: 0, b: 0, a: 0 } })
    await send('Page.navigate', { url: 'data:text/html;charset=utf-8,' + encodeURIComponent(html) })
    await sleep(600)
    const shot = await send('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: false })
    fs.mkdirSync(path.dirname(file), { recursive: true })
    fs.writeFileSync(file, Buffer.from(shot.result.data, 'base64'))
    console.log('wrote', path.relative(root, file))
  }

  await send('Page.enable')
  for (const app of APPS) {
    const color = brand(app)
    const out = (f) => path.join(root, app.dir, 'assets', f)
    await render(out('icon.png'), 1024, color, app.glyph, 0.6) // store / launcher icon: full bleed
    await render(out('adaptive-icon.png'), 1024, 'transparent', app.glyph, 0.42) // Android adaptive foreground: inside the 66 % safe zone
    await render(out('splash.png'), 1024, 'transparent', app.glyph, 0.5) // centred on the splash background colour from the app config
    await render(out('favicon.png'), 48, color, app.glyph, 0.7)
  }
  ws.close()
  proc.kill()
})()
