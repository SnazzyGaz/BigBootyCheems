// More than one cheems: each is its own window with its own outfit, tools and menu. Which ones
// exist is remembered (Units=0,3,4 in settings.ini); each one's outfit is saved under its own number.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BigBootyCheems
{
    partial class Buddy
    {
        public static readonly List<Buddy> All = new List<Buddy>();
        public static ApplicationContext Context;
        readonly int unit;
        static readonly Random unitRng = new Random();

        // settings key for this cheems (the first keeps the plain names, so old settings still work)
        string UnitKey(string k) { return unit == 0 ? k : "U" + unit + "." + k; }

        static List<int> SavedUnits()
        {
            var ids = new List<int>();
            var s = LoadSetting("Units");
            if (!string.IsNullOrEmpty(s))
                foreach (var part in s.Split(',')) { int v; if (int.TryParse(part.Trim(), out v) && !ids.Contains(v)) ids.Add(v); }
            if (ids.Count == 0) ids.Add(0);
            return ids;
        }
        static void SaveUnits() { SaveSetting("Units", string.Join(",", All.Select(b => b.unit.ToString()).ToArray())); }

        public static void StartAll()
        {
            bool first = true;
            foreach (var id in SavedUnits()) { var b = new Buddy(id, first); first = false; b.Show(); }
            RestackAll();
        }

        public static void ExitAll()
        {
            foreach (var b in All.ToArray()) { try { b.Close(); } catch { } }
        }

        ToolStripMenuItem addItem, removeItem;

        void SetupUnits()
        {
            addItem = new ToolStripMenuItem("Add another Cheems", null, (s, e) => AddUnit());
            removeItem = new ToolStripMenuItem("Remove this Cheems", null, (s, e) => { forgetMe = true; Close(); });
            // lay out the lower part of the menu in a sensible order (clothes and tools go on top later)
            ToolStripItem updates = null, version = null, exit = null;
            foreach (ToolStripItem it in menu.Items)
            {
                if (it.Text == "Check for updates...") updates = it;
                else if (it.Text == "Exit") exit = it;
                else if (it.Text.StartsWith("Big Booty Cheems v")) version = it;
            }
            menu.Items.Clear();
            menu.Items.AddRange(new ToolStripItem[] {
                new ToolStripSeparator(), breathItem, autoItem, gravityItem, soundItem, flipItem,
                new ToolStripSeparator(), poopItem, saverItem,
                new ToolStripSeparator(), addItem, removeItem,
                new ToolStripSeparator(), updates, version,
                new ToolStripSeparator(), exit });
            if (unit != 0 && Environment.GetEnvironmentVariable("BBC_TESTREMOVE") == "1")   // testing: remove me after 6 s
            {
                var tr = new Timer { Interval = 6000 };
                tr.Tick += (s, e) => { tr.Stop(); forgetMe = true; Close(); };
                tr.Start();
            }
            menu.Closed += (s, e) => RestackAll();          // opening the menu brings this one forward; put the order back
            menu.Opening += (s, e) =>
            {
                removeItem.Enabled = All.Count > 1;
                flipItem.Checked = flipPref;
                if (gravityItem != null) gravityItem.Checked = gravity;     // shared switches: another cheems may have flipped them
                if (soundItem != null) soundItem.Checked = !Mixer.Muted;
            };
        }

        void AddUnit()
        {
            int id = All.Count == 0 ? 0 : All.Max(b => b.unit) + 1;
            var nb = new Buddy(id, false);
            nb.Show();
            SaveUnits();
            RestackAll();
        }

        // where a new cheems turns up: somewhere along the taskbar, dropping in from above if gravity's on
        void PlaceNew(Rectangle wa)
        {
            winX = wa.Left + unitRng.Next(Math.Max(1, wa.Width - cw));
            winY = gravity ? wa.Top + 40 : wa.Bottom - ch + Tune.Pad + Tune.FeetSink;
        }

        bool forgetMe;          // closed with "Remove this Cheems" (not by Exit)
        bool closing;           // on the way out: nothing may draw to this window any more

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            closing = true;
            timer.Stop(); poll.Stop();
            poopMode = false; crawling = false;
            // take this one's bits with it; unhook them first so they don't call back into a closed window
            foreach (var t in toolsOut.Values) if (!t.IsDisposed) t.Close();
            toolsOut.Clear();
            var sp = spray; spray = null;
            if (sp != null && !sp.IsDisposed) { sp.FrameReady = null; sp.WallHit = null; sp.Close(); }
            var fu = fxUnder; fxUnder = null;
            if (fu != null && !fu.IsDisposed) { fu.FrameReady = null; fu.Close(); }
            var f = fx; fx = null;
            if (f != null && !f.IsDisposed) f.Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            All.Remove(this);
            if (forgetMe) SaveUnits();
            if (All.Count == 0 && Context != null) Context.ExitThread();
        }
    }
}
