# Ärger: native Neuimplementierung für Windows

Windows-10/11-Anwendung in C# / Windows Forms, mit den originalen Grafik- und Soundarchiven aus
`../original`. Sieht aus und spielt sich wie das Original von 1995 – ohne Shareware-Abfrage.
Kein Java, kein Emulator und kein separat installiertes .NET für das fertige Paket.

**Fertig gebaut:** [Aerger-Windows.zip](https://github.com/topsrek/aerger/releases/latest)
herunterladen, entpacken, `Aerger.exe` starten (`Aerger.exe` und `Data` zusammenhalten).

Selbst bauen: .NET-8-SDK installieren und in diesem Ordner `powershell -File ./build.ps1`
ausführen; das Paket landet in `windows/release`. Alle Pfade unten gelten relativ zu `windows/`.

Diese Fassung ist eingefroren; weiterentwickelt wird die [Web-Fassung](../README.md). Die im Text
genannten Werkzeuge unter `research/` (Disassembly, x86-Emulator, Extraktionsskripte) dienten dem
Reverse Engineering von `AERGER.EXE` und sind nicht Teil dieses Repositorys; ihre Ergebnisse
stecken in den Tests und in `src/Aerger/Reference`.

## Start

`Aerger.exe` doppelklicken.
Über Spiel → Neues Spiel starten. Würfeln per Mausklick (linke oder rechte Taste),
Leertaste oder Enter. Der Mauszeiger zeigt wie im Original Würfel, Figur oder Maus.
Eine Figur anklicken oder per Tab auswählen und mit Enter/Leertaste ziehen.
Spielertypen und Namen werden über Spiel → Spieler eingestellt.
Die Hilfe öffnet sich über Hilfe → Inhalt oder F1. Links stehen die Themen;
das Suchfeld durchsucht Titel und Texte. Grüne unterstrichene Querverweise öffnen
verwandte Themen per Klick oder Tab/Enter. Zurück (Alt+Links) führt zum vorherigen Thema, Strg+F zur
Suche und Escape schließt die Hilfe.
Neue Spiele beginnen mit Spieler 1; ist dieser inaktiv, beginnt der erste aktive
Spieler. Geladene Spielstände behalten ihren gespeicherten Start-/Zugzustand.
Die Brettgrößen 200, 400 und 700 Pixel lassen sich über Optionen → Fenstergröße
oder durch Ziehen an einer Fensterecke einstellen. Nach dem Loslassen wird die
größte Stufe gewählt, die in beide Dimensionen passt. Maximieren wählt die größte
Stufe im Bildschirmarbeitsbereich. Minimieren und Wiederherstellen erhalten sie.

## Stand

- Originalbretter für vier/sechs Spieler, drei feste Brettgrößen und native Menüs.
- Originale Fenster-Innenmaße mit größenabhängiger Seitenfläche; Größenwahl über
  Fensterrahmen und Maximieren, zentriertes Titelbild in allen drei Größen.
- Regeln aus der Originalhilfe einschließlich der drei Schmeißzwang-Varianten;
  die Zugprüfung ist gegen die übersetzte Originalroutine (seg13:1140) abgeglichen.
  Bewusste Abweichung: Ein Fehler des Originals (Scheinschlag einer Figur auf ihrem
  letzten Bahnfeld bei Schmeißzwang, seg13:0e32) wird nicht nachgebildet.
- Drei Startversuche entsprechend der Originalroutine: bereits angekommene
  Figuren müssen die hintersten Zielfelder lückenlos belegen. Bei einer Lücke
  im Ziel beendet ein nicht spielbarer hoher Wurf den Zug sofort.
  Nach dem dritten erfolglosen Startversuch verlängert „Nochmal bei 1“ den
  Zug nicht, wenn „Raussetzen mit 1“ ausgeschaltet ist.
- Originale ERG-Spielstände laden/speichern; Data/BEISPIEL.ERG liegt bei.
- Originalkonfiguration lesen/speichern, Sounds, Würfel- und Schrittanimationen.
- Computerbewertung aus der Originalroutine übersetzt und Befehl für Befehl gegengeprüft;
  Start-, Ziel- und Ruhefelder stammen aus den Originaltabellen (Ruhefelder auf
  dem Sechserbrett: Felder 4, 12, 20, 28, 36, 44).
- Originalgrafiken der Borland-Dialogknöpfe und Auswahlfelder.
- Funktionsfähiger Nein-Knopf bei Neustart/Spielende/Schließen, Escape zum
  Abbrechen und getrennte Radioauswahl per Pfeiltasten in den Dialogen.
- Spielerfeld mit Originalmaßen, Spielerfigur und separatem Würfel darunter,
  passend zu allen drei Brettgrößen; lange Namen bleiben innerhalb des Felds.
- Zusätzlicher Abschlusston nur bei Schlagzügen, nicht nach jedem normalen Zug.
- Schlagton vor dem ersten Schritt, getrennte Tempogrenzen für Laufweg-/Hof-
  Schlagzüge und Rückkehr in den Hof erst nach Ende des Rückkehrtons.
- Bei schnellem Schlagzug endet auch der letzte Schrittton vor der Rückkehr
  in den Hof; die Würfelansage wartet auf den laufenden Ton.
- Wie im Original wirken Änderungen an Spielern, Spielregeln und Spielbrett während
  einer Partie erst beim nächsten Spielerwechsel (Meldung „Die Änderungen werden
  nach dem nächsten Zug wirksam.“); am Spielende werden sie sofort übernommen.
  Ein dann unmöglicher Wechsel aufs Viererbrett wird beim Spielerwechsel gemeldet.
- Zugabgleich nach Laden: keine Übernahme alter Würfe durch inaktive Spieler;
  unspielbare Zugauswahl wird aufgelöst.
- Brett, Figuren, Ruhefelder und Mausklick-Erkennung nach den Original-Koordinatentabellen
  aller drei Brettgrößen; Figuren im Hof mit traurigem Gesicht wie im Original.
- Originale Mauszeiger je nach Spielzustand; Mausklicks wirken wie im Original beim
  Drücken der linken oder rechten Taste.
- Tab-Auswahl wie im Original: Die zuletzt gewählte Figur bleibt gemerkt (bis zu einem
  neuen Spiel), der schwarze Rahmen hat Feldgröße und sitzt an der Figurenposition.
- Schlagen wie im Original: Die geschlagene Figur schaut traurig, bis der Angreifer ankommt;
  die ziehende Figur liegt über überquerten Figuren. Der Würfel verschwindet nach dem Zug
  und beim Spielerwechsel.
- Computergegner geprüft gegen den Originalcode: `research/emulate_ai.py` führt Zugprüfung und
  Zugwahl aus AERGER.EXE in einem x86-Emulator aus; 200.000 Entscheidungen stimmen überein.
- Würfelauswertung und Züge ebenso geprüft (`research/emulate_turns.py`, 200.000 Übergänge);
  verbleibende Unterschiede sind dokumentierte Fehler des Originals.
- Vorgemerkte Spieler-, Regel- und Brettänderungen ebenso geprüft (200.000 Übergänge). Wo das
  Original abstürzt (Spieler mit Figur im Ziel deaktiviert) oder hängen bleibt (kein Spieler
  mehr am Zug), läuft die Portierung weiter bzw. beendet die Partie mit der Rangliste.
- Wartezeiten auf Klänge wie im Original: Es wartet nicht auf das Ende der WAV-Datei, sondern
  nach einer festen Dauertabelle; Schritt-, Hof-, Gewinner- und Infoklang blockieren nie.
- Würfel mit dem Zufallsgenerator des Originals (Turbo Pascal); Wartezeiten und Animationen
  laufen bei gleichem Startwert Ereignis für Ereignis wie im Original (Zeitachsen aus
  `research/emulate_timeline.py`, geprüft im UI-Test).
- Dialoge in der System-Rasterschrift des Originals (Textbreiten pixelgenau wie im
  Original-Screenshot), ohne Schließen-Knopf; drei Meldungen ohne Titelleiste wie im Original.
- Dialogtexte passen vollständig in ihre Originalfelder; zentrierte Texte und
  wörtliche „&“-Zeichen entsprechen den Stilangaben der Originalressourcen.
- Deaktivierte Spieler geben ihre Bahn- und Zielfelder frei; nach Reaktivierung
  beginnen ihre Figuren wieder im Hof. Mensch/Computer-Wechsel und Abbrechen
  erhalten die Figuren. Originalspielstände mit Feldwert 0 für entfernte
  Figuren können geladen und gespeichert werden.
- Keine Shareware-Abfrage und kein „immer im Vordergrund“.
- Native Hilfenavigation mit Originalthemen, Volltextsuche, Querverweisen und
  Verlauf; historische Hilfetexte bleiben unverändert.

Noch keine abgeschlossene 1:1-Portierung. Offen sind insbesondere die exakte
historische Rahmendarstellung, alle Dialogdetails, weitere Verhaltensvergleiche
und die exakte Darstellung der ursprünglichen Hilfe.
Freies Skalieren, Lautstärkeregler und zusätzliche Tempooptionen kommen erst später.

## Prüfung

`powershell -File ./build.ps1` führt Regel-/Dateiformattests aus, veröffentlicht
das eigenständige x64-Paket nach release und prüft dessen Start samt Originaldaten.
Das Paket besteht danach nur aus `Aerger.exe` und `Data`; Symboldatei und die von
WinForms nicht benötigten WPF-Bibliotheken (`*_cor3.dll`) werden entfernt.

Die automatischen EXE-Prüfungen laufen in einem separaten Prozess auf einem
eigenen, unsichtbaren Windows-Desktop. Dadurch überdecken andere Anwendungen
keine Testfenster und normale Tastatureingaben gelangen nicht in den Test.
Der sichtbare Desktop wird nicht umgeschaltet. Fehlercode und Fehlermeldungen
werden an den aufrufenden Prozess weitergegeben; ein Testlauf hat höchstens
zwei Minuten Zeit. Screenshots und Protokolle liegen in `release/test-output`.

`release/Aerger.exe --help-test` prüft die Hilfenavigation separat, einschließlich
echter Mausnachrichten an unverdeckte Textstellen und Tastaturbedienung sowie
des Erhalts eines ausstehenden Zuges. Der vollständige UI-Test enthält dieselbe
Hilfeprüfung.

`release/Aerger.exe --timeline-test` spielt 13 Computerpartien (alle Tempi, mit und ohne
Ton) und vergleicht sie Ereignis für Ereignis mit Zeitachsen aus dem emulierten Original
(`research/emulate_timeline.py`); Dauer etwa 2 Minuten.

`release/Aerger.exe --ui-test` prüft Tastatursteuerung, Animationen, Menü,
Topmost-Status, Originalbeispiel, Dialogdarstellung und Beenden. Zusätzliche Fälle
prüfen vorgemerkte Regel-, Spieler- und Brettänderungen bis zum Spielerwechsel,
Dialogabbruch, Spieler deaktivieren/reaktivieren und Laden mit inaktivem Spieler.
Außerdem werden Startspieler, Soundauslöser und Spielerfeld/Würfel für beide
Bretter in allen drei Größen geprüft. Hilfeprüfungen decken Menü/F1,
Themenkennungen, Querverweisziele, Umlaute, Volltextsuche, leere Ergebnisse,
Verlauf und unveränderten Spielstand ab. Testbilder und Protokolle werden in
`release/test-output` erzeugt; Testtöne sind stummgeschaltet.
Fensterprüfungen decken die Innenmaße, Rahmen-Größenänderung, Maximieren,
Minimieren/Wiederherstellen und den Erhalt eines noch ausstehenden Zuges ab.
Animationstests prüfen Tonreihenfolgen, Zieleinlauf, verdeckte/heimkehrende
Figuren und die vier Zwischenschritte eines Rundlaufs auf dem Sechserbrett.
Zeitprüfungen decken das Tonende vor der Hofrückkehr und vor der Würfelansage
sowie die Wartezeit bei unterdrückter Ansage in den schnellen Tempostufen ab.
`release/Aerger.exe --dialog-test` prüft die Dialoge separat: Nein/Bestätigen,
Escape/Enter, Leertaste, Pfeiltasten, Tab/Umschalt+Tab und getrennte Spieler-
und Geschwindigkeitsgruppen. Er prüft außerdem, dass alle Texte aller Originaldialoge
in ihre Felder passen, und speichert je Dialog ein Bild (`dialog-<Name>.png`).
Der vollständige UI-Test enthält diese Fälle ebenfalls.
Während der nichtmodalen Steuerungsprüfungen bleibt das Spielbrett inaktiv.
Gezielte Leertasten-/Enter-Eingaben prüfen, dass keine Hintergrundanimation
startet und der Spielstand unverändert bleibt.

Der ERG-Rücktest durch das Originalprogramm hat für eine exportierte Beispielpartie
alle 331 Bytes unverändert zurückgegeben. Das belegt diesen Dateiformatfall,
nicht die vollständige Originaltreue.
Die Referenzprogramme unter research sind Entwicklungswerkzeuge und gehören nicht
zum nativen Paket.
