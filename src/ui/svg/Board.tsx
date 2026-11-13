// Spielbretter (GRAPH02), Würfel (GRAPH04) und Titelbild (GRAPH01) als Vektorgrafik im
// 700er-Koordinatensystem des Originals. Maße und Farben aus den Originalbitmaps vermessen.
import { memo } from 'preact/compat';
import { geometry } from '../../core/board';
import { BOARD_YELLOW, FRAME_RED, PLAYERS } from '../theme';
import { Symbol } from './Face';

type Pt = readonly [number, number];

/** Pfeil mit gefüllter Spitze und vier Federkerben am Ende (wie im Original). */
function Arrow({ tail, head }: { tail: Pt; head: Pt }) {
  const [x1, y1] = tail, [x2, y2] = head;
  const len = Math.hypot(x2 - x1, y2 - y1);
  const angle = (Math.atan2(y2 - y1, x2 - x1) * 180) / Math.PI;
  const fletch = [0, 4.5, 9, 13.5].map(x => `M${x} -3.5 l3.5 3.5 l-3.5 3.5`).join(' ');
  return (
    <g transform={`translate(${x1} ${y1}) rotate(${angle})`}>
      <path d={`M2 0 H${len - 4}`} stroke="#000" strokeWidth={1.6} />
      <path d={fletch} stroke="#000" strokeWidth={1.4} fill="none" />
      <path d={`M${len} 0 l-8 -4 l2 4 l-2 4z`} fill="#000" />
    </g>
  );
}

const ARROWS_FOUR: [Pt, Pt][] = [0, 1, 2, 3].map(p => {
  const rot = ([x, y]: Pt, k: number): Pt => k === 0 ? [x, y] : rot([700 - y, x], k - 1);
  return [rot([40, 242], p), rot([106, 242], p)];
});
const ARROWS_SIX: [Pt, Pt][] = [
  [[141, 124], [201, 164]], [[438, 57], [438, 122]], [[643, 261], [585, 301]],
  [[558, 565], [502, 521]], [[259, 633], [259, 568]], [[61, 423], [119, 389]],
];

function Frame() {
  return (
    <g>
      <rect x={0} y={0} width={700} height={700} fill={BOARD_YELLOW} />
      <rect x={3.5} y={3.5} width={693} height={693} fill="none" stroke={FRAME_RED} strokeWidth={7} />
      <rect x={12} y={12} width={676} height={676} fill="none" stroke="#000" strokeWidth={2} />
    </g>
  );
}

const TEXT = { fontFamily: '"Comic Neue", "Comic Sans MS", cursive', fontWeight: 700, fill: '#000' };

export interface BoardProps { players: 4 | 6; rest: boolean; colorblind: boolean; labels?: boolean }

/** Das leere Brett mit Bahn, Häusern, Zielen, Pfeilen und Schriftzug. */
export const BoardBackground = memo(function BoardBackground({ players, rest, colorblind, labels = true }: BoardProps) {
  const geo = geometry(players);
  const half = geo.fieldSize / 2;
  const trackR = players === 4 ? 25.5 : 20;
  const smallR = players === 4 ? 18 : 12;
  const stride = geo.track.length / players;
  const c = ([x, y]: Pt): Pt => [x + half, y + half];
  const lines = geo.track.map((p, i) => {
    const [x1, y1] = c(p), [x2, y2] = c(geo.track[(i + 1) % geo.track.length]);
    return `M${x1} ${y1}L${x2} ${y2}`;
  }).join('');
  const restOffset = players === 4 ? 4 : 3;
  return (
    <g>
      <Frame />
      <path d={lines} stroke="#000" strokeWidth={1.6} />
      {geo.track.map((p, i) => {
        const owner = i % stride === 0 ? i / stride : -1;
        const isRest = rest && i % stride === restOffset;
        const [x, y] = c(p);
        return (
          <g key={`t${i}`}>
            <circle cx={x} cy={y} r={trackR} fill={owner >= 0 ? PLAYERS[owner].fill : isRest ? '#c0c0c0' : '#fff'} stroke="#000" strokeWidth={2} />
            {colorblind && owner >= 0 && <g transform={`translate(${x} ${y})`} opacity={0.55}><Symbol player={owner} size={trackR} /></g>}
          </g>
        );
      })}
      {Array.from({ length: players }, (_, p) => [...geo.yard[p], ...geo.goal[p]].map((pt, k) => {
        const [x, y] = c(pt);
        return (
          <g key={`y${p}-${k}`}>
            <circle cx={x} cy={y} r={smallR} fill={PLAYERS[p].fill} stroke="#000" strokeWidth={2} />
            {colorblind && <g transform={`translate(${x} ${y})`} opacity={0.55}><Symbol player={p} size={smallR * 1.1} /></g>}
          </g>
        );
      }))}
      {(players === 4 ? ARROWS_FOUR : ARROWS_SIX).map(([t, h], i) => <Arrow key={`a${i}`} tail={t} head={h} />)}
      {!labels ? null : players === 4 ? (
        <g {...TEXT} fontSize={44}>
          <text x={91} y={215} textLength={144} lengthAdjust="spacingAndGlyphs">Mensch</text>
          <text x={486} y={215} textLength={123} lengthAdjust="spacingAndGlyphs">ärgere</text>
          <text x={124} y={510} textLength={77} lengthAdjust="spacingAndGlyphs">Dich</text>
          <text x={494} y={510} textLength={104} lengthAdjust="spacingAndGlyphs">nicht!</text>
        </g>
      ) : (
        <text {...TEXT} x={219} y={354} fontSize={20} textLength={265} lengthAdjust="spacingAndGlyphs">Mensch ärgere Dich nicht!</text>
      )}
    </g>
  );
});

