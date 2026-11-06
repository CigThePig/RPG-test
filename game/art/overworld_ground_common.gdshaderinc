uniform sampler2D ground_map : filter_linear, repeat_disable;
uniform vec2 ground_lookup_size;
uniform vec4 ground_palette[12];
uniform uint ground_seed;
float ground_hash(vec2 p){
    uint h=uint(p.x)*374761393u+uint(p.y)*668265263u+ground_seed*1442695041u;
    h=(h^(h>>13u))*1274126177u;
    return float(h^(h>>16u))/4294967295.0;
}
vec4 reach_ground(vec2 p){
    vec4 data=texture(ground_map,(p/3.0+vec2(0.5))/ground_lookup_size);
    float soil=data.r*96.0-48.0,wet=data.g*48.0;
    float broad=data.b,clump=data.a;
    float fleck=ground_hash(floor(p/vec2(5.0,3.0)));
    float tone=broad*0.65+clump*0.35;
    vec4 c=ground_palette[2];
    if(tone<0.35)c=ground_palette[1];
    if(tone>0.67)c=ground_palette[3];
    if(wet<8.0&&tone<0.54)c=ground_palette[10];
    float boundary=soil+(clump-0.5)*11.0+(broad-0.5)*5.0;
    if(boundary>0.0){
        c=ground_palette[7];if(tone<0.35)c=ground_palette[6];if(tone>0.73)c=ground_palette[8];
        if(wet<5.0)c=ground_palette[5];
        // Broken sediment flecks; a lit upper edge and restrained shadow below.
        if(fleck>0.965&&mod(p.x,5.0)<3.0&&mod(p.y,3.0)<1.0)c=ground_palette[8];
        if(fleck>0.965&&mod(p.x,5.0)<3.0&&mod(p.y,3.0)>1.0)c=ground_palette[6];
    }else{
        // Clustered short blades with consistent upper-left light and contact shade.
        if(clump>0.51&&fleck>0.80&&mod(p.x,5.0)<3.0&&mod(p.y,3.0)>1.0)c=ground_palette[1];
        if(clump>0.51&&fleck>0.80&&mod(p.x,5.0)<2.0&&mod(p.y,3.0)<1.0)c=ground_palette[3];
        if(clump>0.70&&fleck>0.93&&mod(p.x,5.0)<1.0&&mod(p.y,3.0)<1.0)c=ground_palette[4];
    }
    return c;
}
