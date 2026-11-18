using Godot;
using PixelCreatures.Runtime;

namespace Mosslight;

/// <summary>Interior dressing and goods on display. Only the ordinary item icons
/// are connected to inventory, so the displays imply no new shop or equipment rules.</summary>
public static class CuratedTown
{
    static readonly string[][] Furniture =
    {
        new[] { "bed-double-red", "bed-double-green", "wardrobe", "drawers", "chair", "stool", "washbasin", "curtain-red", "curtain-green", "carpet-red", "carpet-green" },
        new[] { "bed-double-blue", "bed-double-violet", "desk", "chair-side", "coat-rack", "plant-tree", "curtain-blue", "curtain-violet", "carpet-blue", "carpet-violet" },
        new[] { "bunk-bed", "dining-table", "cupboard", "oven", "bread-rack", "cooking-pot", "fireplace", "barrel-apples" },
        new[] { "workbench", "strongbox", "shop-shelf-wares", "crate-open" },
        new[] { "bookcase-tall", "bookcase-scrolls", "plant-fern", "plant-flower", "vase", "shop-shelf-potions" },
        new[] { "sawhorse", "ladder", "trophy-antlers" }
    };

    static readonly string[][] Goods =
    {
        new[] { "feather", "thread", "leather", "candle", "pouch", "lantern" },
        new[] { "letter", "quill", "ring-iron", "key-iron", "coin", "coins" },
        new[] { "apple", "berries", "bread", "mushroom", "bucket" },
        new[] { "sword-iron", "dagger-iron", "axe-iron", "battleaxe-iron", "mace-iron", "spear-iron", "hammer-iron",
                "shield-iron", "helmet-iron", "cuirass-iron", "boots-iron", "gauntlets-iron", "ingot-iron", "ore-iron" },
        new[] { "bone", "fang", "claw", "skull", "bell", "torch" },
        new[] { "pickaxe", "hatchet", "shovel", "hoe", "sickle", "rope", "log", "plank", "coal", "stone" }
    };

    public static List<CuratedScenery> Dress(TownPlan plan)
    {
        var result = new List<CuratedScenery>();
        foreach (var house in plan.Houses)
        {
            // The smaller legacy houses keep their original footprint and furniture.
            if (house.Width < 10 || house.Height < 9) continue;
            int style = (int)house.Style;
            var occupied = plan.Pieces.Select(piece => (piece.X, piece.Y)).ToHashSet();
            var slots = new List<(int X, int Y)>();
            int left = house.X + 1, right = house.X + house.Width - 2;
            int aisle = house.X + house.Door;
            void AddSlot(int x, int y)
            {
                if (occupied.Add((x, y))) slots.Add((x, y));
            }
            for (int y = house.Y + 2; y <= house.Y + house.Height - 3; y += 2)
            {
                AddSlot(left, y);
                AddSlot(right, y);
            }
            for (int x = left + 1; x < right; x++)
                if (x < aisle - 1 || x > aisle + house.DoorWidth)
                    AddSlot(x, house.Y + 1);
            for (int y = house.Y + 3; y <= house.Y + house.Height - 3; y += 2)
            {
                AddSlot(left, y);
                AddSlot(right, y);
            }
            for (int x = left + 1; x < right; x++)
                if (x < aisle - 1 || x > aisle + house.DoorWidth)
                    AddSlot(x, house.Y + house.Height - 2);

            var curtains = Furniture[style].Where(asset => asset.StartsWith("curtain-", StringComparison.Ordinal)).ToArray();
            for (int i = 0; i < curtains.Length; i++)
            {
                int x = i == 0 ? house.X + 2 : house.X + house.Width - 3;
                var foot = GodotConvert.GroundToScreen((x + .5f) * 24, (house.Y + 1) * 24);
                var hanging = CuratedScenery.Create(foot + new Vector2(0, 1));
                hanging.Sprites.Add(new(curtains[i], Vector2.Zero, .58f));
                result.Add(hanging);
            }
            var art = Furniture[style].Except(curtains).ToArray();
            string[] goods = Goods[style];
            int pictures = Math.Min(art.Length, slots.Count);
            int nextGood = 0;
            for (int i = 0; i < pictures; i++)
            {
                var (x, y) = slots[i];
                string asset = art[i];
                var foot = GodotConvert.GroundToScreen((x + .5f) * 24, (y + .5f) * 24);
                bool carpet = asset.StartsWith("carpet-", StringComparison.Ordinal);
                var view = CuratedScenery.Create(foot);
                view.Sprites.Add(new(asset, Vector2.Zero, carpet ? .48f : .58f, asset is "fireplace" or "cooking-pot"));
                result.Add(view);
            }

            // Lay the goods out in small assortments on existing furniture, or on
            // a worktable along the wall in rooms that have no suitable surface.
            var tables = plan.Pieces.Where(piece => house.Contains(piece.X, piece.Y) &&
                (TownProp)piece.Kind is TownProp.Table or TownProp.Counter or TownProp.Crates
                    or TownProp.Shelf or TownProp.Anvil or TownProp.Pew).ToArray();
            int standCount = (goods.Length + 3) / 4;
            for (int i = 0; i < standCount; i++)
            {
                bool existing = i < tables.Length;
                if (!existing && pictures + i - tables.Length >= slots.Count) break;
                var (x, y) = existing ? (tables[i].X, tables[i].Y) : slots[pictures + i - tables.Length];
                var foot = GodotConvert.GroundToScreen((x + .5f) * 24, (y + .5f) * 24);
                var display = CuratedScenery.Create(foot + new Vector2(0, .5f));
                if (!existing) display.Sprites.Add(new("workbench", Vector2.Zero, .58f));
                for (int j = 0; j < 4 && nextGood < goods.Length; j++, nextGood++)
                    display.Sprites.Add(new(goods[nextGood], new Vector2((j - 1.5f) * 7, -12), .43f));
                result.Add(display);
            }
        }
        return result;
    }
}
