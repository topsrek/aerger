# Modernisierung

Entscheidungen (2026-11-04):

- Kein Online-/LAN-Mehrspieler.

- Web-Neubau in TypeScript (Preact, Vite), Server in Node.
- Look wie bisher, aber als Vektorgrafik; Dialogelemente im BWCC-Stil.
- Die originalgetreue Windows-Portierung (C#/WinForms) ist eingefroren und liegt in `windows/`.

## Erledigt

- [x] Spielkern portiert, alle Regeltests übernommen (inkl. Differenztest gegen seg13).
- [x] SVG-Brett (4/6 Spieler), Figuren, Würfel, Titelbild, Spielerfeld; Quer-/Hochformat.
- [x] Klänge per Web Audio, nicht blockierend, Lautstärke.
- [x] Autosave, Rückgängig, Spielstände .ERG/.JSON.
- [x] Mögliche Züge hervorheben, Farbenblind-Modus, Touch.
- [x] Computerstärken leicht/normal/schwer, Zusatzregeln, Statistik.
- [x] Kurzhilfe als Modal.
- [x] Dockerfile + Node-Server; GitHub Pages.
- [x] Menüleiste ersetzt durch Werkzeugleiste (Rand, niedrige Fenster nur Symbole, Hochformat unten
      mit „Mehr“); Spielfläche wird in Pixeln ins Fenster eingepasst, kein Scrollbalken mehr.
- [x] Info-Dialog nennt den Autor des Originals und die Entstehung der Web-Fassung.
- [x] Längere Pausen zwischen Computerzügen; Turbo, sobald alle Menschen im Ziel sind (abschaltbar).
- [x] Neben dem Brett: Statuszeile (würfelt/zieht/überlegt) und Startversuche „Versuch 2 von 3“.
      (Stand und Verlauf waren kurz drin, dem Nutzer zu viel Information – wieder entfernt.)
- [x] Geschlagene Figuren fliegen zurück ins Haus; Rückgängig: erst den Figurenzug (Wurf bleibt), dann den Wurf; beliebig oft bis zum Spielanfang.
- [x] Spielstände mit Namen in der App (Browser-Speicher); Datei-Export/-Import nur noch als Zusatz.
- [x] Auto-Sicherung: wird eine Partie ersetzt (Neu, Laden, Beenden), landet sie unter „Laden“ (letzte 5); „Weiterspielen“ auf dem Titelbild.
- [x] Menschen hören im Turbo alle Klänge; Computer im Turbo bleiben still.
- [x] Leiste und Dialoge wie die BWCC-Elemente des Originals (ein moderner Mischstil wurde
      ausprobiert und verworfen); Seitenleiste einklappbar.
- [x] Brettgröße im Tab per Griff an der Brettecke oder Regler; Titel-Schriftzug schwarzgrau.

## Offen / Ideen

- „Schwer“ weiter verbessern (heute ~28 % Siege gegen drei Original-Gegner statt 25 %);
  Monte-Carlo-Vorausschau wäre der nächste Schritt.
- PWA-Installation braucht HTTPS (auf GitHub Pages gegeben).
