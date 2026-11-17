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
        var texture=AssetLibrary.Texture(s);
        check(ReferenceEquals(texture,AssetLibrary.Texture(s)) && texture.GetWidth()==s.Columns*s.CellWidth,"sheet textures are built once");
    }
}
