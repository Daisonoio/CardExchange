// Rigenera le icone PNG dell'app dai file vettoriali. Uso: node tools/brand/render_icons.cjs
const { chromium } = require('playwright');
const fs = require('fs'); const path = require('path');
const pub = path.join(__dirname, '..', '..', 'card-exchange-frontend', 'public');
const jobs = [
  ['icon.svg', 'icon-192.png', 192, true], ['icon.svg', 'icon-512.png', 512, true],
  ['brand/icon-maskable.svg', 'icon-maskable-512.png', 512, false], ['brand/icon-maskable.svg', 'apple-touch-icon.png', 180, false],
];
(async () => {
  const b = await chromium.launch();
  for (const [src, out, size, transparent] of jobs) {
    const p = await b.newPage({ viewport: { width: size, height: size } });
    const svg = fs.readFileSync(path.join(pub, src), 'utf8');
    await p.setContent(`<html><body style="margin:0;background:transparent"><img style="display:block" width="${size}" height="${size}" src="data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}"></body></html>`);
    await p.waitForTimeout(150);
    await p.screenshot({ path: path.join(pub, out), omitBackground: transparent });
    await p.close();
  }
  await b.close(); console.log('icone aggiornate');
})();
