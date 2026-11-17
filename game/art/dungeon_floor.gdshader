shader_type canvas_item;
render_mode unshaded;
// Ruin floors: staggered flagstones in the hall's own stone, cobbled corridors, moss
// and blades where the Reach has crept in, inlaid tiles in the sanctuaries, standing
// water in the low slabs, and the shade thrown by the walls to the upper-left.
#include "res://game/art/dungeon_common.gdshaderinc"
varying vec2 place;
void vertex(){place=VERTEX;}
void fragment(){
    vec2 p=floor(place)+vec2(0.5);
    vec4 data=dungeon_data(p);
    float d=data.r,broad=data.g,clump=data.b,fine=data.a;
    int theme=dungeon_theme(p,clump,fine);
    float room=texture(dungeon_tiles,p/vec2(24.0,12.0)/dungeon_tile_size).a;
    float grit=dungeon_hash(floor(p/vec2(2.0,1.0)));
    float checker=mod(floor(p.x)+floor(p.y),2.0);
    bool corridor=room+(grit-0.5)*0.3<0.5;
    int base=2+theme*6;
    // Shade: walls rise to the upper-left of the light, so their shadow falls down-right.
    int shade=0;
    if(dungeon_distance(p+vec2(0.0,-3.0))<0.0||dungeon_distance(p+vec2(-4.0,-2.0))<0.0)shade=2;
    else if(dungeon_distance(p+vec2(-1.0,-6.0))<0.0||dungeon_distance(p+vec2(-9.0,-3.0))<0.0)shade=1;
    if(d<2.0)shade=max(shade,1);
    int t;
    bool joint=false;
    if(theme==2&&!corridor){
        // Sanctuary: small polished tiles in two tones inside bordered panels.
        vec2 g=floor(p/vec2(12.0,6.0)),l=p-vec2(0.5)-g*vec2(12.0,6.0);
        vec2 panel=mod(p-vec2(0.5),vec2(48.0,24.0));
        float h=dungeon_hash(g*vec2(3.0,5.0)+vec2(7.0,1.0));
        t=mod(g.x+g.y,2.0)<1.0?2:3;
        if(broad>0.62)t+=1;
        if(l.y==0.0)t+=1;
        if(h>0.965){t=1;if(l.x==floor(3.0+l.y*(h>0.98?1.4:-1.2)+(h>0.98?0.0:6.0))&&l.y>=1.0)t=0;}
        if(l.x==11.0||l.y==5.0){t=0;joint=true;}
        if(panel.x<2.0||panel.y<1.0){t=1;joint=false;}
        if(grit>0.95&&t>1)t-=1;
        t=max(t-shade,0);
        vec4 c=dungeon_palette[base+t];
        // Gold studs where the panel borders cross, and a worn inlay line between them.
        if(panel.x<2.0&&panel.y<1.0)c=dungeon_palette[shade>1?25:26];
        else if(panel.y<1.0&&mod(floor(p.x),4.0)<1.0&&h<0.8)c=dungeon_palette[25];
        COLOR=c;
    }else{
        vec2 local,size;float id;
        if(corridor){
            // Corridors: small staggered cobbles.
            float row=floor(p.y/6.0);
            float qx=p.x+mod(row,2.0)*6.0;
            float column=floor(qx/12.0);
            local=vec2(qx-0.5-column*12.0,p.y-0.5-row*6.0);size=vec2(12.0,6.0);
            id=dungeon_hash(vec2(column,row)*vec2(5.0,11.0)+vec2(41.0,9.0));
        }else{
            float row=floor(p.y/12.0);
            vec2 q=vec2(p.x+mod(row,2.0)*12.0,p.y);
            vec2 block=floor(q/vec2(24.0,12.0));
            vec2 l=q-vec2(0.5)-block*vec2(24.0,12.0);
            float h=dungeon_hash(block*vec2(3.0,7.0)+vec2(17.0,3.0));
            vec2 origin=vec2(0.0);size=vec2(24.0,12.0);
            if(h>0.86){size=vec2(12.0,6.0);origin=floor(l/size)*size;}
            else if(h<0.22){}
            else if(h<0.42){size=vec2(12.0,12.0);origin=vec2(floor(l.x/12.0)*12.0,0.0);}
            else if(h<0.58){size=vec2(24.0,6.0);origin=vec2(0.0,floor(l.y/6.0)*6.0);}
            else{
                bool left=h<0.72;
                if((l.x<12.0)==left){size=vec2(12.0,12.0);origin=vec2(left?0.0:12.0,0.0);}
                else{size=vec2(12.0,6.0);origin=vec2(left?12.0:0.0,floor(l.y/6.0)*6.0);}
            }
            local=l-origin;
            id=dungeon_hash(block*vec2(5.0,11.0)+origin+vec2(41.0,9.0));
        }
        float id2=fract(id*17.0+0.37);
        // Whole slabs take the damp or the wear, so stains follow the stonework.
        float stain=dungeon_data(p-local+size*0.5).g+(id2-0.5)*0.16;
        t=id>0.80?3:id<0.16?1:2;
        if(stain<0.38)t-=1;
        if(stain>0.64)t+=1;
        t=clamp(t,1,3);
        // Chiselled edges: a lit lip along the top and left, shade above the lower joint.
        if(local.y==0.0&&local.x<size.x-1.0&&id2>0.3)t+=1;
        else if(local.x==0.0&&id2>0.55)t+=1;
        else if(local.y==size.y-2.0&&size.y>6.0&&id2<0.5)t-=1;
        if(grit>0.95)t-=1;
        if(grit<0.03)t+=1;
        t=clamp(t,1,4);
        if(local.x==size.x-1.0||local.y==size.y-1.0){t=0;joint=true;}
        // Chipped corners and a few cracked slabs.
        if(id2>0.7&&local.x>=size.x-3.0&&local.y>=size.y-2.0&&local.x+local.y*2.0>=size.x+size.y*2.0-6.0){t=0;joint=true;}
        if(id<0.16&&size.x>12.0){
            float cx=floor(size.x*(0.3+0.4*id2)+(local.y-size.y*0.5)*(id2>0.5?0.8:-0.8)+(mod(local.y,3.0)<1.0?1.0:0.0));
            if(local.x==cx&&local.y>0.0)t=0;
        }
        t=max(t-shade,0);
        vec4 c=dungeon_palette[base+t];
        // Growth. The overgrown halls carry carpets of moss with blades and flowers;
        // elsewhere moss only keeps to damp joints near the walls.
        float moss=clump*0.62+fine*0.38+(d<14.0?(14.0-d)*0.012:0.0)+(grit-0.5)*0.06;
        if(theme==1){
            if(moss>0.56){
                int m=moss>0.69?22:21;
                if(moss<0.575)m=checker>0.5?21:20;
                if(joint)m=20;
                else if(local.y==0.0&&moss>0.63)m+=1;
                vec2 cell=floor(p/vec2(6.0,4.0)),l=p-vec2(0.5)-cell*vec2(6.0,4.0);
                float h=dungeon_hash(cell+vec2(3.0,29.0)),k=dungeon_hash(cell+vec2(91.0,17.0));
                if(moss>0.62&&h>0.72){
                    float gust=sin(p.x*0.021+p.y*0.034-reach_time*0.7+broad*7.0);
                    int motif=k<0.3?0x008292:k<0.55?0x008304:k<0.8?0x0062a9:0x000184;
                    vec2 lean=l+vec2(gust>0.9&&l.y<2.0?1.0:0.0,0.0);
                    if(lean.x<6.0&&dungeon_bit(motif,lean))m=l.y==0.0&&k>0.5?24:23;
                }else if(moss>0.64&&h<0.05&&l.x>=2.0&&l.x<=3.0&&l.y>=1.0&&l.y<=2.0){
                    c=dungeon_palette[k>0.5?31:32];if(l.y==2.0)c.rgb*=0.8;
                    COLOR=c;m=-1;
                }
                if(m>=0){m=max(m-(shade>1?1:0),20);c=dungeon_palette[m];}
            }
        }else if(joint&&moss>0.62&&d<26.0)c=dungeon_palette[shade>0?20:21];
        // Standing water gathers on the low weathered slabs, away from the walls.
        float pool=clump*0.8+fine*0.2;
        if(theme==0&&!corridor&&d>9.0&&pool>0.765&&broad<0.5){
            // Dark still water; its upper rim catches a line of light.
            vec4 above=dungeon_data(p+vec2(0.0,-1.5));
            c=dungeon_palette[pool<0.773?27:28];
            if(above.b*0.8+above.a*0.2<=0.765)c=dungeon_palette[29];
            vec2 cell=floor(p/vec2(7.0,3.0));
            float h=dungeon_hash(cell+vec2(53.0,7.0));
            float wink=sin(reach_time*1.3+h*40.0);
            if(pool>0.775&&h>0.78&&p.y-0.5-cell.y*3.0==1.0){
                float lx=p.x-0.5-cell.x*7.0;
                if(lx>=2.0&&lx<=(wink>0.2?4.0:3.0))c=dungeon_palette[wink>0.75?30:29];
            }
        }
        COLOR=c;
    }
}
