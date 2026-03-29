#include "sim.h"

#include "assertion.h"
#include "maths.h"

#include "cea.h"
#include "contour.h"
#include "relations.h"




void sim_execute(simState* rstr s) {
    printf("helloooo c\n");


    assert(s->out_count > 20, "output contour array is too small (%lld)",
            s->out_count);
    assert(s->out_z, "output array 'out_z' is null");
    assert(s->out_r, "output array 'out_r' is null");
    assert(s->out_T, "output array 'out_T' is null");
    assert(s->out_P, "output array 'out_P' is null");
    assert(s->out_M, "output array 'out_M' is null");


    /* Combustion */

    assert(s->dm_ox > 0.0, "invalid input: dm_ox=%g", s->dm_ox);
    assert(s->dm_fu > 0.0, "invalid input: dm_fu=%g", s->dm_fu);
    assert(s->P0_cc > 0.0, "invalid input: P0_cc=%g", s->P0_cc);
    assert(s->P_exit > 0.0, "invalid input: P_exit=%g", s->P_exit);

    s->ofr = s->dm_ox / s->dm_fu;
    s->T0_cc = cea_T_cc(s->P0_cc, s->ofr);
    s->gamma_tht = cea_gamma_tht(s->P0_cc, s->ofr);
    s->Mw_tht = cea_Mw_tht(s->P0_cc, s->ofr);

    SpecificHeatRatio* shr = get_shr(s->gamma_tht);

    f64 M_exit = isentropic_M_from_P_on_P0(s->P_exit / s->P0_cc, shr);
    f64 P_exit = s->P0_cc * isentropic_P_on_P0(M_exit, shr);
    f64 T_tht = s->T0_cc * isentropic_T_on_T0(1.0, shr);
    f64 P_tht = s->P0_cc * isentropic_P_on_P0(1.0, shr);

    // thrust = mdot * vel  [no pressure work]
    // note this is a pretty bad V_exit formula, disagrees with cea by ~10%.
    f64 V_exit = sqrt(2*shr->y/(shr->y - 1.0)
                    * GAS_CONSTANT/s->Mw_tht
                    * T_tht
                    * (1.0 - pow(s->P_exit / P_tht, (shr->y - 1.0)/shr->y)));
    s->Thrust = (s->dm_ox + s->dm_fu) * V_exit;
    assert(nearto(P_exit, s->P_exit),
            "failed to find perfectly expanded nozzle?");


    /* Geometry */

    s->A_tht = (s->dm_ox + s->dm_fu) / s->P0_cc
             * sqrt(s->T0_cc * GAS_CONSTANT / s->Mw_tht / shr->y)
             * pow(0.5*(shr->y + 1.0), shr->n);

    s->AEAT = isentropic_A_on_Astar(M_exit, shr);

    assert(s->R_cc > 0.0, "invalid input: R_cc=%g", s->R_cc);
    assert(s->A_tht > 0.0, "invalid input: A_tht=%g", s->A_tht);
    assert(s->AEAT > 1.0, "invalid input: AEAT=%g", s->AEAT);
    assert(0.0 < s->NLF && s->NLF <= 1.0, "invalid input: NLF=%g", s->NLF);
    assert(-PI_2 <= s->phi_conv && s->phi_conv < 0.0,
                "invalid input: phi_conv=%g", s->phi_conv);

    Contour* cnt = &(Contour){0};
    { // Get chamber contour and find chamber cyl length.
        f64 V_subsonic = s->A_tht*s->Lstar;
        f64 A_cc = PI * sqed(s->R_cc);
        s->L_cc = V_subsonic / A_cc * 0.8; // guess.
        init_cnt(cnt, s->R_cc, s->L_cc, s->A_tht, s->AEAT, s->NLF, s->phi_conv);
        // Fix cc straight length in one iteration (newton-raphson with a linear
        // section up-to the root, so onebang it).
        f64 DL_cc = (V_subsonic - cnt_V_subsonic(cnt)) / A_cc;
        cnt_change_length(cnt, DL_cc);
        f64 V = cnt_V_subsonic(cnt);
        assert(nearto(V, V_subsonic),
                "failed to size chamber? V_subonic=%g vs %g", V_subsonic, V);
    }

    s->R_tht = cnt->R_tht;
    s->R_exit = cnt->R_exit;
    s->z_tht = cnt->z_tht;
    s->z_exit = cnt->z_exit;
    s->phi_div = cnt->phi_div;
    s->phi_exit = cnt->phi_exit;


    /* Outputs */

    for (i64 i=0; i<s->out_count; ++i) {
        f64 z = lerpidx(0.0, cnt->z_exit, i, s->out_count);
        f64 r = cnt_r(cnt, z);
        f64 A_on_Astar = sqed(r) / sqed(s->R_tht);
        f64 M = (z < cnt->z_tht)
              ? isentropic_sub_M(A_on_Astar, shr)
              : isentropic_sup_M(A_on_Astar, shr);
        f64 T = s->T0_cc * isentropic_T_on_T0(M, shr);
        f64 P = s->P0_cc * isentropic_P_on_P0(M, shr);
        s->out_z[i] = z;
        s->out_r[i] = r;
        s->out_T[i] = T;
        s->out_P[i] = P;
        s->out_M[i] = M;
    }


    printf("goodbye c :(\n");
}


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
