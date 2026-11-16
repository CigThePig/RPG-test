shader_type canvas_item;
render_mode unshaded;
// A hipped roof seen from above: a ridge along its length, lit north slope and west hip,
// shaded east hip, courses following each slope. UV carries roof pixels; roof_alpha fades
// the whole roof while the wayfarer is inside.
uniform vec4 town_palette[35];
uniform vec2 roof_size;
uniform int roof_style;
uniform float roof_alpha=1.0;
float town_hash(vec2 p){return fract(sin(dot(floor(p),vec2(127.1,311.7)))*43758.5453);}
void fragment(){
    vec2 q=floor(UV);
    float w=roof_size.x,h=roof_size.y;
    float west=q.x,east=w-1.0-q.x,north=q.y,south=h-1.0-q.y;
    float depth=min(north,south)*2.0;
    int base=16+roof_style*4;
    int t;
    bool hip=min(west,east)<depth;
    if(hip){
        // Hips: courses run up the slope, lit on the west, shaded on the east.
        bool lit=west<east;
        float run=lit?west:east;
        t=lit?3:1;
        if(roof_style==0){float n=town_hash(vec2(floor(run/3.0),q.y));if(n>0.8)t-=1;}
        else if(mod(run,5.0)>=4.0||mod(q.y+floor(run/5.0)*3.0,6.0)>=5.0)t-=1;
        if(abs(min(west,east)-depth)<2.0)t=lit?3:0;
    }else{
        bool lit=north<south;
        float run=lit?north:south;
        t=lit?2:2;
        if(roof_style==0){
            float n=town_hash(vec2(q.x,floor(run/3.0)));
            t=n>0.82?3:n<0.2?1:2;
            if(mod(run,7.0)>=6.0)t=1;
        }else{
            float course=floor(run/4.0);
            if(mod(run,4.0)<1.0)t=3;
            if(mod(run,4.0)>=3.0)t=1;
            else if(mod(q.x+mod(course,2.0)*4.0,8.0)>=7.0)t=1;
            else if(town_hash(vec2(floor((q.x+mod(course,2.0)*4.0)/8.0),course+(lit?40.0:0.0)))>0.84)t=max(t-1,1);
        }
        if(!lit&&t>1&&mod(run,4.0)>=1.0)t=max(t-0,1);
        if(lit&&t<3&&town_hash(q)>0.7)t+=0;
        // The ridge: a bright cap with a dark line beneath it.
        float ridge=q.y-floor(h*0.5);
        if(ridge>=-1.0&&ridge<1.0)t=3;
        if(ridge>=1.0&&ridge<2.0)t=0;
        if(!lit&&ridge>=2.0&&t==3)t=2;
    }
    if(south<3.0)t=south<1.0?0:1;
    vec4 c=town_palette[base+t];
    if(west<1.0||east<1.0||north<1.0||south<1.0)c=town_palette[0];
    COLOR=vec4(c.rgb,roof_alpha);
}
