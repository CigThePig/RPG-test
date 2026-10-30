using Godot;
namespace Mosslight;
/// <summary>Static untextured terrain goes through one colored triangle mesh, avoiding thousands of canvas commands.</summary>
public sealed class TerrainBatch : IDisposable
{
    ArrayMesh? mesh;
    readonly List<Vector3> vertices = new();
    readonly List<Color> colors = new();
    readonly List<Vector2> uvs = new();
    public Texture2D? Texture { get; init; }
    public void Rect(Rect2 rect, Color color)
    {
        var a=rect.Position; var b=rect.End;
        Add(a); Add(new Vector2(b.X,a.Y)); Add(b); Add(a); Add(b); Add(new Vector2(a.X,b.Y));
        void Add(Vector2 p) { vertices.Add(new Vector3(p.X,p.Y,0)); colors.Add(color); }
    }
    public void Sprite(Rect2 destination, Rect2 source)
    {
        var a=destination.Position;var b=destination.End;
        var u=source.Position/Texture!.GetSize();var v=source.End/Texture.GetSize();
        Add(a,u);Add(new Vector2(b.X,a.Y),new Vector2(v.X,u.Y));Add(b,v);
        Add(a,u);Add(b,v);Add(new Vector2(a.X,b.Y),new Vector2(u.X,v.Y));
        void Add(Vector2 p,Vector2 uv){vertices.Add(new Vector3(p.X,p.Y,0));colors.Add(Colors.White);uvs.Add(uv);}
    }
    public int VertexCount => vertices.Count;
    public long VertexBytes => vertices.Count * (Texture==null?28L:36L);
    public void Dispose() { mesh?.Dispose(); mesh=null; }
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
        if(uvs.Count>0)arrays[(int)Mesh.ArrayType.TexUV]=uvs.ToArray();
        mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        target.DrawMesh(mesh,Texture);
    }
}
