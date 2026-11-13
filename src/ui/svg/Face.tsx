// Die zwölf Spielfiguren (GRAPH03) als Vektorgrafik: je Farbe ein fröhliches Gesicht
// (auf dem Brett) und ein trauriges (im Haus und beim Rauswurf). Koordinaten 0..100.
import type { JSX } from 'preact';
import { PLAYERS } from '../theme';

const ink = { stroke: '#000', strokeWidth: 3, fill: 'none', strokeLinecap: 'round', strokeLinejoin: 'round' } as const;

function Eye({ x, y, rx = 8, ry = 9, px = 0, py = 0, pr = 4.5, white = '#fff', pupil = '#000', outline = '#000' }:
  { x: number; y: number; rx?: number; ry?: number; px?: number; py?: number; pr?: number; white?: string; pupil?: string; outline?: string }) {
  return (
    <g>
      <ellipse cx={x} cy={y} rx={rx} ry={ry} fill={white} stroke={outline} strokeWidth={2.5} />
      <circle cx={x + px} cy={y + py} r={pr} fill={pupil} />
    </g>
  );
}

const features: Record<string, () => JSX.Element> = {
  // Gelb: verschmitzt mit herausgestreckter Zunge / erstaunt mit offenem Mund
  '0h': () => (
    <g>
      <Eye x={35} y={39} px={2} py={-1} pr={6.5} />
      <Eye x={66} y={39} px={2} py={-1} pr={6.5} />
      <path d="M30 31 q5 -4 10 0 M61 31 q5 -4 10 0" stroke="#ffd343" strokeWidth={4} fill="none" />
      <path d="M43 61 q-3 -8 7 -7 q10 -1 7 7 q-7 4 -14 0z" {...ink} strokeWidth={2.5} />
      <path d="M17 60 q4 4 4 1 q29 20 58 0 q0 3 4 -1" {...ink} strokeWidth={2.8} />
      <path d="M51 73 q7 1 8 7 q-5 6 -10 0z" fill="#ff2040" stroke="#000" strokeWidth={2} />
    </g>
  ),
  '0s': () => (
    <g>
      <path d="M27 24 q8 -8 15 0 M58 24 q8 -8 15 0" {...ink} strokeWidth={2.5} />
      <Eye x={35} y={40} rx={8} ry={8} pr={4.5} />
      <Eye x={66} y={40} rx={8} ry={8} pr={4.5} />
      <path d="M43 61 q-3 -8 7 -7 q10 -1 7 7 q-7 4 -14 0z" {...ink} strokeWidth={2.5} />
      <ellipse cx={50} cy={78} rx={3.5} ry={4.5} fill="#000" />
    </g>
  ),
  // Grün: breites Grinsen mit Zähnen / wütend mit Zickzackmund
  '1h': () => (
    <g>
      <path d="M27 22 h12 M60 22 h12" {...ink} />
      <Eye x={33} y={38} rx={9} ry={11} px={2} py={1} pr={7} />
      <Eye x={66} y={38} rx={9} ry={11} px={2} py={1} pr={7} />
      <circle cx={30} cy={34} r={3} fill="#fff" /><circle cx={63} cy={34} r={3} fill="#fff" />
      <path d="M48 57 l3 2" {...ink} />
      <path d="M14 62 q36 34 72 -4 q-36 18 -72 4z" fill="#fff" stroke="#000" strokeWidth={3} strokeLinejoin="round" />
      <path d="M24 69 v8 M33 74 v9 M42 77 v9 M51 78 v9 M60 77 v9 M69 73 v9 M77 67 v7" stroke="#000" strokeWidth={2} />
    </g>
  ),
  '1s': () => (
    <g>
      <path d="M26 18 l12 6 l-4 -7 M74 18 l-12 6 l4 -7" {...ink} strokeWidth={2.5} />
      <Eye x={34} y={38} rx={9} ry={11} px={3} py={2} pr={7} />
      <Eye x={66} y={38} rx={9} ry={11} px={-3} py={2} pr={7} />
      <path d="M48 57 l3 2" {...ink} />
      <path d="M19 72 q4 -4 8 0 t8 0 t8 0 t8 0 t8 0 t8 0 t8 0 t8 0" {...ink} strokeWidth={2.5} />
    </g>
  ),
  // Rot: freundlich / traurig
  '2h': () => (
    <g>
      <Eye x={34} y={38} rx={8} ry={11} pr={0} />
      <ellipse cx={34} cy={39} rx={2.5} ry={5} fill="#000" />
      <Eye x={66} y={38} rx={8} ry={11} pr={0} />
      <ellipse cx={66} cy={39} rx={2.5} ry={5} fill="#000" />
      <path d="M21 62 l2 -4 M79 62 l-2 -4 M22 61 q28 26 56 0" {...ink} strokeWidth={2.8} />
    </g>
  ),
  '2s': () => (
    <g>
      <Eye x={34} y={38} rx={8} ry={11} pr={0} />
      <path d="M34 34 l3 4 l-3 4 l-3 -4z" fill="#000" />
      <Eye x={66} y={38} rx={8} ry={11} pr={0} />
      <path d="M66 34 l3 4 l-3 4 l-3 -4z" fill="#000" />
      <path d="M22 81 l3 -4 M78 81 l-3 -4 M24 78 q26 -22 52 0" {...ink} strokeWidth={2.8} />
    </g>
  ),
  // Blau: mit Wimpern und breitem Lächeln / mit dicken Brauen und schiefem Mund
  '3h': () => (
    <g>
      <path d="M27 21 l3 6 M35 17 v8 M43 21 l-3 6 M57 21 l3 6 M65 17 v8 M73 21 l-3 6" {...ink} strokeWidth={2.5} />
      <Eye x={35} y={38} rx={8} ry={8} pr={4} />
      <Eye x={65} y={38} rx={8} ry={8} pr={4} />
      <path d="M50 51 l-5 8 l6 2" {...ink} strokeWidth={2.5} />
      <path d="M26 68 q24 16 48 0 q-24 26 -48 0z" fill="#000" stroke="#000" strokeWidth={2.5} strokeLinejoin="round" />
    </g>
  ),
  '3s': () => (
    <g>
      <path d="M28 25 q6 -6 12 0 M60 25 q6 -6 12 0" {...ink} strokeWidth={4} />
      <Eye x={34} y={39} rx={6} ry={6} pr={3} />
      <Eye x={66} y={39} rx={6} ry={6} pr={3} />
      <path d="M50 52 l-5 8 l6 2" {...ink} strokeWidth={2.5} />
      <path d="M30 76 q20 -9 40 0" {...ink} strokeWidth={3} />
    </g>
  ),
  // Lila: zufrieden / betrübt
  '4h': () => (
    <g>
      <path d="M26 22 h11 M63 22 h11" {...ink} />
      <Eye x={34} y={38} rx={7.5} ry={10} px={1} pr={5} />
      <Eye x={66} y={38} rx={7.5} ry={10} px={1} pr={5} />
      <path d="M47 55 h7 l-3.5 4z" fill="#000" stroke="#000" strokeWidth={2} strokeLinejoin="round" />
      <path d="M23 66 q27 22 54 0" {...ink} strokeWidth={2.8} />
    </g>
  ),
  '4s': () => (
    <g>
      <path d="M27 21 h11 M62 21 h11" {...ink} />
      <Eye x={34} y={38} rx={7.5} ry={10} px={1} pr={5} />
      <Eye x={66} y={38} rx={7.5} ry={10} px={1} pr={5} />
      <path d="M47 55 h7 l-3.5 4z" fill="#000" stroke="#000" strokeWidth={2} strokeLinejoin="round" />
      <path d="M30 80 q20 -18 40 0" {...ink} strokeWidth={2.8} />
    </g>
  ),
  // Schwarz: weiße Linien
  '5h': () => (
    <g stroke="#fff" fill="none" strokeWidth={3} strokeLinecap="round">
      <circle cx={34} cy={38} r={6.5} /><circle cx={66} cy={38} r={6.5} />
      <circle cx={50} cy={58} r={1.5} fill="#fff" />
      <path d="M24 72 q26 16 52 0 q-26 6 -52 0z" fill="#fff" strokeLinejoin="round" />
    </g>
  ),
  '5s': () => (
    <g stroke="#fff" fill="none" strokeWidth={3} strokeLinecap="round">
      <circle cx={34} cy={38} r={6.5} /><circle cx={66} cy={38} r={6.5} />
      <circle cx={34} cy={38} r={1.5} fill="#fff" /><circle cx={66} cy={38} r={1.5} fill="#fff" />
      <circle cx={50} cy={58} r={1.5} fill="#fff" />
      <path d="M40 76 q10 -8 20 0" />
    </g>
  ),
};

