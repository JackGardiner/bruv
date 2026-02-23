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


static void testing(void);

void sim_execute(simState* rstr s) {
    printf("<START>\n");
    printf("dujj\nhi: %f\nbye: %f\n", s->hi, s->bye);
    for (int i=0; i<s->size; ++i)
        printf("%hu\n", s->data[i]);
    s->data[1] = 5;
    printf("<END>\n");
    testing();
}






#include "maths.h"
#include "rand.h"

#define SVEC2 "(%f, %f)"
#define SVEC3 "(%f, %f, %f)"
#define SVEC4 "(%f, %f, %f, %f)"
#define PVEC2(x...) (f64)(x)[0], (f64)(x)[1]
#define PVEC3(x...) (f64)(x)[0], (f64)(x)[1], (f64)(x)[2]
#define PVEC4(x...) (f64)(x)[0], (f64)(x)[1], (f64)(x)[2], (f64)(x)[3]

static void testing(void) {
    printf("TESTING\n");

    printf("sin test:\n");
    brRand* rand = &(brRand){0};
    rand_init_init();
    rand_init(rand);
    for (i32 _=0; _<100; ++_) {
        f32 x = rand_0to1(rand);
        x *= 2*fTWOPI;

        f32 sinx;
        f32 cosx;
        sincos(x, &sinx, &cosx);
        printf("%+g\n", hypot(sinx, cosx) - 1.f);
    }

    printf("END TESTING\n");
}
