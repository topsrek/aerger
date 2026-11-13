# Mensch ärgere Dich nicht!

Das Windows-Spiel „Mensch ärgere Dich nicht!“ von Oliver Kraheck (1995), neu für den Browser:
gleiche Regeln, gleicher Computergegner, gleiche Klänge, gleicher Look – aber als stufenlos
skalierbare Vektorgrafik, mit Touch-Bedienung und vielen Komfortfunktionen.

**▶ Jetzt spielen: <https://topsrek.github.io/aerger/>**

Läuft im Browser auf PC, Tablet und Handy, lässt sich als App installieren und funktioniert
danach auch ohne Netz.

Ganz ohne Internet: **[Aerger.html](https://github.com/topsrek/aerger/releases/latest)**
herunterladen und doppelklicken – das ganze Spiel in einer einzigen Datei, samt Klängen.

## In English

This is a browser remake of a German Windows 3.1 shareware Ludo game (“Mensch ärgere Dich nicht!”,
Oliver Kraheck, 1995). The original is a 16-bit program that no longer runs on current Windows,
and it can no longer be registered. My grandmother loves this exact game and kept a Windows XP
virtual machine around for years just to play it. In 2026 the game was reverse engineered and
rebuilt with AI (Claude by Anthropic): same rules, same computer opponent, same sounds, now in
any browser. The repository also contains the untouched original (`original/`) and a faithful
native port for Windows 10/11 (`windows/`). The game and the documentation are in German.
Play it at <https://topsrek.github.io/aerger/>.

## Die Geschichte

Meine Oma spielt dieses eine „Mensch ärgere Dich nicht!“ seit den Neunzigern am liebsten. Das
Original ist ein 16-Bit-Programm für Windows 3.1 und startet auf heutigen 64-Bit-Systemen gar
nicht mehr. Jahrelang lief es bei ihr deshalb in einem emulierten Windows XP – umständlich zu
starten, klein auf dem Bildschirm und jedes Mal mit den Shareware-Hinweisen.

Diese Hinweise lassen sich auch nicht mehr abstellen: Das Spiel war Shareware, die Vollversion
gab es gegen Registrierung beim Autor. Das ist seit vielen Jahren nicht mehr möglich. Auf meine
Anfrage per E-Mail hat der Autor keinen Schlüssel mehr ausgegeben, und auch auf seiner Website
stand, dass es keine Registrierung mehr gibt.

2026 ließ sich das Spiel mit heutigen KI-Werkzeugen aus der alten `AERGER.EXE` herauslösen:
Grafiken, Klänge, Hilfetexte, die Brettgeometrie, der Zufallsgenerator und vor allem die
Strategie des Computergegners. Daraus entstanden zuerst eine originalgetreue Windows-Fassung
und dann die Web-Fassung hier. Reverse Engineering, Portierung und Umbau hat vollständig
**Claude** (Anthropic) umgesetzt; ich habe gesagt, was ich haben will, und getestet. Jetzt
läuft das Spiel überall, füllt den ganzen Bildschirm und ist mit einem Klick gestartet.

## Was im Repository liegt

| Ordner | Inhalt |
| --- | --- |
| `/` (`src`, `public`, `server`, `test`) | Die Web-Fassung – das Hauptprojekt. |
| `original/` | Die unveränderte Shareware-Version von 1995 (`AERGER.EXE` samt Daten). |
| `windows/` | Originalgetreue Portierung für Windows 10/11 (C#/WinForms), Quelltext. |
| `assets/original/` | Aus dem Original herausgelöstes Material (Hilfetext, Mauszeiger, Titelbild). |

### Windows-Fassung

Fertig gebaut zum Herunterladen: **[Aerger-Windows.zip](https://github.com/topsrek/aerger/releases/latest)**
– entpacken, `Aerger.exe` starten, nichts installieren. Sie sieht aus und spielt sich wie das
Original, nur ohne Shareware-Hinweise. Selbst bauen: siehe [windows/README.md](windows/README.md).

### Das Original

`original/AERGER.EXE` braucht Windows 3.1 bis Windows XP (32 Bit) oder einen Emulator wie
[otvdm/winevdm](https://github.com/otya128/winevdm). Mehr dazu in [original/README.md](original/README.md).

## Neu gegenüber 1995

- Passt sich jeder Fenstergröße an (Quer- und Hochformat), Werkzeugleiste statt Menü, Touch,
  Tastatur wie im Original (Leertaste/Enter/Tab).
- Klänge laufen parallel und bremsen das Spiel nicht; Lautstärkeregler.
- Automatisches Speichern, „Weiterspielen“, Rückgängig bis zum Spielanfang.
- Mögliche Züge und Zielfelder hervorheben, Farbsymbole für Farbenblinde.
- Computerstärken leicht / normal (= Original) / schwer; ruhigere Pausen zwischen den Computerzügen,
  auf Wunsch Turbo, sobald alle Menschen im Ziel sind.
- Zusatzregeln (Standard = Original): Zusatzwurf nach 6, drei 6en, Raussetzpflicht,
  Startfeld räumen, Spielende beim ersten Sieger.
- Statusanzeige neben dem Brett; geschlagene Figuren fliegen ins Haus.
- Brettgröße per Griff an der Brettecke verstellbar.
- Statistik je Spielername. Kurzhilfe statt Hilfedatei.
- Spielstände mit Namen im Browser, dazu Export/Import im Original-Format `.ERG`
  (mit Zusatzregeln als JSON).

## Entwickeln

```sh
npm install
npm run dev       # Entwicklungsserver
npm test          # Regeltests (inkl. Abgleich mit der Originalroutine in 200 000 Stellungen)
npm run build     # dist/
npm start         # liefert dist/ auf Port 8080 aus (PORT überschreibbar)
npm run standalone # dist-standalone/Aerger.html: alles in einer Datei, läuft per Doppelklick
npm run e2e       # Ablauftest im lokalen Chrome gegen http://localhost:4173 (vorher: npx vite preview)
npm run shots     # Bildschirmfotos in mehreren Fenstergrößen, prüft auf Scrollbalken
npm run bench     # Stärkevergleich der Computergegner
```

Das gebaute Spiel in `dist/` ist rein statisch und läuft in jedem Unterverzeichnis; GitHub Pages
wird bei jedem Push auf `main` über `.github/workflows/pages.yml` aktualisiert.
Ein Release entsteht durch ein Tag: `git tag v1.0.1 && git push github v1.0.1` baut `Aerger.html` und
`Aerger-Windows.zip` und hängt beide an das neue Release (`.github/workflows/release.yml`).
Docker: `docker build -t aerger . && docker run -p 8080:8080 aerger`.

### Aufbau

- `src/core` – Spielregeln und Zugablauf (`game.ts`), Originalstrategie und neue Stärken
  (`strategy.ts`), Original-Zufallsgenerator, ERG-Spielstände, Brettgeometrie aus `AERGER.EXE`.
  Ohne DOM, voll getestet.
- `src/ui` – Preact-Oberfläche: `controller.ts` (Ablauf, Animation, Computerzüge, Rückgängig,
  Autosave, Statistik), `Stage.tsx` (SVG-Spielfläche, ins Fenster eingepasst), `Toolbar.tsx`
  (einklappbare Leiste am Rand bzw. unten), `svg/` (Brett, Würfel, Figuren),
  `bwcc.tsx` + `dialogs.tsx` (Dialoge im Stil der Borland-Controls), `sound.ts` (Web Audio).
- `test` – Regeltests (Vitest), `originalMoveCheck.ts` als Orakel (die übersetzte Zugprüfung
  des Originals), `e2e/` Browser-Tests.
- `server/server.mjs` – abhängigkeitsfreier Webserver für den Container.
- `public/sounds` – die 14 Originalklänge aus `SOUNDS.DAT`.

Der Verlauf der Umbauentscheidungen steht in [MODERNISIERUNG.md](MODERNISIERUNG.md).

## Rechte

Der neu geschriebene Quelltext steht unter der MIT-Lizenz: verwenden, verändern und weitergeben
ist erlaubt, auch kommerziell – siehe [LICENSE.md](LICENSE.md). Das Originalspiel, seine Klänge,
Grafiken und Texte stammen von Oliver Kraheck; Spielidee und Gestaltung des Brettspiels
liegen bei Schmidt Spiele. Für dieses Material kann ich keine Rechte vergeben. Die Anschrift, Telefonnummer und Bankverbindung des Autors aus der
Registrieranleitung von 1995 wurden aus den lesbaren Texten entfernt; das unveränderte
Original in `original/` enthält sie weiterhin, so wie es der Autor selbst verbreitet hat.

Dies ist ein privates Projekt ohne Gewinnabsicht, damit ein altes Spiel spielbar bleibt.
Wer Rechte an dem Material hat und es hier nicht sehen möchte, meldet sich bitte über ein
[Issue](https://github.com/topsrek/aerger/issues) – dann nehme ich es heraus.
