// Kurzhilfe, gekürzt aus der Originalhilfe ERGWIN.HLP (assets/original/ERGWIN.redacted-personal-info.txt).
import { useState } from 'preact/hooks';
import { Button, Dialog, Group } from './bwcc';

export type HelpTopic = 'rules' | 'options' | 'controls';

const TOPICS: Record<HelpTopic, { title: string; body: preact.JSX.Element }> = {
  rules: {
    title: 'Spielregeln',
    body: (
      <>
        <p>Wer zuerst alle vier Figuren im Ziel hat, gewinnt. Die anderen spielen um die weiteren Plätze weiter.</p>
        <h3>Raussetzen</h3>
        <p>Mit einer <b>6</b> kommt eine Figur aus dem Haus auf das Startfeld – das ist Pflicht, solange noch Figuren im Haus sind.
          Sind alle Figuren im Haus (oder lückenlos hinten im Ziel), gibt es <b>drei Versuche</b>.
          Das eigene Startfeld muss geräumt werden, solange noch Figuren im Haus warten.</p>
        <h3>Ziehen</h3>
        <p>Figuren laufen im Uhrzeigersinn so viele Felder, wie der Würfel zeigt. Nach einer 6 wird nochmal gewürfelt.
          Steht auf dem Zielfeld eine fremde Figur, wird sie <b>rausgeworfen</b> und muss zurück ins Haus.
          Auf eigene Figuren darf man nicht ziehen; überspringen ist erlaubt.</p>
        <h3>Ziel</h3>
        <p>Nach einer Runde geht es ins Ziel. Dort darf keine Figur übersprungen werden.</p>
      </>
    ),
  },
  options: {
    title: 'Einstellbare Regeln',
    body: (
      <>
        <dl>
          <dt>Ruhefelder</dt><dd>Auf den grauen Feldern kann niemand rausgeworfen werden.</dd>
          <dt>Schmeisszwang</dt><dd>Wer schlagen kann, muss schlagen. Raussetzen und Startfeld räumen gehen vor.</dd>
          <dt>… knallhart</dt><dd>Schlagen geht auch vor dem Räumen des Startfelds, nur Raussetzen nicht.</dd>
          <dt>… absolut</dt><dd>Schlagen geht vor allem anderen.</dd>
          <dt>kein Selbstüberspringen</dt><dd>Eigene Figuren dürfen nirgends übersprungen werden.</dd>
          <dt>bei 1 raussetzen / nochmal würfeln</dt><dd>Die 1 zählt dafür wie eine 6.</dd>
        </dl>
        <p class="dim">Neu: Zusatzwurf nach 6 abschaltbar, nach drei 6en Schluss, Raussetzen und Startfeld räumen
          als Pflicht abschaltbar, Spielende beim ersten Sieger. „Original“ stellt die ursprünglichen Regeln wieder her.</p>
        <p class="dim">Änderungen während einer Partie gelten ab dem nächsten Spielerwechsel.</p>
      </>
    ),
  },
  controls: {
    title: 'Bedienung',
    body: (
      <>
        <dl>
          <dt>Würfeln</dt><dd>Aufs Brett oder den Würfel klicken/tippen, oder Leertaste / Enter.</dd>
          <dt>Ziehen</dt><dd>Eine hervorgehobene Figur oder ihr Zielfeld anklicken. Tastatur: Tab wählt eine Figur, Enter zieht.</dd>
          <dt>Rückgängig</dt><dd>Strg+Z oder „Zurück“: nimmt zuerst den letzten Figurenzug zurück (der Wurf bleibt), beim nächsten Mal auch den Wurf – beliebig oft, bis zum Spielanfang, auch während die Computer ziehen.</dd>
          <dt>Speichern</dt><dd>Das Spiel wird automatisch gesichert und beim nächsten Öffnen fortgesetzt.
            Wird eine Partie durch ein neues Spiel oder Laden ersetzt, landet sie automatisch unter „Laden“ (die letzten fünf). „Speichern“ legt zusätzlich einen benannten Spielstand in der App ab, „Laden“ holt ihn zurück. Dateien (.ERG wie im Original) gibt es dort unter Exportieren/Importieren.</dd>
          <dt>Tasten</dt><dd>F1 Hilfe · F2 Neues Spiel · Strg+S speichern · Strg+O laden</dd>
        </dl>
      </>
    ),
  },
};

export function HelpDialog({ topic: initial = 'rules', close }: { topic?: HelpTopic; close: () => void }) {
  const [topic, setTopic] = useState<HelpTopic>(initial);
  return (
    <Dialog title="Hilfe" onCancel={close} class="help" buttons={<Button kind="ok" isDefault onClick={close} />}>
      <div class="tabs" role="tablist">
        {(Object.keys(TOPICS) as HelpTopic[]).map(t => (
          <button type="button" key={t} role="tab" aria-selected={t === topic} class={`tab${t === topic ? ' on' : ''}`} onClick={() => setTopic(t)}>{TOPICS[t].title}</button>
        ))}
      </div>
      <Group class="help-body">{TOPICS[topic].body}</Group>
    </Dialog>
  );
}
