// Fx: a click-through layer over the whole monitor for the costume easter eggs: confetti, gas clouds,
// bouncing or floating poop, hearts and signs. Plus a few sounds made in code.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BigBootyCheems
{
    class Particle
    {
        public const int Confetti = 0, Puff = 1, Blob = 2, Sprite = 3;
        public int Kind;
        public float X, Y, VX, VY, Age, Life, Size, Grow, Spin, SpinV, Gravity, Drag, Bounce = -1, Delay, Sway;
        public int Color;               // straight RGB (confetti, puff)
        public float Alpha = 1f;        // peak opacity
        public int[] Spr; public int SW, SH;
        public bool OnFloor;
    }

    class FxOverlay : Form
    {
        readonly Rectangle bounds; readonly int floorY;
        readonly int ow, oh;
        readonly int[] px;
        IntPtr memDC, dib, bits;
        int dx0, dy0, dx1, dy1;             // last drawn box (overlay px)
        readonly List<Particle> parts = new List<Particle>();
        readonly Timer timer;
        int lastTick;
        readonly Random rng = new Random();
        public Action Ricochet;             // a bouncing particle hit an edge hard
        int lastRicochet = Environment.TickCount - 3600000;

        readonly IntPtr below;              // sit just under this window (the cheems), or on top of everything

        public FxOverlay(Screen screen, IntPtr below)
        {
            this.below = below;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            bounds = screen.Bounds; floorY = screen.WorkingArea.Bottom;
            ow = bounds.Width; oh = bounds.Height;
            px = new int[ow * oh];
            Location = bounds.Location; Size = new Size(1, 1);
            var bmi = new Native.BITMAPINFO { biSize = Marshal.SizeOf(typeof(Native.BITMAPINFO)), biWidth = ow, biHeight = -oh, biPlanes = 1, biBitCount = 32 };
            IntPtr sdc = Native.GetDC(IntPtr.Zero);
            memDC = Native.CreateCompatibleDC(sdc);
            dib = Native.CreateDIBSection(sdc, ref bmi, 0, out bits, IntPtr.Zero, 0);
            Native.ReleaseDC(IntPtr.Zero, sdc);
            Native.SelectObject(memDC, dib);
            timer = new Timer { Interval = 16 };
            timer.Tick += (s, e) => Step();
        }

        public Rectangle ScreenBounds { get { return bounds; } }
        // under-layer only: the cheems draws whatever falls inside its own window (behind its art, same
        // frame), so this layer leaves that rectangle empty. FrameReady lets it redraw in lock-step.
        public Rectangle Hole;
        public Action FrameReady;
        public int[] Pixels { get { return px; } }
        public int Width { get { return ow; } }
        public int Height { get { return oh; } }
        public Rectangle DrawnRect { get { return dx1 > dx0 && dy1 > dy0 ? new Rectangle(bounds.Left + dx0, bounds.Top + dy0, dx1 - dx0, dy1 - dy0) : Rectangle.Empty; } }
        public void RePush() { if (!IsDisposed) Push(); }
        public int FloorY { get { return floorY; } }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8 | 0x8000000; return cp; }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaint(PaintEventArgs e) { }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            if (memDC != IntPtr.Zero) { Native.DeleteObject(dib); Native.DeleteDC(memDC); memDC = IntPtr.Zero; }
            base.OnFormClosed(e);
        }

        public void Add(Particle p)
        {
            parts.Add(p);
            if (!timer.Enabled)
            {
                lastTick = Environment.TickCount; timer.Start();
                Native.SetWindowPos(Handle, below == IntPtr.Zero ? new IntPtr(-1) : below, 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
            }
        }

        void Step()
        {
            int now = Environment.TickCount;
            float dt = Math.Min(0.033f, Math.Max(0.001f, (now - lastTick) / 1000f));
            lastTick = now;
            float L = bounds.Left, R = bounds.Right, T = bounds.Top, F = floorY;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                if (p.Delay > 0) { p.Delay -= dt; continue; }
                p.Age += dt;
                if (p.Age >= p.Life) { parts.RemoveAt(i); continue; }
                if (!p.OnFloor)
                {
                    p.VY += p.Gravity * dt;
                    float k = Math.Max(0f, 1 - p.Drag * dt); p.VX *= k; p.VY *= k;
                    p.X += (p.VX + (p.Sway != 0 ? p.Sway * (float)Math.Sin(p.Age * 5 + p.Spin) : 0)) * dt; p.Y += p.VY * dt;
                }
                p.Spin += p.SpinV * dt; p.Size += p.Grow * dt;
                float r = p.Kind == Particle.Blob ? p.Size : 3;
                if (p.Bounce >= 0)
                {
                    bool hit = false; float sp = Math.Abs(p.VX) + Math.Abs(p.VY);
                    if (p.X - r < L) { p.X = L + r; p.VX = Math.Abs(p.VX) * p.Bounce; hit = true; }
                    if (p.X + r > R) { p.X = R - r; p.VX = -Math.Abs(p.VX) * p.Bounce; hit = true; }
                    if (p.Y - r < T) { p.Y = T + r; p.VY = Math.Abs(p.VY) * p.Bounce; hit = true; }
                    if (p.Y + r > F)
                    {
                        p.Y = F - r; p.VY = -Math.Abs(p.VY) * p.Bounce; p.VX *= 0.85f; hit = Math.Abs(p.VY) > 120;
                        if (Math.Abs(p.VY) < 60 && p.Gravity > 0) p.VY = 0;
                    }
                    if (hit && sp > 700 && Ricochet != null && now - lastRicochet > 90) { lastRicochet = now; Ricochet(); }
                }
                else if (p.Gravity > 0 && p.Y + r >= F && !p.OnFloor) { p.Y = F - r; p.OnFloor = true; }
            }
            Draw();
            if (parts.Count == 0) timer.Stop();
        }

        // ---------------------------------------------------------- drawing
        int bx0, by0, bx1, by1;
        void Grow(int x0, int y0, int x1, int y1)
        {
            bx0 = Math.Min(bx0, Math.Max(0, x0)); by0 = Math.Min(by0, Math.Max(0, y0));
            bx1 = Math.Max(bx1, Math.Min(ow, x1)); by1 = Math.Max(by1, Math.Min(oh, y1));
        }

        void Draw()
        {
            if (dx1 > dx0) for (int y = dy0; y < dy1; y++) Array.Clear(px, y * ow + dx0, dx1 - dx0);
            bx0 = ow; by0 = oh; bx1 = 0; by1 = 0;
            DrawPuffs();
            foreach (var p in parts)
            {
                if (p.Delay > 0) continue;
                float fade = Math.Min(1f, Math.Min(p.Age / 0.08f, (p.Life - p.Age) / Math.Min(1.2f, p.Life * 0.4f))) * p.Alpha;
                if (fade <= 0.01f) continue;
                int cx = (int)(p.X - bounds.Left), cy = (int)(p.Y - bounds.Top);
                switch (p.Kind)
                {
                    case Particle.Confetti:
                    {
                        // a paper flake tumbling: its width flips as it turns
                        int w = (int)(Math.Abs(Math.Cos(p.Spin)) * p.Size) + 1, hh = (int)(p.Size * 0.55f) + 1;
                        if (((int)(p.Spin / Math.PI)) % 2 == 1) { int t = w; w = hh; hh = t; }
                        float shade = 0.75f + 0.25f * (float)Math.Abs(Math.Cos(p.Spin));
                        FillRect(cx - w / 2, cy - hh / 2, w, hh, p.Color, fade, shade);
                        break;
                    }
                    case Particle.Blob: Blit(BlobSprite((int)p.Size), cx, cy, fade); break;
                    case Particle.Sprite: Blit(p.Spr, p.SW, p.SH, cx - p.SW / 2, cy - p.SH / 2, fade); break;
                }
            }
            if (bx1 <= bx0 || by1 <= by0) { bx0 = by0 = 0; bx1 = by1 = 0; }
            dx0 = bx0; dy0 = by0; dx1 = bx1; dy1 = by1;
            if (FrameReady != null) FrameReady();
            Push();
        }

        void Over(int i, int c)
        {
            int a = (c >> 24) & 0xFF;
            if (a == 0) return;
            if (a == 255) { px[i] = c; return; }
            int d = px[i], k = 255 - a, r = 0;
            for (int sh = 0; sh < 32; sh += 8) r |= Math.Min(255, ((c >> sh) & 0xFF) + (((d >> sh) & 0xFF) * k + 127) / 255) << sh;
            px[i] = r;
        }
        static int Pm(int rgb, float a, float shade)
        {
            int ia = (int)(a * 255); if (ia <= 0) return 0;
            int r = (int)(((rgb >> 16) & 0xFF) * shade * a), g = (int)(((rgb >> 8) & 0xFF) * shade * a), b = (int)((rgb & 0xFF) * shade * a);
            return (ia << 24) | (Math.Min(ia, r) << 16) | (Math.Min(ia, g) << 8) | Math.Min(ia, b);
        }

        void FillRect(int x, int y, int w, int h, int rgb, float a, float shade)
        {
            int c = Pm(rgb, a, shade);
            int x0 = Math.Max(0, x), y0 = Math.Max(0, y), x1 = Math.Min(ow, x + w), y1 = Math.Min(oh, y + h);
            if (x1 <= x0 || y1 <= y0) return;
            Grow(x0, y0, x1, y1);
            for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) Over(yy * ow + xx, c);
        }

        // gas is soft, so it's built on a coarse grid (every Q px) and smoothly scaled up: far cheaper
        // than painting every big puff pixel by pixel
        const int Q = 4;
        float[] gA, gR, gG, gB;
        void DrawPuffs()
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue, any = 0;
            foreach (var p in parts)
            {
                if (p.Kind != Particle.Puff || p.Delay > 0) continue;
                int cx = (int)(p.X - bounds.Left), cy = (int)(p.Y - bounds.Top), r = (int)p.Size + 1;
                x0 = Math.Min(x0, cx - r); y0 = Math.Min(y0, cy - r); x1 = Math.Max(x1, cx + r); y1 = Math.Max(y1, cy + r); any++;
            }
            if (any == 0) return;
            x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(ow, x1); y1 = Math.Min(oh, y1);
            if (x1 <= x0 || y1 <= y0) return;
            int gw = (x1 - x0) / Q + 2, gh = (y1 - y0) / Q + 2, n = gw * gh;
            if (gA == null || gA.Length < n) { gA = new float[n]; gR = new float[n]; gG = new float[n]; gB = new float[n]; }
            Array.Clear(gA, 0, n); Array.Clear(gR, 0, n); Array.Clear(gG, 0, n); Array.Clear(gB, 0, n);
            foreach (var p in parts)
            {
                if (p.Kind != Particle.Puff || p.Delay > 0) continue;
                float a = Math.Min(1f, Math.Min(p.Age / 0.08f, (p.Life - p.Age) / Math.Min(1.2f, p.Life * 0.4f))) * p.Alpha;
                if (a <= 0.01f) continue;
                float gx = (p.X - bounds.Left - x0) / Q, gy = (p.Y - bounds.Top - y0) / Q, rg = p.Size / Q, inv = 1f / (rg * rg);
                float pr = ((p.Color >> 16) & 0xFF) / 255f, pg = ((p.Color >> 8) & 0xFF) / 255f, pb = (p.Color & 0xFF) / 255f;
                int a0 = Math.Max(0, (int)(gx - rg)), a1 = Math.Min(gw, (int)(gx + rg) + 2), b0 = Math.Max(0, (int)(gy - rg)), b1 = Math.Min(gh, (int)(gy + rg) + 2);
                for (int b = b0; b < b1; b++)
                    for (int c = a0; c < a1; c++)
                    {
                        float d2 = ((c - gx) * (c - gx) + (b - gy) * (b - gy)) * inv;
                        if (d2 >= 1) continue;
                        float k = (1 - d2); k = k * k * a;
                        float shade = 0.85f + 0.25f * (gy - b) / rg;
                        int i = b * gw + c; float keep = 1 - k;
                        gA[i] = k + gA[i] * keep;
                        gR[i] = pr * shade * k + gR[i] * keep; gG[i] = pg * shade * k + gG[i] * keep; gB[i] = pb * shade * k + gB[i] * keep;
                    }
            }
            Grow(x0, y0, x1, y1);
            for (int y = y0; y < y1; y++)
            {
                float fy = (float)(y - y0) / Q; int b = (int)fy; float ty = fy - b;
                for (int x = x0; x < x1; x++)
                {
                    float fx = (float)(x - x0) / Q; int c = (int)fx; float tx = fx - c;
                    int i = b * gw + c;
                    float A = (gA[i] * (1 - tx) + gA[i + 1] * tx) * (1 - ty) + (gA[i + gw] * (1 - tx) + gA[i + gw + 1] * tx) * ty;
                    if (A < 0.004f) continue;
                    float R = (gR[i] * (1 - tx) + gR[i + 1] * tx) * (1 - ty) + (gR[i + gw] * (1 - tx) + gR[i + gw + 1] * tx) * ty;
                    float G = (gG[i] * (1 - tx) + gG[i + 1] * tx) * (1 - ty) + (gG[i + gw] * (1 - tx) + gG[i + gw + 1] * tx) * ty;
                    float B = (gB[i] * (1 - tx) + gB[i + 1] * tx) * (1 - ty) + (gB[i + gw] * (1 - tx) + gB[i + gw + 1] * tx) * ty;
                    int ia = Math.Min(255, (int)(A * 255));
                    Over(y * ow + x, (ia << 24) | (Math.Min(ia, (int)(R * 255)) << 16) | (Math.Min(ia, (int)(G * 255)) << 8) | Math.Min(ia, (int)(B * 255)));
                }
            }
        }

        void Puff(int cx, int cy, float rad, int rgb, float a)
        {
            int R = (int)rad + 1;
            int x0 = Math.Max(0, cx - R), y0 = Math.Max(0, cy - R), x1 = Math.Min(ow, cx + R), y1 = Math.Min(oh, cy + R);
            if (x1 <= x0 || y1 <= y0) return;
            Grow(x0, y0, x1, y1);
            float inv = 1f / (rad * rad);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    float d2 = ((x - cx) * (x - cx) + (y - cy) * (y - cy)) * inv;
                    if (d2 >= 1) continue;
                    float k = (1 - d2); k *= k;
                    // lighter on top, a little darker underneath
                    float shade = 0.85f + 0.25f * (cy - y) / rad;
                    Over(y * ow + x, Pm(rgb, a * k, shade));
                }
        }

        void Blit(int[] spr, int cx, int cy, float a) { int s = (int)Math.Sqrt(spr.Length); Blit(spr, s, s, cx - s / 2, cy - s / 2, a); }
        void Blit(int[] spr, int sw, int sh, int x, int y, float a)
        {
            int x0 = Math.Max(0, x), y0 = Math.Max(0, y), x1 = Math.Min(ow, x + sw), y1 = Math.Min(oh, y + sh);
            if (x1 <= x0 || y1 <= y0) return;
            Grow(x0, y0, x1, y1);
            int ia = (int)(a * 256);
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                {
                    int c = spr[(yy - y) * sw + (xx - x)];
                    if (c == 0) continue;
                    if (ia < 256)
                    {
                        int r = 0;
                        for (int s = 0; s < 32; s += 8) r |= ((((c >> s) & 0xFF) * ia) >> 8) << s;
                        c = r;
                    }
                    Over(yy * ow + xx, c);
                }
        }

        // a glossy brown ball (premultiplied), cached per radius
        static readonly Dictionary<int, int[]> blobs = new Dictionary<int, int[]>();
        public static int[] BlobSprite(int r)
        {
            r = Math.Max(2, Math.Min(40, r));
            int[] s;
            if (blobs.TryGetValue(r, out s)) return s;
            int n = 2 * r + 2; s = new int[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x - n / 2f + 0.5f) / r, fy = (y - n / 2f + 0.5f) / r;
                    float d = (float)Math.Sqrt(fx * fx + fy * fy);
                    float al = Math.Max(0f, Math.Min(1f, (1 - d) * r));          // anti-aliased edge
                    if (al <= 0) continue;
                    float nz = (float)Math.Sqrt(Math.Max(0, 1 - d * d));
                    float lit = Math.Max(0f, -0.45f * fx - 0.6f * fy + 0.65f * nz);
                    float shade = 0.45f + 0.7f * lit;
                    float spec = (float)Math.Pow(Math.Max(0, lit), 12) * 0.7f;
                    float cr = 120 * shade + 255 * spec, cg = 74 * shade + 235 * spec, cb = 34 * shade + 200 * spec;
                    int ia = (int)(al * 255);
                    s[y * n + x] = (ia << 24) | (Math.Min(ia, (int)(cr * al)) << 16) | (Math.Min(ia, (int)(cg * al)) << 8) | Math.Min(ia, (int)(cb * al));
                }
            blobs[r] = s;
            return s;
        }

        // draw something with GDI+ into a premultiplied sprite
        public static int[] MakeSprite(int w, int h, Action<Graphics> paint)
        {
            using (var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.Clear(System.Drawing.Color.Transparent);
                    paint(g);
                }
                var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                var o = new int[w * h]; Marshal.Copy(d.Scan0, o, 0, o.Length); b.UnlockBits(d);
                return o;
            }
        }

        int[] zeros;
        void Push()
        {
            int x0 = dx0, y0 = dy0, x1 = dx1, y1 = dy1;
            if (x1 <= x0 || y1 <= y0) { x0 = 0; y0 = 0; x1 = 1; y1 = 1; px[0] = 0; }
            int bw = x1 - x0, bh = y1 - y0;
            int hx0 = Hole.Left - bounds.Left, hx1 = Hole.Right - bounds.Left, hy0 = Hole.Top - bounds.Top, hy1 = Hole.Bottom - bounds.Top;
            if (zeros == null) zeros = new int[ow];
            for (int y = y0; y < y1; y++)
            {
                if (y >= hy0 && y < hy1 && hx1 > x0 && hx0 < x1)
                {
                    int a0 = Math.Max(x0, hx0), a1 = Math.Min(x1, hx1);
                    if (a0 > x0) Marshal.Copy(px, y * ow + x0, IntPtr.Add(bits, (y * ow + x0) * 4), a0 - x0);
                    Marshal.Copy(zeros, 0, IntPtr.Add(bits, (y * ow + a0) * 4), a1 - a0);
                    if (x1 > a1) Marshal.Copy(px, y * ow + a1, IntPtr.Add(bits, (y * ow + a1) * 4), x1 - a1);
                }
                else Marshal.Copy(px, y * ow + x0, IntPtr.Add(bits, (y * ow + x0) * 4), bw);
            }
            Native.GdiFlush();
            IntPtr sdc = Native.GetDC(IntPtr.Zero);
            var dst = new Native.POINT { x = bounds.Left + x0, y = bounds.Top + y0 }; var sz = new Native.SIZE { cx = bw, cy = bh };
            var s0 = new Native.POINT { x = x0, y = y0 };
            var bf = new Native.BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = 1 };
            Native.UpdateLayeredWindow(Handle, sdc, ref dst, ref sz, memDC, ref s0, 0, ref bf, 2);
            Native.ReleaseDC(IntPtr.Zero, sdc);
        }
    }

    // little sounds made in code, mono at the mixer rate
    static class Synth
    {
        static short[] Render(double secs, Func<double, double> f)
        {
            int n = (int)(Mixer.Rate * secs); var o = new short[n];
            for (int i = 0; i < n; i++) o[i] = (short)Math.Max(-32767, Math.Min(32767, f((double)i / Mixer.Rate) * 32767));
            return o;
        }
        static double Env(double t, double len, double a, double r) { return Math.Min(1, t / a) * Math.Max(0, Math.Min(1, (len - t) / r)); }

        // two police "whoops"
        public static short[] Siren()
        {
            double ph = 0;
            return Render(1.5, t =>
            {
                double u = (t % 0.75) / 0.75;
                double f = 650 + 750 * Math.Sin(u * Math.PI * 0.5) * (u < 0.8 ? 1 : (1 - u) / 0.2 * 1);
                ph += f / Mixer.Rate;
                double s = Math.Sin(2 * Math.PI * ph) * 0.6 + Math.Sin(4 * Math.PI * ph) * 0.2 + Math.Sin(6 * Math.PI * ph) * 0.1;
                return s * 0.45 * Env(t, 1.5, 0.02, 0.15);
            });
        }

        // a party blower: buzzy, wobbly, two toots
        public static short[] Horn()
        {
            double ph = 0; var rng = new Random(3);
            return Render(1.0, t =>
            {
                bool on = t < 0.22 || t > 0.32;
                double f = (t < 0.22 ? 470 : 520) + 18 * Math.Sin(t * 2 * Math.PI * 7);
                ph += f / Mixer.Rate;
                double sq = (ph - Math.Floor(ph)) < 0.5 ? 1 : -1;
                double saw = 2 * (ph - Math.Floor(ph)) - 1;
                double s = 0.5 * sq + 0.4 * saw + (rng.NextDouble() - 0.5) * 0.15;
                double env = t < 0.22 ? Env(t, 0.22, 0.01, 0.03) : Env(t - 0.32, 0.68, 0.02, 0.2);
                return on ? s * 0.28 * env : 0;
            });
        }

        // a cartoon gunshot "pew"
        public static short[] Pew()
        {
            double ph = 0; var rng = new Random(5);
            return Render(0.32, t =>
            {
                double f = 1800 * Math.Exp(-t * 9) + 250;
                ph += f / Mixer.Rate;
                double crack = t < 0.025 ? (rng.NextDouble() * 2 - 1) * (1 - t / 0.025) : 0;
                return (Math.Sin(2 * Math.PI * ph) * 0.5 * Env(t, 0.32, 0.003, 0.12) + crack * 0.8) * 0.8;
            });
        }

        // the ricochet "pyeew"
        public static short[] Ricochet()
        {
            double ph = 0;
            return Render(0.28, t =>
            {
                double f = 2600 - 2400 * t / 0.28 + 120 * Math.Sin(t * 2 * Math.PI * 30);
                ph += f / Mixer.Rate;
                return Math.Sin(2 * Math.PI * ph) * 0.35 * Env(t, 0.28, 0.004, 0.1);
            });
        }

        // a soft whoosh for floating off (filtered noise rising)
        public static short[] Whoosh(double secs)
        {
            var rng = new Random(9); double lp = 0;
            return Render(secs, t =>
            {
                double u = t / secs, a = 0.02 + 0.2 * u;
                lp += a * ((rng.NextDouble() * 2 - 1) - lp);
                return lp * 1.6 * Env(t, secs, 0.15, 0.4);
            });
        }
    }
}
