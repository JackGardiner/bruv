#include "br.h"


// =========================================================================== //
// = IMPL ==================================================================== //
// =========================================================================== //


// VECTOR HELPERS //

vec2 vec2_copy(vec2 xy) { return xy; }
vec3 vec3_copy(vec3 xyz) { xyz[3] = 1.f; return xyz; }
vec4 vec4_copy(vec4 xyzw) { return xyzw; }

vec2 vec2_from_elems(f32 x, f32 y) {
    return (vec2){ x, y };
}
vec3 vec3_from_elems(f32 x, f32 y, f32 z) {
    return (vec3){ x, y, z, 1.f };
}
vec4 vec4_from_elems(f32 x, f32 y, f32 z, f32 w) {
    return (vec4){ x, y, z, w };
}

vec2 vec2_from_rep(f32 x) { return (vec2){ x, x }; }
vec3 vec3_from_rep(f32 x) { return (vec3){ x, x, x, 1.f }; }
vec4 vec4_from_rep(f32 x) { return (vec4){ x, x, x, x }; }

vec3 vec3_from_vec4(vec4 x) {
    x[3] = 1.f;
    return x;
}
vec4 vec4_from_vec3(vec3 xyz, f32 w) {
    xyz[3] = w;
    return xyz;
}



// LIMITS //

u32 fp_bits_f32(f32 f) {
    u32 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}
u64 fp_bits_f64(f64 f) {
    u64 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}
i32x2 fp_bits_vec2(vec2 f) {
    i32x2 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}
i32x4 fp_bits_vec3(vec3 f) {
    i32x4 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}
i32x4 fp_bits_vec4(vec4 f) {
    i32x4 i;
    memcpy(&i, &f, sizeof(i));
    return i;
}

f32 fp_from_bits_f32(u32 i) {
    f32 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
f64 fp_from_bits_f64(u64 i) {
    f64 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
vec2 fp_from_bits_vec2(i32x2 i) {
    vec2 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
vec3 fp_from_bits_vec3(i32x4 i) {
    vec3 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
vec4 fp_from_bits_vec4(i32x4 i) {
    vec4 f;
    memcpy(&f, &i, sizeof(f));
    return f;
}
