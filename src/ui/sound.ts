// Originalklänge (SOUNDS.DAT) über Web Audio: mehrere Klänge gleichzeitig, nichts blockiert
// den Spielablauf, Lautstärke regelbar. Nummern wie im Original (1-basiert):
// 1 Willkommen, 2 Würfeln, 3 Schlagen, 4 Zurück ins Haus/Beenden, 5 Raussetzen, 6 Schritt,
// 7 Gewinner, 8 Info, 9..14 gesprochene Würfelzahlen 1..6.

export const SOUND = { welcome: 1, roll: 2, capture: 3, home: 4, enter: 5, step: 6, winner: 7, info: 8 } as const;
const SPEECH = new Set([1, 9, 10, 11, 12, 13, 14]);

export interface SoundSettings { speech: boolean; effects: boolean; volume: number }

export class SoundPlayer {
  private context: AudioContext | null = null;
  private gain: GainNode | null = null;
  private buffers = new Map<number, AudioBuffer>();
  private loading: Promise<void> | null = null;
  private pendingWelcome = false;
  private welcomeTimer = 0;
  private unlocked = false;

  constructor(private settings: () => SoundSettings) {}

  private ensureContext() {
    if (this.context) return this.context;
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return null;
    this.context = new Ctx();
    this.gain = this.context.createGain();
    this.gain.connect(this.context.destination);
    this.applyVolume();
    this.loading = this.load();
    return this.context;
  }

  /** Muss aus einer Benutzeraktion heraus aufgerufen werden (Autoplay-Regeln der Browser). */
  unlock() {
    const ctx = this.ensureContext();
    if (!ctx) return;
    this.unlocked = true;
    if (ctx.state === 'suspended') void ctx.resume();
    if (this.pendingWelcome) {
      this.pendingWelcome = false;
      // Kurz warten: löst dieselbe Aktion selbst einen Klang aus (z. B. Würfeln), entfällt der Gruß,
      // statt mit diesem Klang durcheinander zu laufen.
      this.welcomeTimer = window.setTimeout(() => { this.welcomeTimer = 0; this.play(SOUND.welcome); }, 60);
    }
  }

  /** Willkommensgruß beim Start: sofort, wenn der Browser Ton erlaubt, sonst bei der ersten Aktion. */
  welcome() {
    const ctx = this.ensureContext();
    if (!ctx) return;
    this.pendingWelcome = true;
    const greet = () => { if (ctx.state === 'running' && this.pendingWelcome) { this.pendingWelcome = false; this.play(SOUND.welcome); } };
    if (ctx.state === 'running') greet();
    else void ctx.resume().then(greet, () => {});
    const first = () => { window.removeEventListener('pointerdown', first, true); window.removeEventListener('keydown', first, true); this.unlock(); };
    window.addEventListener('pointerdown', first, true);
    window.addEventListener('keydown', first, true);
  }

  applyVolume() {
    if (this.gain && this.context) this.gain.gain.setValueAtTime(Math.max(0, Math.min(1, this.settings().volume)) ** 2, this.context.currentTime);
  }

  private async load() {
    const ctx = this.context!;
    await Promise.all(Array.from({ length: 14 }, async (_, i) => {
      try {
        // Einzeldatei-Fassung (scripts/standalone.mjs): Klänge stecken als Base64 in der Seite.
        const embedded = (globalThis as { AERGER_SOUNDS?: string[] }).AERGER_SOUNDS?.[i];
        const bytes = embedded
          ? Uint8Array.from(atob(embedded), c => c.charCodeAt(0)).buffer
          : await (await fetch(`${import.meta.env.BASE_URL}sounds/${i + 1}.wav`)).arrayBuffer();
        this.buffers.set(i + 1, await ctx.decodeAudioData(bytes));
      } catch { /* ohne diesen Klang weiterspielen */ }
    }));
  }

  play(sample: number) {
    const s = this.settings();
    if (s.volume <= 0 || !(SPEECH.has(sample) ? s.speech : s.effects)) return;
    if (!this.context || !this.gain) return;
    // Vor der ersten Benutzeraktion nichts im angehaltenen Kontext stauen, sonst erklingt später alles auf einmal.
    if (!this.unlocked && this.context.state !== 'running') return;
    if (sample !== SOUND.welcome && this.welcomeTimer) { clearTimeout(this.welcomeTimer); this.welcomeTimer = 0; }
    const start = () => {
      const buffer = this.buffers.get(sample);
      if (!buffer || !this.context || !this.gain) return;
      const source = this.context.createBufferSource();
      source.buffer = buffer;
      source.connect(this.gain);
      source.start();
    };
    if (this.buffers.has(sample)) start(); else void this.loading?.then(start);
  }
}
