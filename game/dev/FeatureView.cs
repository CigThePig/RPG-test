using Godot;
namespace Mosslight;
/// <summary>Presentation only. RegionState and Gloot own object state and contents.</summary>
public partial class FeatureView : Node2D
{
    public string Kind { get; set; } = "";
    public bool Spent { get; set; }
    public bool Selected { get; set; }
    static readonly Color Ink = new("263333"), Gold = new("d4b66b"), Wood = new("896448");
    void Box(float x, float y, float w, float h, Color c) => DrawRect(new Rect2(x,y,w,h),c);
    void Gem(Vector2 p, float s, Color c) => DrawColoredPolygon(new[] { p + new Vector2(0,-s), p + new Vector2(s,0), p + new Vector2(0,s), p + new Vector2(-s,0) },c);
    public override void _Draw()
    {
        if (Spent && Kind != "chest" && Kind != "lock") return;
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1,.45f));
        DrawCircle(new Vector2(0,2), Kind is "portal" or "exit" ? 24 : 17, new Color(0,0,0,.25f));
        if (Selected) DrawArc(Vector2.Zero,23,0,Mathf.Tau,24,Gold,2);
        DrawSetTransform(Vector2.Zero);
        switch (Kind)
        {
            case "chest":
                Box(-17,-20,34,22,Ink); Box(-15,-18,30,18,Wood);
                Box(-17,Spent ? -29 : -26,34,Spent ? 7 : 12,Ink);
                Box(-15,Spent ? -27 : -24,30,Spent ? 3 : 8,new Color("ad8257"));
                Box(-11,-24,3,24,Gold); Box(8,-24,3,24,Gold);
                if (Spent) Box(-13,-17,26,8,new Color("302b29"));
                else { Box(-3,-15,6,7,Gold); Box(-1,-13,2,3,Ink); }
                break;
            case "herb":
                Box(-2,-22,4,24,new Color("48684e"));
                for(int i=0;i<3;i++) { Gem(new Vector2(-7,-6-i*7),6,new Color("7eaa68")); Gem(new Vector2(7,-10-i*7),5,new Color("597f55")); }
                Gem(new Vector2(0,-29),5,new Color("d7c095")); break;
            case "ore":
                DrawColoredPolygon(new[] { new Vector2(-21,0),new Vector2(-18,-18),new Vector2(-5,-29),new Vector2(13,-23),new Vector2(22,-4),new Vector2(10,4) },new Color("535d5d"));
                Gem(new Vector2(-8,-13),6,new Color("96b1b1")); Gem(new Vector2(7,-19),5,Gold); Gem(new Vector2(12,-7),4,new Color("96b1b1")); break;
            case "wood":
                Box(-7,-28,14,30,new Color("514536")); Box(-5,-27,10,27,Wood);
                DrawLine(new Vector2(-3,-23),new Vector2(-15,-33),Wood,5);
                DrawLine(new Vector2(4,-18),new Vector2(16,-28),Wood,4);
                Gem(new Vector2(-12,-29),8,new Color("638352")); Gem(new Vector2(14,-26),7,new Color("86a567"));
                DrawArc(new Vector2(0,-27),5,0,Mathf.Tau,12,new Color("c6aa78"),2); break;
            case "portal": case "exit":
                Box(-24,-42,9,43,new Color("66777a")); Box(15,-42,9,43,new Color("66777a")); Box(-24,-49,48,12,new Color("96a5a0"));
                if(Kind == "portal") { DrawArc(new Vector2(0,-20),17,Mathf.Pi,Mathf.Tau,20,new Color("7ad7cf"),4); Box(-13,-20,26,18,new Color(.25f,.65f,.65f,.35f)); }
                else { Box(-14,-6,28,5,Gold); Box(-10,-13,20,5,Gold); Box(-6,-20,12,5,Gold); } break;
            case "key":
                DrawArc(new Vector2(0,-17),7,0,Mathf.Tau,16,Gold,4); Box(-2,-12,4,16,Gold); Box(0,-1,8,4,Gold); break;
            case "relic":
                Box(-14,-6,28,9,new Color("677274")); Gem(new Vector2(0,-20),12,Gold); Gem(new Vector2(0,-22),6,new Color("b7eee0")); break;
            case "lock":
                if(!Spent) { Box(-12,-32,24,33,Wood); Box(-12,-26,24,4,Gold); Box(-12,-7,24,4,Gold); Box(-3,-19,6,8,Gold); }
                else { Box(-12,-3,24,4,Wood); } break;
        }
        if (Selected) DrawString(ThemeDB.FallbackFont,new Vector2(-25,-55),Spent ? "Empty chest" : Kind,HorizontalAlignment.Left,-1,13,Gold);
    }
}
