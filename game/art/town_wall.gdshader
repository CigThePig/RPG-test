shader_type canvas_item;
render_mode unshaded;
// Walls of the block houses. Vertex colour: r 0 wall top / 1 south face; on a top, g is
// the mask of neighbouring wall tiles (north 1, east 2, south 4, west 8) over 15; on a
// face, g and b are its left and right ends (0 continues, 0.5 stops, 1 inside corner);
// a carries the wall style (0 timber, 1 stone, 2 plank), +4 for a window, +8 if it is lit, over 16.
uniform vec4 town_palette[35];
const float RISE=16.0;
varying vec2 place;
varying vec4 flags;
void vertex(){place=(MODEL_MATRIX*vec4(VERTEX,0.0,1.0)).xy;flags=COLOR;}
float town_hash(vec2 p){return fract(sin(dot(floor(p),vec2(127.1,311.7)))*43758.5453);}
void fragment(){
    vec2 p=floor(place)+vec2(0.5);
    int code=int(floor(flags.a*16.0+0.5));
    int style=code-code/4*4;
    bool window=(code/4)-(code/8)*2==1,lit=code/8==1;
    int t;
    if(flags.r<0.5){
        vec2 q=p+vec2(0.0,RISE)-vec2(0.5);
        vec2 l=q-floor(q/vec2(24.0,12.0))*vec2(24.0,12.0);
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
        float ty=floor((p.y+4.0)/12.0),h=p.y-0.5-(ty*12.0-4.0);
        float tx=floor(p.x/24.0),lx=p.x-0.5-tx*24.0;
        float grit=town_hash(p+vec2(3.0,9.0));
        if(style==0){
            // Limewashed panels in an oak frame on a stone footing.
            t=6;if(grit>0.93)t=5;if(h<4.0)t=5;
            if(h<2.0)t=h<1.0?1:2;
            if(lx<2.0)t=lx<1.0?3:2;
            if(!window&&lx==floor(17.0-h*0.9)&&h>=2.0&&h<13.0)t=2;
            if(h>=12.0)t=h<13.0?11:h<15.0?(mod(p.x-0.5,9.0)<1.0?8:10):8;
        }else if(style==1){
            float course=floor(h/5.0),cy=h-course*5.0;
            float bx=p.x-0.5+mod(course+ty,2.0)*6.0,id=town_hash(vec2(floor(bx/12.0),course+ty*3.0));
            t=id>0.7?11:id<0.25?9:10;
            if(cy<1.0)t+=1;
            if(cy>=4.0||mod(bx,12.0)>=11.0)t=8;
            if(h<1.0)t=8;if(h>=15.0)t=8;
            if(grit>0.95&&t>8)t-=1;
        }else{
            float board=floor((p.x-0.5)/4.0);
            t=town_hash(vec2(board,ty))>0.5?3:2;
            if(mod(p.x-0.5,4.0)>=3.0)t=1;
            if(h<2.0)t=1;if(h>=14.0)t=h<15.0?9:8;
        }
        if(window&&lx>=6.0&&lx<18.0&&h>=3.0&&h<12.0){
            bool frame=lx<7.0||lx>=17.0||h<4.0||h>=11.0||lx==12.0||h==7.0;
            t=frame?(style==1?8:1):lit?(h<6.0?14:13):15;
            if(!frame&&!lit&&lx+h==20.0)t=25;
        }
        // Ends: a lit corner post or quoin on the left, shade on the right and in inside corners.
        if(flags.g>0.25&&flags.g<0.75&&lx<2.0)t=style==1?12:4;
        if(flags.b>0.25&&flags.b<0.75&&lx>=21.0)t=lx>=23.0?0:(style==1?8:1);
        if(flags.g>0.75&&lx<2.0)t=style==1?8:1;
    }
    COLOR=town_palette[t];
}
