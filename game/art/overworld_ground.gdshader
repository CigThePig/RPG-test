shader_type canvas_item;
render_mode unshaded;
#include "res://game/art/overworld_ground_common.gdshaderinc"
varying vec2 place;
void vertex(){place=VERTEX;}
void fragment(){COLOR=reach_ground(floor(place)+vec2(0.5));}
