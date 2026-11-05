shader_type canvas_item;
render_mode unshaded;
uniform sampler2D depth_map : filter_linear, repeat_disable;
uniform vec2 lookup_size;
uniform float cosmetic_seed;
uniform vec4 palette[12];
varying vec2 place;
void vertex() { place = VERTEX; }
void fragment() {
    vec2 p = floor(place) + vec2(0.5);
    vec2 sample_data = texture(depth_map, (p / 3.0 + vec2(0.5)) / lookup_size).rg;
    float d = sample_data.r * 68.0 - 8.0;
    float v = sample_data.g;
    float edge = d - (0.2 + v * 2.6);
    float depth = d + (v - 0.5) * 10.0;
    vec4 c = palette[7];
    if (depth < 33.0) c=palette[6];
    if (depth < 21.0) c=palette[5];
    if (depth < 12.0) c=palette[4];
    if (depth < 6.0) c=palette[3];
    // Stable integer-coordinate clusters, not photographic or per-frame noise.
    float cluster=mod(floor(p.x/5.0)*37.0+floor(p.y/3.0)*71.0+cosmetic_seed,799.0);
    if (d<9.0 && mod(cluster,17.0)<1.0 && mod(p.x,5.0)<3.0 && mod(p.y,3.0)<1.0) c=palette[10];
    if (d>5.0 && mod(cluster,47.0)<1.0 && mod(p.y,3.0)<1.0 && mod(p.x,5.0)<4.0) c=depth<21.0?palette[9]:palette[8];
    if (edge<2.6) c=palette[2];
    if (edge<1.3) c=v>0.7?palette[2]:palette[1];
    if (edge<0.1) c=v<0.42?palette[0]:palette[1];
    COLOR=c;
}
