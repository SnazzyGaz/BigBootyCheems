// Super Saiyan: pooping while wearing the full Saiyan kit (hair, scouter, armour, boots) powers him
// up for a few seconds: white flash, gold hair, a flickering golden aura and crackling sparks.
using System;
using System.Collections.Generic;

namespace BigBootyCheems
{
    partial class Buddy
    {
        const int SsjMs = 8000, SsjFlashMs = 350;
        int ssjStart; bool ssj;
        int[] ssjHair;          // gold copy of the worn hair
        float[] glow;           // soft halo around his silhouette (art size + pad), 0..1
        short[] ssjSound;
        readonly Random ssjRng = new Random();

        bool FullSaiyan
        {
            get
            {
                var want = new Dictionary<string, string> { { "Hat", "saiyanhair" }, { "Face", "Saiyanscouter" }, { "Top", "saiyanarmour" }, { "Shoes", "saiyanshoe" } };
                int ok = 0;
                foreach (var s in slots) { string v; if (want.TryGetValue(s.Key, out v) && s.Current == v) ok++; }
                return ok == 4;
            }
        }

        void StartSsj()
        {
            int now = Environment.TickCount;
            if (ssjSound == null) ssjSound = MakePowerUp();
            Mixer.Play(ssjSound, 0.8f);
            ssjStart = now;
            if (ssj) return;            // already powered up: just restart the clock
            ssj = true;
            if (glow == null) glow = MakeGlow();
            RebuildFrames(); artValid = false;
            timer.Start();
        }

        void SsjTick(int now)
        {
            if (!ssj) return;
            if (now - ssjStart > SsjMs || !FullSaiyan) { ssj = false; RebuildFrames(); artValid = false; Render(); }
        }

        // the hair layer to bake into his frames: gold while powered up
        int[] HairFor(Slot s, int[] px)
        {
            if (!ssj || s.Key != "Hat" || s.Current != "saiyanhair") return px;
            if (ssjHair == null || ssjHairOf != px) { ssjHair = Goldify(px); ssjHairOf = px; }
            return ssjHair;
        }
        int[] ssjHairOf;

        // dark hair -> gold, keeping its shading (darkest stays a deep amber outline)
        static int[] Goldify(int[] px)
        {
            var o = new int[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 0xFF;
                if (a == 0) continue;
                float r = ((c >> 16) & 0xFF) * 255f / a, g = ((c >> 8) & 0xFF) * 255f / a, b = (c & 0xFF) * 255f / a;
                float t = Math.Min(1f, (0.3f * r + 0.59f * g + 0.11f * b) / 90f);
                float R, G, B;
                if (t < 0.35f) { float u = t / 0.35f; R = 150 + 95 * u; G = 90 + 112 * u; B = 10 + 30 * u; }
                else { float u = (t - 0.35f) / 0.65f; R = 245 + 10 * u; G = 202 + 46 * u; B = 40 + 130 * u; }
                o[i] = (a << 24) | ((int)(R * a / 255) << 16) | ((int)(G * a / 255) << 8) | (int)(B * a / 255);
            }
            return o;
        }

        // blurred silhouette of the bare hero, on the padded canvas
        float[] MakeGlow()
        {
            int P = Tune.Pad;
            var a = new float[cw * ch];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    a[(y + P) * cw + x + P] = ((baseOpen[y * w + x] >> 24) & 0xFF) / 255f;
            // three box blurs ~ gaussian, radius ~ 3*9 px
            var tmp = new float[a.Length];
            for (int pass = 0; pass < 3; pass++) { Box(a, tmp, cw, ch, 11, true); Box(tmp, a, cw, ch, 11, false); }
            float mx = 0; foreach (var v in a) mx = Math.Max(mx, v);
            for (int i = 0; i < a.Length; i++) a[i] = Math.Min(1f, a[i] / (mx * 0.55f));
            return a;
        }
        static void Box(float[] s, float[] d, int W, int H, int r, bool horiz)
        {
            int n = horiz ? W : H, m = horiz ? H : W;
            for (int j = 0; j < m; j++)
            {
                float sum = 0; int cnt = 0;
                for (int k = -r; k <= r; k++) if (k >= 0 && k < n) { sum += s[horiz ? j * W + k : k * W + j]; cnt++; }
                for (int i = 0; i < n; i++)
                {
                    d[horiz ? j * W + i : i * W + j] = sum / (2 * r + 1);
                    int add = i + r + 1, rem = i - r;
                    if (add < n) sum += s[horiz ? j * W + add : add * W + j];
                    if (rem >= 0) sum -= s[horiz ? j * W + rem : rem * W + j];
                }
            }
        }