export function Face({ player, sad = false }: { player: number; sad?: boolean }) {
  return (
    <g>
      <circle cx={50} cy={50} r={47} fill={PLAYERS[player].fill} stroke="#000" strokeWidth={4} />
      {features[`${player}${sad ? 's' : 'h'}`]()}
    </g>
  );
}

/** Farbenblind-Symbol (weiß umrandet, gut sichtbar auf jeder Spielerfarbe). */
export function Symbol({ player, size = 26 }: { player: number; size?: number }) {
  const s = size / 2;
  const style = { fill: PLAYERS[player].ink === '#fff' ? '#fff' : '#000', stroke: PLAYERS[player].ink === '#fff' ? '#000' : '#fff', strokeWidth: 2.5 };
  switch (PLAYERS[player].symbol) {
    case 'circle': return <circle r={s * 0.75} {...style} />;
    case 'triangle': return <path d={`M0 ${-s} L${s} ${s * 0.8} L${-s} ${s * 0.8}Z`} {...style} />;
    case 'square': return <rect x={-s * 0.75} y={-s * 0.75} width={s * 1.5} height={s * 1.5} {...style} />;
    case 'diamond': return <path d={`M0 ${-s} L${s} 0 L0 ${s} L${-s} 0Z`} {...style} />;
    case 'star': {
      const pts = Array.from({ length: 10 }, (_, k) => {
        const r = k % 2 === 0 ? s : s * 0.45, a = -Math.PI / 2 + (k * Math.PI) / 5;
        return `${(r * Math.cos(a)).toFixed(2)},${(r * Math.sin(a)).toFixed(2)}`;
      });
      return <polygon points={pts.join(' ')} {...style} />;
    }
    case 'cross': {
      const t = s * 0.35;
      return <path d={`M${-t} ${-s}H${t}V${-t}H${s}V${t}H${t}V${s}H${-t}V${t}H${-s}V${-t}H${-t}Z`} {...style} />;
    }
  }
}
