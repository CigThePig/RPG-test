shader_type canvas_item;
render_mode unshaded;
// Ruin walls. The top of a wall is dark packed stone ringed by lit coping; the face
// below is coursed masonry carrying each hall's wear: damp streaks, ivy or gilt.
// Vertex colour r selects top (0) or face (1); g and b describe the face's left and
// right ends (0 the face continues, 0.5 open floor, 1 an inside corner).
#include "res://game/art/dungeon_common.gdshaderinc"
const float RISE=16.0;
const float RIM=9.0;
varying vec2 place;
varying vec4 flags;
void vertex(){place=(MODEL_MATRIX*vec4(VERTEX,0.0,1.0)).xy;flags=COLOR;}
vec4 wall_top(vec2 p){
    vec2 q=p+vec2(0.0,RISE);
    vec4 data=dungeon_data(q);
    float e=-data.r,broad=data.g,clump=data.b,fine=data.a;
    int theme=dungeon_theme(q,clump,fine);
    int base=2+theme*6;
    float grit=dungeon_hash(floor(p/vec2(2.0,1.0))+vec2(7.0,3.0));
    if(e>=RIM){
        // Packed fill: nearly black, with a little broken stone showing near the coping.
        float rubble=dungeon_hash(floor(p/vec2(3.0,2.0))+vec2(19.0,5.0));
        int t=33;
        if(e<RIM+2.5)t=1;
        else if(e<RIM+9.0){t=rubble>0.45?35:34;if(e>RIM+6.0&&mod(floor(p.x)+floor(p.y),2.0)<1.0)t=34;}
        else if(e<RIM+20.0){t=rubble>0.72?35:rubble>0.3?34:33;}
        else t=rubble>0.9&&broad>0.45?34:33;
        return dungeon_palette[t];
    }
    // Outward normal of the nearest edge, in screen terms.
    vec2 n=-vec2(dungeon_distance(q+vec2(3.0,0.0))-dungeon_distance(q-vec2(3.0,0.0)),(dungeon_distance(q+vec2(0.0,3.0))-dungeon_distance(q-vec2(0.0,3.0)))*0.5);
    n=-normalize(n+vec2(0.0001));
    bool south=n.y>0.6,level=abs(n.y)>abs(n.x);
    float lit=dot(n,vec2(-0.6,-0.8));
    // Coping stones: long along the edge they follow.
    float along=level?floor((p.x+floor(p.y/5.0)*4.0)/10.0):floor((p.y+floor(p.x/9.0)*2.0)/6.0);
    float seam=level?mod(p.x-0.5+floor(p.y/5.0)*4.0,10.0):mod(p.y-0.5+floor(p.x/9.0)*2.0,6.0);
    float id=dungeon_hash(vec2(along,level?floor(p.y/5.0)+50.0:floor(p.x/9.0)+90.0));
    int t=lit>0.25?4:lit<-0.5&&!south?2:3;
    if(south)t=4;
    if(id>0.7)t+=1;if(id<0.2)t-=1;
    if(grit>0.93)t-=1;
    if(seam<1.0)t=1;
    if(e>RIM-1.8)t=1;
    t=clamp(t,1,5);
    if(e<1.7)t=south?5:lit>0.25?5:0;
    vec4 c=dungeon_palette[base+t];
    if(e<1.7&&!south&&lit<=0.25)c=dungeon_palette[1];
    float moss=clump*0.6+fine*0.4+(grit-0.5)*0.08;
    if(theme==1&&moss>0.53&&e>1.7)c=dungeon_palette[seam<1.0?20:moss>0.66?(grit>0.8?23:22):21];
    else if(theme!=2&&moss>0.66&&e>1.7&&seam>=1.0)c=dungeon_palette[21];
    if(theme==2&&e>3.6&&e<5.4&&seam>=1.0&&mod(floor(level?p.x:p.y),3.0)<2.0)c=dungeon_palette[lit>0.0||south?26:25];
    return c;
}
vec4 wall_face(vec2 p,float left,float right){
    float ty=floor((p.y+4.0)/12.0);
    float h=p.y-0.5-(ty*12.0-4.0);
    float tx=floor(p.x/24.0),lx=p.x-0.5-tx*24.0;
    vec2 foot=vec2(p.x,ty*12.0+6.0);
    vec4 data=dungeon_data(foot);
    float broad=data.g,clump=data.b,fine=data.a;
    int theme=dungeon_theme(foot,clump,fine);
    int base=2+theme*6;
    float grit=dungeon_hash(floor(p/vec2(2.0,1.0))+vec2(11.0,23.0));
    // Two courses of large dressed blocks over a darker plinth, lit from above.
    float course=h<8.0?0.0:h<13.0?1.0:2.0,cy=course<1.0?h-1.0:course<2.0?h-8.0:h-13.0;
    float tall=course<1.0?7.0:course<2.0?5.0:3.0;
    float bx=p.x-0.5+(mod(course+ty,2.0)<1.0?0.0:8.0);
    float brick=floor(bx/16.0),l=bx-brick*16.0;
    float id=dungeon_hash(vec2(brick,course+ty*3.0)+vec2(61.0,13.0));
    int t=course<1.0?4:course<2.0?3:2;
    if(id>0.78)t+=1;if(id<0.2)t-=1;
    if(cy==0.0)t+=1;
    if(cy==tall-2.0&&id<0.6)t-=1;
    if(grit>0.93)t-=1;
    t=clamp(t,2,5);
    if(cy==tall-1.0||l==15.0)t=1;
    // A few blocks have fallen out or cracked through.
    if(id<0.04&&cy<tall-1.0&&l<15.0&&course<2.0)t=cy==0.0?0:1;
    if(id>0.95&&cy>0.0&&cy<tall-1.0&&l==floor(4.0+cy*1.5))t=1;
    if(h<1.0)t=1;
    if(h>=15.0)t=0;
    // Ends: a lit quoin where the wall stops on the left, shade where it stops on the
    // right, and a soft occlusion shadow in inside corners.
    if(left>0.25&&left<0.75){if(lx<1.0)t=min(t+2,5);else if(lx<2.0&&t>0)t=min(t+1,5);}
    if(right>0.25&&right<0.75){if(lx>22.0)t=0;else if(lx>20.0)t=max(t-1,0);}
    if(left>0.75&&lx<3.0-floor(h/6.0))t=max(t-1,0);
    if(right>0.75&&lx>22.0)t=max(t-1,0);
    vec4 c=dungeon_palette[base+t];
    float column=dungeon_hash(vec2(floor(p.x/2.0),ty*7.0+3.0));
    float drop=dungeon_hash(vec2(floor(p.x/5.0),ty*5.0+9.0));
    if(theme==0){
        // Damp runs down from the coping and pools along the foot.
        if(column>0.88&&h>0.0&&h<4.0+drop*9.0&&t>0)c=dungeon_palette[base+max(t-1,0)];
        if(clump>0.6&&h>12.0+column*2.0&&h<15.0)c=dungeon_palette[column>0.5?21:20];
    }else if(theme==1){
        // Ivy hangs from the top and moss climbs from the floor.
        float hang=clump>0.42?2.0+drop*10.0+column*3.0:drop*3.0;
        float climb=15.0-(clump*9.0+column*3.0-3.0);
        if(h>0.0&&h<hang){c=dungeon_palette[column>0.7?23:column>0.3?22:21];if(h>hang-1.5)c=dungeon_palette[column>0.5?24:23];if(column<0.12)c=dungeon_palette[20];}
        else if(h>climb&&h<15.0)c=dungeon_palette[column>0.6?22:column>0.2?21:20];
        if(h>0.0&&h<hang&&drop>0.86&&column>0.8&&mod(h,4.0)<1.0)c=dungeon_palette[31];
    }else{
        // A gilt band below the coping and studs along it.
        if(h>=2.0&&h<3.0&&mod(floor(p.x),4.0)<3.0)c=dungeon_palette[25];
        if(h>=2.0&&h<3.0&&mod(floor(p.x),12.0)<1.0)c=dungeon_palette[26];
        if(h>=7.0&&h<9.0&&mod(floor(p.x)+ty*5.0,24.0)>=11.0&&mod(floor(p.x)+ty*5.0,24.0)<13.0)c=dungeon_palette[h<8.0?26:25];
    }
    return c;
}
void fragment(){
    vec2 p=floor(place)+vec2(0.5);
    COLOR=flags.r<0.5?wall_top(p):wall_face(p,flags.g,flags.b);
}
