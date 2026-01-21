#include "sim.h"

#include "assert.h"


c_IH sim_interpretation_hash(void) {
    c_IH h = c_ih_initial();
    #define X(name, type, flags)                            \
        h = c_ih_add(h, #name, flags | generic(objof(type)  \
            , f64:  C_F64                                   \
            , i64:  C_I64                                   \
            , f32*: C_PTR_F32                               \
            , f64*: C_PTR_F64                               \
            , i8*:  C_PTR_I8                                \
            , i16*: C_PTR_I16                               \
            , i32*: C_PTR_I32                               \
            , i64*: C_PTR_I64                               \
            , u8*:  C_PTR_U8                                \
            , u16*: C_PTR_U16                               \
            , u32*: C_PTR_U32                               \
            , u64*: C_PTR_U64                               \
        ));
    SIM_INTERPRETATION
    #undef X
    return h;
}


void sim_execute(brState* rstr s) {
    s->another_one = 3;
    printf("dujj\nhi: %f\nbye: %f\nanother_one: %lli\ndata: %p\n",
        s->hi, s->bye, s->another_one, (void*)s->data);
    // assert(0, " ");
    // assert(0, "oops i did it again");
}
