// Gravity: he always ends up on the taskbar. Fling him and he flies, bounces off the screen edges
// and lands with a cheek clap, then skids to a stop. On by default; can be turned off in the menu.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace BigBootyCheems
{
    partial class Buddy
    {
        bool gravity = true, flying, astroFlight;
        int astroStart;
        float flyX, flyY, flyVX, flyVY;
        int lastBonk;
        ToolStripMenuItem gravityItem;
        const float G = 2600f;

        void SetupGravity()
        {
            gravity = LoadSetting("Gravity") != "0";
            gravityItem = new ToolStripMenuItem("Gravity (throw him about)", null, (s, e) =>
            {
                gravity = !gravity; gravityItem.Checked = gravity; SaveSetting("Gravity", gravity ? "1" : "0");
                if (!gravity) flying = false;
            }) { Checked = gravity };
            menu.Items.Insert(2, gravityItem);
        }

        // where his window sits when his feet are on the taskbar, and how far left/right he can go
        Rectangle FlightArea()
        {
            var wa = Screen.FromRectangle(new Rectangle(winX, winY, cw, ch)).WorkingArea;
            return Rectangle.FromLTRB(wa.Left - Tune.Pad, wa.Top - Tune.Pad, wa.Right - cw + Tune.Pad, wa.Bottom - ch + Tune.Pad + Tune.FeetSink);
        }

        // let go mid-drag: off he goes
        void Throw(float vx, float vy)
        {
            float sp = (float)Math.Sqrt(vx * vx + vy * vy);
            if (sp > 5000) { vx *= 5000 / sp; vy *= 5000 / sp; }
            flyX = winX; flyY = winY; flyVX = vx; flyVY = vy;
            flying = true;
            timer.Start();
        }

        void GravityTick(int now, float dt)
        {
            if ((!gravity && !astroFlight) || dragging || floating || jetting) { if (dragging) { flying = false; astroFlight = false; } return; }
            var a = FlightArea();
            if (!flying)
            {
                if (winY < a.Bottom - 1) { flyX = winX; flyY = winY; flyVX = 0; flyVY = 0; flying = true; timer.Start(); }   // in mid-air: drop
                else { if (winY > a.Bottom + 1) winY = a.Bottom; return; }
            }
            bool lowG = astroFlight || CurrentOutfit == Outfit.Astro;     // the full spacesuit: always floaty
            if (lowG)
            {
                // the poop is a rocket: thrust up and away while it sprays, then drift in low gravity
                float t = (now - astroStart) / 1000f;
                if (astroFlight && t < 0.55f) { flyVX -= 1500 * dt; flyVY -= 1250 * dt; if (spray != null && !spray.IsDisposed) spray.SetEmit(ButtHole()); }
                flyVY += 210 * dt;
                float k = 1 - 1.3f * dt; flyVX *= k; flyVY *= k;
            }
            else
            {
                flyVY += G * dt;
                flyVX *= 1 - 0.15f * dt;
            }
            flyX += flyVX * dt; flyY += flyVY * dt;
            // walls: his butt is on the right, so the right wall is a proper cheek slap
            if (flyX > a.Right) { flyX = a.Right; Bonk(Math.Abs(flyVX), true, now); flyVX = -flyVX * 0.55f; }
            if (flyX < a.Left) { flyX = a.Left; Bonk(Math.Abs(flyVX), false, now); flyVX = -flyVX * 0.55f; }
            if (flyY < a.Top) { flyY = a.Top; flyVY = Math.Abs(flyVY) * 0.4f; }
            if (flyY >= a.Bottom)
            {
                flyY = a.Bottom;
                float impact = flyVY;
                if (lowG && impact > 40) { astroFlight = false; Landed(380); flyVY = 0; }     // a soft moon landing
                else if (lowG) { astroFlight = false; flyVY = 0; flyVX *= (float)Math.Exp(-3 * dt); if (Math.Abs(flyVX) < 15) { flyVX = 0; flying = false; } }
                else if (impact > 260)
                {
                    // hits the deck: cheeks clap, a squash, and a bounce
                    Clap(impact, now);
                    Landed(Math.Min(4000, impact));
                    flyVY = -impact * 0.38f;
                }
                else
                {
                    flyVY = 0;
                    flyVX *= (float)Math.Exp(-7 * dt);              // skid to a stop
                    if (Math.Abs(flyVX) < 25) { flyVX = 0; flying = false; }
                }
            }
            winX = (int)Math.Round(flyX); winY = (int)Math.Round(flyY);
        }

        void StartAstro()
        {
            astroFlight = true; astroStart = Environment.TickCount; astroRebound = true;
            flyX = winX; flyY = winY; flyVX = -150; flyVY = -150; flying = true;
            timer.Start();
        }

        // astro poop that hits the wall: some of it bounces back off and floats about
        bool astroRebound;
        void OnWallHit()
        {
            splatSound.Play(0.85f);
            if (!astroRebound || spray == null || spray.IsDisposed) return;
            astroRebound = false;
            var scr = Screen.FromRectangle(new Rectangle(winX, winY, cw, ch));
            var f = FxUnder();
            for (int i = 0; i < 12; i++)
                f.Add(new Particle
                {
                    Kind = Particle.Blob, X = scr.Bounds.Right - Rnd(8, 20), Y = astroHitY + Rnd(-40, 40), Delay = Rnd(0, 0.5f),
                    VX = Rnd(-260, -50), VY = Rnd(-90, 40), Gravity = -5, Drag = 0.04f, Bounce = 0.9f,
                    Size = Rnd(7, 14), Life = Rnd(7.5f, 9.5f)
                });
        }
        int astroHitY;

        Point ButtCentre()
        {
            return new Point(winX + Tune.Pad + (int)(Tune.ButtCX * Tune.Scale), winY + Tune.Pad + (int)(Tune.ButtCY * Tune.Scale));
        }

        void Clap(float speed, int now)
        {
            if (now - lastBonk < 120) return;
            lastBonk = now;
            var b = ButtCentre();
            DoSlap(b.X, b.Y + 10, 0, Math.Min(1.7f, 0.35f + speed / 1600f));
        }

        void Bonk(float speed, bool buttSide, int now)
        {
            if (speed < 250 || now - lastBonk < 120) return;
            lastBonk = now;
            var b = ButtCentre();
            float p = Math.Min(1.7f, 0.3f + speed / 1800f);
            if (buttSide) DoSlap(b.X + 20, b.Y, -1, p);             // butt into the wall
            else DoSlap(b.X - 30, b.Y, 1, p * 0.6f);                 // face-first: the cheeks still slosh
        }
    }
}
