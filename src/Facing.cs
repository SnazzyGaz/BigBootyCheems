// Facing, stacking and the screensaver.
// - Any cheems can face the other way (menu, remembered per cheems). His art is drawn mirrored and
//   everything that cares which side his butt is on (hits, poop, recoil) uses LX/SX/Facing.
// - Cheemses keep a fixed front-to-back order: newer ones in front, and clicking one doesn't lift it.
// - Screensaver: after a while with no mouse or keyboard, one cheems quietly puts on skates or the
//   spacesuit and poops his way round the screen until you're back.
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BigBootyCheems
{
    partial class Buddy
    {
        bool flip;              // facing right (butt on the left) right now
        bool flipPref;          // what the menu says
        ToolStripMenuItem flipItem;

        // +1: poop goes right (normal), -1: poop goes left (flipped)
        float Facing { get { return flip ? -1f : 1f; } }

        // screen x -> x in his own (unflipped) art, and back
        int LX(int screenX) { int x = screenX - winX; if (flip) x = cw - 1 - x; return x - Tune.Pad; }
        int SX(float localX) { int x = Tune.Pad + (int)localX; if (flip) x = cw - 1 - x; return winX + x; }

        void Mirror(int[] px)
        {
            for (int y = 0; y < ch; y++)
            {
                int a = y * cw, b = a + cw - 1;
                while (a < b) { int t = px[a]; px[a] = px[b]; px[b] = t; a++; b--; }
            }
        }

        void SetFlip(bool f)
        {
            if (flip == f) return;
            flip = f; artValid = false;
            if (IsHandleCreated) Render();
        }

        void SetupFacing()
        {
            flipPref = LoadSetting(UnitKey("Flip")) == "1";
            flip = flipPref;
            flipItem = new ToolStripMenuItem("Face the other way", null, (s, e) =>
            {
                flipPref = !flipPref; flipItem.Checked = flipPref; SaveSetting(UnitKey("Flip"), flipPref ? "1" : "0");
                SetFlip(flipPref);
            }) { Checked = flipPref };
        }

        // ---------------------------------------------------------- stacking: oldest at the back, newest in front
        public static void RestackAll()
        {
            foreach (var b in All.OrderBy(x => x.unit))
            {
                if (b.closing || !b.IsHandleCreated) continue;
                Native.SetWindowPos(b.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
                if (b.spray != null && !b.spray.IsDisposed) Native.SetWindowPos(b.spray.Handle, b.Handle, 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
                if (b.fxUnder != null && !b.fxUnder.IsDisposed) Native.SetWindowPos(b.fxUnder.Handle, b.Handle, 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
            }
        }

        // is every cheems above all the older ones? (clicking one can lift it out of order)
        static bool StackInOrder()
        {
            var list = All.Where(b => !b.closing && b.IsHandleCreated).OrderBy(b => b.unit).ToList();
            for (int i = 1; i < list.Count; i++)
            {
                IntPtr older = list[i - 1].Handle;
                IntPtr h = Native.GetWindow(list[i].Handle, 3);       // windows above the newer one
                for (int k = 0; k < 600 && h != IntPtr.Zero; k++, h = Native.GetWindow(h, 3))
                    if (h == older) return false;
            }
            return true;
        }

        // clicks must never lift a cheems (and he never takes the focus)
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        // ---------------------------------------------------------- screensaver
        static int saverMinutes = 5;            // 0 = off
        static bool saverOn;
        bool saverSound; string[] saverOutfit;
        ToolStripMenuItem saverItem;

        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public int cbSize; public int dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO lii);

        static int IdleMs()
        {
            var li = new LASTINPUTINFO { cbSize = Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            try { if (GetLastInputInfo(ref li)) return Environment.TickCount - li.dwTime; } catch { }
            return 0;
        }

        void SetupSaver()
        {
            int m; var s = LoadSetting("Screensaver");
            if (s != null && int.TryParse(s, out m)) saverMinutes = m;
            saverItem = new ToolStripMenuItem("Screensaver");
            foreach (var mins in new[] { 0, 2, 5, 10, 20, 30 })
            {
                int v = mins;
                var it = new ToolStripMenuItem(v == 0 ? "Off" : "After " + v + " min idle", null, (o, e) => { saverMinutes = v; SaveSetting("Screensaver", v.ToString()); });
                saverItem.DropDownItems.Add(it);
            }
            saverItem.DropDownOpening += (o, e) =>
            {
                for (int i = 0; i < saverItem.DropDownItems.Count; i++)
                    ((ToolStripMenuItem)saverItem.DropDownItems[i]).Checked = new[] { 0, 2, 5, 10, 20, 30 }[i] == saverMinutes;
            };
            if (Environment.GetEnvironmentVariable("BBC_SAVER") == "1") saverMinutes = -1;   // testing: start after 10 s
        }

        int lastSaverCheck, saverLastMove = Environment.TickCount; static System.Drawing.Point saverLastCursor;
        void SaverTick(int now)
        {
            if (All.Count == 0 || All[0] != this) return;          // only the first cheems does the screensaver
            if (now - lastSaverCheck < 400) return;
            lastSaverCheck = now;
            // Windows' own idle clock, plus the mouse moving as a backstop
            var c = Cursor.Position;
            if (c != saverLastCursor) { saverLastCursor = c; saverLastMove = now; }
            int idle = Math.Min(IdleMs(), now - saverLastMove);
            int after = saverMinutes < 0 ? 10000 : saverMinutes * 60000;
            if (!saverOn && saverMinutes != 0 && idle > after && !dragging) StartSaver();
            else if (saverOn && idle < 1500) StopSaver();
        }

        Slot SlotFor(string key) { foreach (var s in slots) if (s.Key == key) return s; return null; }

        void StartSaver()
        {
            saverOn = true;
            saverOutfit = slots.Select(s => s.Current).ToArray();
            saverSound = Mixer.Muted;
            Mixer.Muted = true;                                    // a silent screensaver
            if (new Random().Next(2) == 0)
            {
                var sh = SlotFor("Shoes"); if (sh != null && sh.Names.Contains("roller-skates")) Wear(sh, "roller-skates", false);
            }
            else
            {
                var h = SlotFor("Hat"); var t = SlotFor("Top");
                if (h != null && h.Names.Contains("astronaut-helmet")) Wear(h, "astronaut-helmet", false);
                if (t != null && t.Names.Contains("spacesuit")) Wear(t, "spacesuit", false);
            }
            poopMode = true; if (poopItem != null) poopItem.Checked = true;
            timer.Start();
        }

        void StopSaver()
        {
            saverOn = false;
            poopMode = false; if (poopItem != null) poopItem.Checked = false;
            if (crawling) EndCrawl();
            for (int i = 0; i < slots.Count && saverOutfit != null; i++)
                if (slots[i].Current != saverOutfit[i]) Wear(slots[i], saverOutfit[i], false);
            Mixer.Muted = saverSound;
            SetFlip(flipPref);
        }
    }
}
