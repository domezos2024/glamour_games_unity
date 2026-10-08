using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public sealed class GameInfo
    {
        /// <summary>Stabile Kennung (Netzwerkprotokoll, gleich OppKey der Szene), Name, Untertitel; Min/MaxSeats = erlaubte Spielerzahl im Bluetooth-Spiel.</summary>
        public string Id, Name, Sub; public Col Col; public Func<Scene> Make; public int MinSeats = 2, MaxSeats = 2;
        public GameInfo(string id, string name, string sub, Col col, Func<Scene> make, int minSeats = 2, int maxSeats = 2) { Id = id; Name = name; Sub = sub; Col = col; Make = make; MinSeats = minSeats; MaxSeats = maxSeats; }
    }
    public static class Registry
    {
        public static readonly List<GameInfo> All = new List<GameInfo>
        {
            new GameInfo("memory", "Memory", "Paare finden", C.Purple, () => new MemoryGame(), 2, 4),
            new GameInfo("ttt", "Tic Tac Toe", "Best of 3 Runden", C.Cyan, () => new TicTacToe()),
            new GameInfo("c4", "Vier Gewinnt", "Diskus-Duell", C.Blue, () => new ConnectFour()),
            new GameInfo("ships", "Schiffe Versenken", "Flotten-Gefecht", C.Blue.Mix(C.Cyan, .4f), () => new Battleship()),
            new GameInfo("snake", "Snake", "Neon-Schlangen-Duell", C.Green, () => new SnakeGame(), 2, 4),
            new GameInfo("kniffel", "Kniffel", "3D-Würfelpoker", C.Red, () => new Kniffel(), 2, 4),
            new GameInfo("nim", "Nim", "Wer den letzten nimmt", C.Orange, () => new Nim()),
            new GameInfo("slot", "Buch der Pharaonen", "Slot mit Freispielen", C.Gold, () => new SlotGame()),
            new GameInfo("bj", "Black Jack", "2–4 Spieler gegen die Bank", C.Pink, () => new Blackjack(), 2, 4),
            new GameInfo("poker", "Poker", "Texas Hold'em No Limit", C.Magenta, () => new Poker(), 2, 4),
        };
        public static GameInfo ById(string id) { foreach (var g in All) if (g.Id == id) return g; return null; }
    }
    public class Menu : Scene
    {
        public override bool Chrome => false;
        public override Col Acc1 => C.Purple; public override Col Acc2 => C.Cyan;
        readonly Spring[] hov = Enumerable.Range(0, 10).Select(_ => new Spring(0) { K = 300, D = 20 }).ToArray();
        readonly Spring[] tx = Enumerable.Range(0, 10).Select(_ => new Spring(0) { K = 200, D = 18 }).ToArray();
        readonly Spring[] ty = Enumerable.Range(0, 10).Select(_ => new Spring(0) { K = 200, D = 18 }).ToArray();
        int over = -1; float intro;
        const float TW = 270, TH = 300, G = 26, X0 = (1600 - (5 * TW + 4 * G)) / 2, Y0 = 200;
        static Box Tile(int i) => Gfx.R(X0 + (i % 5) * (TW + G), Y0 + (i / 5) * (TH + G), TW, TH);
        public override bool WantsHand => over >= 0;
        public override void Enter()
        {
            base.Enter(); Sfx.Play(S.Turn, .4f); Ui.Add(new Button { R = Gfx.R(1476, 24, 96, 96), Col = C.Cyan, Round = true, Click = () => App.Go(new Options()), Custom = (c, r, h) => { Gfx.Gear(c, r.MidX, r.MidY, 30, Col.White); return true; } });
            // Bluetooth-Mehrspieler (Symbol oben links)
            Ui.Add(new Button { R = Gfx.R(28, 24, 96, 96), Col = Link.Connected ? C.Green : C.Blue, Round = true, Click = () => App.Go(new BtLobby()), Custom = (c, r, h) => { BtIcon(c, r.MidX, r.MidY, 30, Col.White); return true; } });
        }
        /// <summary>Bluetooth-Rune aus Linien.</summary>
        static void BtIcon(Canvas2D c, float x, float y, float s, Col col)
        {
            var p = Gfx.Line(col, s * .14f); p.Glow = 1.3f; float h = s * .9f, w = s * .45f;
            c.DrawLine(x, y - h, x, y + h, p); c.DrawLine(x, y - h, x + w, y - h / 2, p); c.DrawLine(x + w, y - h / 2, x - w, y + h / 2, p);
            c.DrawLine(x, y + h, x + w, y + h / 2, p); c.DrawLine(x + w, y + h / 2, x - w, y - h / 2, p);
        }
        public override void Update(float dt)
        {
            intro += dt;
            for (int i = 0; i < 10; i++)
            {
                hov[i].Target = i == over ? 1 : 0; hov[i].Update(dt);
                var r = Tile(i); tx[i].Target = i == over ? Math.Clamp((App.MX - r.MidX) / (TW / 2), -1, 1) : 0; ty[i].Target = i == over ? Math.Clamp((App.MY - r.MidY) / (TH / 2), -1, 1) : 0; tx[i].Update(dt); ty[i].Update(dt);
            }
            if (Rng.Shared.NextSingle() < dt * 6) Fx.Spark(Rng.Shared.NextSingle() * 1600, 900, Registry.All[Rng.Shared.Next(10)].Col, 1, 60);
        }
        public override void MouseMove(float x, float y) { over = -1; for (int i = 0; i < 10; i++) if (Tile(i).Contains(x, y)) over = i; }
        public override void MouseDown(float x, float y) { }
        public override void MouseUp(float x, float y) { if (over >= 0) Launch(over); }
        void Launch(int i)
        {
            if (Link.Connected && !Link.IsHost) { App.Toast($"{Link.PeerName} wählt das Spiel"); return; }
            Sfx.Play(S.Click); if (Link.Connected) { Link.Start(Registry.All[i]); return; } App.Go(Registry.All[i].Make());
        }
        public override void KeyDown(Key k)
        {
            int n = k >= Key.Number1 && k <= Key.Number9 ? k - Key.Number1 : k == Key.Number0 ? 9 : -1; if (n >= 0) Launch(n);
        }
        public override void Draw(Canvas2D c)
        {
            float ti = Ease.OutBack(intro / .8f);
            c.Save(); c.Translate(800, 100); c.Scale(ti, ti);
            string t = "GLAMOUR GAMES"; float w = Gfx.TW(t, 96, true, true);
            Gfx.Text(c, t, 0, 0, 96, C.Pink, Al.C, true, 30, true); Gfx.Text(c, t, 0, 0, 96, C.Cyan, Al.C, true, 14, true);
            {
                // goldener Schriftzug mit wanderndem Glanz
                float sw = MathF.Sin(Time * .9f) * w * .35f; var p = Gfx.Fill(Col.White); p.Glow = 1.2f;
                p.Shader = Grad.Linear(-w / 2 + sw, -40, w / 2 + sw, 40, new Col(255, 250, 200), C.Orange); Gfx.TextPaint(c, t, 0, 0, 96, p, Al.C, true, true);
            }
            c.Restore();
            if (Link.Connected) Gfx.Text(c, Link.IsHost ? $"Bluetooth: {Link.Count} Spieler verbunden  ·  wähle ein Spiel, alle spielen an ihrem Gerät mit" : $"Bluetooth: {Link.Count} Spieler verbunden  ·  {Link.Name(0)} wählt das Spiel ...", 800, 168, 22, C.Green.Light(.4f), Al.C, true);
            else Gfx.Text(c, Platform.Pick("10 Spiele  ·  zu zweit, per Bluetooth oder gegen den Computer  ·  Zifferntasten starten direkt", "10 Spiele  ·  zu zweit, per Bluetooth oder gegen den Computer  ·  Kachel antippen zum Starten"), 800, 168, 22, C.Dim, Al.C, false);
            for (int i = 0; i < 10; i++)
            {
                float a = Ease.OutCubic((intro - .15f - i * .05f) / .45f); if (a <= 0) continue;
                var g = Registry.All[i]; var r = Tile(i); float h = hov[i].V, sc = 1 + h * .06f;
                c.Save(); c.Translate(r.MidX, r.MidY + (1 - a) * 60 - h * 8); c.Scale(sc, sc);
                var rr = Gfx.Ctr(0, 0, TW, TH);
                Gfx.Glow(c, rr, 26, g.Col, 14 + h * 16, (.3f + h * .5f) * a);
                Gfx.RectGrad(c, rr, 26, g.Col.Dark(.3f + h * .12f).A(a), new Col(10, 3, 24).A(a)); Gfx.Stroke(c, rr, 26, g.Col.A(a * (.6f + h * .4f)), 2.5f + h * 1.5f);
                var shine = new Box(-TW / 2 + 4, -TH / 2 + 3, TW / 2 - 4, -TH / 2 + 90); Gfx.RectGrad(c, shine, 22, Col.White.A(.09f * a), Col.White.A(0));
                c.Save(); c.Translate(tx[i].V * 6, ty[i].V * 4); Icon(c, i, 0, -32, 1 + h * .12f, Time + i, h); c.Restore();
                Gfx.Text(c, g.Name, 0, 88, g.Name.Length > 14 ? 25 : 30, Col.White.A(a), Al.C, true, 5 * h, true);
                Gfx.Text(c, g.Sub, 0, 124, 18, g.Col.Light(.55f).A(a), Al.C, false);
                Gfx.Text(c, ((i + 1) % 10).ToString(), -TW / 2 + 26, -TH / 2 + 28, 20, g.Col.Light(.5f).A(.7f * a), Al.C);
                c.Restore();
            }
        }
        static void Icon(Canvas2D c, int i, float x, float y, float s, float t, float h)
        {
            c.Save(); c.Translate(x, y); c.Scale(s, s); float bob = MathF.Sin(t * 1.6f) * 4;
            switch (i)
            {
                case 0:
                    CardArt.Card(c, -36, bob, 84, "", 0, 0, -12 + h * -6);
                    c.Save(); c.Translate(36, -bob); c.RotateDegrees(10 + h * 6); Gfx.RectGrad(c, Gfx.Ctr(0, 0, 84, 118), 10, new Col(70, 30, 120), new Col(20, 6, 50)); Gfx.Stroke(c, Gfx.Ctr(0, 0, 84, 118), 10, C.Purple, 3); Gfx.Image(c, Assets.Img("diamond"), Gfx.Ctr(0, 0, 70, 70)); c.Restore(); break;
                case 1:
                    { var p = Gfx.Line(C.Cyan.A(.9f), 5); for (int k = -1; k <= 1; k += 2) { c.DrawLine(k * 27, -80, k * 27, 80, p); c.DrawLine(-80, k * 27, 80, k * 27, p); }
                        float ph = t % 4; var pk = Gfx.Line(C.Cyan, 8); c.DrawLine(-70, -70, -40, -40, pk); c.DrawLine(-40, -70, -70, -40, pk);
                        c.DrawCircle(0, 0, 18, Gfx.Line(C.Pink, 8)); c.DrawLine(40, 40, 70, 70, Gfx.Line(C.Cyan, 8)); c.DrawLine(70, 40, 40, 70, Gfx.Line(C.Cyan, 8)); c.DrawCircle(-54, 54, 15, Gfx.Line(C.Pink, 7)); c.DrawCircle(54, -54, 15, Gfx.Line(C.Pink, 7)); break; }
                case 2:
                    { Gfx.RectGrad(c, Gfx.Ctr(0, 0, 170, 150), 16, new Col(30, 60, 200), new Col(10, 20, 100));
                        for (int r = 0; r < 3; r++) for (int q = 0; q < 4; q++) { var col = (r + q) % 3 == 0 ? C.Cyan : (r + q) % 3 == 1 ? C.Pink : Col.Black.A(.6f); Gfx.Ball(c, -57 + q * 38, -40 + r * 40, 15, col.Alpha < 200 ? new Col(5, 0, 20) : col); }
                        float fy = -110 + ((t * 1.2f) % 1) * 70; Gfx.Ball(c, 57, fy, 15, C.Gold); break; }
                case 3:
                    { var wp = new Path2D(); wp.MoveTo(-90, 40 + bob); for (int k = 0; k <= 18; k++) wp.LineTo(-90 + k * 10, 40 + MathF.Sin(t * 2 + k * .7f) * 6); wp.LineTo(90, 90); wp.LineTo(-90, 90); wp.Close();
                        var hull = new Path2D(); hull.MoveTo(-70, 8); hull.LineTo(60, 8); hull.LineTo(88, -14); hull.LineTo(-58, -14); hull.Close(); c.Save(); c.Translate(0, MathF.Sin(t * 2) * 3);
                        c.DrawPath(hull, Gfx.Fill(new Col(120, 140, 170))); c.DrawRect(-30, -40, 50, 26, Gfx.Fill(new Col(160, 180, 210))); c.DrawRect(-8, -60, 6, 20, Gfx.Fill(new Col(200, 210, 230))); c.DrawRect(-50, -26, 16, 12, Gfx.Fill(new Col(90, 105, 135))); c.Restore();
                        c.DrawPath(wp, Gfx.Fill(C.Blue.A(.55f))); float cr = 26 + MathF.Sin(t * 4) * 3; var cp = Gfx.Line(C.Red, 3); c.DrawCircle(60, -50, cr, cp); c.DrawLine(60 - cr - 8, -50, 60 + cr + 8, -50, cp); c.DrawLine(60, -50 - cr - 8, 60, -50 + cr + 8, cp); break; }
                case 4:
                    { var pts = new List<Pt>(); for (int k = 0; k < 16; k++) pts.Add(new(-88 + k * 11.5f, MathF.Sin(k * .55f - t * 3) * 34));
                        for (int k = 0; k < pts.Count; k++) { float f = k / (float)pts.Count; Gfx.Radial(c, pts[k].X, pts[k].Y, 18, C.Green, .5f); Gfx.Ball(c, pts[k].X, pts[k].Y, 6 + f * 9, C.Green.Dark(.45f + .55f * f)); }
                        var hp = pts[^1]; c.DrawCircle(hp.X + 5, hp.Y - 6, 4, Gfx.Fill(Col.White)); c.DrawCircle(hp.X + 6, hp.Y - 6, 2, Gfx.Fill(Col.Black)); Gfx.Radial(c, 60, -50, 24, C.Yellow, .6f); Gfx.Ball(c, 60, -50, 10, C.Yellow); break; }
                case 5:
                    { var m = Die3D.Mul(Die3D.RX(.5f), Die3D.Mul(Die3D.RY(t * .9f), Die3D.RZ(.2f))); Die3D.Draw(c, -22, 6, 100, m, new Col(250, 240, 220), new Col(30, 20, 40));
                      var m2 = Die3D.Mul(Die3D.RX(-.4f + t * .6f), Die3D.RY(.9f)); Die3D.Draw(c, 44, -28, 64, m2, C.Red.Light(.1f), Col.White); break; }
                case 6:
                    { int[] pl = { 3, 5, 7 }; for (int p = 0; p < 3; p++) for (int k = 0; k < pl[p]; k++) { float sx = -70 + p * 70, sy = 80 - k * 20; Gfx.RectGrad(c, Gfx.Ctr(sx, sy, 46, 14), 6, C.Gold.Light(.3f), C.Orange.Dark(.6f)); } break; }
                case 7:
                    { string[] ic = { "slot_BOOK", "slot_EXPLORER", "slot_PHARAOH" }; for (int k = 0; k < 3; k++) { var r = Gfx.Ctr(-62 + k * 62, 0, 58, 130); Gfx.RectGrad(c, r, 10, new Col(255, 240, 200), new Col(200, 160, 90)); Gfx.Stroke(c, r, 10, C.Gold, 3); var im = Assets.Img(ic[(k + (int)(t * 1.2f)) % 3]); Gfx.Image(c, im, Gfx.Ctr(r.MidX, r.MidY + MathF.Sin(t * 3 + k) * 4, 54, 54)); }
                      Gfx.Radial(c, 0, 0, 100, C.Gold, .25f + .1f * MathF.Sin(t * 3)); break; }
                case 8:
                    CardArt.Card(c, -36, 6 + bob * .5f, 92, "A", 0, 1, -14 + h * -6); CardArt.Card(c, 36, 6 - bob * .5f, 92, "K", 1, 1, 12 + h * 6); Gfx.Text(c, "21", 0, -76, 34, C.Gold, Al.C, true, 12); break;
                default:
                    for (int k = 0; k < 5; k++) { float cy2 = 70 - k * 12; Gfx.RectGrad(c, Gfx.Ctr(-58, cy2, 62, 16), 8, C.Red.Light(.15f), C.Red.Dark(.5f)); }
                    for (int k = 0; k < 4; k++) { float cy2 = 70 - k * 12; Gfx.RectGrad(c, Gfx.Ctr(-58 + 78, cy2 + 8, 62, 16), 8, C.Cyan.Light(.2f), C.Blue.Dark(.5f)); }
                    CardArt.Card(c, -14, -34 + bob, 76, "A", 1, 1, -12); CardArt.Card(c, 34, -34 - bob, 76, "A", 0, 1, 10); break;
            }
            c.Restore();
        }
    }
}
