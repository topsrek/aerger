// Bildschirmfotos der wichtigsten Zustände in mehreren Fenstergrößen, mit dem lokalen Chrome.
// Prüft nebenbei, dass nirgends ein Scrollbalken entsteht.
//   node test/e2e/shots.mjs <url> <ausgabeordner>
import { chromium } from 'playwright-core';
const [url = 'http://localhost:4173/', out = '.'] = process.argv.slice(2);
const browser = await chromium.launch({ executablePath: process.env.CHROME ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
const errors = [];
const humans = { players: Array.from({ length: 6 }, (_, i) => ({ name: `Spieler ${i + 1}`, kind: i < 2 ? 0 : 1, strength: 'normal' })), speedHuman: 2 };

async function page(width, height, name, settings = humans) {
  const ctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 1, hasTouch: width < 600 });
  const p = await ctx.newPage();
  p.on('pageerror', e => errors.push(`${name}: ${e.message}`));
  p.on('console', m => { if (m.type() === 'error') errors.push(`${name}: ${m.text()}`); });
  await p.goto(url);
  await p.evaluate(s => { localStorage.clear(); if (s) localStorage.setItem('aerger.settings.v1', JSON.stringify(s)); }, settings);
  await p.reload();
  await p.waitForSelector('.stage');
  await p.waitForTimeout(300);
  return p;
}
async function checkFit(p, name) {
  const r = await p.evaluate(() => {
    const d = document.documentElement, s = document.querySelector('.stage').getBoundingClientRect();
    return { sw: d.scrollWidth, sh: d.scrollHeight, w: innerWidth, h: innerHeight, stage: [Math.round(s.width), Math.round(s.height), Math.round(s.right), Math.round(s.bottom)] };
  });
  const ok = r.sw <= r.w && r.sh <= r.h && r.stage[2] <= r.w && r.stage[3] <= r.h;
  console.log(`${ok ? 'ok  ' : 'FEHLER'} ${name}: Fenster ${r.w}x${r.h}, Brett ${r.stage[0]}x${r.stage[1]}`);
  if (!ok) errors.push(`${name}: passt nicht ins Fenster ${JSON.stringify(r)}`);
}

const p = await page(1280, 720, 'desktop');
await checkFit(p, 'Titel 1280x720');
await p.screenshot({ path: `${out}/01-title.png` });
await p.keyboard.press('F2');
await p.waitForTimeout(300);
await checkFit(p, 'Brett 1280x720');
for (let i = 0; i < 12; i++) {
  await p.keyboard.press('Space');
  await p.waitForTimeout(1400);
  if (await p.locator('.piece.movable').count()) break;
}
await p.locator('.piece.movable').first().hover().catch(() => {});
await p.waitForTimeout(200);
await p.screenshot({ path: `${out}/02-board.png` });
for (const [label, n] of [['Einstellungen', 3], ['Spieler', 4], ['Regeln', 5], ['Laden', 5.5]]) {
  await p.locator('.tb-button', { hasText: label }).click();
  await p.waitForTimeout(200);
  await p.screenshot({ path: `${out}/0${n}-${label.toLowerCase()}.png` });
  await p.keyboard.press('Escape');
}
for (const [w, h] of [[1920, 1080], [1024, 600], [800, 900], [1600, 500]]) {
  await p.setViewportSize({ width: w, height: h });
  await p.waitForTimeout(250);
  await checkFit(p, `Brett ${w}x${h}`);
  await p.screenshot({ path: `${out}/06-size-${w}x${h}.png` });
}

const m = await page(390, 844, 'mobile');
await m.locator('.tb-button', { hasText: 'Neu' }).click();
await m.waitForTimeout(300);
await checkFit(m, 'Handy 390x844');
await m.screenshot({ path: `${out}/07-mobile.png` });
await m.locator('.tb-button', { hasText: 'Mehr' }).click();
await m.waitForTimeout(150);
await m.screenshot({ path: `${out}/08-mobile-more.png` });
await m.setViewportSize({ width: 844, height: 390 });
await m.waitForTimeout(250);
await checkFit(m, 'Handy quer 844x390');
await m.locator('.tb-button[title="Mehr"]').click();
await m.waitForTimeout(150);
await m.screenshot({ path: `${out}/09-mobile-landscape.png` });

await browser.close();
console.log(errors.length ? `FEHLER:\n${errors.join('\n')}` : 'keine Konsolenfehler');
if (errors.length) process.exitCode = 1;
