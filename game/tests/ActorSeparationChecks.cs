using V = System.Numerics.Vector2;
namespace Mosslight;
public static class ActorSeparationChecks
{
    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World; string saved = Persistence.Capture(w, app.Narrative);
        try
        {
            var r = new RegionState { Id = "spacing", Width = 24, Height = 24, PlacementRevision = 1, Tiles = Enumerable.Repeat(Terrain.Floor, 576).ToArray() }; w.RegionsById[r.Id] = r;
            foreach (var actor in w.Actors.Values) actor.Region = "reach";
            var grid = new ActorSeparation();
            foreach (int count in new[] { 2, 4, 8 })
            {
                var actors = Enumerable.Range(0, count).Select(i => new ActorState { Id = "spacing:" + i, Region = r.Id, X = 250, Y = 250, Radius = i % 2 == 0 ? 6 : 11 }).ToArray();
                grid.Rebuild(actors);
                var one = grid.Velocity(actors[0], r, V.Zero, 50, 1f / 60);
                grid.Rebuild(actors.Reverse());
                check(one == grid.Velocity(actors[0], r, V.Zero, 50, 1f / 60), count + " separation independent of insertion order");
                for (int tick = 0; tick < 180; tick++)
                {
                    grid.Rebuild(actors);
                    foreach (var a in actors)
                    {
                        var velocity = grid.Velocity(a, r, V.Zero, 50, 1f / 60);
                        check(float.IsFinite(velocity.X) && float.IsFinite(velocity.Y) && velocity.Length() <= 50.001f, "separation finite and speed bounded");
                        w.Move(a, velocity / 60);
                    }
                }
                check(V.Distance(actors[0].Position, actors[1].Position) > 12, count + " overlapping actors separate in free space");
                check(actors.All(a=>actors.All(b=>a==b || V.Distance(a.Position,b.Position)>=(a.Radius+b.Radius)*.9f)),count+" freely resting bodies resolve substantial overlap");
                check(actors.All(a => r.CanStand(a.Position, a)), "mixed radii retain valid terrain footprints");
                actors[1].Health = 0; grid.Rebuild(actors.Take(2));
                check(grid.Velocity(actors[0], r, V.Zero, 50, .016f) == V.Zero, "dead actor removed from avoidance");
                actors[1].Health = 100; actors[1].Region = "reach"; grid.Rebuild(actors.Take(2));
                check(grid.Velocity(actors[0], r, V.Zero, 50, .016f) == V.Zero, "region changes remove neighbour influence");
            }
            // Doorway and corner movement use the same footprint clearance as normal navigation.
            for (int y = 0; y < r.Height; y++) if (y != 10) r.Tiles[y * r.Width + 12] = Terrain.Wall;
            var left = new ActorState { Id = "left", Region = r.Id, X = 260, Y = 252, Radius = 6 };
            var right = new ActorState { Id = "right", Region = r.Id, X = 260, Y = 252, Radius = 11 };
            for (int i = 0; i < 100; i++) { grid.Rebuild(new[] { left, right }); w.Move(left, grid.Velocity(left, r, new V(40, 0), 40, .016f) * .016f); }
            check(r.CanStand(left.Position, left) && left.X > 310, "soft avoidance preserves travel through narrow doorway");

            // Regression: the two starting Reach wardens must stay upright and
            // stationary when a player walks into/through them. The player is
            // intentionally inside their avoidance radius; world movement does
            // not use actor-to-actor collision for these friendly merchants.
            var wardens = new[] { w.Actors["npc:0"], w.Actors["npc:1"] };
            var player = w.Player;
            foreach (var other in w.Actors.Values.Where(a => a.Id != player.Id && !wardens.Contains(a)))
                other.Region = "reach";
            player.Region = r.Id; player.X = 252; player.Y = 250;
            w.Cancel(player);
            foreach (var guard in wardens)
            {
                guard.Region = r.Id; guard.X = guard.HomeX = 252; guard.Y = guard.HomeY = 250;
                w.Cancel(guard);
                guard.MotionX = guard.MotionY = 0;
                guard.Activity = "rest"; // Explicitly reproduce the Hour=0 schedule before rendering.
                check(!w.Hostile(guard, player) && guard.Profession == "merchant", "starting warden is a friendly merchant");
            }
            var positions = wardens.ToDictionary(a => a.Id, a => a.Position);
            w.Time = 0;
            app.RebuildViews();
            check(wardens.All(a => !app.Views[a.Id].Creature.Resting), "starting merchant rest schedule keeps both NPCs upright");
            for (int tick = 0; tick < 120; tick++) w.Step(1f / 60);
            check(wardens.All(a => V.DistanceSquared(a.Position, positions[a.Id]) < .0001f),
                "player overlap does not push idle wardens around");
            w.Time = 7 * 60; // Scheduled trading is also stationary.
            for (int tick = 0; tick < 120; tick++) w.Step(1f / 60);
            foreach (var guard in wardens) app.Views[guard.Id].Sync(1f / 60);
            check(wardens.All(a => a.Activity == "trade" && V.DistanceSquared(a.Position, positions[a.Id]) < .0001f),
                "player overlap does not push trading wardens around");
            check(wardens.All(a => !app.Views[a.Id].Creature.Resting),
                "trading wardens retain upright PPC idle poses");
        }
        finally { Persistence.Restore(w, app.Narrative, saved); app.RebuildViews(); }
    }
}
