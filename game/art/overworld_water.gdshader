shader_type canvas_item;
render_mode unshaded;
// Living Reach water: dithered depth, a raised northern bank, drifting swell lines,
// lapping foam and sparse sparkle. reach_time is frozen at zero under reduced motion.
#include "res://game/art/overworld_ground_common.gdshaderinc"
uniform sampler2D depth_map : filter_linear, repeat_disable;
uniform vec2 lookup_size;
uniform float cosmetic_seed;
uniform vec4 palette[12];
varying vec2 place;
void vertex() { place = VERTEX; }
float bayer(vec2 p){
    vec2 a=mod(floor(p),4.0);
    float m=mod(a.x*2.0+a.y*3.0,4.0)*4.0+mod(a.x+a.y*2.0,4.0);
    return (m+0.5)/16.0;
}
vec3 field(vec2 p){
    vec3 s=texture(depth_map,(p/3.0+vec2(0.5))/lookup_size).rgb;
    return vec3(s.r*68.0-8.0-s.b*8.0,s.g,s.b);
}
vec4 reach_water(vec2 p){
    vec3 here=field(p);
    float edge=here.x,v=here.y;
    if(edge<0.1)return reach_ground(p);
    // The raised bank only shades water lying straight below it. Measure that vertical
    // drop to the visible shoreline (the field already includes the irregular inset)
    // instead of guessing from the local slope, which flipped with the inset noise and
    // at corners: shadow went missing under banks and leaked onto side shores.
    float drop=-1.0,previous=edge;
    for(int k=1;k<=6;k++){
        float sample=field(p-vec2(0.0,float(k))).x;
        if(sample<0.1){
            // Screen rows are two ground units; interpolate where the shoreline crossed.
            drop=2.0*(float(k)-1.0+clamp((previous-0.1)/max(previous-sample,0.001),0.0,1.0));
            break;
        }
        previous=sample;
    }
    bool lip=drop>=0.0&&drop<8.5+v*3.0;
    float t=reach_time;
    float depth=edge+(v-0.5)*9.0+(bayer(p)-0.5)*3.4;
    int tone=depth<5.0?3:depth<12.0?4:depth<21.0?5:depth<33.0?6:7;
    // Swell: short lighter dashes drifting along alternating rows.
    float row=floor(p.y/5.0);
    float rate=2.2+3.0*ground_hash(vec2(row,3.0));
    float along=p.x+t*rate+row*7.0;
    vec2 swell=vec2(floor(along/26.0),row);
    float sh=ground_hash(swell+vec2(cosmetic_seed,0.0));
    float span=along-swell.x*26.0;
    bool crest=mod(p.y,5.0)<1.0&&sh>0.58&&span<5.0+sh*9.0&&edge>7.0;
    if(crest&&sin(t*0.8+sh*40.0)>-0.55)tone=max(tone-1,3);
    vec4 c=palette[tone];
    // Sparkle sits on a crest and blinks.
    if(crest&&sh>0.90&&span>2.0&&span<5.0&&sin(t*2.1+sh*90.0)>0.25)c=palette[9];
    if(lip){
        // The bank stands above the surface: soil face, then its shadow on the water.
        if(drop<2.4)c=palette[0];
        else if(drop<4.6)c=palette[1];
        else c=palette[2];
    }else{
        // Foam laps the low shores in broken, slowly breathing segments.
        float lap=sin(t*0.9+v*18.0+p.x*0.05)*0.9;
        float broken=ground_hash(vec2(floor((p.x+p.y)/3.0),floor(p.y/2.0)+floor(t*0.5+v*4.0)));
        if(edge<2.6+lap)c=broken>0.3?palette[10]:palette[11];
        else if(edge<4.4+lap)c=palette[3];
        float ring=edge-(8.0+sin(t*0.7+v*25.0)*2.2);
        if(ring>0.0&&ring<1.3&&broken>0.45)c=palette[11];
    }
    return c;
}
void fragment(){COLOR=reach_water(floor(place)+vec2(0.5));}
