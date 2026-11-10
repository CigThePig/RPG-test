shader_type canvas_item;
render_mode unshaded, blend_mul;
// Cloud shadow multiplied over the finished world, so it crosses terrain, props and
// actors alike. Reduced motion freezes reach_time.
#include "res://game/art/overworld_cloud.gdshaderinc"
varying vec2 place;
void vertex(){place=VERTEX;}
void fragment(){COLOR=vec4(reach_cloud(vec3(1.0),floor(place)+vec2(0.5)),1.0);}
