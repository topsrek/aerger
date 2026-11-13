// Spielfläche als eine SVG-Grafik. Querformat: Originalanordnung des 700er-Fensters
// (Brett bei 2,2, Spielerfeld und Würfel rechts, PlayerPanelLayout 700). Hochformat:
// Spielerfeld unter dem Brett.
import { useState } from 'preact/hooks';
import { geometry, origin } from '../core/board';
import { Kind, type Game } from '../core/game';
import { FLY_MS, type Controller, type View } from './controller';
import { BoardBackground, Die, RestCorners, Title } from './svg/Board';
import { Face, Symbol } from './svg/Face';


const STEP_MS = [500, 200, 100, 0];

export const LANDSCAPE = { width: 860, height: 704 };
export const PORTRAIT = { width: 704, height: 866 };

interface Props { controller: Controller; view: View; portrait: boolean; area: { width: number; height: number } }

/** Pixelgröße, mit der eine Fläche w×h in den freien Bereich passt. */
function fit(area: Props['area'], w: number, h: number) {
  const scale = Math.max(0, Math.min(area.width / w, area.height / h));
  return { width: `${Math.floor(w * scale)}px`, height: `${Math.floor(h * scale)}px` };
}

function Piece({ game, view, player, piece, colorblind, highlight, onClick, onHover, speed }: {
  game: Game; view: View; player: number; piece: number; colorblind: boolean; highlight: boolean; speed: number;
  onClick?: () => void; onHover?: (on: boolean) => void;
}) {
  const geo = geometry(game.playerCount);
  const moving = view.moving && view.moving.player === player && view.moving.piece === piece ? view.moving : null;
  const position = moving ? moving.position : game.position(player, piece);
  const [x, y] = origin(game, player, piece, position);
  const victim = view.victim?.player === player && view.victim.piece === piece;
  const flying = view.flying?.player === player && view.flying.piece === piece;
  const sad = position < 0 || victim;
  const size = geo.fieldSize;
  const duration = flying ? FLY_MS[speed] : moving ? Math.min(STEP_MS[speed] * 0.75, 160) : 420;
  return (
    <g class={`piece${highlight ? ' movable' : ''}${flying ? ' flying' : ''}`}
      style={{ transform: `translate(${x}px, ${y}px)`, transition: `transform ${duration}ms ${flying ? 'cubic-bezier(.3,.1,.3,1)' : 'ease-out'}`, '--fly': `${duration}ms` }}
      onClick={onClick ? (e) => { e.stopPropagation(); onClick(); } : undefined}
      onPointerEnter={onHover ? () => onHover(true) : undefined} onPointerLeave={onHover ? () => onHover(false) : undefined}>
      {highlight && <circle class="pulse" cx={size / 2} cy={size / 2} r={size / 2 + 3} />}
      <g class="body" style={{ transformOrigin: `${size / 2}px ${size / 2}px` }}>
        <g transform={`scale(${size / 100})`}><Face player={player} sad={sad} /></g>
      </g>
      {colorblind && <g transform={`translate(${size * 0.83} ${size * 0.83})`}><Symbol player={player} size={size * 0.34} /></g>}
    </g>
  );
}

export function Stage({ controller, view, portrait, area }: Props) {
  const [hover, setHover] = useState(-1);
  const s = controller.settings;
  const game = view.game;
  const box = portrait ? PORTRAIT : LANDSCAPE;
  const layout = portrait ? 'portrait' : 'landscape';

  if (!game || !view.playing) {
    const x = portrait ? 2 : 79;
    return (
      <svg class="stage" style={fit(area, box.width, portrait ? 704 : box.height)} viewBox={`0 0 ${box.width} ${portrait ? 704 : box.height}`} role="img" aria-label="Mensch ärgere Dich nicht!">
        <g transform={`translate(${x} 2)`}><Title /></g>
      </svg>
    );
  }

  const geo = geometry(game.playerCount);
  const moves = controller.humanMoves;
  const human = controller.humanTurn;
  const canRoll = human && !game.awaitingMove && !view.rolling && !view.moving;
  const focus = view.selected >= 0 ? view.selected : hover;
  const preview = s.showMoves && focus >= 0 && moves.includes(focus) ? focus : -1;
  const cursor = human ? (game.awaitingMove ? 'figure' : 'dice') : 'mouse';
  const speed = controller.speedOf(game.player);

  const pieces: preact.JSX.Element[] = [];
  let movingPiece: preact.JSX.Element | null = null;
  let flyingPiece: preact.JSX.Element | null = null;
  for (let p = 0; p < game.playerCount; p++) {
    if (game.kinds[p] === Kind.Inactive) continue;
    for (let i = 0; i < 4; i++) {
      const mine = human && p === game.player && moves.includes(i);
      const el = (
        <Piece key={`${p}-${i}`} game={game} view={view} player={p} piece={i} colorblind={s.colorblind} speed={speed}
          highlight={mine && s.showMoves}
          onClick={mine ? () => controller.moveRequest(i) : undefined}
          onHover={mine ? (on) => setHover(on ? i : -1) : undefined} />
      );
      if (view.moving?.player === p && view.moving.piece === i) movingPiece = el;
      else if (view.flying?.player === p && view.flying.piece === i) flyingPiece = el;   // fliegt über allem
      else pieces.push(el);
    }
  }

  // Zielvorschau für die gewählte/überfahrene Figur.
  let target: preact.JSX.Element | null = null;
  if (preview >= 0) {
    const [tx, ty] = origin(game, game.player, preview, game.target(preview));
    const r = geo.fieldSize / 2;
    target = (
      <g class="target" onClick={(e) => { e.stopPropagation(); controller.moveRequest(preview); }} transform={`translate(${tx} ${ty})`}>
        <g transform={`scale(${geo.fieldSize / 100})`} opacity={0.4}><Face player={game.player} /></g>
        <circle cx={r} cy={r} r={r + 2} class="target-ring" />
        {game.isCapture(preview) && <path d={`M${r - 9} ${r - 9}l18 18m0 -18l-18 18`} class="target-capture" />}
      </g>
    );
  }

  const rest = game.rules.rest ? Array.from({ length: game.playerCount }, (_, p) => geo.track[p * game.stride + game.restOffset]) : [];
  const selection = view.selected >= 0 && game.awaitingMove ? origin(game, game.player, view.selected, game.position(game.player, view.selected)) : null;

  return (
    <svg class={`stage cursor-${cursor}`} style={fit(area, box.width, box.height)} viewBox={`0 0 ${box.width} ${box.height}`} role="application"
      aria-label={`Mensch ärgere Dich nicht! ${game.names[game.player]} ist am Zug.`}
      onClick={() => { controller.sound.unlock(); if (canRoll) controller.rollRequest(); }}>
      <g transform="translate(2 2)">
        <BoardBackground players={game.playerCount} rest={game.rules.rest} colorblind={s.colorblind} />
        {target}
        {pieces}
        {movingPiece}
        {flyingPiece}
        {rest.map(([x, y]) => <RestCorners key={`r${x}-${y}`} x={x} y={y} corners={geo.restCorners} />)}
        {selection && <rect x={selection[0]} y={selection[1]} width={geo.fieldSize - 1} height={geo.fieldSize - 1} fill="none" stroke="#000" strokeWidth={2} />}
      </g>
      <Panel game={game} view={view} layout={layout} canRoll={canRoll} controller={controller} />
    </svg>
  );
}

