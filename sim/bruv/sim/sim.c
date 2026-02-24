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
#include "optim.h"

#define SVEC2 "(%f, %f)"
#define SVEC3 "(%f, %f, %f)"
#define SVEC4 "(%f, %f, %f, %f)"
#define PVEC2(x...) (f64)(x)[0], (f64)(x)[1]
#define PVEC3(x...) (f64)(x)[0], (f64)(x)[1], (f64)(x)[2]
#define PVEC4(x...) (f64)(x)[0], (f64)(x)[1], (f64)(x)[2], (f64)(x)[3]

static f64 function(const f64* rstr params, void* rstr user) {
    (void)user;
    f64 x = *params;
    return 4.0 + x*(1.46787 + x*(2.0));
    // return 4.0 + x*(1.46787 + x*(-2.0 + x*(5.6 + x*1.2)));
}

static i64 counter= 0;
static f64 function2D(const f64* rstr params, void* rstr user) {
    (void)user;
    ++counter;

    // f64 x = params[0];
    // f64 y = params[1];
    // return sqed(x - 2.0) + sqed(y + 3.0);
    // return sqed(sqed(x) + y - 11) + sqed(x + sqed(y) - 7);
    // return sqed(1 - x) + 100*sqed(y - sqed(x));
    // godDAMN that one is hard to minimise.

    f64 f = 0.0;
    for (i64 i=0; i<50; ++i) {
        f += 100.0*sqed(params[i + 1] - sqed(params[i])) + sqed(1 - params[i]);
        // f += sqed(params[i] - (f64)(i*i - i));
    }
    return f;
}

static void testing(void) {
    printf("TESTING\n");

    f64 best_cost;
    printf("1D test:\n");
    f64 r = 0.0;
    f64 m = 1.0;
    f64 mem;
    f64 root = opt_run1D(function, NULL, 1, &mem,
            &r, &m, -10e3, 10e3,
            1e-5, 1e-9,
            &best_cost
        );
    printf("root: %.18g\n", root);
    printf("fval: %.18g\n", best_cost);
    printf("\n");

    printf("2D test:\n");
    // f64 x[2] = { -100, -3 };
    // f64 x[6] = { -100, 400, 100, -5e5, 1, -90 };
    f64 x[50] = { 0};
    for (i64 i=0; i<numel(x); ++i)
        x[i] = -10*i;
    u8 tmp[OPT_RUN_MEMSIZE(numel(x))];
    i32 success = opt_run(function2D, NULL, numel(x), tmp,
            1e-10, 1e-10, x, &best_cost
        );
    if (!success)
        printf("FAILLLLLLLLLEDDDDDDDDDDDDD\n");
    printf("root: %.18g", x[0]);
    for (i64 i=1; i<numel(x); ++i)
        printf(", %.18g", x[i]);
    printf("\n");
    printf("fval: %.18g\n", best_cost);
    printf("fevals: %lld\n", counter);

    printf("AGAIN !!:\n");
    success = opt_run(function2D, NULL, numel(x), tmp,
            1e-15, 1e-15, x, &best_cost
        );
    if (!success)
        printf("FAILLLLLLLLLEDDDDDDDDDDDDD\n");
    printf("root: %.18g", x[0]);
    for (i64 i=1; i<numel(x); ++i)
        printf(", %.18g", x[i]);
    printf("\n");
    printf("fval: %.18g\n", best_cost);
    printf("fevals: %lld\n", counter);


    printf("AGAIN !!!!!!:\n");
    success = opt_run(function2D, NULL, numel(x), tmp,
            0, 0, x, &best_cost
        );
    // holy shit it works w zero tol??
    if (!success)
        printf("FAILLLLLLLLLEDDDDDDDDDDDDD\n");
    printf("root: %.18g", x[0]);
    for (i64 i=1; i<numel(x); ++i)
        printf(", %.18g", x[i]);
    printf("\n");
    printf("fval: %.18g\n", best_cost);
    printf("fevals: %lld\n", counter);

    printf("END TESTING\n");
}