        // aura behind him, flash over him, sparks on top. Works on the final frame (outPx).
        void DrawSsj(int[] o)
        {
            if (!ssj || glow == null) return;
            int now = Environment.TickCount;
            float t = (now - ssjStart) / 1000f;
            float fade = Math.Min(1f, Math.Min(t / 0.25f, (SsjMs / 1000f - t) / 0.8f)); if (fade <= 0) return;
            float flash = now - ssjStart < SsjFlashMs ? 1f - (now - ssjStart) / (float)SsjFlashMs : 0f;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(t * 23);
            for (int y = 0; y < ch; y++)
            {
                int row = y * cw;
                for (int x = 0; x < cw; x++)
                {
                    if (glow[row + x] < 0.02f && glow[Math.Min(ch - 1, y + 26) * cw + x] < 0.02f) continue;
                    // flames: the halo is stretched upwards in licks that rise and flicker
                    float lick = 0.5f + 0.5f * (float)Math.Sin(x * 0.19f + t * 7) * (float)Math.Sin(x * 0.053f - t * 3.1f + (y + t * 160) * 0.045f);
                    int ys = Math.Min(ch - 1, y + (int)(lick * 26));
                    float gv = Math.Max(glow[row + x], glow[ys * cw + x] * (0.6f + 0.4f * lick));
                    if (gv < 0.02f) continue;
                    int c = o[row + x], ca = (c >> 24) & 0xFF;
                    float fl = 0.7f + 0.3f * (float)Math.Sin(x * 0.21f + y * 0.05f + t * 9);
                    float A = Math.Min(1f, (float)Math.Pow(gv, 0.65) * fl * pulse * 1.5f) * fade * 0.9f;
                    if (flash > 0) A = Math.Min(1f, A + flash * gv);
                    if (A > 0.01f && ca < 255)
                    {
                        // gold aura, whiter towards the body
                        float k = A * (255 - ca) / 255f;
                        int ar = (int)(255 * k), ag = (int)((205 + 45 * gv) * k), ab = (int)((50 + 150 * gv * gv + 200 * flash) * Math.Min(1f, k));
                        c = Add(c, ((int)(k * 255) << 24) | (ar << 16) | (ag << 8) | Math.Min(ab, (int)(k * 255)));
                    }
                    if (flash > 0 && ca > 0)
                    {
                        int f = (int)(flash * 0.85f * ca);
                        c = Add(c, (0 << 24) | (f << 16) | (f << 8) | f);
                    }
                    o[row + x] = c;
                }
            }
            // electric crackle: a few short jagged bolts, re-rolled every frame, only now and then
            if (ssjRng.NextDouble() < 0.45)
            {
                int bolts = 1 + ssjRng.Next(3);
                for (int b = 0; b < bolts; b++) Bolt(o, fade);
            }
        }

        static int Add(int c, int d)
        {
            int r = 0;
            for (int sh = 0; sh < 32; sh += 8) r |= Math.Min(255, ((c >> sh) & 0xFF) + ((d >> sh) & 0xFF)) << sh;
            // keep it valid premultiplied
            int a = (r >> 24) & 0xFF;
            return (a << 24) | (Math.Min(a, (r >> 16) & 0xFF) << 16) | (Math.Min(a, (r >> 8) & 0xFF) << 8) | Math.Min(a, r & 0xFF);
        }

        void Bolt(int[] o, float fade)
        {
            // start somewhere in the halo
            int x = 0, y = 0;
            for (int tries = 0; tries < 30; tries++)
            {
                x = ssjRng.Next(cw); y = ssjRng.Next(ch);
                float g = glow[y * cw + x];
                if (g > 0.25f && g < 0.8f) break;
            }
            float ang = (float)(ssjRng.NextDouble() * Math.PI * 2);
            int segs = 4 + ssjRng.Next(4);
            for (int s = 0; s < segs; s++)
            {
                ang += (float)(ssjRng.NextDouble() - 0.5) * 2.2f;
                float len = 5 + (float)ssjRng.NextDouble() * 9;
                int nx = x + (int)(Math.Cos(ang) * len), ny = y + (int)(Math.Sin(ang) * len);
                Line(o, x, y, nx, ny, fade);
                x = nx; y = ny;
            }
        }

        void Line(int[] o, int x0, int y0, int x1, int y1, float fade)
        {
            int n = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= n; i++)
            {
                int x = x0 + (x1 - x0) * i / n, y = y0 + (y1 - y0) * i / n;
                for (int dy = 0; dy <= 1; dy++)
                {
                    int yy = y + dy;
                    if (x < 0 || yy < 0 || x >= cw || yy >= ch) continue;
                    int a = (int)(255 * fade), core = dy == 0 ? 255 : 170;
                    int v = core * a / 255;
                    // white core, pale blue-white edge
                    o[yy * cw + x] = Add(o[yy * cw + x], (v << 24) | (v * 220 / 255 << 16) | (v * 235 / 255 << 8) | v);
                }
            }
        }

        // ~1.4 s power-up: a rising buzzy hum with crackle, made on the spot (no extra sound file)
        static short[] MakePowerUp()
        {
            int n = Mixer.Rate * 14 / 10;
            var o = new short[n];
            var rng = new Random(7);
            double ph = 0, ph2 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Mixer.Rate, u = t / 1.4;
                double f = 70 + 260 * u * u;                       // pitch climbs
                ph += f / Mixer.Rate; ph2 += f * 1.505 / Mixer.Rate;
                double saw = 2 * (ph - Math.Floor(ph)) - 1, saw2 = 2 * (ph2 - Math.Floor(ph2)) - 1;
                double env = Math.Min(1, t / 0.05) * Math.Min(1, (1.4 - t) / 0.25);
                double crackle = rng.NextDouble() < 0.004 + 0.02 * u ? (rng.NextDouble() * 2 - 1) * 1.6 : 0;
                double v = (0.45 * saw + 0.25 * saw2) * (0.6 + 0.4 * Math.Sin(t * 2 * Math.PI * 11)) + crackle + (rng.NextDouble() * 2 - 1) * 0.08 * u;
                o[i] = (short)Math.Max(-32767, Math.Min(32767, v * env * 9000));
            }
            return o;
        }
    }
}