/** Was der Spieler am Zug gerade tut, und ggf. der wievielte Startversuch. */
function status(game: Game, view: View, controller: Controller): [string, string] {
  const human = game.kinds[game.player] === Kind.Human;
  const attempts = !game.awaitingMove && game.entryAttempts ? `Versuch ${game.attempts + 1} von 3` : '';
  if (view.rolling) return ['würfelt …', attempts];
  if (view.moving) return ['zieht …', ''];
  if (game.awaitingMove) return human ? [controller.humanMoves.length > 1 ? 'Figur wählen' : 'Figur ziehen', ''] : ['überlegt …', ''];
  return human ? ['Würfeln!', attempts] : ['ist dran', attempts];
}

function Panel({ game, view, layout, canRoll, controller }: { game: Game; view: View; layout: 'portrait' | 'landscape'; canRoll: boolean; controller: Controller }) {
  const six = game.playerCount === 6;
  const figure = six ? 41 : 54;
  const name = game.names[game.player];
  const roll = (e: MouseEvent) => { e.stopPropagation(); controller.sound.unlock(); if (canRoll) controller.rollRequest(); };
  const fitText = (text: string, perChar: number, width: number) => text.length * perChar > width ? width : undefined;
  // Im Querformat ist das Spielerfeld nur 150 breit: längere Hinweise werden gestaucht.
  const toast = (width: number) => view.toast && (
    <text class="toast" textLength={fitText(view.toast, 11, width)} lengthAdjust="spacingAndGlyphs">{view.toast}</text>
  );
  const [doing, attempt] = status(game, view, controller);
  const die = (scale: number, x: number, y: number) => (
    <g class={`die${canRoll ? ' can-roll' : ''}${view.rolling ? ' rolling' : ''}`} transform={`translate(${x} ${y}) scale(${scale})`} onClick={roll}>
      <rect x={-20} y={-20} width={140} height={140} fill="transparent" />
      {view.shownDie > 0 ? <Die value={view.shownDie} /> : canRoll ? <g opacity={0.35}><Die value={6} /></g> : null}
    </g>
  );

  if (layout === 'landscape') {
    // PlayerPanelLayout 700: Feld (705,2,151,150), Figur (752|759,32), Name (782,110); darunter neu
    // die Statuszeile, dann der Würfel (754,300).
    return (
      <g>
        <rect x={705.5} y={2.5} width={150} height={149} fill="#ffff4b" stroke="#ff0000" />
        <g transform={`translate(${six ? 759 : 752} 32) scale(${figure / 100})`}><Face player={game.player} /></g>
        <text x={782} y={131} class="panel-name" textLength={name.length > 9 ? 140 : undefined} lengthAdjust="spacingAndGlyphs">{name}</text>
        <text x={780} y={182} class="status" textLength={fitText(doing, 10, 146)} lengthAdjust="spacingAndGlyphs">{doing}</text>
        {attempt && <text x={780} y={204} class="status-sub">{attempt}</text>}
        {die(0.54, 754, 300)}
        <g transform="translate(780 262)">{toast(146)}</g>
      </g>
    );
  }
  // Hochformat: Spielerfeld unter dem Brett.
  return (
    <g>
      <rect x={2.5} y={712.5} width={699} height={150} fill="#ffff4b" stroke="#ff0000" />
      <g transform="translate(22 730) scale(1.15)"><Face player={game.player} /></g>
      <text x={150} y={785} class="panel-name portrait" textLength={name.length > 14 ? 300 : undefined} lengthAdjust="spacingAndGlyphs">{name}</text>
      <text x={150} y={830} class="status portrait">{doing}{attempt ? ` · ${attempt}` : ''}</text>
      {die(1.15, 570, 730)}
      <g transform="translate(350 857)">{toast(400)}</g>
    </g>
  );
}
