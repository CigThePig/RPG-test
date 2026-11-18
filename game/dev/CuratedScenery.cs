using Godot;
using PixelCreatures.Runtime;

namespace Mosslight;

/// <summary>Presentation dressing derived from the saved geography and town. It owns
/// no collision, inventory, or save state.</summary>
public partial class CuratedScenery : Node2D
{
    public readonly record struct Sprite(string Asset, Vector2 Offset, float Scale = 1, bool Animate = false);

    public List<Sprite> Sprites { get; } = new();
    public string Surface { get; init; } = "";
    public override void _Ready() => SetProcess(Sprites.Any(sprite => sprite.Animate));
    int drawnFrame = -1;
    static int Frame => (int)(ReachAmbience.Time * 8);
    public override void _Process(double delta)
    {
        if (Frame != drawnFrame) QueueRedraw();
    }

    public override void _Draw()
    {
        drawnFrame = Frame;
        if (Surface != "" && AssetLibrary.TryFind(Surface, out var sheet, out int cell))
        {
            // Fade the edges of the patch into the existing terrain rather than
            // replacing the underlying shore, paving or room material.
            var source = sheet.Source(cell);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 6; x++)
                {
                    var offset = new Vector2(x * 8, y * 6);
                    float alpha = x is 0 or 5 || y is 0 or 3 ? .22f : .62f;
                    DrawTextureRectRegion(AssetLibrary.Texture(sheet),
                        new Rect2(offset + new Vector2(-24, -6), new Vector2(8, 6)),
                        new Rect2(source.Position + offset, new Vector2(8, 6)), new Color(1, 1, 1, alpha));
                }
        }
        foreach (var sprite in Sprites)
        {
            string asset = sprite.Asset;
            if (sprite.Animate)
            {
                int frames = asset is "campfire" or "brazier-post" or "fireplace" ? 4 : asset == "cooking-pot" ? 2 : 8;
                int phase = Frame % frames;
                asset += "-" + (phase + 1);
            }
            AssetLibrary.Draw(this, asset, sprite.Offset, sprite.Scale);
        }
    }

    static string Pick(string[] choices, uint seed) => choices[seed % choices.Length];
    static readonly string[] Pines = { "pine-tall", "pine-young", "tree-dead-1", "tree-dead-2", "stump-mossy", "fern-large", "log-hollow" };
    static readonly string[] Meadows = { "bush-berry-red", "bush-berry-blue", "bush-rose", "flowers-red", "flowers-blue", "flowers-yellow", "flowers-white", "mushroom-ring" };
    static readonly string[] Banks = { "willow-1", "willow-2", "reeds", "driftwood" };
    static readonly string[] Stones = { "rock-moss", "boulder-pile", "beast-skull", "ribs" };
    static readonly string[][] Stops =
    {
        new[] { "campfire", "bedroll", "cooking-spit", "lean-to", "water-barrel" },
        new[] { "grave-round", "grave-slab", "grave-broken", "tomb" },
        new[] { "shrine", "offering-bowl", "brazier-post" },
        new[] { "obelisk", "standing-stone-1", "standing-stone-2", "standing-stone-3", "stone-arch", "cairn", "milestone" },
        new[] { "campfire-cold", "brazier-post-cold", "vine-post" }
    };
    static readonly string[] Landmarks = { "mine-entrance", "cave-mouth", "ruined-temple" };

    static bool Near(RegionState region, int x, int y, Terrain terrain, int radius)
    {
        for (int yy = Math.Max(0, y - radius); yy <= Math.Min(region.Height - 1, y + radius); yy++)
            for (int xx = Math.Max(0, x - radius); xx <= Math.Min(region.Width - 1, x + radius); xx++)
                if (region.Tiles[yy * region.Width + xx] == terrain) return true;
        return false;
    }

    public static string WildProp(RegionState region, int x, int y, uint seed, string original)
    {
        if (seed % 4 != 0) return "";
        if (Near(region, x, y, Terrain.Water, 2)) return Pick(Banks, seed >> 4);
        if (original == "rock1") return Pick(Stones, seed >> 4);
        return Pick(original == "tree1" ? Pines : Meadows, seed >> 4);
    }

    public static List<CuratedScenery> TrailStops(RegionState region, Rect2I cells)
    {
        var result = new List<CuratedScenery>();
        int col = cells.Position.X / OverworldChunkStreamer.ChunkTiles;
        int row = cells.Position.Y / OverworldChunkStreamer.ChunkTiles;
        if ((col + row) % 2 != 0) return result;
        uint seed = EnvironmentStyle.Hash(region.Seed, col, row, 9821);
        bool large = (col + 2 * row) % 5 == 0;
        int group = (int)((col + 3 * row) % Stops.Length);
        var town = TownStage.For(region);
        bool Clear(int x, int y)
        {
            if (x < 1 || y < 1 || x >= region.Width - 1 || y >= region.Height - 1) return false;
            if (region.Tiles[y * region.Width + x] != Terrain.Grass || town?.Occupied(x, y) == true) return false;
            if (region.Features.Keys.Any(at => Math.Abs(at % region.Width - x) < 4 && Math.Abs(at / region.Width - y) < 4)) return false;
            return !ReachEncounters.ObscuresCombat(region, x, y, "tree1");
        }

        for (int attempt = 0; attempt < 48; attempt++)
        {
            uint candidate = EnvironmentStyle.Hash(region.Seed, col, row, 9830 + attempt);
            int x = cells.Position.X + 5 + (int)(candidate % 22);
            int y = cells.Position.Y + 5 + (int)((candidate >> 12) % 22);
            if (x + 3 >= cells.End.X || y + 2 >= cells.End.Y) continue;
            int span = large ? 2 : (Stops[group].Length + 1) / 2;
            bool fits = true;
            for (int yy = y - 1; yy <= y + 1 && fits; yy++)
                for (int xx = x - span; xx <= x + span && fits; xx++)
                    fits = Clear(xx, yy);
            if (!fits) continue;
            // Use rock-side clearings for sealed mouths. A temple can stand
            // on open ground, so a candidate need not be discarded if no
            // suitable ridge is present in this chunk.
            bool rockSide = Near(region, x, y, Terrain.Rock, 8);
            int landmark = rockSide ? (int)(seed % 2) : 2;
            if (large && attempt < 36 && !rockSide && (seed & 3) != 0) continue;
            var foot = GodotConvert.GroundToScreen((x + .5f) * 24, (y + .5f) * 24);
            if (large)
            {
                var marker = Create(foot);
                marker.Sprites.Add(new(Landmarks[landmark], Vector2.Zero, .72f));
                // These are landscape silhouettes. Rubble across the mouths
                // distinguishes them from the working ruin portal.
                if (landmark < 2) marker.Sprites.Add(new("rubble-large", new(0, 3), .72f));
                result.Add(marker);
            }
            else
            {
                string[] assets = Stops[group];
                // A pair of nearby footprints gives the ruin, campsite or grave
                // its own silhouette without filling the surrounding meadow.
                for (int i = 0; i < assets.Length; i++)
                {
                    var p = foot + new Vector2((i - (assets.Length - 1) * .5f) * 24, i % 2 * 10);
                    var marker = Create(p, i == assets.Length / 2 ? Pick(StopSurfaces[group], seed) : "");
                    string asset = assets[i];
                    marker.Sprites.Add(new(asset, Vector2.Zero, .72f, asset is "campfire" or "brazier-post"));
                    if (asset == "campfire")
                    {
                        marker.Sprites.Add(new("smoke", new(0, -30), .63f, true));
                        marker.Sprites.Add(new("embers", new(0, -15), .57f, true));
                    }
                    else if (asset == "offering-bowl")
                        marker.Sprites.Add(new("sparkle-gold", new(0, -8), .55f, true));
                    else if (asset == "shrine")
                        marker.Sprites.Add(new("sparkle-blue", new(0, -19), .52f, true));
                    else if (asset == "vine-post")
                        marker.Sprites.Add(new("leaves", new(0, -10), .5f, true));
                    else if (asset == "brazier-post-cold")
                        marker.Sprites.Add(new("dust", new(0, -3), .55f, true));
                    result.Add(marker);
                }
            }
            break;
        }

        // Water effects are anchored to actual water; leave bridge crossings clear.
        for (int y = cells.Position.Y + 2; y < cells.End.Y - 2; y++)
        {
            for (int x = cells.Position.X + 2; x < cells.End.X - 2; x++)
            {
                int at = y * region.Width + x;
                if (region.Tiles[at] != Terrain.Water || region.Crossings.Contains(at) ||
                    EnvironmentStyle.Hash(region.Seed, x, y, 9890) % 17 != 0 ||
                    !Near(region, x, y, Terrain.Grass, 1)) continue;
                var w = Create(GodotConvert.GroundToScreen((x + .5f) * 24, (y + .5f) * 24));
                w.ZIndex = 1;
                w.Sprites.Add(new("ripple", Vector2.Zero, .7f, true));
                w.Sprites.Add(new("splash", new(0, -3), .48f, true));
                result.Add(w);
                return result;
            }
        }
        return result;
    }

    static readonly string[][] StopSurfaces =
    {
        new[] { "grass-dry", "gravel" }, new[] { "moss-stone" },
        new[] { "stone-flags", "cobble" }, new[] { "grass-dark" }, new[] { "mud", "planks" }
    };

    public static CuratedScenery Create(Vector2 foot, string surface = "") => new()
    {
        Position = foot,
        Surface = surface,
        TextureFilter = TextureFilterEnum.Nearest
    };
}
