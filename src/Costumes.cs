// Costume easter eggs: some full outfits change what happens when he poops (belly 5-poke),
// and the leather daddy look changes how he takes a slap.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BigBootyCheems
{
    partial class Buddy
    {
        enum Outfit { None, Saiyan, Leather, Builder, Cop, Party, Outlaw, Astro, Gas, Jet, Propeller }

        string Worn(string key) { foreach (var s in slots) if (s.Key == key) return s.Current; return ""; }

        Outfit CurrentOutfit
        {
            get
            {
                if (FullSaiyan) return Outfit.Saiyan;
                string hat = Worn("Hat"), face = Worn("Face"), top = Worn("Top"), shoes = Worn("Shoes");
                if (top == "ltherd") return Outfit.Leather;
                if (hat == "HardHat" && top == "HiVis" && shoes == "boots") return Outfit.Builder;
                if (hat == "Policehat" && face == "sung") return Outfit.Cop;
                if (hat == "Partyhat" && top == "hawa" && face == "sung") return Outfit.Party;
                if (hat == "cb" && face == "bnda" && shoes == "boots") return Outfit.Outlaw;
                if (hat == "Astro" && shoes != "") return Outfit.Astro;
                if (face == "Gasm") return Outfit.Gas;
                if (shoes == "rollers") return Outfit.Jet;
                if (hat == "pplr") return Outfit.Propeller;
                return Outfit.None;
            }
        }

        // where it all comes out (screen)
        Point ButtHole()
        {
            return new Point(winX + Tune.Pad + (int)((Tune.ButtCX + Tune.ButtRX * 0.30f) * Tune.Scale),
                             winY + Tune.Pad + (int)((Tune.ButtCY + Tune.ButtRY * 0.38f) * Tune.Scale));
        }

        FxOverlay fx, fxUnder;
        short[] sndSiren, sndHorn, sndPew, sndRicochet, sndWhoosh;
        readonly Random fxRng = new Random();
        float Rnd(float a, float b) { return a + (float)fxRng.NextDouble() * (b - a); }

        // effects that come out of him go under him (behind the butt); hearts and signs go on top
        FxOverlay Fx() { return Fx(false); }
        FxOverlay FxUnder() { return Fx(true); }
        FxOverlay Fx(bool under)
        {
            var scr = Screen.FromRectangle(new Rectangle(winX, winY, cw, ch));
            var cur = under ? fxUnder : fx;
            if (cur == null || cur.IsDisposed || cur.ScreenBounds != scr.Bounds)
            {
                if (cur != null && !cur.IsDisposed) cur.Close();
                cur = new FxOverlay(scr, under ? Handle : IntPtr.Zero);
                cur.Ricochet = () => { if (sndRicochet == null) sndRicochet = Synth.Ricochet(); Mixer.Play(sndRicochet, 0.45f); };
                if (under)
                {
                    cur.Hole = new Rectangle(winX, winY, cw, ch);
                    cur.FrameReady = FxFrame;
                }
                cur.Show();
                if (under) fxUnder = cur; else fx = cur;
            }
            return cur;
        }

        // the under-layer drew a frame: draw the part inside our window behind the art, in step with it
        bool hadFx;
        void FxFrame()
        {
            if (fxUnder == null) return;
            if (Detached) { fxUnder.Hole = Rectangle.Empty; return; }
            fxUnder.Hole = new Rectangle(winX, winY, cw, ch);
            if (fxUnder.DrawnRect.IntersectsWith(fxUnder.Hole) || hadFx) Render();
        }

        // ---------------------------------------------------------- the poops that aren't
        // returns true if the outfit replaces the usual spray
        bool CostumePoop(Outfit o, Point e)
        {
            switch (o)
            {
                case Outfit.Party: PartyPoop(e); return true;
                case Outfit.Gas: GasPoop(e); return true;
                case Outfit.Astro: AstroPoop(e); return true;
                case Outfit.Outlaw: OutlawPoop(e); return true;
                case Outfit.Propeller: PropellerPoop(e); return true;
            }
            return false;
        }

        static readonly int[] ConfettiColours = { 0xFF3B5C, 0xFFC93C, 0x3CC8FF, 0x7BE04A, 0xB067FF, 0xFF8A2B, 0xFFFFFF, 0xFF6FD0 };

        void PartyPoop(Point e)
        {
            if (sndHorn == null) sndHorn = Synth.Horn();
            Mixer.Play(sndHorn, 0.8f);
            var f = FxUnder();
            for (int i = 0; i < 240; i++)
                f.Add(new Particle
                {
                    Kind = Particle.Confetti, X = e.X + Rnd(-4, 4), Y = e.Y + Rnd(-4, 4),
                    VX = Rnd(-550, 650), VY = Rnd(-1300, -480), Gravity = 520, Drag = 1.6f, Sway = Rnd(20, 70),
                    Size = Rnd(6, 11), Spin = Rnd(0, 6.3f), SpinV = Rnd(-14, 14), Life = Rnd(5.5f, 7.5f), Delay = Rnd(0, 0.45f),
                    Color = ConfettiColours[fxRng.Next(ConfettiColours.Length)]
                });
            StartMove(MoveKind.Twerk, 2.4f, 1f);
        }

        static readonly int[] GasColours = { 0x9BC83C, 0x7FB52E, 0xB4C850, 0x6E9A34 };

        void GasPoop(Point e)
        {
            Fart(1f);
            var f = FxUnder();
            for (int i = 0; i < 26; i++)
                f.Add(new Particle
                {
                    Kind = Particle.Puff, X = e.X + Rnd(-3, 3), Y = e.Y + Rnd(-3, 3), Delay = i * 0.04f,
                    VX = Rnd(70, 260), VY = Rnd(-90, -5), Drag = 0.8f, Gravity = -22,
                    Size = Rnd(10, 16), Grow = Rnd(22, 34), Life = Rnd(3.8f, 5.5f), Alpha = Rnd(0.35f, 0.55f),
                    Color = GasColours[fxRng.Next(GasColours.Length)]
                });
            StartMove(MoveKind.Clench, 1.0f, 1f);
        }

        void AstroPoop(Point e)
        {
            Fart(0.6f);
            var f = FxUnder();
            for (int i = 0; i < 12; i++)
                f.Add(new Particle
                {
                    Kind = Particle.Blob, X = e.X + Rnd(-3, 3), Y = e.Y + Rnd(-3, 3), Delay = i * 0.06f,
                    VX = Rnd(50, 230), VY = Rnd(-90, 25), Gravity = -5, Drag = 0.04f, Bounce = 0.9f,
                    Size = Rnd(7, 14), Life = Rnd(7.5f, 9.5f)
                });
            StartFloat(true);
        }

        void OutlawPoop(Point e)
        {
            if (sndPew == null) sndPew = Synth.Pew();
            Mixer.Play(sndPew, 0.9f);
            var f = FxUnder();
            for (int i = 0; i < 14; i++)
                f.Add(new Particle
                {
                    Kind = Particle.Blob, X = e.X, Y = e.Y + Rnd(-3, 3), Delay = i * 0.03f,
                    VX = Rnd(1500, 2300), VY = Rnd(-550, 120), Gravity = 1300, Drag = 0.12f, Bounce = 0.78f,
                    Size = Rnd(5, 9), Life = Rnd(4.5f, 6f)
                });
            int now = Environment.TickCount;
            slaps.Add(new Slap { X = e.X - winX - Tune.Pad - 6, Y = e.Y - winY - Tune.Pad, Dir = -1, Amp = Tune.Amp, T0 = now });
        }

        void PropellerPoop(Point e)
        {
            Fart(1f);
            var f = FxUnder();
            for (int i = 0; i < 14; i++)
                f.Add(new Particle
                {
                    Kind = Particle.Puff, X = e.X + Rnd(-6, 6), Y = e.Y + Rnd(0, 10), Delay = i * 0.02f,
                    VX = Rnd(-60, 140), VY = Rnd(60, 200), Drag = 1.8f, Gravity = 0,
                    Size = Rnd(5, 9), Grow = Rnd(22, 36), Life = Rnd(1.3f, 2.2f), Alpha = Rnd(0.25f, 0.4f),
                    Color = 0x9A8A6A
                });
            StartFloat(false);
        }

        // ---------------------------------------------------------- floating (astro / propeller)
        bool floating, floatAstro; int floatStart, floatBaseX, floatBaseY; float floatSecs, floatH;

        void StartFloat(bool astro)
        {
            int now = Environment.TickCount;
            if (floating) { floatStart = now - 1400; return; }      // keep him up a bit longer
            floating = true; floatAstro = astro; floatStart = now;
            floatBaseX = winX; floatBaseY = winY;
            floatSecs = astro ? 7.5f : 3.6f; floatH = astro ? 170 : 115;
            timer.Start();
        }

        void FloatTick(int now)
        {
            if (!floating) return;
            if (dragging) { floating = false; return; }     // grabbed mid-air: he's yours now
            float t = (now - floatStart) / 1000f;
            if (t >= floatSecs)
            {
                floating = false; winX = floatBaseX; winY = floatBaseY;
                Landed(floatAstro ? 250 : 900);
                return;
            }
            float up = Smooth(0, 1, t / (floatAstro ? 1.8f : 0.5f)) * Smooth(0, 1, (floatSecs - t) / (floatAstro ? 2.2f : 0.45f));
            float bob = floatAstro ? 8 * (float)Math.Sin(t * 1.9f) : 3.5f * (float)Math.Sin(t * 17) + 3 * (float)Math.Sin(t * 5.3f);
            float drift = floatAstro ? 22 * (float)Math.Sin(t * 0.8f) : 6 * (float)Math.Sin(t * 3.1f);
            winY = floatBaseY - (int)((floatH + bob) * up);
            winX = floatBaseX + (int)(drift * up);
        }

        // ---------------------------------------------------------- roller skates: poop-propelled
        bool jetting; float jetV, jetX; int jetStart, fxLastTick;

        void StartJet()
        {
            jetting = true; jetStart = Environment.TickCount; jetX = winX;
            timer.Start();
        }

        void JetTick(int now, float dt)
        {
            if (!jetting) return;
            if (dragging) { jetting = false; jetV = 0; return; }
            float t = (now - jetStart) / 1000f;
            if (t < 0.55f) jetV -= 2100 * dt;                                    // thrust while it's coming out
            else
            {
                float s = Math.Sign(jetV), nv = jetV - s * 850 * dt;             // rolling to a stop
                jetV = Math.Sign(nv) != s ? 0 : nv;
            }
            jetX += jetV * dt;
            var wa = Screen.FromRectangle(new Rectangle(winX, winY, cw, ch)).WorkingArea;
            float minX = wa.Left - Tune.Pad, maxX = wa.Right - cw + Tune.Pad;
            if (jetX < minX) { jetX = minX; jetV = -jetV * 0.35f; DoSlap(winX + Tune.Pad + 10, winY + ch / 2, 1, 0.5f); }
            if (jetX > maxX) { jetX = maxX; jetV = -jetV * 0.35f; }
            winX = (int)jetX;
            if (spray != null && !spray.IsDisposed && t < 0.65f) spray.SetEmit(ButtHole());
            if (t > 0.6f && Math.Abs(jetV) < 1) jetting = false;
        }

        // ---------------------------------------------------------- cop lights
        int copStart = -100000; const int CopMs = 4500;
        bool CopOn { get { return Environment.TickCount - copStart < CopMs; } }

        void StartCop()
        {
            if (sndSiren == null) sndSiren = Synth.Siren();
            Mixer.Play(sndSiren, 0.5f);
            if (glow == null) glow = MakeGlow();
            copStart = Environment.TickCount;
            timer.Start();
        }

        // red/blue strobes behind him, and their light on him
        void DrawCop(int[] o)
        {
            if (!CopOn || glow == null) return;
            int el = Environment.TickCount - copStart;
            float fade = Math.Min(1f, (CopMs - el) / 600f);
            bool phase = (el / 260) % 2 == 0;
            int sub = el % 260;
            float on = (sub < 90 || (sub > 120 && sub < 210)) ? 1f : 0.15f;
            float mid = cw / 2f, soft = cw * 0.22f;
            for (int y = 0; y < ch; y++)
            {
                int row = y * cw;
                for (int x = 0; x < cw; x++)
                {
                    float gv = glow[row + x];
                    if (gv < 0.02f) continue;
                    float rw = Smooth(mid + soft, mid - soft, x); if (!phase) rw = 1 - rw;   // 1 = red side
                    int c = o[row + x], ca = (c >> 24) & 0xFF;
                    float A = Math.Min(1f, (float)Math.Pow(gv, 0.7) * 1.1f) * on * fade * 0.9f;
                    float k = A * (255 - ca) / 255f;
                    if (k > 0.01f)
                    {
                        int ka = (int)(k * 255);
                        int cr = (int)(ka * (rw + (1 - rw) * 40 / 255f)), cg = (int)(ka * (rw * 30 + (1 - rw) * 100) / 255f), cb = (int)(ka * (rw * 40 / 255f + (1 - rw)));
                        int add = (ka << 24) | (cr << 16) | (cg << 8) | cb;
                        c = Add(c, add);
                    }
                    if (ca > 0)
                    {
                        // the light falling on him
                        float v = ca * 0.1f * on * fade;
                        c = Add(c, ((int)(v * rw) << 16) | ((int)(v * (1 - rw) / 3) << 8) | (int)(v * (1 - rw)));
                    }
                    o[row + x] = c;
                }
            }
        }

        // ---------------------------------------------------------- builder: cordon off the mess
        static int[] signSpr, tapeSpr;
        const int SignW = 76, SignH = 130, TapeW = 300, TapeH = 170, TapeStrip = 28;

        int cordonTick = -100000;
        void CordonOff(Point e, SprayOverlay sp)
        {
            int now = Environment.TickCount;
            if (now - cordonTick < 7000) return;          // already taped off
            cordonTick = now;
            if (signSpr == null) signSpr = FxOverlay.MakeSprite(SignW, SignH, DrawSign);
            if (tapeSpr == null) tapeSpr = FxOverlay.MakeSprite(TapeW, TapeH, DrawTape);
            var f = Fx();
            var scr = Screen.FromPoint(e);
            int wall = scr.Bounds.Right, floor = scr.WorkingArea.Bottom;
            f.Add(new Particle { Kind = Particle.Sprite, Spr = tapeSpr, SW = TapeW, SH = TapeH, X = wall - TapeW / 2 + 30, Y = floor - TapeH / 2 + 12, Delay = 0.8f, Life = 7.6f });
            f.Add(new Particle { Kind = Particle.Sprite, Spr = signSpr, SW = SignW, SH = SignH, X = wall - 190, Y = floor - SignH / 2, Delay = 1.2f, Life = 7.2f });
        }

        // the wet floor sign art (Fx_wetfloor.png), scaled down
        static void DrawSign(Graphics g)
        {
            using (var st = Res("Fx_wetfloor.png")) using (var img = new Bitmap(st))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, 0, 0, SignW, SignH);
            }
        }

        // two crossed strips of the repeating tape art (Fx_tape.png)
        static void DrawTape(Graphics g)
        {
            using (var st = Res("Fx_tape.png")) using (var tile = new Bitmap(st))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float k = (float)TapeStrip / tile.Height;
                foreach (var ang in new[] { -15f, 13f })
                {
                    var save = g.Save();
                    g.TranslateTransform(TapeW / 2f, TapeH / 2f); g.RotateTransform(ang);
                    float len = TapeW + 60, x0 = -len / 2f, y0 = -TapeStrip / 2f;
                    using (var b = new TextureBrush(tile, WrapMode.Tile))
                    {
                        b.ScaleTransform(k, k, MatrixOrder.Append);
                        b.TranslateTransform(x0, y0, MatrixOrder.Append);
                        g.FillRectangle(b, x0, y0, len, TapeStrip);
                    }
                    using (var pen = new Pen(Color.FromArgb(70, 0, 0, 0), 1)) g.DrawRectangle(pen, x0, y0, len, TapeStrip);
                    g.Restore(save);
                }
            }
        }

        // ---------------------------------------------------------- leather daddy: he likes it
        static int[][] heartSpr; static readonly int[] HeartSizes = { 20, 28, 36 };

        void Hearts(int sx, int sy, float power)
        {
            if (heartSpr == null)
            {
                heartSpr = new int[HeartSizes.Length][];
                for (int i = 0; i < HeartSizes.Length; i++)
                {
                    int s = HeartSizes[i];
                    heartSpr[i] = FxOverlay.MakeSprite(s, s, g =>
                    {
                        float k = s / 32f;
                        using (var p = new GraphicsPath())
                        {
                            p.AddBezier(16 * k, 29 * k, 2 * k, 18 * k, 0 * k, 4 * k, 9 * k, 4 * k);
                            p.AddBezier(9 * k, 4 * k, 13 * k, 4 * k, 16 * k, 8 * k, 16 * k, 10 * k);
                            p.AddBezier(16 * k, 10 * k, 16 * k, 8 * k, 19 * k, 4 * k, 23 * k, 4 * k);
                            p.AddBezier(23 * k, 4 * k, 32 * k, 4 * k, 30 * k, 18 * k, 16 * k, 29 * k);
                            using (var b = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(255, 255, 110, 160), Color.FromArgb(255, 215, 20, 70), 90f)) g.FillPath(b, p);
                            using (var pen = new Pen(Color.FromArgb(200, 140, 0, 40), Math.Max(1f, 1.3f * k))) g.DrawPath(pen, p);
                        }
                        using (var b = new SolidBrush(Color.FromArgb(170, 255, 255, 255))) g.FillEllipse(b, 6 * k, 7 * k, 5 * k, 4 * k);
                    });
                }
            }
            var f = Fx();
            int n = 3 + (int)(power * 4);
            for (int i = 0; i < n; i++)
            {
                int which = fxRng.Next(HeartSizes.Length), s = HeartSizes[which];
                f.Add(new Particle
                {
                    Kind = Particle.Sprite, Spr = heartSpr[which], SW = s, SH = s,
                    X = sx + Rnd(-14, 14), Y = sy + Rnd(-10, 6), VX = Rnd(-70, 70), VY = Rnd(-280, -140),
                    Drag = 0.7f, Gravity = -40, Sway = Rnd(20, 45), Spin = Rnd(0, 6), Life = Rnd(1.0f, 1.7f), Delay = i * 0.04f
                });
            }
        }

        // ---------------------------------------------------------- per tick (from the idle poll)
        // while he's moving fast (carried, skating, floating) the layers under him draw everything
        // themselves: two windows can't move in perfect step, which showed as seams in the poop
        bool Detached { get { return dragging || jetting || floating; } }

        bool FxActive { get { return floating || jetting || CopOn; } }

        void FxTick(int now)
        {
            float dt = fxLastTick == 0 ? 0.015f : Math.Max(0.001f, Math.Min(0.04f, (now - fxLastTick) / 1000f));
            fxLastTick = now;
            FloatTick(now);
            JetTick(now, dt);
            if (FxActive && !timer.Enabled) timer.Start();
        }
    }
}
