#include "sim.h"

#include "assert.h"


c_IH sim_interpretation_hash(void) {
    c_IH h = c_ih_initial();
    #define X(name, type, flags)                                \
        h = c_ih_append(h, #name, flags | generic(objof(type)   \
            , f64:  C_F64                                       \
            , i64:  C_I64                                       \
            , f32*: C_PTR_F32                                   \
            , f64*: C_PTR_F64                                   \
            , i8*:  C_PTR_I8                                    \
            , i16*: C_PTR_I16                                   \
            , i32*: C_PTR_I32                                   \
            , i64*: C_PTR_I64                                   \
            , u8*:  C_PTR_U8                                    \
            , u16*: C_PTR_U16                                   \
            , u32*: C_PTR_U32                                   \
            , u64*: C_PTR_U64                                   \
        ));
    SIM_INTERPRETATION
    #undef X
    return h;
}



__attribute((__noinline__, __unused__))
static f32 br_sin(f32 x) {
    // Now use a polynomial approximation of `sin(x)`, for `x` in 0..pi/2. See:
    // https://www.desmos.com/calculator/puilcnyfxn
    f32 c1 = +1.00000000000000000;
    f32 c3 = -0.16666658910800000;
    f32 c5 = +0.00833305796660000;
    f32 c7 = -0.00019809319994700;
    f32 c9 = +0.00000260558007796;
    f32 x2 = x*x;
    f32 sinx = (
        ((((c9 * x2 + c7) * x2 + c5) * x2 + c3) * x2 + c1) * x
    );
    return sinx;
}

// typedef f32 vec24 __attribute((__vector_size__(8)));

// __attribute((__used__))
// static void sin2(vec2 f) {
//     f[0] = br_sin(f[0]);
//     f[1] = br_sin(f[1]);
//     // return f;
// }

// typedef struct vec2s {
//     f32 x;
//     f32 y;
// } vec2s;
// __attribute((__used__))
// static void sin2s(vec2s* f) {
//     f->x = br_sin(f->x);
//     f->y = br_sin(f->y);
//     // return f;
// }

#include "maths.h"
static void testing(void) {
    printf("TESTING\n");
    vec2 a = vec2(7);
    vec2 b = vec2(3, 3);
    vec3 c = vec3(8);
    vec4 d = v4PI;
    (void)(a + b);
    (void)(c + d);
    printf("%d\n", sametype(vec3, vec4));
    i32 x = fp_selectx(
        1 /* f32 */,
        2 /* f64 */,
        3 /* vec2 */,
        4 /* vec3 */,
        5 /* vec4 */,
        d
    );
    printf("%d\n", x);
    printf("nan/inf tests:\n");
    printf("1 %d\n", isnan(vec4(1, 1, 1, NAN)));
    printf("0 %d\n", isnan(vec4(1, 1, 1, INF)));
    printf("0 %d\n", isnan(vec4(1, 1, 1, 1)));
    printf("1 %d\n", isnan(vec3(1, 1, NAN)));
    printf("0 %d\n", isnan(vec3(1, 1, INF)));
    printf("0 %d\n", isnan(vec3(1, 1, 1)));
    printf("1 %d\n", isinf(vec4(1, 1, 1, INF)));
    printf("0 %d\n", isinf(vec4(1, 1, 1, NAN)));
    printf("0 %d\n", isinf(vec4(1, 1, 1, 1)));
    printf("1 %d\n", isinf(vec3(1, 1, INF)));
    printf("0 %d\n", isinf(vec3(1, 1, NAN)));
    printf("0 %d\n", isinf(vec3(1, 1, 1)));
    printf("0 %d\n", isallnan(vec4(NAN, NAN, NAN, 1)));
    printf("1 %d\n", isallnan(vec4(NAN, NAN, NAN, NAN)));
    printf("0 %d\n", isallnan(vec4(NAN, NAN, NAN, INF)));
    printf("0 %d\n", isallnan(vec3(NAN, NAN, 1)));
    printf("1 %d\n", isallnan(vec3(NAN, NAN, NAN)));
    printf("0 %d\n", isallnan(vec3(NAN, NAN, INF)));
    printf("0 %d\n", isallinf(vec4(INF, INF, INF, 1)));
    printf("1 %d\n", isallinf(vec4(INF, INF, INF, INF)));
    printf("0 %d\n", isallinf(vec4(INF, INF, INF, NAN)));
    printf("0 %d\n", isallinf(vec3(INF, INF, 1)));
    printf("1 %d\n", isallinf(vec3(INF, INF, INF)));
    printf("0 %d\n", isallinf(vec3(INF, INF, NAN)));
    // printf("%d\n", isallinf(10.));
    // printf("%d\n", isinf(1));
    printf("eq tests:\n");
    printf("1 %d\n", eq(v2PI, vec2(fPI)));
    printf("0 %d\n", eq(v2PI, vec2(fPI, 1)));
    printf("0 %d\n", eq(v3PI, vec3(fPI, 1, 3)));
    // printf("%d\n", eq(v3PI, v2PI));


    // lowk pretty beefy expansion already but like she'll be right.
    printf("misc:\n");
    printf("1 %d\n", eq(a, b + b + 1));
    printf("1 %d\n", isvec(a));
    printf("7 %f\n", max(a, b)[0]);
    printf("3 %f\n", min(a, b)[1]);
    printf("-7 %f\n", -abs(-a)[0]);

}




void sim_execute(simState* rstr s) {
    printf("<START>\n");
    printf("dujj\nhi: %f\nbye: %f\n", s->hi, s->bye);
    for (int i=0; i<s->size; ++i)
        printf("%hu\n", s->data[i]);
    s->data[1] = 5;
    printf("<END>\n");
    testing();
}
