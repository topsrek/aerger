// Ablauftest im echten Browser: komplette Computerpartie (Turbo), Rückgängig, Autosave.
//   node test/e2e/play.mjs <url>
import { chromium } from 'playwright-core';
const url = process.argv[2] ?? 'http://localhost:4173/';
const browser = await chromium.launch({ executablePath: process.env.CHROME ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
const errors = [];
const fail = (msg) => { console.error('FEHLER:', msg); process.exitCode = 1; };
const ctx = await browser.newContext({ viewport: { width: 1100, height: 760 } });
const p = await ctx.newPage();
p.on('pageerror', e => errors.push(e.message));
p.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
const settings = (players, extra = {}) => ({ players: players.map((kind, i) => ({ name: `S${i + 1}`, kind, strength: ['easy', 'normal', 'hard', 'normal'][i % 4] })), speedComputer: 3, speedHuman: 3, ...extra });
async function fresh(s) {
  await p.goto(url);
  await p.evaluate(s => { localStorage.clear(); localStorage.setItem('aerger.settings.v1', JSON.stringify(s)); }, s);
  await p.reload();
  await p.waitForSelector('.stage');
  await p.waitForTimeout(200);   // Tastatur-Handler hängen sich nach dem ersten Zeichnen an
}

// 1) Vier Computer, Turbo: Partie läuft bis zum Gewinnerdialog.
await fresh(settings([1, 1, 1, 1, 2, 2]));
await p.keyboard.press('F2');
const t0 = Date.now();
await p.waitForSelector('.bw-dialog[aria-label="Gewinner"]', { timeout: 120_000 }).catch(() => fail('kein Gewinner nach 120 s'));
console.log(`erster Sieger nach ${((Date.now() - t0) / 1000).toFixed(1)} s`);
for (let k = 0; k < 4 && await p.locator('.bw-dialog[aria-label="Gewinner"]').count(); k++) {
  await p.locator('.bw-dialog button', { hasText: 'OK' }).click();
  await p.waitForTimeout(50);
  await p.waitForSelector('.bw-dialog[aria-label="Gewinner"]', { timeout: 120_000 }).catch(() => {});
}
const stats = await p.evaluate(() => JSON.parse(localStorage.getItem('aerger.stats.v1') ?? '{}'));
const games = Object.values(stats).reduce((n, s) => n + s.games, 0);
if (games !== 4) fail(`Statistik zählt ${games} statt 4 Teilnahmen`); else console.log('Statistik ok:', JSON.stringify(Object.fromEntries(Object.entries(stats).map(([k, v]) => [k, `${v.wins}/${v.games}`]))));
if (await p.locator('.stage .piece').count()) fail('nach Spielende sollte das Titelbild erscheinen');

// 2) Mensch gegen Computer: würfeln, ziehen, rückgängig, Autosave nach Reload.
await fresh(settings([0, 1, 2, 2, 2, 2], { speedHuman: 3, speedComputer: 3 }));
await p.keyboard.press('F2');
let moved = false;
for (let i = 0; i < 200 && !moved; i++) {
  await p.waitForFunction(() => document.querySelector('.stage')?.classList.contains('cursor-dice') || document.querySelector('.piece.movable'), null, { timeout: 10_000 });
  if (await p.locator('.piece.movable').count()) {
    const before = await p.evaluate(() => localStorage.getItem('aerger.save.v1'));
    await p.locator('.piece.movable').first().click();
    await p.waitForTimeout(150);
    await p.keyboard.press('Control+z');
    await p.waitForTimeout(150);
    const after = JSON.parse(await p.evaluate(() => localStorage.getItem('aerger.save.v1')));
    const b = JSON.parse(before);
    if (JSON.stringify(after.game.pieces) !== JSON.stringify(b.game.pieces) || !after.game.awaitingMove || after.game.die !== b.game.die) fail('1. Zurück: Zug nicht zurückgenommen oder Wurf weg');
    else console.log('1. Zurück ok (Zug weg, Wurf', after.game.die, 'bleibt)');
    await p.keyboard.press('Control+z');
    await p.waitForTimeout(150);
    const after2 = JSON.parse(await p.evaluate(() => localStorage.getItem('aerger.save.v1')));
    if (after2.game.awaitingMove) fail('2. Zurück: Wurf nicht zurückgenommen'); else console.log('2. Zurück ok (Wurf weg, neu würfeln)');
    moved = true;
  } else await p.keyboard.press('Space');
}
if (!moved) fail('keine Zugmöglichkeit erreicht');
const saved = await p.evaluate(() => localStorage.getItem('aerger.save.v1'));
await p.reload();
await p.waitForSelector('.stage .piece');
await p.waitForTimeout(300);
const pieces = await p.locator('.stage .piece').count();
const again = await p.evaluate(() => localStorage.getItem('aerger.save.v1'));
if (pieces !== 8 || JSON.parse(again).game.pieces.toString() !== JSON.parse(saved).game.pieces.toString()) fail(`Autosave: nach Reload ${pieces} Figuren`);
else console.log('Autosave ok');

// 2a) Rückgängig gilt auch während der Computerzüge (bis der nächste Mensch dran ist).
await fresh(settings([0, 1, 1, 1, 2, 2], { speedHuman: 3, speedComputer: 1 }));
await p.keyboard.press('F2');
const during = await p.evaluate(async () => {
  const c = window.aerger, sleep = ms => new Promise(r => setTimeout(r, ms));
  for (let i = 0; i < 2400; i++) {
    await sleep(50);
    if (!c.humanTurn) continue;
    if (!c.game.awaitingMove) { c.rollRequest(); continue; }
    const m = c.humanMoves;
    if (!m.length) continue;
    if (c.game.die === 6) { c.moveRequest(m[0]); await sleep(100); continue; }   // nach einer 6 bleibt man dran
    const before = JSON.stringify(c.game.snapshot().pieces);
    c.moveRequest(m[0]);
    for (let k = 0; k < 100 && c.game.player === 0; k++) await sleep(20);
    const can = c.canUndo;
    c.undo();
    return { can, back: JSON.stringify(c.game.snapshot().pieces) === before && c.game.player === 0 };
  }
  return 'kein Zug';
});
if (typeof during === 'string') console.log('Rückgängig im Computerzug übersprungen:', during);
else if (!during.can || !during.back) fail(`Rückgängig im Computerzug: ${JSON.stringify(during)}`);
else console.log('Rückgängig im Computerzug ok');

// 2b) Nach einem Mausklick in der Leiste würfelt die Leertaste (und löst nicht den Knopf erneut aus);
//     Bestätigungsdialoge haben den Fokus auf dem Standardknopf.
await fresh(settings([0, 1, 1, 1, 2, 2], { speedHuman: 3 }));
await p.click('.tb-button.primary');
await p.keyboard.press('Space');
await p.waitForTimeout(300);
const afterSpace = await p.evaluate(() => ({ dialog: !!document.querySelector('.bw-dialog'), rolled: window.aerger.game.awaitingMove || window.aerger.game.attempts > 0 || window.aerger.game.player !== 0 }));
if (afterSpace.dialog || !afterSpace.rolled) fail(`Leertaste nach Leistenklick: ${JSON.stringify(afterSpace)}`); else console.log('Leertaste nach Leistenklick ok');
await p.click('.tb-button.primary');
await p.waitForSelector('.bw-dialog');
const focused = await p.evaluate(() => document.activeElement.textContent);
if (focused !== 'Ja') fail(`Fokus im Bestätigungsdialog auf "${focused}"`); else console.log('Dialogfokus ok');
await p.keyboard.press('Escape');

// 2c) Brettgröße per Griff an der Ecke: kleiner ziehen, bleibt nach Neuladen, Doppelklick = ganz.
const width = async () => p.evaluate(() => document.querySelector('.stage').getBoundingClientRect().width);
const full = await width();
const grip = await p.locator('.resize-grip').boundingBox();
await p.mouse.move(grip.x + 10, grip.y + 10); await p.mouse.down();
await p.mouse.move(grip.x - 150, grip.y - 150, { steps: 5 }); await p.mouse.up();
const small = await width();
await p.reload(); await p.waitForSelector('.stage'); await p.waitForTimeout(200);
const kept = await width();
await p.locator('.resize-grip').dblclick(); await p.waitForTimeout(100);
const back = await width();
if (!(small < full * 0.8) || Math.abs(kept - small) > 2 || Math.abs(back - full) > 2) fail(`Brettgröße: ${full} → ${small} → ${kept} → ${back}`);
else console.log(`Brettgröße ok (${Math.round(full)} → ${Math.round(small)} px)`);

// 2d) Seitenleiste einklappen: nur Symbole, Brett wird breiter, Zustand bleibt gespeichert.
const barWidth = () => p.evaluate(() => document.querySelector('.toolbar').getBoundingClientRect().width);
const open = await barWidth();
await p.locator('.tb-button', { hasText: 'Einklappen' }).click();
const shut = await barWidth();
await p.reload(); await p.waitForSelector('.toolbar'); await p.waitForTimeout(200);
const keptShut = await barWidth();
await p.locator('.tb-button[title="Leiste aufklappen"]').click();
if (!(shut < open / 2) || Math.abs(keptShut - shut) > 1 || Math.abs(await barWidth() - open) > 1) fail(`Einklappen: ${open} → ${shut} → ${keptShut}`);
else console.log(`Einklappen ok (${Math.round(open)} → ${Math.round(shut)} px)`);

// 2e) Spielstand in der App speichern und wieder laden; Rangliste von Hand ohne „Ende“.
await fresh(settings([0, 1, 1, 1, 2, 2], { speedHuman: 3, speedComputer: 3 }));
await p.keyboard.press('F2');
await p.waitForTimeout(300);
await p.evaluate(() => window.aerger.rollRequest());   // ein Wurf: die Partie hat begonnen
await p.waitForTimeout(300);
const savedPieces = await p.evaluate(() => JSON.stringify(window.aerger.game.snapshot()));
await p.keyboard.press('Control+s');
await p.waitForSelector('.bw-dialog.saves input');
await p.locator('.bw-dialog.saves input').fill('Testspiel');
await p.keyboard.press('Enter');
await p.waitForTimeout(150);
await p.keyboard.press('F2'); await p.locator('.bw-dialog button', { hasText: 'Ja' }).click();
await p.waitForTimeout(800);
await p.keyboard.press('Control+o');
await p.waitForSelector('.slot');
const slotName = await p.locator('.slot .slot-name', { hasText: 'Testspiel' }).first().textContent();
await p.locator('.slot', { hasText: 'Testspiel' }).click();
await p.locator('.bw-buttons button', { hasText: 'Laden' }).click();
await p.waitForTimeout(150);
const restored = await p.evaluate(() => JSON.stringify(window.aerger.game.snapshot()));
const same = JSON.parse(restored).pieces.toString() === JSON.parse(savedPieces).pieces.toString();
if (slotName !== 'Testspiel' || !same) fail(`Spielstand in der App: ${slotName}, gleich=${same}`); else console.log('Spielstand in der App ok');
const autos = await p.evaluate(() => JSON.parse(localStorage.getItem('aerger.slots.v1')).filter(s => s.auto).length);
if (autos < 1) fail('Beim Ersetzen der Partie wurde nichts automatisch gesichert'); else console.log(`Auto-Sicherung ok (${autos})`);
await p.locator('.tb-button', { hasText: 'Rangliste' }).click();
await p.waitForSelector('.bw-dialog.winners');
const endButtons = await p.locator('.bw-dialog.winners button', { hasText: 'Ende' }).count();
if (endButtons) fail('Rangliste von Hand zeigt „Ende“'); else console.log('Rangliste ohne Ende ok');
await p.keyboard.press('Escape');

// 3) Turbo, sobald alle Menschen im Ziel sind (Einstellung „fastWithoutHumans“).
await fresh(settings([0, 1, 1, 1, 2, 2], { speedHuman: 1, speedComputer: 1 }));
await p.keyboard.press('F2');
const turbo = await p.evaluate(() => {
  const c = window.aerger, before = c.speedOf(1);
  c.game.winners.push(0);
  const after = c.speedOf(1);
  c.updateSettings({ fastWithoutHumans: false });
  return [before, after, c.speedOf(1)];
});
if (turbo.join() !== '1,3,1') fail(`Turbo ohne Menschen: ${turbo}`); else console.log('Turbo ohne Menschen ok');

await browser.close();
if (errors.length) fail(`Konsolenfehler:\n${errors.join('\n')}`);
if (!process.exitCode) console.log('E2E OK');
