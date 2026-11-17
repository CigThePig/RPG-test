// Shared ruin lookups. Everything is chosen from absolute canvas pixels, the region
// lookups and a cosmetic seed: stable under camera motion and at every zoom.
uniform sampler2D dungeon_map : filter_linear, repeat_disable;
uniform sampler2D dungeon_tiles : filter_linear, repeat_disable;
uniform vec2 dungeon_map_size;
uniform vec2 dungeon_tile_size;
uniform vec4 dungeon_palette[36];
uniform uint dungeon_seed;
uniform float reach_time;
float dungeon_hash(vec2 p){
    uint h=uint(int(p.x)+4096)*374761393u+uint(int(p.y)+4096)*668265263u+dungeon_seed*1442695041u;
    h=(h^(h>>13u))*1274126177u;
    return float(h^(h>>16u))/4294967295.0;
}
// r: signed ground distance (positive on floor), g: broad stain, b: clump, a: fine.
vec4 dungeon_data(vec2 p){
    vec4 s=texture(dungeon_map,(p/3.0+vec2(0.5))/dungeon_map_size);
    return vec4(s.r*96.0-48.0,s.g,s.b,s.a);
}
float dungeon_distance(vec2 p){return dungeon_data(p).r;}
// 0 weathered, 1 overgrown, 2 ceremonial; seams between zones break up along the noise.
int dungeon_theme(vec2 p,float clump,float fine){
    vec3 w=texture(dungeon_tiles,p/vec2(24.0,12.0)/dungeon_tile_size).rgb;
    w.g+=(clump-0.5)*0.7;w.b+=(fine-0.5)*0.5;
    return w.g>w.r&&w.g>=w.b?1:w.b>w.r?2:0;
}
bool dungeon_bit(int mask,vec2 l){return ((mask>>(int(l.y)*6+int(l.x)))&1)==1;}
