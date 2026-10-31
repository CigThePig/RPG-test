using Godot;
namespace Mosslight;
/// <summary>Presentation only. RegionState and Gloot own object state and contents.</summary>
public partial class FeatureView : Node2D
{
    public Dictionary<string,Texture2D>? Art { get; set; }
    public uint VisualSeed { get; set; }
    public string Kind { get; set; } = "";
    public int[] GateCells { get; set; } = Array.Empty<int>();
    public Vector2[] GateOffsets { get; set; } = Array.Empty<Vector2>();
    public bool Spent { get; set; }
    public bool Selected { get; set; }
    // Brazier flame state from RegionState.Lit; presentation only.
    public bool Lit { get; set; }
    static readonly Color Iron = new("3b4244"), Flame = new("f2a443"), FlameCore = new("ffe7a3"), Ward = new("9fe3e6");
    public override void _Ready() => SetProcess(Kind is "brazier" or "seal");
    // Only lit braziers and intact seals animate; at most a handful exist per ruin.
    public override void _Process(double delta) { if ((Kind == "brazier" && Lit) || (Kind == "seal" && !Spent)) QueueRedraw(); }
    float Flicker(float rate, float phase) => .5f + .5f * Mathf.Sin((float)Time.GetTicksMsec() * .001f * rate + phase + VisualSeed % 17);
    static readonly Color Ink = new("263333"), Gold = new("d4b66b"), Wood = new("896448");
    void Box(float x, float y, float w, float h, Color c) => DrawRect(new Rect2(x,y,w,h),c);
    void Gem(Vector2 p, float s, Color c) => DrawColoredPolygon(new[] { p + new Vector2(0,-s), p + new Vector2(s,0), p + new Vector2(0,s), p + new Vector2(-s,0) },c);
    public override void _Draw()
    {
        if (Spent && Kind is not ("chest" or "lock" or "lever" or "brazier" or "portcullis" or "seal")) return;
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1,.45f));
        if (Kind is not ("portcullis" or "seal")) DrawCircle(new Vector2(0,2), Kind is "portal" or "exit" ? 24 : 17, new Color(0,0,0,.25f));
        if (Kind == "brazier" && Lit) DrawCircle(new Vector2(0,2), 34 + Flicker(7,0)*4, new Color(Flame, .10f));
        if (Selected) DrawArc(Vector2.Zero,23,0,Mathf.Tau,24,Gold,2);
        DrawSetTransform(Vector2.Zero);
        switch (Kind)
        {
            case "chest":
                // Bevelled lid and right side use the same upper-left light as PPC.
                DrawColoredPolygon(new[]{new Vector2(-17,-22),new Vector2(12,-22),new Vector2(19,-16),new Vector2(19,1),new Vector2(12,5),new Vector2(-17,1)},Ink);
                Box(-15,-19,27,19,Wood); Box(12,-16,5,16,Wood.Darkened(.28f));
                for(int plank=0;plank<4;plank++)Box(-14+plank*7,-17,1,15,Wood.Darkened(.22f));
                Box(-15,-19,27,2,new Color("be9566"));
                Box(-17,Spent ? -29 : -26,34,Spent ? 7 : 12,Ink);
                Box(-15,Spent ? -27 : -24,30,Spent ? 3 : 8,new Color("ad8257"));
                Box(-11,-24,3,24,Gold); Box(8,-24,3,24,Gold);
                if (Spent) Box(-13,-17,26,8,new Color("302b29"));
                else { Box(-3,-15,6,7,Gold); Box(-1,-13,2,3,Ink); }
                break;
            case "herb":
                Box(-2,-22,4,24,new Color("48684e"));
                for(int i=0;i<3;i++) { Gem(new Vector2(-7,-6-i*7),6,new Color("7eaa68").Darkened((VisualSeed%3)*.025f)); Gem(new Vector2(7,-10-i*7),5,new Color("597f55")); }
                Gem(new Vector2(0,-29),5,new Color("d7c095")); break;
            case "ore":
                if(Art?.TryGetValue("rock1",out var rock)==true)
                    DrawTextureRect(rock,new Rect2(-24,-32,48,38),false);
                DrawColoredPolygon(new[] { new Vector2(-21,0),new Vector2(-18,-18),new Vector2(-5,-29),new Vector2(13,-23),new Vector2(22,-4),new Vector2(10,4) },new Color("535d5d"));
                Gem(new Vector2(-8,-13),6,new Color("96b1b1")); Gem(new Vector2(7,-19),5,Gold); Gem(new Vector2(12,-7),4,new Color("96b1b1")); break;
            case "wood":
                if(Art?.TryGetValue("tree1",out var tree)==true)DrawTexture(tree,new Vector2(-36,-100));
                else { Box(-5,-30,10,32,Wood); Gem(new Vector2(0,-40),23,new Color("638352")); }
                break;
            case "portal": case "exit":
                Box(-24,-42,9,43,EnvironmentStyle.Stone); Box(15,-42,9,43,EnvironmentStyle.Shade); Box(-24,-49,48,12,EnvironmentStyle.Cap);
                Box(-24,-49,48,2,EnvironmentStyle.Highlight);
                for(int course=0;course<5;course++) { Box(-24,-40+course*8,9,1,EnvironmentStyle.Shade); Box(15,-40+course*8,9,1,EnvironmentStyle.Mortar); }
                Box(-23,-40,2,39,EnvironmentStyle.Cap); Box(16,-40,2,39,EnvironmentStyle.Stone);
                if(Kind == "portal") { DrawArc(new Vector2(0,-20),17,Mathf.Pi,Mathf.Tau,20,new Color("7ad7cf"),4); Box(-13,-20,26,18,new Color(.25f,.65f,.65f,.35f)); }
                else { Box(-14,-6,28,5,Gold); Box(-10,-13,20,5,Gold); Box(-6,-20,12,5,Gold); } break;
            case "key":
                DrawArc(new Vector2(0,-17),7,0,Mathf.Tau,16,Gold,4); Box(-2,-12,4,16,Gold); Box(0,-1,8,4,Gold); break;
            case "relic":
                Box(-14,-6,28,9,EnvironmentStyle.Shade); Box(-14,-8,28,3,EnvironmentStyle.Cap);
                Box(-10,-15,20,7,EnvironmentStyle.Stone); Gem(new Vector2(0,-20),12,Gold); Gem(new Vector2(0,-22),6,new Color("b7eee0")); break;
            case "lever":
            {
                // Stone plinth with an iron slot; the handle rests up before the pull, down after.
                Box(-14,-12,28,13,EnvironmentStyle.Stone); Box(9,-12,5,13,EnvironmentStyle.Shade); Box(-14,-14,28,3,EnvironmentStyle.Cap);
                Box(-14,-14,28,1,EnvironmentStyle.Highlight); Box(-3,-13,6,3,Ink);
                var pivot = new Vector2(0,-12);
                var tip = pivot + (Spent ? new Vector2(15,-9) : new Vector2(-11,-20));
                DrawLine(pivot, tip, Ink, 5); DrawLine(pivot, tip, Iron.Lightened(.25f), 2);
                DrawCircle(tip, 4, Spent ? Gold.Darkened(.35f) : Gold); DrawCircle(pivot, 3, Ink);
                break;
            }
            case "brazier":
            {
                // Short pedestal and iron bowl; a cold bowl shows ash, a burning one a flame.
                Box(-5,-14,10,15,EnvironmentStyle.Stone); Box(2,-14,3,15,EnvironmentStyle.Shade); Box(-8,-2,16,3,EnvironmentStyle.Cap);
                DrawColoredPolygon(new[]{new Vector2(-14,-22),new Vector2(14,-22),new Vector2(9,-14),new Vector2(-9,-14)},Ink);
                Box(-13,-22,26,2,Iron.Lightened(.3f));
                if (!Lit) { Box(-9,-24,18,3,new Color("6d6863")); DrawCircle(new Vector2(2,-24),1.5f,new Color("b0583a")); break; }
                float f = Flicker(9,0), g = Flicker(13,1.7f);
                var core = Spent ? Ward : FlameCore;
                DrawColoredPolygon(new[]{new Vector2(-10,-22),new Vector2(-6,-34-f*4),new Vector2(-1,-29),new Vector2(2,-42-g*5),new Vector2(6,-30),new Vector2(9,-36-f*3),new Vector2(11,-22)},Flame);
                DrawColoredPolygon(new[]{new Vector2(-5,-22),new Vector2(0,-33-g*3),new Vector2(5,-22)},core);
                break;
            }
            case "portcullis":
            {
                // Iron bars on every authoritative gate cell; raised, only the teeth show under the lintel.
                var cells = GateOffsets.Length > 0 ? GateOffsets : new[] { Vector2.Zero };
                foreach (var offset in cells)
                {
                    var foot = offset + new Vector2(0, 2);
                    Box(foot.X-6,foot.Y-37,12,4,EnvironmentStyle.Shade);
                    for (int bar = -1; bar <= 1; bar++)
                    {
                        var top = foot + new Vector2(bar*4,-34);
                        var bottom = Spent ? top + new Vector2(0,6) : foot + new Vector2(bar*4,0);
                        DrawLine(top, bottom, Ink, 3); DrawLine(top+new Vector2(-1,0), bottom+new Vector2(-1,0), Iron.Lightened(.35f), 1);
                    }
                    if (!Spent) { DrawLine(foot+new Vector2(-7,-22),foot+new Vector2(7,-22),Ink,3); DrawLine(foot+new Vector2(-7,-10),foot+new Vector2(7,-10),Ink,3); }
                }
                break;
            }
            case "seal":
            {
                // A curtain of pale light over the corridor with one ward rune; broken, a faint glyph stays.
                var cells = GateOffsets.Length > 0 ? GateOffsets : new[] { Vector2.Zero };
                var middle = cells.Aggregate(Vector2.Zero, (sum,p) => sum+p) / cells.Length;
                if (Spent) { DrawSetTransform(middle + new Vector2(0,2), 0, new Vector2(1,.45f)); DrawArc(Vector2.Zero,14,0,Mathf.Tau,20,new Color(Ward,.35f),2); DrawSetTransform(Vector2.Zero); break; }
                float pulse = Flicker(3,0);
                foreach (var offset in cells)
                {
                    var foot = offset + new Vector2(0, 2);
                    DrawRect(new Rect2(foot + new Vector2(-12,-38), new Vector2(24,38)), new Color(Ward, .16f + pulse*.08f));
                    DrawLine(foot + new Vector2(0,-38), foot, new Color(Ward, .45f), 1);
                }
                Gem(middle + new Vector2(0,-22), 9, new Color(Ward, .75f)); Gem(middle + new Vector2(0,-22), 4, new Color(1,1,1,.85f));
                break;
            }
            case "lock":
                // One connected barrier spanning every blocking cell, with one
                // central lock plate rather than six unrelated lock glyphs.
                var offsets = GateOffsets.Length > 0 ? GateOffsets : new[] { Vector2.Zero };
                foreach (var offset in offsets)
                {
                    var foot = offset + new Vector2(0, 2);
                    // Metal staves and stone sockets, aligned with every authoritative gate cell.
                    Box(foot.X-5,foot.Y-3,10,4,EnvironmentStyle.Shade);
                    DrawLine(foot, foot + new Vector2(0, Spent ? -3 : -29), Ink, 5);
                    if(!Spent)DrawLine(foot+new Vector2(-1,-1),foot+new Vector2(-1,-28),EnvironmentStyle.Cap,1);
                    if (!Spent) DrawLine(foot + new Vector2(-12,-12), foot + new Vector2(12,-12), Gold, 3);
                }
                if (!Spent)
                {
                    var middle = offsets.Aggregate(Vector2.Zero, (sum,p) => sum+p) / offsets.Length;
                    DrawRect(new Rect2(middle+new Vector2(-4,-22),new Vector2(8,9)),Gold);
                }
                break;
        }
        if(Kind=="chest" && !Spent) { Box(-12,-8,4,1,new Color("b48c60")); Box(4,-5,3,1,Wood.Darkened(.2f)); }
        if(Kind=="ore") { Gem(new Vector2(-10,-18),3,new Color("c2d1c0")); Gem(new Vector2(6,-12),3,new Color("8ba5a1")); }
        if (Selected) DrawString(ThemeDB.FallbackFont,new Vector2(-25,-55),Label(),HorizontalAlignment.Left,-1,13,Gold);
    }
    string Label() => Kind switch
    {
        "chest" when Spent => "Empty chest",
        "lever" => Spent ? "Pulled lever" : "Lever",
        "brazier" => Lit ? "Burning brazier" : "Cold brazier",
        "portcullis" => "Portcullis",
        "seal" => "Light seal",
        _ => Kind
    };
}
