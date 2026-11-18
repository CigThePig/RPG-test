namespace Mosslight;

/// <summary>Finite watershed revision 1. World-scale channels, basins and ridges are
/// composed first; smooth deterministic fields vary their banks and surface cover.
/// Nothing here consumes a World RNG or depends on presentation chunks.</summary>
public static class LivingReachGeography
{
    public const int Revision=1;
    static float Phase(int seed,int slot)=>(EnvironmentStyle.Hash(seed,0,0,12000+slot)&65535)/65535f*MathF.Tau;
    public static float RiverX(int seed,float y,int width,int height)
    {
        float t=y/height,p=Phase(seed,0);
        return width*(.57f+.075f*MathF.Sin(t*MathF.Tau*1.25f+p)+.025f*MathF.Sin(t*MathF.Tau*2.4f+p*.7f));
    }
    public static float RiverRadius(int seed,float y,int height)=>3.8f+1.2f*MathF.Sin(y/height*7+Phase(seed,1));
    public static float Forest(int seed,int x,int y)=>OverworldGroundArt.Noise(seed,x/43f,y/38f,120);
    public static void Generate(RegionState r)
    {
        float p=Phase(r.Seed,2),q=Phase(r.Seed,3);
        float lakeY=r.Height*(.29f+.025f*MathF.Sin(p)),lowerY=r.Height*.74f;
        float lakeX=RiverX(r.Seed,lakeY,r.Width,r.Height),lowerX=RiverX(r.Seed,lowerY,r.Width,r.Height);
        float joinY=r.Height*.55f,joinX=RiverX(r.Seed,joinY,r.Width,r.Height);
        bool Basin(float x,float y,float cx,float cy,float rx,float ry,float phase)
        {
            float dx=(x-cx)/rx,dy=(y-cy)/ry,angle=MathF.Atan2(dy,dx);
            float rim=1+.13f*MathF.Sin(angle*3+phase)+.065f*MathF.Sin(angle*5-phase);
            return dx*dx+dy*dy<rim*rim;
        }
        for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;x++)
        {
            float channel=RiverX(r.Seed,y,r.Width,r.Height);
            float radius=RiverRadius(r.Seed,y,r.Height);
            // Tributary descends from the eastern uplands into the main valley.
            float t=Math.Clamp((x-joinX)/(r.Width-joinX),0,1);
            float branchY=joinY-r.Height*.27f*t+3*MathF.Sin(t*MathF.Tau);
            bool water=Math.Abs(x-channel)<radius ||
                (x>=joinX && Math.Abs(y-branchY)<1.8f+.8f*(1-t)) ||
                Basin(x,y,lakeX,lakeY,24,15,p) || Basin(x,y,lowerX,lowerY,19,23,q) ||
                Basin(x,y,r.Width*.25f,r.Height*.22f,8,5,q);
            float ridge=r.Width*(.84f+.035f*MathF.Sin(y/29f+p));
            float ridgeWest=r.Width*(.12f+.035f*MathF.Sin(y/23f+q));
            bool rock=(Math.Abs(x-ridge)<3.5f+2*MathF.Sin(y/17f) && y>r.Height*.12f && y<r.Height*.79f) ||
                (Math.Abs(x-ridgeWest)<3 && y>r.Height*.12f && y<r.Height*.34f);
            float cover=Forest(r.Seed,x,y);
            var terrain=water?Terrain.Water:rock?Terrain.Rock:cover<.28f?Terrain.Earth:Terrain.Grass;
            if((x==0||y==0||x==r.Width-1||y==r.Height-1)&&!water)terrain=Terrain.Rock;
            r.Tiles[y*r.Width+x]=terrain;
        }
        // A local route and a branching woodland trail, rather than a road spanning
        // the continent. The western valley is deliberately clear of the watershed.
        Road(r,8,r.Height/2,76,r.Height/2,1.8f);
        Road(r,70,r.Height/2,110,r.Height*.37f,1.1f);
        Road(r,70,r.Height/2,105,r.Height*.65f,1.1f);
        // One modest timber crossing opens the eastern bank without interrupting
        // the water geometry. Collision reads the saved deck cells above water.
        int bridgeY=r.Height/2+7;
        int bank=(int)RiverX(r.Seed,bridgeY,r.Width,r.Height);
        for(int y=bridgeY-1;y<=bridgeY+1;y++)for(int x=bank-12;x<=bank+12;x++)
        {
            int at=y*r.Width+x;
            if(r.Tiles[at]==Terrain.Water)r.Crossings.Add(at);
            else r.Tiles[at]=Terrain.Earth;
        }
        Road(r,76,r.Height/2,bank-12,bridgeY,1);
        r.GeographyRevision=Revision;
    }
    static void Road(RegionState r,float ax,float ay,float bx,float by,float bend)
    {
        int steps=(int)(Math.Abs(bx-ax)+Math.Abs(by-ay))*3;
        for(int i=0;i<=steps;i++)
        {
            float t=i/(float)steps;
            int x=(int)MathF.Round(ax+(bx-ax)*t),y=(int)MathF.Round(ay+(by-ay)*t+MathF.Sin(t*MathF.PI)*bend*3);
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                if(x+dx>0&&y+dy>0&&x+dx<r.Width-1&&y+dy<r.Height-1)
                    r.Tiles[(y+dy)*r.Width+x+dx]=Terrain.Earth;
        }
    }
}
