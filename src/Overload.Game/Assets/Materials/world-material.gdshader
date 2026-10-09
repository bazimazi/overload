shader_type canvas_item;
render_mode unshaded;
uniform sampler2D earth_material : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D moss_material : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D stone_material : source_color, filter_linear_mipmap, repeat_enable;
uniform bool outdoors = true;
uniform int biome = 0;
uniform vec4 bounds = vec4(0.0, 0.0, 6144.0, 3840.0);
uniform vec2 player_position = vec2(0.0);
varying vec2 world_position;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float field(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), f.x), f.y);
}
vec2 mirrored(vec2 p) { return abs(fract(p) * 2.0 - 1.0); }
void vertex() { world_position = VERTEX; }
void fragment() {
    vec2 p = world_position;
    float macro = field(p / 470.0) * 0.65 + field(p / 137.0) * 0.35;
    vec3 earth = mix(textureLod(earth_material, mirrored(p / 510.0), 2.0).rgb,
        textureLod(earth_material, mirrored(p.yx / 683.0 + vec2(0.23, 0.47)), 2.0).rgb, 0.32);
    vec3 base;
    if (outdoors) {
        float vegetation = biome == 1 ? 0.72 : biome == 2 ? 0.45 : 0.08;
        vegetation *= smoothstep(0.22, 0.76, macro);
        base = mix(earth, textureLod(moss_material, mirrored(p / 591.0 + vec2(0.21)), 2.0).rgb, vegetation);
        if (biome == 1) {
            float damp = smoothstep(0.64, 0.83, field(p / 227.0));
            base = mix(base, vec3(0.105, 0.145, 0.14), damp * 0.42);
        }
    } else {
        base = textureLod(stone_material, mirrored(p / 330.0), 1.0).rgb;
        base = mix(base, earth, smoothstep(0.62, 0.86, macro) * 0.13);
    }
    vec3 tone = biome == 1 ? vec3(0.85, 1.04, 0.99) : biome == 2 ? vec3(0.94, 0.93, 1.08)
        : biome == 3 ? vec3(1.06, 0.93, 0.95) : vec3(1.04, 0.99, 0.90);
    base *= tone * mix(0.86, 1.12, macro);
    base = mix(vec3(dot(base, vec3(0.3, 0.5, 0.2))), base, 0.83) * 0.88;
    float lantern = exp(-dot(p - player_position, p - player_position) / 8500.0);
    base += vec3(0.023, 0.034, 0.035) * lantern;
    bool outside = p.x < bounds.x || p.y < bounds.y || p.x > bounds.x + bounds.z || p.y > bounds.y + bounds.w;
    COLOR = vec4(base * (outside ? 0.36 : 1.0), 1.0);
}
