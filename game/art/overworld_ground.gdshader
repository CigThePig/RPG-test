shader_type canvas_item;
render_mode unshaded;
#include "res://game/art/overworld_ground_common.gdshaderinc"
varying vec2 place;
void vertex(){place=VERTEX;}
void fragment(){vec2 p=floor(place)+vec2(0.5);COLOR=vec4(reach_cloud(reach_ground(p).rgb,p),1.0);}
