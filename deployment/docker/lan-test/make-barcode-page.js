// node deployment/docker/lan-test/make-barcode-page.js [sku] [out.html]
// Writes a page with the test product's SKU as a Code 128 barcode and as a QR code, to be shown on this PC's screen (or printed) and scanned
// with the staff app's camera. The libraries (JsBarcode, qrcodejs) are loaded from cdnjs when the page is opened - nothing is added to the repo.
const fs = require('fs')
const os = require('os')
const path = require('path')

const sku = process.argv[2] || 'R68-TEST-001'
const out = process.argv[3] || path.join(os.tmpdir(), 'r68-test-barcode.html')

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Barcode ${sku}</title>
<style>
  body { font-family: system-ui, sans-serif; background: #fff; color: #111; margin: 0; padding: 32px; text-align: center }
  h1 { font-size: 22px; margin: 0 0 4px } p { margin: 0 0 24px; color: #444 }
  .card { display: inline-block; margin: 12px 24px; padding: 24px; border: 1px solid #ccc; border-radius: 12px; background: #fff }
  #qr { display: inline-block }
</style></head><body>
<h1>Test product ${sku}</h1>
<p>Hold the phone camera about 20-30 cm from the screen. Turn the screen brightness up. Do not zoom the page below 100 %.</p>
<div class="card"><svg id="code128"></svg></div>
<div class="card"><div id="qr"></div></div>
<script src="https://cdnjs.cloudflare.com/ajax/libs/jsbarcode/3.11.6/JsBarcode.all.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js"></script>
<script>
  JsBarcode('#code128', ${JSON.stringify(sku)}, { format: 'CODE128', width: 3, height: 130, displayValue: true, fontSize: 24, margin: 16 })
  new QRCode(document.getElementById('qr'), { text: ${JSON.stringify(sku)}, width: 220, height: 220, correctLevel: QRCode.CorrectLevel.M })
</script>
</body></html>
`
fs.writeFileSync(out, html)
console.log('wrote', out)
