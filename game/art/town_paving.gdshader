shader_type canvas_item;
render_mode unshaded;
// Town cobbles: rounded setts in staggered rows, worn paler along the middle of the
// paved ground, breaking up into loose stones and turf at its edge. Static; no clock.
uniform sampler2D paving_map : filter_linear, repeat_disable;
uniform vec2 paving_map_size;
uniform vec4 paving_palette[8];
uniform uint paving_seed;
varying vec2 place;
void vertex(){place=VERTEX;}
float paving_hash(vec2 p){
    uint h=uint(int(p.x)+4096)*374761393u+uint(int(p.y)+4096)*668265263u+paving_seed*1442695041u;
    h=(h^(h>>13u))*1274126177u;
    return float(h^(h>>16u))/4294967295.0;
}
void fragment(){
    vec2 p=floor(place)+vec2(0.5);
    float d=texture(paving_map,(p/3.0+vec2(0.5))/paving_map_size).r*48.0;
    float row=floor(p.y/5.0);
    float qx=p.x+mod(row,2.0)*4.0+floor(paving_hash(vec2(row,3.0))*3.0);
    float column=floor(qx/8.0);
    vec2 l=vec2(qx-0.5-column*8.0,p.y-0.5-row*5.0);
    float id=paving_hash(vec2(column,row)*vec2(5.0,11.0)+vec2(13.0,7.0));
    float fray=(paving_hash(floor(p/vec2(2.0,1.0)))-0.5)*2.0;
    // Past the edge only scattered setts remain, then none.
    float edge=d+fray+(id-0.5)*6.0;
    if(edge<2.0||(edge<6.5&&id<0.45)){COLOR=vec4(0.0);}
    else{
        bool joint=l.x>=7.0||l.y>=4.0||((l.x<1.0||l.x>=6.0)&&(l.y<1.0||l.y>=3.0));
        int t=id>0.78?3:id<0.2?1:2;
        if(d>20.0&&id>0.45)t+=1;
        if(l.y<1.0&&!joint)t+=1;
        if(l.y>=3.0&&id<0.5)t-=1;
        if(paving_hash(floor(p)+vec2(31.0,9.0))>0.95)t-=1;
        t=clamp(t,1,4);
        vec4 c=paving_palette[t];
        if(joint){
            c=paving_palette[0];
            // Grass and moss take the joints near the edge and here and there inside.
            float green=paving_hash(vec2(column,row)+vec2(77.0,41.0));
            if(edge<10.0&&green>0.35)c=paving_palette[green>0.7?5:6];
            else if(green>0.93)c=paving_palette[6];
            else if(id>0.6)c=paving_palette[7];
        }
        COLOR=c;
    }
}
