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


#include "maths.h"
static void testing(void) {
    printf("TESTING\n");

    f32 test00 = 1.f;
    f32 test01 = fNAN;
    f32 test02 = -fINF;
    f64 test03 = 123.456;
    f64 test04 = NAN;
    vec2 test05 = vec2(0.f, 5.f);
    vec2 test06 = v2NAN;
    vec2 test07 = vec2(0.f, -fINF);
    vec2 test08 = vec2(fNAN, 123.f);
    vec3 test09 = vec3(1.f, 2.f, 3.f);
    vec3 test10 = vec3(fNAN, 2.f, fINF);
    vec3 test11 = vec3(-1000e30, -fINF, -fINF);
    vec3 test12 = v3NAN;
    vec4 test13 = vec4(1.f, 0.f, 3.f, fINF);
    vec4 test14 = vec4(6.f, fINF, fNAN, fINF);
    vec4 test15 = vec4(-4012.f);
    vec4 test16 = v4NAN;

    #define SVEC2 "(%f, %f)"
    #define SVEC3 "(%f, %f, %f)"
    #define SVEC4 "(%f, %f, %f, %f)"
    #define PVEC2(x...) (f64)(x)[0], (f64)(x)[1]
    #define PVEC3(x...) (f64)(x)[0], (f64)(x)[1], (f64)(x)[2]
    #define PVEC4(x...) (f64)(x)[0], (f64)(x)[1], (f64)(x)[2], (f64)(x)[3]

    printf("eq:\n");
    printf("1 %d\n", eq(test00, test00));
    printf("0 %d\n", eq(test00, test02));
    printf("0 %d\n", eq(test04, test04));
    printf("1 %d\n", eq(test13, test13));
    printf("0 %d\n", eq(test14, test14));
    // printf("~ %d\n", eq(test06, test16));
    // printf("~ %d\n", eq(test00, test04));

    printf("anyeq:\n");
    printf("1 %d\n", anyeq(test00, test00));
    printf("0 %d\n", anyeq(test00, test02));
    printf("0 %d\n", anyeq(test04, test04));
    printf("1 %d\n", anyeq(test13, test13));
    printf("1 %d\n", anyeq(test14, test14));
    // printf("~ %d\n", anyeq(test06, test16));
    // printf("~ %d\n", anyeq(test00, test04));

    printf("isinf:\n");
    printf("0 %d\n", isinf(test00));
    printf("0 %d\n", isinf(test01));
    printf("1 %d\n", isinf(test02));
    printf("0 %d\n", isinf(test03));
    printf("0 %d\n", isinf(test04));
    printf("0 %d\n", isinf(test05));
    printf("0 %d\n", isinf(test06));
    printf("1 %d\n", isinf(test07));
    printf("0 %d\n", isinf(test08));
    printf("0 %d\n", isinf(test09));
    printf("1 %d\n", isinf(test10));
    printf("1 %d\n", isinf(test11));
    printf("0 %d\n", isinf(test12));
    printf("1 %d\n", isinf(test13));
    printf("1 %d\n", isinf(test14));
    printf("0 %d\n", isinf(test15));
    printf("0 %d\n", isinf(test16));
    // printf("~ %d\n", isinf("hi"));
    // printf("~ %d\n", isinf(0));

    printf("isnan:\n");
    printf("0 %d\n", isnan(test00));
    printf("1 %d\n", isnan(test01));
    printf("0 %d\n", isnan(test02));
    printf("0 %d\n", isnan(test03));
    printf("1 %d\n", isnan(test04));
    printf("0 %d\n", isnan(test05));
    printf("1 %d\n", isnan(test06));
    printf("0 %d\n", isnan(test07));
    printf("1 %d\n", isnan(test08));
    printf("0 %d\n", isnan(test09));
    printf("1 %d\n", isnan(test10));
    printf("0 %d\n", isnan(test11));
    printf("1 %d\n", isnan(test12));
    printf("0 %d\n", isnan(test13));
    printf("1 %d\n", isnan(test14));
    printf("0 %d\n", isnan(test15));
    printf("1 %d\n", isnan(test16));
    // printf("~ %d\n", isnan("hi"));
    // printf("~ %d\n", isnan(0));

    printf("ifnan:\n");
    printf("1.0 %f\n", ifnan(test00, -6.f));
    // printf("~ "SVEC2"\n", ifnan(test05, -6.f));
    printf("(0.0, 5.0) "SVEC2"\n", PVEC2(ifnan(test05, vec2(-6.f))));
    printf("(-6.0, -6.0) "SVEC2"\n", PVEC2(ifnan(test06, vec2(-6.f))));
    printf("(0.0, -inf) "SVEC2"\n", PVEC2(ifnan(test07, vec2(-6.f))));
    printf("(nan, 1.0) "SVEC2"\n", PVEC2(ifnan(test08, vec2(fNAN, 1.f))));
    printf("(1.0, 2.0, 3.0) "SVEC3"\n", PVEC3(ifnan(test09, v3INF)));
    printf("(inf, inf, inf) "SVEC3"\n", PVEC3(ifnan(test10, v3INF)));
    // printf("~ "SVEC4"\n", PVEC4(ifnan(test15, fNAN)));
    printf("(-4012.0, -4012.0, -4012.0) "SVEC4"\n", PVEC4(ifnan(test15, v4NAN)));

    printf("ifnanelem:\n");
    printf("1.0 %f\n", ifnanelem(test00, -6.f));
    // printf("~ "SVEC2"\n", ifnanelem(test05, -6.f));
    printf("(0.0, 5.0) "SVEC2"\n", PVEC2(ifnanelem(test05, vec2(-6.f))));
    printf("(-6.0, -6.0) "SVEC2"\n", PVEC2(ifnanelem(test06, vec2(-6.f))));
    printf("(0.0, -inf) "SVEC2"\n", PVEC2(ifnanelem(test07, vec2(-6.f))));
    printf("(nan, 123.0) "SVEC2"\n", PVEC2(ifnanelem(test08, vec2(fNAN, 1.f))));
    printf("(1.0, 2.0, 3.0) "SVEC3"\n", PVEC3(ifnanelem(test09, v3INF)));
    printf("(inf, 2.0, inf) "SVEC3"\n", PVEC3(ifnanelem(test10, v3INF)));
    // printf("~ "SVEC4"\n", PVEC4(ifnanelem(test15, fNAN)));
    printf("(-4012.0, -4012.0, -4012.0) "SVEC4"\n", PVEC4(ifnanelem(test15, v4NAN)));

    printf("abs:\n");
    printf("+128 %u\n", abs((i8)-128));
    printf("%u\n", abs(intmin(i32)));
    printf("1.0 %f\n", abs(test00));
    printf("nan %f\n", abs(test01));
    printf("inf %f\n", abs(test02));
    printf("123.456 %f\n", abs(test03));
    printf("nan %f\n", abs(test04));
    printf("(0.0, 5.0) "SVEC2"\n", PVEC2(abs(test05)));
    printf("(nan, nan) "SVEC2"\n", PVEC2(abs(test06)));
    printf("(0.0, inf) "SVEC2"\n", PVEC2(abs(test07)));
    printf("(nan, 123.0) "SVEC2"\n", PVEC2(abs(test08)));
    printf("(1.0, 2.0, 3.0) "SVEC3"\n", PVEC3(abs(test09)));
    printf("(nan, 2.0, inf) "SVEC3"\n", PVEC3(abs(test10)));
    printf("(1000e30, inf, inf) "SVEC3"\n", PVEC3(abs(test11)));
    printf("(nan, nan, nan) "SVEC3"\n", PVEC3(abs(test12)));
    printf("(1.0, 0.0, 3.0, inf) "SVEC4"\n", PVEC4(abs(test13)));
    printf("(6.0, inf, nan, inf) "SVEC4"\n", PVEC4(abs(test14)));
    printf("(4012.0, 4012.0, 4012.0, 4012.0) "SVEC4"\n", PVEC4(abs(test15)));
    printf("(nan, nan, nan, nan) "SVEC4"\n", PVEC4(abs(test16)));
    // printf("~ %d\n", abs("hi"));

    printf("iabs:\n");
    printf("-128 %i\n", iabs((i8)-128));
    printf("%i\n", iabs(intmin(i32)));
    printf("1.0 %f\n", iabs(test00));
    printf("nan %f\n", iabs(test01));
    printf("inf %f\n", iabs(test02));
    printf("123.456 %f\n", iabs(test03));
    printf("nan %f\n", iabs(test04));
    printf("(0.0, 5.0) "SVEC2"\n", PVEC2(iabs(test05)));
    printf("(nan, nan) "SVEC2"\n", PVEC2(iabs(test06)));
    printf("(0.0, inf) "SVEC2"\n", PVEC2(iabs(test07)));
    printf("(nan, 123.0) "SVEC2"\n", PVEC2(iabs(test08)));
    printf("(1.0, 2.0, 3.0) "SVEC3"\n", PVEC3(iabs(test09)));
    printf("(nan, 2.0, inf) "SVEC3"\n", PVEC3(iabs(test10)));
    printf("(1000e30, inf, inf) "SVEC3"\n", PVEC3(iabs(test11)));
    printf("(nan, nan, nan) "SVEC3"\n", PVEC3(iabs(test12)));
    printf("(1.0, 0.0, 3.0, inf) "SVEC4"\n", PVEC4(iabs(test13)));
    printf("(6.0, inf, nan, inf) "SVEC4"\n", PVEC4(iabs(test14)));
    printf("(4012.0, 4012.0, 4012.0, 4012.0) "SVEC4"\n", PVEC4(iabs(test15)));
    printf("(nan, nan, nan, nan) "SVEC4"\n", PVEC4(iabs(test16)));
    // printf("~ %d\n", iabs("hi"));

    printf("min:\n");
    printf("-6.0 %f\n", min(test00, -6.f));
    // printf("~ "SVEC2"\n", min(test05, -6.f));
    printf("(-6.0, -6.0) "SVEC2"\n", PVEC2(min(test05, vec2(-6.f))));
    printf("(-6.0, -6.0) "SVEC2"\n", PVEC2(min(test06, vec2(-6.f))));
    printf("(-6.0, -inf) "SVEC2"\n", PVEC2(min(test07, vec2(-6.f))));
    printf("(nan, 1.0) "SVEC2"\n", PVEC2(min(test08, vec2(fNAN, 1.f))));
    printf("(1.0, 2.0, 3.0) "SVEC3"\n", PVEC3(min(test09, v3INF)));
    printf("(inf, 2.0, inf) "SVEC3"\n", PVEC3(min(test10, v3INF)));
    // printf("~ "SVEC4"\n", PVEC4(min(test15, fNAN)));
    printf("(-4012.0, -4012.0, -4012.0) "SVEC4"\n", PVEC4(min(test15, v4NAN)));

    printf("max:\n");
    printf("1.0 %f\n", max(test00, -6.f));
    // printf("~ "SVEC2"\n", max(test05, -6.f));
    printf("(0.0, 5.0) "SVEC2"\n", PVEC2(max(test05, vec2(-6.f))));
    printf("(-6.0, -6.0) "SVEC2"\n", PVEC2(max(test06, vec2(-6.f))));
    printf("(0.0, -6.0) "SVEC2"\n", PVEC2(max(test07, vec2(-6.f))));
    printf("(nan, 123.0) "SVEC2"\n", PVEC2(max(test08, vec2(fNAN, 1.f))));
    printf("(inf, inf, inf) "SVEC3"\n", PVEC3(max(test09, v3INF)));
    printf("(inf, inf, inf) "SVEC3"\n", PVEC3(max(test10, v3INF)));
    // printf("~ "SVEC4"\n", PVEC4(max(test15, fNAN)));
    printf("(-4012.0, -4012.0, -4012.0) "SVEC4"\n", PVEC4(max(test15, v4NAN)));


    printf("minelem:\n");
    printf("1.0 %f\n", minelem(test00));
    printf("nan %f\n", minelem(test01));
    printf("-inf %f\n", minelem(test02));
    printf("123.456 %f\n", minelem(test03));
    printf("nan %f\n", minelem(test04));
    printf("0.0 %f\n", minelem(test05));
    printf("nan %f\n", minelem(test06));
    printf("-inf %f\n", minelem(test07));
    printf("123.0 %f\n", minelem(test08));
    printf("1.0 %f\n", minelem(test09));
    printf("2.0 %f\n", minelem(test10));
    printf("-inf %f\n", minelem(test11));
    printf("nan %f\n", minelem(test12));
    printf("0.0 %f\n", minelem(test13));
    printf("6.0 %f\n", minelem(test14));
    printf("-4012.0 %f\n", minelem(test15));
    printf("nan %f\n", minelem(test16));
    // printf("~ %d\n", minelem("hi"));
    printf("0 %d\n", minelem(0));

    printf("maxelem:\n");
    printf("1.0 %f\n", maxelem(test00));
    printf("nan %f\n", maxelem(test01));
    printf("-inf %f\n", maxelem(test02));
    printf("123.456 %f\n", maxelem(test03));
    printf("nan %f\n", maxelem(test04));
    printf("5.0 %f\n", maxelem(test05));
    printf("nan %f\n", maxelem(test06));
    printf("0.0 %f\n", maxelem(test07));
    printf("123.0 %f\n", maxelem(test08));
    printf("3.0 %f\n", maxelem(test09));
    printf("inf %f\n", maxelem(test10));
    printf("-1000e30 %f\n", maxelem(test11));
    printf("nan %f\n", maxelem(test12));
    printf("inf %f\n", maxelem(test13));
    printf("inf %f\n", maxelem(test14));
    printf("-4012.0 %f\n", maxelem(test15));
    printf("nan %f\n", maxelem(test16));
    // printf("~ %d\n", maxelem("hi"));
    printf("0 %d\n", maxelem(0));


    printf("%f\n", 1.0 / 0.0);
    printf("%f\n", 0.0 / 0.0);
    printf("%f\n", -1.0 / 0.0);

    printf("maths:\n");
    printf("%f\n", argbeta(vec3(1,1,1), -v3ONE));
    printf("%f\n", argphi(vec3(1,1,1)));

    printf("END TESTING\n");
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
