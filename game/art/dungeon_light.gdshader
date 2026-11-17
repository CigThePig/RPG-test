shader_type canvas_item;
render_mode unshaded, blend_mul;
// The ruin's darkness, multiplied over the finished world so it crosses floor, walls,
// props and actors alike. Torches, braziers and relics are baked into one small light
// map; the wayfarer's own lantern is added here. Light steps down in dithered bands.
uniform sampler2D light_map : filter_linear, repeat_disable;
uniform vec2 light_map_size;
uniform sampler2D lantern_visibility : filter_nearest, repeat_disable;
uniform vec2 lantern;
uniform float lantern_radius;
uniform vec3 ambient;
uniform float reach_time;
varying vec2 place;
void vertex(){place=VERTEX;}
void fragment(){
    vec2 p=floor(place)+vec2(0.5);
    vec3 light=texture(light_map,(p/3.0+vec2(0.5))/light_map_size).rgb;
    // Firelight breathes a little; reduced motion pins the clock and stills it.
    light*=1.0-0.045*(0.5+0.5*sin(reach_time*5.1))*(0.5+0.5*sin(reach_time*2.3+1.7));
    vec2 v=(p-lantern)*vec2(1.0,1.6);
    float k=clamp(1.0-length(v)/lantern_radius,0.0,1.0);
    float visibility=texture(lantern_visibility,(p/3.0+vec2(0.5))/light_map_size).r;
    light=max(light,vec3(1.0,0.93,0.80)*k*(2.0-k)*visibility);
    float level=max(light.r,max(light.g,light.b));
    vec3 hue=level>0.004?light/level:vec3(1.0);
    float bayer=mod(floor(p.x),2.0)<1.0?(mod(floor(p.y),2.0)<1.0?0.0:0.75):(mod(floor(p.y),2.0)<1.0?0.5:0.25);
    float q=min(floor(level*6.0+bayer)/6.0,1.0);
    COLOR=vec4(ambient+(vec3(1.0)-ambient)*hue*q,1.0);
}
