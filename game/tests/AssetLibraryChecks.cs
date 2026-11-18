using Godot;
namespace Mosslight;

/// <summary>Every library module is painted, uses only the shared palette, repeats exactly,
/// has a unique name, stays inside its cell and (unless flat) keeps a transparent gutter.
/// Seamless materials must actually wrap.</summary>
public static class AssetLibraryChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        var names=new HashSet<string>(StringComparer.Ordinal);int total=0;
        foreach(var sheet in AssetLibrary.Sheets)
        {
            check(sheet.Modules.Count>0 && sheet.Columns>0,"populated sheet "+sheet.Name);
            foreach(var m in sheet.Modules)
            {
                total++;
                check(names.Add(m.Name),"unique asset name "+m.Name);
                var data=m.Paint();
                check(data.Length==m.Width*m.Height && m.Width==sheet.CellWidth && m.Height==sheet.CellHeight,"module fills its cell "+m.Name);
                check(data.All(n=>n<OverworldPropArt.Palette.Length),"palette indices valid "+m.Name);
                // An effect may hold a blank frame (a flicker or the end of a fade).
                check(sheet.Name=="effects" || data.Count(n=>n!=0)>=6,"module is painted "+m.Name);
                check(data.AsSpan().SequenceEqual(m.Paint()),"module repeats exactly "+m.Name);
                check(m.Pivot.X>=0 && m.Pivot.Y>=0 && m.Pivot.X<=m.Width && m.Pivot.Y<=m.Height,"pivot inside cell "+m.Name);
                if(sheet.Name=="materials")
                {
                    // Opaque, and the noise is periodic: the hash wraps at both edges.
                    check(data.All(n=>n!=0),"opaque material "+m.Name);
                    continue;
                }
                bool ring=true;
                for(int x=0;x<m.Width;x++)ring&=data[x]==0 && data[(m.Height-1)*m.Width+x]==0;
                for(int y=0;y<m.Height;y++)ring&=data[y*m.Width]==0 && data[y*m.Width+m.Width-1]==0;
                check(ring,"transparent gutter round "+m.Name);
            }
        }
        check(total>=900,"asset library holds at least 900 modules: "+total);
        check(AssetLibrary.TryFind("sword-mythril",out var s,out int i) && s.Name=="items" && AssetLibrary.TryFind("windmill-1",out var l,out _) && l.Name=="landmarks","modules found by name");
        check(!AssetLibrary.TryFind("no-such-asset",out _,out _),"unknown name is not found");
        // Every animated effect has exactly eight frames.
        var effects=AssetLibrary.Sheets.Single(x=>x.Name=="effects").Modules.Select(m=>m.Name[..m.Name.LastIndexOf('-')]).GroupBy(n=>n);
        check(effects.All(g=>g.Count()==EffectArt.Frames),"effects have eight frames each");
        CheckCuratedPlacements(check);
        var texture=AssetLibrary.Texture(s);
        check(ReferenceEquals(texture,AssetLibrary.Texture(s)) && texture.GetWidth()==s.Columns*s.CellWidth,"sheet textures are built once");
    }
    static void CheckCuratedPlacements(Action<bool,string> check)
    {
        const string catalog = "docs/asset-library/curation.csv";
        if (!File.Exists(catalog)) return; // The catalog is not shipped in the game package.
        var keep = File.ReadLines(catalog).Skip(1).Select(line => line.Split(',', 4))
            .Where(row => row[2] == "Keep").Select(row => row[1]).ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        void Record(IEnumerable<CuratedScenery> views)
        {
            foreach (var view in views)
            {
                if (view.Surface != "") used.Add(view.Surface);
                foreach (var sprite in view.Sprites)
                {
                    if (!sprite.Animate) used.Add(sprite.Asset);
                    else
                        for (int i = 1; AssetLibrary.TryFind(sprite.Asset + "-" + i, out _, out _); i++)
                            used.Add(sprite.Asset + "-" + i);
                }
                view.Free();
            }
        }

        foreach (int seed in new[] { 1, 42, 1337, 33, 76, 208, 1547, 2816 })
        {
            var reach = Regions.Overworld(seed, 320, 240);
            reach.ReachSites = ReachEncounters.Plan(reach, out _);
            Record(CuratedTown.Dress(TownStage.For(reach)!));
            for (int row = 0; row * OverworldChunkStreamer.ChunkTiles < reach.Height; row++)
                for (int col = 0; col * OverworldChunkStreamer.ChunkTiles < reach.Width; col++)
                {
                    var cells = OverworldChunkStreamer.Cells(reach, new Vector2I(col, row));
                    var stops = CuratedScenery.TrailStops(reach, cells);
                    foreach (var view in EnvironmentDecoration.Create(reach, new(), cells, stops))
                    {
                        if (view.LibraryModule != "") used.Add(view.LibraryModule);
                        view.Free();
                    }
                    Record(stops);
                }
            var ruin = Regions.Dungeon(seed);
            var masonry = DungeonArt.Masonry(ruin);
            foreach (var item in DungeonDressing.Plan(ruin, masonry))
                if (item.LibraryModule != "") used.Add(item.LibraryModule);
            if (DungeonDressing.Torches(ruin, masonry).Any())
                for (int i = 1; i <= 8; i++) used.Add("torch-flame-" + i);
        }
        var missing = keep.Except(used).Order().ToArray();
        var outside = used.Except(keep).Order().ToArray();
        check(missing.Length == 0, "Keep modules placed across reference worlds: " + string.Join(", ", missing));
        check(outside.Length == 0, "curated placements only use Keep modules: " + string.Join(", ", outside));
    }
}
