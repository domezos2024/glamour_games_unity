using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace GlamourGames
{
    public enum S { Click, Hover, Flip, Match, NoMatch, PlaceX, PlaceO, Win, Lose, Drop, Hit, Miss, Sunk, Eat, Die, Dice, Deal, Coin, Spin, Stop, Big, Chip, Take, Turn, Tick, Splash, Blub, Boom, Crackle, Thunder, Zap, Launch, FwPop, Cheer, Clap, Step, Shake, Sparkle, Fanfare, Creak, Gurgle, Whoosh, Pop, Boing, Gong }

    /// <summary>
    /// Prozedurale Soundeffekte und Musik (Synthese wie im Original), abgespielt ueber Unity-AudioSources.
    /// </summary>
    public static class Sfx
    {
        const int SR = 44100;
        static readonly System.Random R = new System.Random(7);
        static readonly Dictionary<S, AudioClip> bank = new Dictionary<S, AudioClip>();
        static AudioSource[] voices; static int voiceI; static AudioSource music;
        static readonly Dictionary<int, AudioClip> musicClips = new Dictionary<int, AudioClip>();
        static Task<float[]> pendingMusic; static int pendingTrack = -2;
        public static bool Muted, MusicOff, SfxOff;
        public static float Vol = .5f, MusicVol = .8f;
        public static float SfxVol => Vol;
        public static int Track;
        public static readonly string[] TrackNames = { "Glamour", "Oase", "Casino-Groove", "Arcade", "Mystik" };

        public static void Init(GameObject host)
        {
            if (Platform.Touch) { var cfg = AudioSettings.GetConfiguration(); cfg.dspBufferSize = 512; AudioSettings.Reset(cfg); } // geringe Latenz fuer Effekte
            Muted = Save.Int("muted", 0) != 0;
            Track = Math.Clamp(Save.Int("music", 0), -1, TrackNames.Length - 1); MusicOff = Save.Int("musicoff", 0) != 0; SfxOff = Save.Int("sfxoff", 0) != 0;
            Vol = Save.Int("sfxvol", 50) / 100f; MusicVol = Save.Int("musicvol", 80) / 100f;
            voices = new AudioSource[28];
            for (int i = 0; i < voices.Length; i++) { var s = host.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0; voices[i] = s; }
            music = host.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.spatialBlend = 0; music.priority = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { Build(); } catch (Exception e) { Log.I("sfx build " + e.Message); }
            Log.I($"Effekt-Bank erzeugt in {sw.ElapsedMilliseconds} ms");
            StartMusic();
        }

        /// <summary>Pro Frame aufrufen: Musiklautstaerke nachfuehren, fertig synthetisierte Musik starten.</summary>
        public static void Update()
        {
            if (music == null) return;
            music.volume = Muted || MusicOff ? 0 : MusicVol * .9f;
            if (pendingMusic != null && pendingMusic.IsCompleted)
            {
                var t = pendingMusic; int track = pendingTrack; pendingMusic = null;
                if (t.Status == TaskStatus.RanToCompletion && track == Track)
                {
                    var data = t.Result; var clip = AudioClip.Create("music" + track, data.Length / 2, 2, SR, false); clip.SetData(data, 0);
                    musicClips[track] = clip; Play(clip);
                }
            }
        }

        static void Play(AudioClip clip) { if (music == null) return; music.clip = clip; music.time = 0; music.Play(); }

        public static void ToggleMute() { Muted = !Muted; Save.Set("muted", Muted ? 1 : 0); if (!Muted) StartMusic(); }

        public static void Play(S s, float vol = 1, float pitch = 1)
        {
            Feel(s);
            if (Muted || SfxOff || voices == null || !bank.TryGetValue(s, out var clip)) return;
            AudioSource v = null;
            for (int k = 0; k < voices.Length; k++) { var c = voices[(voiceI + k) % voices.Length]; if (!c.isPlaying) { v = c; voiceI = (voiceI + k + 1) % voices.Length; break; } }
            if (v == null) { v = voices[voiceI]; voiceI = (voiceI + 1) % voices.Length; }
            v.clip = clip; v.volume = Mathf.Clamp01(Vol * vol * 1.4f); v.pitch = Mathf.Clamp(pitch, .5f, 2f); v.Play();
        }

        static void Feel(S s)
        {
            switch (s)
            {
                case S.Hit: case S.Sunk: Haptics.Hit(); break;
                case S.Win: case S.Big: case S.Fanfare: Haptics.Win(); break;
                case S.Lose: case S.Die: Haptics.Lose(); break;
                case S.Dice: case S.Shake: case S.Match: case S.Coin: Haptics.Tap(); break;
            }
        }

        public static string NextTrack()
        {
            Track = Track >= TrackNames.Length - 1 ? -1 : Track + 1;
            Save.Set("music", Track); StartMusic();
            return Track < 0 ? "Musik aus" : "Musik: " + TrackNames[Track];
        }
        public static void SetTrack(int t) { Track = t; Save.Set("music", Track); StartMusic(); }
        public static void SetMusicOff(bool off) { MusicOff = off; Save.Set("musicoff", off ? 1 : 0); StartMusic(); }
        public static void SetMusicVol(float v) { MusicVol = Math.Clamp(v, 0, 1); Save.Set("musicvol", (int)Math.Round(MusicVol * 100)); }
        public static void SetSfxVol(float v) { Vol = Math.Clamp(v, 0, 1); Save.Set("sfxvol", (int)Math.Round(Vol * 100)); }
        public static void SetSfxOff(bool off) { SfxOff = off; Save.Set("sfxoff", off ? 1 : 0); }

        static void StartMusic()
        {
            if (music == null) return;
            if (Muted || MusicOff || Track < 0) { music.Stop(); return; }
            if (musicClips.TryGetValue(Track, out var clip)) { if (music.clip != clip || !music.isPlaying) Play(clip); return; }
            music.Stop();
            int t = Track; pendingTrack = t;
            pendingMusic = Task.Run(() => MakeMusicLoop(t));
        }

        // ------------------------------------------------------------------ Synthese (aus dem Original)
        static double Saw(double p) { p -= Math.Floor(p); return p * 2 - 1; }
        static double Sq(double p) { p -= Math.Floor(p); return p < .5 ? 1 : -1; }
        static double Tri(double p) { p -= Math.Floor(p); return 4 * Math.Abs(p - .5) - 1; }
        static double Env(double lt, double k) => Math.Min(1, lt * 200) * Math.Exp(-lt * k);
        const double Tau = Math.PI * 2;

        static float[] MakeMusicLoop(int style)
        {
            double[][] chords;
            switch (style)
            {
                case 1: chords = new[] { new[] { 1d, 1.2, 1.5 }, new[] { 1.335, 1.6, 2 }, new[] { 1.125, 1.335, 1.68 }, new[] { 1.5, 1.782, 2.25 } }; break;
                case 2: chords = new[] { new[] { 1d, 1.26, 1.5, 1.78 }, new[] { 1.335, 1.68, 2, 2.38 }, new[] { 1.5, 1.89, 2.25, 2.67 }, new[] { 1.335, 1.68, 2, 2.38 } }; break;
                case 3: chords = new[] { new[] { 1d, 1.26, 1.5 }, new[] { 1.19, 1.5, 1.78 }, new[] { 1.335, 1.68, 2 }, new[] { 1.5, 1.89, 2.25 } }; break;
                case 4: chords = new[] { new[] { 1d, 1.2, 1.44 }, new[] { 1d, 1.189, 1.5 }, new[] { 0.944, 1.125, 1.41 }, new[] { 1d, 1.2, 1.5 } }; break;
                default: chords = new[] { new[] { 1d, 1.26, 1.5 }, new[] { 1.335, 1.68, 2 }, new[] { 1.5, 1.89, 2.25 }, new[] { 1.122, 1.335, 1.68 } }; break;
            }
            double[] baseHz = { 196, 220, 110, 174.61, 130.81 };
            double[] bpms = { 104, 84, 118, 140, 72 };
            double b0 = baseHz[style], beat = 60d / bpms[style];
            // Doppelte Schleifenlaenge (32 Schlaege) fuer weniger Wiederholung
            int frames = (int)(SR * beat * 32);
            var o = new float[frames * 2];
            var rnd = new System.Random(1000 + style * 97);
            var mel = new int[128]; for (int i = 0; i < mel.Length; i++) mel[i] = rnd.Next(0, 6);
            var hat = new float[frames]; for (int i = 0; i < frames; i++) hat[i] = (float)(rnd.NextDouble() * 2 - 1);
            double step = style == 3 ? beat / 4 : style == 4 ? beat * 2 : beat / 2;
            double[] walk = { 1, 1.26, 1.5, 1.26 };
            for (int i = 0; i < frames; i++)
            {
                double t = i / (double)SR;
                int ci = (int)(t / (beat * 4)) % 4; var ch = chords[ci];
                double root = b0 * ch[0];
                double lt = t % step; int si = (int)(t / step);
                double tb = t % beat;
                double L, Rr;
                switch (style)
                {
                    case 0:
                        {
                            double pad = 0; foreach (var r in ch) pad += Math.Sin(Tau * b0 * r * t) + .3 * Math.Sin(Tau * b0 * r * 2 * t);
                            double f = b0 * 2 * ch[mel[si % 128] % ch.Length] * (mel[si % 128] >= 3 ? 2 : 1);
                            double m = Tri(f * t) * Env(lt, 6) * .10;
                            double bass = Math.Sin(Tau * root * .5 * t) * Env(tb, 3) * .16;
                            double sp = Math.Sin(Tau * root * 4 * t) * Env(t % (beat * 2), 9) * .02;
                            L = pad * .03 + m + bass + sp; Rr = pad * .03 + m * .8 + bass + sp; break;
                        }
                    case 1:
                        {
                            double f = b0 * 2 * ch[mel[si % 128] % ch.Length] * (mel[si % 128] % 2 == 0 ? 1 : 1.5);
                            double vib = 1 + .006 * Math.Sin(Tau * 5 * t);
                            double fl = (Math.Sin(Tau * f * vib * t) + .2 * Math.Sin(Tau * f * 2 * vib * t)) * Env(lt, 1.6) * .10;
                            double dr = (Math.Sin(Tau * root * t) + .5 * Math.Sin(Tau * root * 1.5 * t)) * .06 * (.7 + .3 * Math.Sin(t * .9));
                            L = dr + fl; Rr = dr * .9 + fl * 1.1; break;
                        }
                    case 2:
                        {
                            double bf = root * .5 * walk[(int)(t / beat) % 4];
                            double bass = Sq(bf * t) * Env(tb, 5) * .10 + Math.Sin(Tau * bf * t) * Env(tb, 4) * .10;
                            double kick = Math.Sin(Tau * (50 + 90 * Math.Exp(-tb * 30)) * t) * Math.Exp(-tb * 9) * .28;
                            double hh = hat[i] * Env((tb + beat / 2) % beat, 60) * .07;
                            double st = 0; int bi = (int)(t / beat) % 4;
                            if (bi == 1 || bi == 3) foreach (var r in ch) st += Math.Sin(Tau * b0 * 2 * r * t);
                            st *= Env(tb, 7) * .03;
                            L = bass + kick + hh + st; Rr = bass + kick + hh * .6 + st * 1.2; break;
                        }
                    case 3:
                        {
                            double f = b0 * 2 * ch[si % ch.Length] * ((si / ch.Length) % 2 == 0 ? 1 : 2);
                            double ar = Sq(f * t) * Env(lt, 9) * .07;
                            double bf = root * .5 * (((int)(t / (beat / 2)) % 2) == 0 ? 1 : 2);
                            double bass = Tri(bf * t) * .12 * Env(t % (beat / 2), 4);
                            bool odd = (int)(t / beat) % 2 == 1;
                            double sn = odd ? hat[i] * Math.Exp(-tb * 25) * .12 : 0;
                            double kk = !odd ? Math.Sin(Tau * (60 + 80 * Math.Exp(-tb * 40)) * t) * Math.Exp(-tb * 14) * .2 : 0;
                            L = ar + bass + sn + kk; Rr = ar * .8 + bass + sn + kk; break;
                        }
                    default:
                        {
                            double pad = 0; foreach (var r in ch) pad += Saw(b0 * r * t * 1.003) + Saw(b0 * r * t * .997);
                            double lp = pad * .012 * (.6 + .4 * Math.Sin(t * .45));
                            double drone = Math.Sin(Tau * b0 * .5 * t) * .12;
                            double bell = Math.Sin(Tau * b0 * 4 * ch[mel[si % 128] % ch.Length] * t) * Env(lt, 2.2) * (mel[si % 128] < 3 ? .09 : 0);
                            L = lp + drone + bell; Rr = lp * 1.1 + drone + bell * .6; break;
                        }
                }
                o[i * 2] = (float)Math.Max(-.9, Math.Min(.9, L)); o[i * 2 + 1] = (float)Math.Max(-.9, Math.Min(.9, Rr));
            }
            int fade = SR / 200;
            for (int i = 0; i < fade; i++) { float g = i / (float)fade; o[i * 2] *= g; o[i * 2 + 1] *= g; o[(frames - 1 - i) * 2] *= g; o[(frames - 1 - i) * 2 + 1] *= g; }
            return o;
        }

        static float[] Noise(float dur, float decay = 12, float lp = 1) { float y = 0; return Gen(dur, (t, u) => { y += lp * ((float)R.NextDouble() * 2 - 1 - y); return y; }, decay); }
        static float[] Mix(params float[][] a) { var o = new float[a.Max(x => x.Length)]; foreach (var x in a) for (int i = 0; i < x.Length; i++) o[i] += x[i]; return o; }
        static float[] Gap(float sec) => new float[(int)(sec * SR)];
        static float[] Cat(params float[][] a) => a.SelectMany(x => x).ToArray();
        static float[] Ring(float f, float dur, float decay) => Gen(dur, (t, u) => MathF.Sin(MathF.PI * 2 * f * t) + .5f * MathF.Sin(MathF.PI * 2 * f * 2.76f * t) + .3f * MathF.Sin(MathF.PI * 2 * f * 5.4f * t), decay);
        static float[] Arp(float decay, float note, params float[] fs) => Cat(fs.Select(f => Tone(f, note, decay)).ToArray());

        static void Build()
        {
            var raw = new Dictionary<S, float[]>();
            foreach (S s in (S[])Enum.GetValues(typeof(S))) raw[s] = Tone(260 + (int)s * 17, .1f, 7);
            raw[S.Click] = Mix(Tone(1200, .04f, 40), Noise(.02f, 60));
            raw[S.Hover] = Tone(700, .04f, 30);
            raw[S.Flip] = Cat(Noise(.05f, 30, .5f), Sweep(500, 900, .08f, 12));
            raw[S.Match] = Arp(9, .09f, 660, 880, 1320);
            raw[S.NoMatch] = Cat(Tone(300, .1f, 8), Tone(220, .16f, 8));
            raw[S.PlaceX] = Mix(Tone(520, .1f, 14), Noise(.03f, 40));
            raw[S.PlaceO] = Mix(Tone(780, .12f, 12), Noise(.03f, 40));
            raw[S.Win] = Arp(5, .12f, 523, 659, 784, 1047);
            raw[S.Lose] = Cat(Sweep(400, 200, .2f, 5), Sweep(300, 120, .3f, 4));
            raw[S.Drop] = Sweep(700, 150, .18f, 10);
            raw[S.Hit] = Mix(Noise(.25f, 10, .4f), Tone(90, .25f, 9));
            raw[S.Miss] = Mix(Noise(.3f, 8, .15f), Sweep(500, 250, .3f, 6));
            raw[S.Sunk] = Mix(Noise(.5f, 5, .3f), Tone(70, .5f, 4), Sweep(400, 60, .5f, 5));
            raw[S.Eat] = Cat(Sweep(400, 900, .06f, 14), Sweep(500, 1100, .06f, 14));
            raw[S.Die] = Sweep(600, 80, .5f, 4);
            raw[S.Dice] = Cat(Noise(.03f, 60), Gap(.03f), Noise(.03f, 50), Gap(.04f), Noise(.03f, 45), Gap(.05f), Noise(.04f, 30));
            raw[S.Deal] = Noise(.07f, 30, .6f);
            raw[S.Coin] = Cat(Ring(1760, .06f, 25), Ring(2350, .35f, 7));
            raw[S.Spin] = Sweep(180, 900, .35f, 4);
            raw[S.Stop] = Mix(Tone(160, .12f, 12), Noise(.04f, 40));
            raw[S.Big] = Arp(4, .1f, 523, 659, 784, 1047, 1319, 1568);
            raw[S.Chip] = Mix(Ring(1400, .12f, 25), Noise(.02f, 60));
            raw[S.Take] = Sweep(400, 1000, .12f, 12);
            raw[S.Turn] = Cat(Tone(660, .06f, 12), Tone(880, .08f, 10));
            raw[S.Tick] = Tone(1500, .02f, 90);
            raw[S.Splash] = Mix(Noise(.4f, 6, .25f), Sweep(800, 300, .3f, 8));
            raw[S.Blub] = Cat(Sweep(250, 550, .09f, 10), Sweep(300, 650, .07f, 12));
            raw[S.Boom] = Mix(Noise(.55f, 5, .12f), Tone(55, .55f, 4), Sweep(140, 40, .5f, 5));
            raw[S.Crackle] = Cat(Noise(.02f, 50), Gap(.02f), Noise(.02f, 50), Gap(.03f), Noise(.03f, 40), Gap(.02f), Noise(.02f, 50));
            raw[S.Thunder] = Mix(Noise(.6f, 3, .06f), Tone(45, .6f, 3));
            raw[S.Zap] = Mix(Sweep(2000, 200, .18f, 10), Noise(.1f, 20));
            raw[S.Launch] = Mix(Sweep(300, 1800, .5f, 3), Noise(.5f, 4, .3f));
            raw[S.FwPop] = Mix(Noise(.2f, 14, .6f), Tone(150, .2f, 12));
            raw[S.Cheer] = Mix(Noise(.6f, 3, .35f), Sweep(500, 700, .6f, 3));
            raw[S.Clap] = Cat(Noise(.03f, 60), Gap(.02f), Noise(.04f, 45));
            raw[S.Step] = Mix(Noise(.03f, 60, .3f), Tone(120, .04f, 40));
            raw[S.Shake] = Cat(Noise(.05f, 25, .7f), Gap(.02f), Noise(.05f, 25, .7f), Gap(.02f), Noise(.06f, 20, .7f));
            raw[S.Sparkle] = Arp(9, .06f, 2093, 2637, 3136, 2637, 3520);
            raw[S.Fanfare] = Cat(Tone(523, .1f, 4), Tone(523, .1f, 4), Tone(523, .1f, 4), Tone(659, .16f, 3), Tone(784, .3f, 2.5f));
            raw[S.Creak] = Sweep(200, 130, .4f, 3);
            raw[S.Gurgle] = Cat(Sweep(200, 450, .08f, 8), Sweep(250, 500, .08f, 8), Sweep(200, 420, .1f, 8));
            raw[S.Whoosh] = Gen(.4f, (t, u) => ((float)R.NextDouble() * 2 - 1) * MathF.Sin(MathF.PI * u), 0);
            raw[S.Pop] = Sweep(300, 900, .05f, 30);
            raw[S.Boing] = Gen(.35f, (t, u) => MathF.Sin(MathF.PI * 2 * (300 + 140 * MathF.Sin(t * 40)) * t), 8);
            raw[S.Gong] = Ring(110, .55f, 3.5f);
            foreach (var kv in raw)
            {
                var d = kv.Value; float pk = d.Length == 0 ? 0 : d.Max(Math.Abs);
                if (pk > 0) { float g = .6f / pk; for (int i = 0; i < d.Length; i++) d[i] *= g; }
                var clip = AudioClip.Create("sfx_" + kv.Key, Math.Max(1, d.Length), 1, SR, false);
                clip.SetData(d.Length == 0 ? new float[1] : d, 0);
                bank[kv.Key] = clip;
            }
        }
        static float[] Gen(float dur, Func<float, float, float> f, float decay = 6)
        {
            int n = Math.Max(1, (int)(dur * SR)); var b = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)SR, u = i / (float)Math.Max(1, n - 1); b[i] = f(t, u) * MathF.Exp(-decay * t); }
            return b;
        }
        static float[] Tone(float f, float dur, float decay = 5) => Gen(dur, (t, u) => MathF.Sin(MathF.PI * 2 * f * t), decay);
        static float[] Sweep(float f0, float f1, float dur, float decay = 4) { float ph = 0; return Gen(dur, (t, u) => { ph += MathF.PI * 2 * (f0 + (f1 - f0) * u) / SR; return MathF.Sin(ph); }, decay); }
    }
}
