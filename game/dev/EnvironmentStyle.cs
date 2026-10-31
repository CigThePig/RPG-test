using Godot;
namespace Mosslight;
/// <summary>Cosmetic revision only: never serialized and never used by World.</summary>
public static class EnvironmentStyle
{
    public const int Revision = 1;
    public static readonly Color Mortar = new("303c3d"), Stone = new("69726b"), Cap = new("899183"),
        Highlight = new("a4ab96"), Shade = new("465452"), Moss = new("536c50"), Floor = new("59605a");
    // Explicit unsigned overflow, no platform/string hashes or shared Random state.
    public static uint Hash(int seed, int x, int y, int slot = 0)
    {
        unchecked
        {
            uint Mix(uint n) { n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; return n ^ (n >> 16); }
            uint h = Mix((uint)seed ^ 0x6d6f7373u ^ (uint)Revision * 0x9e3779b9u);
            h = Mix(h ^ Mix((uint)x));
            h = Mix(h ^ Mix((uint)y));
            return Mix(h ^ (uint)slot);
        }
    }
}

public static class EnvironmentThemes
{
    public static Color Tint(string theme) => theme switch { "overgrown"=>new Color("b0c9a1"),"ceremonial"=>new Color("cbd1c5"),_=>Colors.White };
    public static Color Accent(string theme) => theme switch { "overgrown"=>new Color("718f56"),"ceremonial"=>new Color("b2a576"),_=>new Color("8c927b") };
    // A block's corridors share its room's theme; halls follow the surrounding zone.
    public static string At(RegionState r,int x,int y)
    {
        if(r.Rooms.Count==0)return "weathered";
        int cx=x/DungeonGeometry.Cell,cy=y/DungeonGeometry.Cell;
        return r.Landmarks.FirstOrDefault(room=>room.X==cx && room.Y==cy)?.Theme ?? Landmarks.Zone(r.Seed,cx,cy);
    }
}
