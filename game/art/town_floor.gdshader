shader_type canvas_item;
render_mode unshaded;
// Floors inside the block houses. Vertex colour: r floor style over 4 (0 boards,
// 1 flagstones, 2 packed earth and straw), g the walls shading the tile over 4
// (north 1, west 2), b 1 for a stone threshold in a doorway.
uniform vec4 town_palette[35];
varying vec2 place;
varying vec4 flags;
void vertex(){place=VERTEX;flags=COLOR;}
float town_hash(vec2 p){return fract(sin(dot(floor(p),vec2(127.1,311.7)))*43758.5453);}
void fragment(){
    vec2 p=floor(place)+vec2(0.5),q=p-vec2(0.5);
    int style=int(floor(flags.r*4.0+0.5)),shade=int(floor(flags.g*4.0+0.5));
    vec2 l=q-floor(q/vec2(24.0,12.0))*vec2(24.0,12.0);
    int t;
    if(flags.b>0.5){t=10;if(l.y<1.0)t=12;if(l.y>=11.0||l.x==0.0||l.x==23.0)t=8;if(l.x==11.0)t=9;}
    else if(style==0){
        // Boards run across the room, four pixels deep, butted at uneven lengths.
        float row=floor(q.y/4.0),qx=q.x+floor(town_hash(vec2(row,7.0))*26.0);
        float board=floor(qx/26.0),id=town_hash(vec2(board,row));
        t=id>0.66?31:id<0.3?29:30;
        if(mod(q.y,4.0)>=3.0||mod(qx,26.0)>=25.0)t=28;
        else if(mod(q.y,4.0)<1.0&&id>0.5)t=min(t+1,31);
        if(town_hash(q+vec2(5.0,1.0))>0.96&&t>28)t-=1;
    }else if(style==1){
        float row=floor(q.y/6.0),qx=q.x+mod(row,2.0)*6.0,id=town_hash(vec2(floor(qx/12.0),row));
        t=id>0.7?10:9;
        if(mod(q.y,6.0)<1.0&&id>0.3)t+=1;
        if(mod(q.y,6.0)>=5.0||mod(qx,12.0)>=11.0)t=8;
    }else{
        float patch=town_hash(floor(q/vec2(9.0,5.0)));
        t=patch>0.5?33:32;
        vec2 cell=floor(q/vec2(6.0,3.0));
        float h=town_hash(cell+vec2(11.0,3.0));
        if(h>0.6&&q.y-cell.y*3.0==1.0&&q.x-cell.x*6.0>=1.0&&q.x-cell.x*6.0<4.0)t=34;
        if(town_hash(q)>0.95)t=32;
    }
    vec4 c=town_palette[t];
    // The walls to the north and west throw their shade a little way onto the floor.
    if((shade==1||shade==3)&&l.y<5.0)c.rgb*=l.y<3.0?0.66:0.82;
    if(shade>=2&&l.x<5.0)c.rgb*=l.x<3.0?0.78:0.9;
    COLOR=c;
}
