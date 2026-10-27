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
            for (int i = 0; i < 8; i++) w.Step(Dt);
            check(p.State == ActionState.Windup, "slash windup has not resolved before tick nine");
            w.Step(Dt);
            check(p.State == ActionState.Recovery, "empty slash resolves at tick nine into recovery");
            for (int i = 0; i < 20; i++) w.Step(Dt);
            check(p.State == ActionState.Idle && Math.Abs(p.X - 512) < .02f && Math.Abs(p.Y - 500) < .02f, "empty slash completes one twelve-unit forward lunge");
            var enemy = w.Actors.Values.First(a => a.Profile == "beast");
            enemy.Region = r.Id; enemy.X = 540; enemy.Y = 500; enemy.Health = enemy.MaxHealth = 1000;
            enemy.State = ActionState.Recovery; enemy.StateRemaining = 100; enemy.MotionX = enemy.MotionY = 0;
            p.X = 500; p.Y = 500; p.Cooldowns.Clear();
            float health = enemy.Health;
            check(w.TryStartPlayerSlash(V.UnitX), "directional slash starts with enemy ahead");
            for (int i = 0; i < 9; i++) w.Step(Dt);
            check(enemy.Health < health, "directional slash damages enemy ahead at impact");
            p.State = ActionState.Idle; p.FreeSlash = false; p.X = 500; p.Y = 500; p.Cooldowns.Clear();
            enemy.X = 470; enemy.Y = 500; enemy.Health = health; enemy.Invulnerable = 0; enemy.State = ActionState.Recovery; enemy.StateRemaining = 100;
            check(w.TryStartPlayerSlash(V.UnitX), "directional slash starts with enemy behind");
            for (int i = 0; i < 9; i++) w.Step(Dt);
            check(enemy.Health == health, "directional slash misses enemy behind");
            w.Cancel(p); p.Cooldowns.Clear(); p.Grasps = false;
            check(!w.TryStartPlayerSlash(V.UnitX) && !p.Cooldowns.ContainsKey("slash"), "incompatible anatomy rejects slash without cooldown");
        }
        finally { Persistence.Restore(w, app.Narrative, saved); app.RebuildViews(); }
    }
}
