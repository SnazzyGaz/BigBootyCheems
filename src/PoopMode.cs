// Non-stop pooping (menu switch, per cheems). The liquid outfits spray in one endless stream; the
// others repeat their trick every so often. On roller skates he's propelled round the edges of the
// screen: along the taskbar, up the left side, across the top, down the right, and round again.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BigBootyCheems
{
    partial class Buddy
    {
        bool poopMode;
        ToolStripMenuItem poopItem;
        static int lastPoopSound = Environment.TickCount - 3600000;   // shared, so several poopers don't stack up
        int lastTrick, lastAstroKick;

        void SetupPoopMode()
        {
            poopItem = new ToolStripMenuItem("Non-stop pooping", null, (s, e) =>
            {
                poopMode = !poopMode; poopItem.Checked = poopMode;
                if (poopMode) { Mixer.Warm(); timer.Start(); }
            });
            menu.Items.Insert(4, poopItem);
            if (Environment.GetEnvironmentVariable("BBC_POOPMODE") == "1") { poopMode = true; poopItem.Checked = true; }   // testing
        }

        static bool LiquidOutfit(Outfit o)
        {
            return o == Outfit.None || o == Outfit.Leather || o == Outfit.Builder || o == Outfit.Cop || o == Outfit.Jet || o == Outfit.Astro;
        }

        void PoopModeTick(int now, float dt)
        {
            if (!poopMode)
            {
                if (crawling) EndCrawl();
                if (spray != null && !spray.IsDisposed && spray.Continuous) spray.Continuous = false;
                return;
            }
            var o = CurrentOutfit;
            if (!LiquidOutfit(o))
            {
                if (spray != null && !spray.IsDisposed && spray.Continuous) spray.Continuous = false;
                if (crawling) EndCrawl();
                if (o == Outfit.Saiyan) { if (!ssj) Spray(); }
                else if (now - lastTrick > 1600 && !dragging) { lastTrick = now; Spray(); }
                return;
            }

            // roller skates: round and round the screen edges
            if (o == Outfit.Jet && !dragging) { if (!crawling) StartCrawl(); CrawlTick(dt); }
            else if (crawling) EndCrawl();

            Point emit; float dx, dy;
            CrawlEmit(out emit, out dx, out dy);
            if (spray == null || spray.IsDisposed || !spray.Continuous)
            {
                EnsureSpray(emit, true);
                spray.Continuous = true;
            }
            if (!crawling) dx *= Facing;
            spray.SetEmit(emit); spray.SetDir(dx, dy);
            if (now - lastPoopSound > 900) { lastPoopSound = now; poopSound.Play(0.8f); }

            if (o == Outfit.Cop && !CopOn) StartCop();
            if (o == Outfit.Builder) CordonOff(emit, spray);
            if (o == Outfit.Astro && now - lastAstroKick > 2600 && !dragging) { lastAstroKick = now; SetFlip(!flip); CrawlEmit(out emit, out dx, out dy); astroHitY = emit.Y; StartAstro(); }   // turns round each time, so he zig-zags about
            timer.Start();
        }

        // ---------------------------------------------------------- crawling the screen edges
        // side 0: along the taskbar going left, 1: up the left edge, 2: along the top going right,
        // 3: down the right edge. His picture is turned 90 degrees clockwise per side (rot).
        bool crawling; int rot; float crawlX, crawlY; Rectangle crawlArea;
        const float CrawlSpeed = 430;

        void StartCrawl()
        {
            SetFlip(false);       // the lap is laid out for his usual direction
            crawling = true; rot = 0; flying = false; jetting = false; floating = false;
            crawlArea = Screen.FromRectangle(new Rectangle(winX, winY, cw, ch)).WorkingArea;
            crawlX = winX; crawlY = crawlArea.Bottom - ch + Tune.Pad + Tune.FeetSink;
            timer.Start();
        }

        void EndCrawl()
        {
            crawling = false;
            if (rot != 0)
            {
                // back upright where he is; gravity (if on) takes it from here
                var r = CrawlRect();
                winX = r.X + r.Width / 2 - cw / 2; winY = r.Y + r.Height / 2 - ch / 2;
                rot = 0; artValid = false;
            }
            flip = flipPref; artValid = false;
            Render();
        }

        // his window on screen while turned (width and height swap on the sides)
        Rectangle CrawlRect()
        {
            bool side = rot % 2 == 1;
            return new Rectangle((int)crawlX, (int)crawlY, side ? ch : cw, side ? cw : ch);
        }

        void CrawlTick(float dt)
        {
            var a = crawlArea; float v = CrawlSpeed * dt; int P = Tune.Pad, S = Tune.FeetSink;
            switch (rot)
            {
                case 0:
                    crawlX -= v; crawlY = a.Bottom - ch + P + S;
                    if (crawlX <= a.Left - P) { rot = 1; crawlX = a.Left - P - S; crawlY = a.Bottom - cw + P; }
                    break;
                case 1:
                    crawlY -= v; crawlX = a.Left - P - S;
                    if (crawlY <= a.Top - P) { rot = 2; crawlY = a.Top - P - S; crawlX = a.Left - P; }
                    break;
                case 2:
                    crawlX += v; crawlY = a.Top - P - S;
                    if (crawlX + cw >= a.Right + P) { rot = 3; crawlX = a.Right + P + S - ch; crawlY = a.Top - P; }
                    break;
                case 3:
                    crawlY += v; crawlX = a.Right + P + S - ch;
                    if (crawlY + cw >= a.Bottom + P) { rot = 0; crawlY = a.Bottom - ch + P + S; crawlX = a.Right - cw + P; }
                    break;
            }
            winX = (int)crawlX; winY = (int)crawlY;
        }

        // where the poop comes out and which way it goes, allowing for the turn
        void CrawlEmit(out Point emit, out float dx, out float dy)
        {
            var b = ButtHole();
            int lx = b.X - winX, ly = b.Y - winY;           // in his own (upright) canvas
            int nx = lx, ny = ly; dx = 1; dy = 0;
            switch (rot)
            {
                case 1: nx = ch - 1 - ly; ny = lx; dx = 0; dy = 1; break;            // poop goes down the wall
                case 2: nx = cw - 1 - lx; ny = ch - 1 - ly; dx = -1; dy = 0; break;  // along the top, leftwards
                case 3: nx = ly; ny = cw - 1 - lx; dx = 0; dy = -1; break;           // up the right side
            }
            emit = new Point(winX + nx, winY + ny);
        }

        // ---------------------------------------------------------- drawing him turned
        IntPtr rotDC, rotDib, rotBits; int[] rotPx; int rotS;

        void PushRotated()
        {
            if (closing || IsDisposed) return;
            if (rotDib == IntPtr.Zero)
            {
                rotS = Math.Max(cw, ch);
                var bmi = new Native.BITMAPINFO { biSize = Marshal.SizeOf(typeof(Native.BITMAPINFO)), biWidth = rotS, biHeight = -rotS, biPlanes = 1, biBitCount = 32 };
                IntPtr sdc0 = Native.GetDC(IntPtr.Zero);
                rotDC = Native.CreateCompatibleDC(sdc0);
                rotDib = Native.CreateDIBSection(sdc0, ref bmi, 0, out rotBits, IntPtr.Zero, 0);
                Native.ReleaseDC(IntPtr.Zero, sdc0);
                Native.SelectObject(rotDC, rotDib);
                rotPx = new int[rotS * rotS];
            }
            int W = rot % 2 == 1 ? ch : cw, H = rot % 2 == 1 ? cw : ch;
            for (int y = 0; y < ch; y++)
            {
                int row = y * cw;
                for (int x = 0; x < cw; x++)
                {
                    int nx, ny;
                    switch (rot)
                    {
                        case 1: nx = ch - 1 - y; ny = x; break;
                        case 2: nx = cw - 1 - x; ny = ch - 1 - y; break;
                        default: nx = y; ny = cw - 1 - x; break;
                    }
                    rotPx[ny * rotS + nx] = outPx[row + x];
                }
            }
            Marshal.Copy(rotPx, 0, rotBits, rotPx.Length);
            Native.GdiFlush();
            IntPtr sdc = Native.GetDC(IntPtr.Zero);
            var dst = new Native.POINT { x = winX, y = winY }; var sz = new Native.SIZE { cx = W, cy = H }; var s0 = new Native.POINT();
            var bf = new Native.BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = 1 };
            Native.UpdateLayeredWindow(Handle, sdc, ref dst, ref sz, rotDC, ref s0, 0, ref bf, 2);
            Native.ReleaseDC(IntPtr.Zero, sdc);
        }
    }
}
