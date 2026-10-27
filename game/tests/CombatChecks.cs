using Godot;
using V = System.Numerics.Vector2;

namespace Mosslight;
/// <summary>Fixed-step regressions against production combat, collision and persistence.</summary>
public static class CombatChecks
{
    const float Dt = 1f / 60;
    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World;
        string saved = Persistence.Capture(w, app.Narrative);
        try
        {
            var r = new RegionState { Id = "combat-regression", Width = 64, Height = 48, Tiles = Enumerable.Repeat(Terrain.Grass, 64 * 48).ToArray() };
            w.RegionsById[r.Id] = r;
            var p = w.Player;
            p.Region = r.Id; p.X = 500; p.Y = 500; p.State = ActionState.Idle;
            p.Mana = p.MaxMana; p.Cooldowns.Clear();
            w.Items.Equip(p, "iron_sword", "weapon");
            check(w.TryStartPlayerSlash(V.UnitX), "free slash starts in an empty region");
            check(p.Cooldowns.GetValueOrDefault("slash") > 0 && p.FreeSlash, "empty slash commits its normal cooldown");
            for (int i = 0; i < 4; i++) w.Step(Dt);
            check(Math.Abs(p.X - 500) < .001f, "lunge does not start before 0.08 seconds");
            w.Step(Dt);
            check(p.X > 500 && p.X < 501, "lunge starts smoothly at the first tick after 0.08 seconds");
            for (int i = 0; i < 3; i++) { w.PlayerMove(V.UnitY, Dt, 3.2f, true); w.Step(Dt); }
            check(p.State == ActionState.Windup, "slash windup has not resolved before tick nine");
            w.Step(Dt);
            check(p.State == ActionState.Recovery, "empty slash resolves at tick nine into recovery");
            for (int i = 0; i < 2; i++) w.Step(Dt);
            check(Math.Abs(p.X - 512) < .02f && Math.Abs(p.Y - 500) < .02f, "lunge completes by 0.18 seconds and ignores changed movement input");
            for (int i = 0; i < 18; i++) w.Step(Dt);
            check(p.State == ActionState.Idle && Math.Abs(p.X - 512) < .02f && Math.Abs(p.Y - 500) < .02f, "empty slash completes one twelve-unit forward lunge");
            var enemy = w.Actors.Values.First(a => a.Profile == "beast");
            enemy.Region = r.Id; enemy.X = 540; enemy.Y = 500; enemy.Health = enemy.MaxHealth = 1000;
            enemy.State = ActionState.Recovery; enemy.StateRemaining = 100; enemy.MotionX = enemy.MotionY = 0;
            p.X = 500; p.Y = 500; p.Cooldowns.Clear();
            float health = enemy.Health;
            check(w.TryStartPlayerSlash(V.UnitX), "directional slash starts with enemy ahead");
            for (int i = 0; i < 9; i++) w.Step(Dt);
            float expectedDamage = Math.Max(1, w.Content.Abilities.Single(a => a.Id == "slash").Damage + w.Stat(p, "strength") + w.Stat(p, "damage") - w.Stat(enemy, "defence"));
            check(Math.Abs((health - enemy.Health) - expectedDamage) < .01f, "directional slash uses the existing strength, weapon and defence calculation");
            p.State = ActionState.Idle; p.FreeSlash = false; p.X = 500; p.Y = 500; p.Cooldowns.Clear();
            enemy.X = 470; enemy.Y = 500; enemy.Health = health; enemy.Invulnerable = 0; enemy.State = ActionState.Recovery; enemy.StateRemaining = 100;
            check(w.TryStartPlayerSlash(V.UnitX), "directional slash starts with enemy behind");
            for (int i = 0; i < 9; i++) w.Step(Dt);
            check(enemy.Health == health, "directional slash misses enemy behind");
            void ResetFight()
            {
                w.Cancel(p); p.X = 500; p.Y = 500; p.Cooldowns.Clear(); p.Invulnerable = 0;
                enemy.Region = r.Id; enemy.Health = enemy.MaxHealth = 1000; enemy.Invulnerable = 0;
                enemy.State = ActionState.Recovery; enemy.StateRemaining = 100; enemy.MotionX = enemy.MotionY = 0;
            }
            ResetFight(); enemy.X = 700; enemy.Y = 500;
            w.TryStartPlayerSlash(V.UnitX);
            for (int i = 0; i < 8; i++) w.Step(Dt);
            enemy.X = p.X + 30; enemy.Y = p.Y; w.Step(Dt);
            check(enemy.Health < 1000, "enemy entering reach during windup is evaluated at impact");
            ResetFight(); enemy.X = 530; enemy.Y = 500;
            w.TryStartPlayerSlash(V.UnitX);
            for (int i = 0; i < 8; i++) w.Step(Dt);
            enemy.X = 700; w.Step(Dt);
            check(enemy.Health == 1000, "enemy leaving reach during windup avoids the hit");
            foreach (float degrees in new[] { 54.9f, 55.1f })
            {
                ResetFight(); enemy.X = 700; enemy.Y = 500; w.TryStartPlayerSlash(V.UnitX);
                for (int i = 0; i < 8; i++) w.Step(Dt);
                float angle = degrees * MathF.PI / 180;
                // Account for the next fixed-step lunge; test the impact position.
                enemy.X = p.X + 2.81f + 35 * MathF.Cos(angle); enemy.Y = p.Y + 35 * MathF.Sin(angle);
                w.Step(Dt);
                check((enemy.Health < 1000) == (degrees < 55), "slash half-angle excludes targets beyond 55 degrees: " + degrees);
            }
            ResetFight(); enemy.X = 540; enemy.Y = 500;
            var other = w.Actors.Values.Where(a => a.Profile == "beast" && a.Id != enemy.Id).First();
            other.Region = r.Id; other.X = enemy.X; other.Y = enemy.Y; other.Health = other.MaxHealth = 1000;
            other.Invulnerable = 0; other.State = ActionState.Recovery; other.StateRemaining = 100;
            w.TryStartPlayerSlash(V.UnitX);
            for (int i = 0; i < 9; i++) w.Step(Dt);
            var first = StringComparer.Ordinal.Compare(enemy.Id, other.Id) < 0 ? enemy : other;
            var second = first == enemy ? other : enemy;
            check(first.Health < 1000 && second.Health == 1000, "equal-distance targets use ordinal ID and only one is damaged");
            other.Region = "reach";
            ResetFight(); p.X = 490; enemy.X = 545; enemy.Y = 500;
            for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 21] = Terrain.Rock;
            w.TryStartPlayerSlash(V.UnitX); for (int i = 0; i < 20; i++) w.Step(Dt);
            check(enemy.Health == 1000 && r.CanStand(p.Position, p) && p.X <= 504 - p.Radius + .05f && p.X < 501.9f, "wall blocks slash line of sight and lunge collision");
            ResetFight(); p.X = 490; enemy.X = 700;
            w.TryStartPlayerSlash(V.Normalize(new V(1,1))); for (int i = 0; i < 15; i++) w.Step(Dt);
            check(r.CanStand(p.Position,p) && p.X <= 504-p.Radius+.05f && p.Y > 500, "diagonal lunge slides safely along wall");
            for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 21] = Terrain.Grass;
            ResetFight(); p.X = 490; enemy.X = 700;
            int door=20*r.Width+21; r.Features[door]="lock";
            w.TryStartPlayerSlash(V.UnitX); for (int i=0;i<15;i++) w.Step(Dt);
            check(r.CanStand(p.Position,p) && p.X <= 504-p.Radius+.05f, "locked door blocks lunge");
            r.Features.Remove(door);
            ResetFight(); p.X=p.Radius+1; enemy.X=700;
            w.TryStartPlayerSlash(-V.UnitX); for (int i=0;i<15;i++) w.Step(Dt);
            check(r.CanStand(p.Position,p) && p.X>=p.Radius-.05f, "map boundary blocks lunge");
            ResetFight(); enemy.X = 700; w.TryStartPlayerSlash(V.UnitX);
            for (int i = 0; i < 6; i++) w.Step(Dt);
            float stopped = p.X; w.Cancel(p);
            for (int i = 0; i < 20; i++) w.Step(Dt);
            check(Math.Abs(p.X - stopped) < .001f, "interrupted lunge never resumes unused displacement");
            ResetFight(); p.Mana = p.MaxMana;
            if (!p.Abilities.Contains("ward")) p.Abilities.Add("ward");
            check(w.Activate(p.Id, "ward", p.Id), "targeted self-buff still starts through SPELL");
            for (int i = 0; i < 24; i++) w.Step(Dt);
            check(p.Statuses.Any(x => x.Id == "ward"), "non-damaging self-buff still applies its status");
            w.Cancel(p); p.Cooldowns.Clear(); p.Mana = p.MaxMana; p.Health = p.MaxHealth - 40;
            float beforeHealing = p.Health;
            check(w.Activate(p.Id, "mend", p.Id), "targeted self-heal still starts through SPELL");
            for (int i = 0; i < 35; i++) w.Step(Dt);
            check(p.Health > beforeHealing, "targeted healing still restores health");
            w.Cancel(p); p.Cooldowns.Clear(); p.Grasps = false;
            check(!w.TryStartPlayerSlash(V.UnitX) && !p.Cooldowns.ContainsKey("slash"), "incompatible anatomy rejects slash without cooldown");
        }
        finally { Persistence.Restore(w, app.Narrative, saved); app.RebuildViews(); }
    }
}
