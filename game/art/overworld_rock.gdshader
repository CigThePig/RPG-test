shader_type canvas_item;
render_mode unshaded;
// Rock cells become one connected outcrop: a bevelled, mossed top lifted above the
// ground, a stratified south face and a soft cast shadow. Only cloud shadow moves.
#include "res://game/art/overworld_cloud.gdshaderinc"
uniform sampler2D rock_map : filter_linear, repeat_disable;
uniform vec2 rock_lookup_size;
uniform vec4 rock_palette[12];
uniform float rock_seed;
const float LIFT=10.0;
varying vec2 place;
void vertex(){place=VERTEX;}
float rock_hash(vec2 p){return fract(sin(dot(floor(p)+vec2(rock_seed,rock_seed*0.37),vec2(127.1,311.7)))*43758.5453);}
vec2 rock(vec2 p){
    vec3 s=texture(rock_map,(p/3.0+vec2(0.5))/rock_lookup_size).rgb;
    return vec2(s.r*68.0-8.0-s.b*8.0,s.g);
}
void fragment(){
    vec2 p=floor(place)+vec2(0.5);
    vec2 q=p+vec2(0.0,LIFT);
    vec2 top=rock(q);
    if(top.x>0.0){
        float d=top.x,v=top.y;
        vec2 grad=vec2(rock(q+vec2(3.0,0.0)).x-d,(rock(q+vec2(0.0,3.0)).x-d)*0.5);
        float lit=dot(normalize(grad+vec2(0.0001)),vec2(0.6,0.8));
        // Fractured planes: the nearest two cell seeds give a facet and its crack.
        vec2 g=p/vec2(15.0,9.0),base=floor(g);
        float near=9.0,next=9.0,sh=0.0;
        for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++){
            vec2 cell=base+vec2(float(i),float(j));
            vec2 seed=cell+vec2(rock_hash(cell*3.0+vec2(1.0,7.0)),rock_hash(cell*3.0+vec2(5.0,2.0)));
            float span=length((g-seed)*vec2(1.0,0.8));
            if(span<near){next=near;near=span;sh=rock_hash(cell*3.0+vec2(11.0,13.0));}else if(span<next)next=span;
        }
        float grain=rock_hash(p/vec2(3.0,2.0));
        int t=sh>0.7?5:sh<0.3?3:4;
        if(next-near<0.09)t=2;
        else if(next-near<0.2&&grain>0.5)t=min(t+1,6);
        if(grain>0.95)t=max(t-1,2);
        if(d<6.5){
            // Bevel: lit toward the upper-left, dark toward the lower-right.
            if(lit>0.3)t=min(t+1,6);
            if(lit<-0.3)t=max(t-2,2);
            if(d<2.6)t=lit>0.25?(grain>0.3?7:6):lit<-0.45?1:3;
        }
        // Moss settles on the sheltered flats and creeps toward lit edges.
        float moss=v+(rock_hash(p/vec2(2.0,1.0))-0.5)*0.14+(sh-0.5)*0.10;
        if(d>3.0&&moss>0.58)t=moss>0.68?(grain>0.72?10:9):8;
        COLOR=vec4(reach_cloud(rock_palette[t].rgb,p),1.0);
    }else{
        vec2 here=rock(p);
        bool a=rock(p+vec2(0.0,LIFT*0.25)).x>0.0,b=rock(p+vec2(0.0,LIFT*0.5)).x>0.0,c=rock(p+vec2(0.0,LIFT*0.75)).x>0.0;
        if(here.x>0.0||a||b){
            // South face. Height below the lip sets the stratum.
            int level=(c?0:1)+(b?0:1)+(a?0:1);
            float wander=rock_hash(vec2(floor(p.x/11.0),4.0))*4.0;
            float column=rock_hash(vec2(floor((p.x+wander)/6.0),9.0));
            int t=level==0?3:level==1?2:level==2?(column>0.5?2:1):1;
            if(column>0.82)t=max(t-1,0);
            if(column<0.18&&level<2)t=min(t+1,4);
            if(level==3&&here.x<2.5)t=0;
            if(mod(p.x+wander,6.0)<1.0&&column>0.35)t=max(t-1,0);
            if(level==0&&rock_hash(p/vec2(4.0,1.0))>0.6)t=min(t+1,4);
            COLOR=vec4(reach_cloud(rock_palette[t].rgb,p),1.0);
        }else{
            // Shadow thrown down-right across the ground.
            float cast=max(rock(p+vec2(-5.0,-2.0)).x,rock(p+vec2(-2.0,-3.0)).x);
            float alpha=cast>0.0?(mod(floor(p.x)+floor(p.y),2.0)<1.0&&cast<3.0?0.14:0.26):0.0;
            COLOR=vec4(rock_palette[11].rgb,alpha);
        }
    }
}
