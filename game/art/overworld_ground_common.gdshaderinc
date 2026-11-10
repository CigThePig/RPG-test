// Living Reach dry ground. Everything is chosen from absolute canvas pixels, the
// region lookup and a cosmetic seed: stable under camera motion and zoom.
#include "res://game/art/overworld_cloud.gdshaderinc"
uniform sampler2D ground_map : filter_linear, repeat_disable;
uniform vec2 ground_lookup_size;
uniform vec4 ground_palette[20];
uniform uint ground_seed;
float ground_hash(vec2 p){
    uint h=uint(int(p.x)+4096)*374761393u+uint(int(p.y)+4096)*668265263u+ground_seed*1442695041u;
    h=(h^(h>>13u))*1274126177u;
    return float(h^(h>>16u))/4294967295.0;
}
// 6x4 pixel motifs, one bit per pixel, row-major from the upper-left corner.
bool ground_bit(int mask,vec2 l){return ((mask>>(int(l.y)*6+int(l.x)))&1)==1;}
vec4 reach_ground(vec2 p){
    vec4 data=texture(ground_map,(p/3.0+vec2(0.5))/ground_lookup_size);
    float soil=data.r*96.0-48.0,wet=data.g*48.0;
    float broad=data.b,clump=data.a;
    vec2 cell=floor(p/vec2(6.0,4.0)),l=floor(p-cell*vec2(6.0,4.0));
    float h=ground_hash(cell),k=ground_hash(cell+vec2(91.0,17.0));
    float tone=broad*0.62+clump*0.38;
    float checker=mod(floor(p.x)+floor(p.y),2.0);
    float fray=(ground_hash(floor(p/vec2(2.0,1.0))+vec2(13.0,5.0))-0.5)*3.2;
    float boundary=soil+(clump-0.5)*13.0+(broad-0.5)*6.0+fray;
    float shore=wet-(clump-0.5)*5.0-fray*0.6;
    float strand=1.0+7.0*clamp((broad-0.42)*3.5,0.0,1.0);
    if(shore<strand){
        // A narrow pale strand: pebbles, a darker waterline and no grass.
        vec4 c=ground_palette[16];
        if(tone>0.6||(tone>0.52&&checker>0.5))c=ground_palette[17];
        if(shore<1.4)c=ground_palette[15];
        if(h>0.86&&l.y==1.0&&l.x>0.0&&l.x<3.0)c=ground_palette[18];
        if(h>0.86&&l.y==2.0&&l.x>0.0&&l.x<3.0)c=ground_palette[15];
        if(shore>strand-1.0)c=ground_palette[1];
        return c;
    }
    if(boundary>0.0){
        int t=tone<0.36?7:tone>0.66?9:8;
        if(abs(tone-0.36)<0.022&&checker>0.5)t=8;
        if(abs(tone-0.66)<0.022&&checker>0.5)t=8;
        if(wet<11.0)t=max(t-1,6);
        // Grass sits above bare soil: a shaded lip directly inside the edge.
        if(boundary<2.1)t=6;
        else if(h>0.91){
            // Pebbles with a lit cap and a grounded shadow.
            if(l.y==1.0&&l.x>=1.0&&l.x<=(k>0.5?3.0:2.0))t=k>0.8?18:10;
            if(l.y==2.0&&l.x>=1.0&&l.x<=(k>0.5?3.0:2.0))t=6;
        }else if(h>0.80){
            // Short scuffs follow the grain of the track.
            if(l.y==(k>0.5?1.0:2.0)&&l.x<4.0)t=k>0.3?max(t-1,6):min(t+1,10);
        }else if(h<0.07&&boundary>5.0){
            if(ground_bit(0x004184,l))t=3;
            if(ground_bit(0x008000,l))t=1;
        }
        return ground_palette[t];
    }
    int t=tone<0.38?1:tone>0.63?3:2;
    if(abs(tone-0.38)<0.02&&checker>0.5)t=2;
    if(abs(tone-0.63)<0.02&&checker>0.5)t=2;
    if(tone>0.83)t=4;
    if(shore<9.0)t=max(t-1,1);
    // The turf catches light where it breaks over bare soil.
    if(boundary>-2.4){return ground_palette[min(t+1,4)];}
    // A slow gust lifts the blade tips in passing bands.
    float density=0.90-clump*0.36;
    if(h>density){
        float gust=sin(p.x*0.021+p.y*0.034-reach_time*0.9+broad*7.0);
        int motif=k<0.3?0x008292:k<0.55?0x008304:k<0.8?0x0062a9:0x000184;
        int shade=k<0.3?0x010000:k<0.55?0x008000:k<0.8?0x006000:0x00c000;
        vec2 lean=l+vec2(gust>0.86&&l.y<2.0?1.0:0.0,0.0);
        if(ground_bit(shade,l))t=max(t-1,0);
        if(lean.x<6.0&&ground_bit(motif,lean))t=l.y==0.0&&(k>0.55||gust>0.86)?min(t+2,5):min(t+1,4);
    }else if(h<0.035&&clump>0.5){
        // Wildflowers gather in drifts of one colour.
        float drift=ground_hash(floor(p/vec2(54.0,36.0))+vec2(5.0,9.0));
        if(drift>0.35&&l.x>=2.0&&l.x<=3.0&&l.y>=1.0&&l.y<=2.0){
            vec4 flower=ground_palette[drift>0.8?14:drift>0.58?12:13];
            return l.y==1.0?flower:vec4(flower.rgb*0.78,1.0);
        }
        if(drift>0.35&&l.x==2.0&&l.y==3.0)t=0;
    }
    return ground_palette[t];
}
