using Godot;
namespace Mosslight;
/// <summary>Static untextured terrain goes through one colored triangle mesh, avoiding thousands of canvas commands.</summary>
public sealed class TerrainBatch
{
    ArrayMesh? mesh;
    readonly List<Vector3> vertices = new();
    readonly List<Color> colors = new();
    public void Rect(Rect2 rect, Color color)
    {
        var a=rect.Position; var b=rect.End;
        foreach(var p in new[]{a,new Vector2(b.X,a.Y),b,a,b,new Vector2(a.X,b.Y)})
        { vertices.Add(new Vector3(p.X,p.Y,0));colors.Add(color); }
    }
    public void Line(Vector2 a, Vector2 b, Color color, float width)
    {
        // Terrain edges in this projection are axis-aligned.
        Rect(new Rect2(a.Min(b)-Vector2.One*width/2,(b-a).Abs()+Vector2.One*width),color);
    }
    public void Draw(CanvasItem target)
    {
        if(vertices.Count==0)return;
        var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]=vertices.ToArray();arrays[(int)Mesh.ArrayType.Color]=colors.ToArray();
        mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        target.DrawMesh(mesh,null);
    }
}
