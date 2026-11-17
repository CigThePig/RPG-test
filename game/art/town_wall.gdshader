shader_type canvas_item;
render_mode unshaded;
// Walls of the block houses. UV carries wall pixels: on a head, the 24x12 top of the tile;
// on a face, x across the tile and y down from the wall head, so a low wall shows the foot
// of the same pattern a full-height wall shows whole. Vertex colour: r 0 head / 1 face; on
// a head, g is the mask of neighbouring wall tiles (north 1, east 2, south 4, west 8) over
// 15; on a face, g and b are its left and right ends (0 continues, 0.5 stops, 1 inside
// corner); a carries the style (0 timber, 1 stone, 2 plank), +4 for a window, +8 if lit, over 16.
uniform vec4 town_palette[35];
uniform float wall_rise=40.0;
uniform float wall_alpha=1.0;
varying vec2 place;
varying vec4 flags;
void vertex(){place=(MODEL_MATRIX*vec4(VERTEX,0.0,1.0)).xy;flags=COLOR;}
float town_hash(vec2 p){return fract(sin(dot(floor(p),vec2(127.1,311.7)))*43758.5453);}
void fragment(){
    vec2 l=floor(UV);
    float tx=floor(place.x/24.0+0.01);
    int code=int(floor(flags.a*16.0+0.5));
    int style=code-code/4*4;
    bool window=(code/4)-(code/8)*2==1,lit=code/8==1;
    int t;
    if(flags.r<0.5){
        int mask=int(floor(flags.g*15.0+0.5));
        bool north=(mask/1)-(mask/2)*2==1,east=(mask/2)-(mask/4)*2==1,south=(mask/4)-(mask/8)*2==1,west=mask/8==1;
        // The wall head: an oak plate on timber and plank walls, dressed coping on stone.
        if(style==1){t=10;if(l.x==11.0||l.x==23.0||l.y==5.0)t=9;if(l.y==0.0||l.y==6.0)t=11;}
        else{t=2;if(l.y==5.0||l.y==6.0)t=1;if(l.x==23.0)t=1;if(l.y==1.0||l.y==7.0)t=3;}
        if(!south&&l.y>=10.0)t=style==1?12:4;
        if(!north&&l.y<2.0)t=l.y<1.0?0:(style==1?12:4);
        if(!west&&l.x<2.0)t=l.x<1.0?0:(style==1?12:4);
        if(!east&&l.x>=22.0)t=l.x>=23.0?0:(style==1?8:1);
    }else{
        float h=l.y,lx=l.x,foot=wall_rise-h;
        float grit=town_hash(vec2(tx*24.0+lx,h)+vec2(3.0,9.0));
        if(style==0){
            // Limewashed panels in an oak frame on a stone footing.
            t=6;if(grit>0.94)t=5;if(h<6.0)t=5;
            if(h<3.0)t=h<1.0?1:h<2.0?3:2;
            if(lx<2.0)t=lx<1.0?3:2;
            if(h>=19.0&&h<21.0&&!window)t=h<20.0?3:2;
            if(!window&&h>=3.0&&h<19.0&&lx==floor(4.0+(19.0-h)*0.9))t=2;
            if(!window&&h>=21.0&&foot>5.0&&lx==12.0)t=2;
            if(foot<=5.0)t=foot>4.0?11:foot>1.0?(mod(tx*24.0+lx,9.0)<1.0?8:10):8;
        }else if(style==1){
            float course=floor(h/5.0),cy=h-course*5.0;
            float bx=tx*24.0+lx+mod(course,2.0)*6.0,id=town_hash(vec2(floor(bx/12.0),course));
            t=id>0.7?11:id<0.25?9:10;
            if(cy<1.0)t+=1;
            if(cy>=4.0||mod(bx,12.0)>=11.0)t=8;
            if(h<1.0||foot<=1.0)t=8;
            if(h>=1.0&&h<4.0&&t>8)t-=1;
            if(grit>0.95&&t>8)t-=1;
        }else{
            float board=floor((tx*24.0+lx)/4.0);
            t=town_hash(vec2(board,3.0))>0.5?3:2;
            if(mod(lx,4.0)>=3.0)t=1;
            if(h<3.0)t=h<1.0?1:2;if(h>=3.0&&h<6.0&&t>1)t-=1;
            if(h>=20.0&&h<22.0)t=h<21.0?4:1;
            if(foot<=3.0)t=foot>2.0?9:8;
        }
        if(window&&lx>=5.0&&lx<19.0&&h>=8.0&&h<25.0){
            bool frame=lx<6.0||lx>=18.0||h<9.0||h>=24.0||lx==12.0||h==16.0;
            t=frame?(style==1?8:1):lit?(h<12.0?14:13):15;
            if(!frame&&!lit&&lx+h==26.0)t=25;
            // Sill.
            if(h>=24.0)t=style==1?12:4;
        }
        // Ends: a lit corner post or quoin on the left, shade on the right and in inside corners.
        if(flags.g>0.25&&flags.g<0.75&&lx<2.0)t=style==1?12:4;
        if(flags.b>0.25&&flags.b<0.75&&lx>=21.0)t=lx>=23.0?0:(style==1?8:1);
        if(flags.g>0.75&&lx<2.0)t=style==1?8:1;
    }
    COLOR=vec4(town_palette[t].rgb,wall_alpha);
}