/** Vier kleine Eckmarken eines Ruhefelds (DS:16d4), die über einer Figur sichtbar bleiben (seg17:06b1). */
export function RestCorners({ x, y, corners }: { x: number; y: number; corners: readonly Pt[] }) {
  return (
    <g fill="#c0c0c0" stroke="#000" strokeWidth={1}>
      {corners.map(([dx, dy]) => <circle key={`${dx}-${dy}`} cx={x + dx + 2.5} cy={y + dy + 2.5} r={2.6} />)}
    </g>
  );
}

const PIPS: Record<number, Pt[]> = {
  1: [[50, 50]], 2: [[27, 27], [73, 73]], 3: [[27, 27], [50, 50], [73, 73]],
  4: [[27, 27], [73, 27], [27, 73], [73, 73]], 5: [[27, 27], [73, 27], [50, 50], [27, 73], [73, 73]],
  6: [[27, 25], [73, 25], [27, 50], [73, 50], [27, 75], [73, 75]],
};

/** Würfel mit rautenförmigen Augen (GRAPH04), Koordinaten 0..100. */
export function Die({ value }: { value: number }) {
  return (
    <g>
      <rect x={3} y={3} width={94} height={94} rx={18} fill="#fff" stroke="#000" strokeWidth={5} />
      {PIPS[value]?.map(([x, y]) => <path key={`${x}-${y}`} d={`M${x} ${y - 8.5}l8.5 8.5l-8.5 8.5l-8.5 -8.5z`} fill="#000" />)}
    </g>
  );
}

/** Titelbild (nach GRAPH01): Viererbrett mit großem, plastischem Schriftzug in Schwarzgrau. */
export const Title = memo(function Title() {
  const word = (text: string, x: number, y: number, size: number, length: number) => (
    <g>
      <text x={x + 8} y={y + 8} fontSize={size} textLength={length} lengthAdjust="spacingAndGlyphs" fill="#000" opacity={0.22}>{text}</text>
      <text x={x} y={y} fontSize={size} textLength={length} lengthAdjust="spacingAndGlyphs" fill="url(#titleInk)" stroke="#ffff9a" strokeWidth={7} paintOrder="stroke">{text}</text>
      <text x={x} y={y} fontSize={size} textLength={length} lengthAdjust="spacingAndGlyphs" fill="none" stroke="#9a9a9a" strokeWidth={1.2} opacity={0.7} transform="translate(-2 -2)">{text}</text>
    </g>
  );
  return (
    <g>
      <defs>
        {/* Schwarzgrau statt Gold: auf dem gelben Brett deutlich besser lesbar. */}
        <linearGradient id="titleInk" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#5a5d63" /><stop offset="0.5" stopColor="#2f3136" /><stop offset="1" stopColor="#141518" />
        </linearGradient>
      </defs>
      <BoardBackground players={4} rest={false} colorblind={false} labels={false} />
      <g fontFamily='"Comic Neue", "Comic Sans MS", cursive' fontWeight={700}>
        {word('Mensch', 30, 118, 150, 615)}
        {word('ärgere', 55, 262, 150, 450)}
        {word('Dich', 290, 462, 150, 280)}
        {word('nicht!', 245, 680, 165, 430)}
      </g>
    </g>
  );
});
