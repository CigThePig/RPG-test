// Slow cloud shadow shared by every Living Reach terrain shader. One small tileable
// field, sampled in chunky two-pixel steps and quantised into three bands.
uniform sampler2D cloud_map : filter_linear, repeat_enable;
uniform float cloud_seed;
uniform float reach_time;
vec3 reach_cloud(vec3 colour,vec2 p){
    vec2 q=floor(p/2.0);
    vec2 c=q*2.0/vec2(1840.0,1040.0)+vec2(cloud_seed*0.013+reach_time*0.0020,cloud_seed*0.029+reach_time*0.00088);
    float n=texture(cloud_map,c).r+mod(q.x+q.y,2.0)*0.012;
    float shade=n>0.60?1.0:n>0.565?0.6:n>0.535?0.28:0.0;
    return mix(colour,colour*vec3(0.72,0.79,0.93),shade);
}
