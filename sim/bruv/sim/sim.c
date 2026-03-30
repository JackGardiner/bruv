#include "sim.h"

#include "assertion.h"
#include "maths.h"

#include "cea.h"
#include "contour.h"
#include "relations.h"
#include "optim.h"



// Simulate the engine from inputs.
static void sim_ulate(simState* rstr s, i32 full_output);
enum { NO_FULL_OUTPUT = 0, GIVE_FULL_OUTPUT = 1 };

// Optimise the engine from the given seed inputs.
static void sim_optimise(simState* rstr s);

void sim_execute(simState* rstr s) {
    // Optimise system.
    sim_optimise(s);

    // Simulate and write all outputs.
    sim_ulate(s, GIVE_FULL_OUTPUT);
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



// =========================================================================== //
// = SIMULATION ============================================================== //
// =========================================================================== //

static void sim_ulate(simState* rstr s, i32 full_output) {

    /* Input validation. */

    assert(s->Lstar > 0.0, "invalid input: Lstar=%g", s->Lstar);
    assert(s->R_cc > 0.0, "invalid input: R_cc=%g", s->R_cc);
    assert(0.0 < s->NLF && s->NLF <= 1.0, "invalid input: NLF=%g", s->NLF);
    assert(-PI_2 <= s->phi_conv && s->phi_conv < 0.0,
                "invalid input: phi_conv=%g", s->phi_conv);

    assert(s->ofr > 0.0, "invalid input: ofr=%g", s->ofr);
    assert(s->dm_cc > 0.0, "invalid input: dm_cc=%g", s->dm_cc);
    assert(s->P_exit > 0.0, "invalid input: P_exit=%g", s->P_exit);
    assert(s->P0_cc > 0.0, "invalid input: P0_cc=%g", s->P0_cc);

    // Our CEA approximations assume ideally expanded at sea level.
    assert(nearto(s->P_exit, 101325.0), "exit pressure must be sea-level "
            "atmospheric, got: %g", s->P_exit);


    /* Combustion */

    s->T0_cc = cea_T0_cc(s->P0_cc, s->ofr);
    s->gamma_tht = cea_gamma_tht(s->P0_cc, s->ofr);
    s->Mw_tht = cea_Mw_tht(s->P0_cc, s->ofr);
    s->Isp = cea_Isp(s->P0_cc, s->ofr);

    s->Thrust = s->Isp * s->dm_cc * STANDARD_GRAVITY;

    SpecificHeatRatio* shr = get_shr(s->gamma_tht);
    s->A_tht = s->dm_cc / s->P0_cc
             * sqrt(s->T0_cc * GAS_CONSTANT / s->Mw_tht / shr->y)
             * pow(0.5*(shr->y + 1.0), shr->n);

    f64 M_exit = isentropic_M_from_P_on_P0(s->P_exit / s->P0_cc, shr);
    f64 P_exit = s->P0_cc * isentropic_P_on_P0(M_exit, shr);
    assert(nearto(P_exit, s->P_exit),
            "failed to find perfectly expanded nozzle?");

    s->AEAT = isentropic_A_on_Astar(M_exit, shr);

    s->dm_fu = s->dm_cc / (s->ofr + 1.0);
    s->dm_ox = s->dm_cc - s->dm_fu;


    /* Geometry */

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


    /* Post-construction tweaks. */

    s->efficiency = cos(s->phi_exit) // divergent exhaust.
                  * 0.97 // estimated viscous losses.
                  * 0.99; // estimated combustion losses.
    s->Isp *= s->efficiency;
    s->Thrust *= s->efficiency;


    /* Outputs */

    if (!full_output)
        return;

    assert(s->out_count > 20, "output contour array is too small (%lld)",
            s->out_count);
    assert(s->out_z, "output array 'out_z' is null");
    assert(s->out_r, "output array 'out_r' is null");
    assert(s->out_T, "output array 'out_T' is null");
    assert(s->out_P, "output array 'out_P' is null");
    assert(s->out_M, "output array 'out_M' is null");

    for (i64 i=0; i<s->out_count; ++i) {
        f64 z = lerpidx(0.0, cnt->z_exit, i, s->out_count);
        f64 r = cnt_r(cnt, z);
        f64 A_on_Astar = sqed(r) / sqed(cnt->R_tht);
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
}



// =========================================================================== //
// = OPTIMISATION ============================================================ //
// =========================================================================== //

typedef struct simParms {
    /* <OPTIM ORDERING> */
    f64 ofr;
    f64 dm_cc;
} simParams;
enum { PARAM_COUNT = sizeof(simParams) / 8 };
typedef struct simUser {
    simState* s;
    i32 N; // how many parameters being optimised.

    // Mapping of "`simParams` index" -> "`params[]` index". If that parameter is
    // not being used, maps to -1.
    i32 mapping[PARAM_COUNT];
} simUser;
static void sim_params_from(simUser* u, const f64* rstr params) {
    i32 i;
    /* <OPTIM ORDERING> */
    if ((i = u->mapping[0]) >= 0)
        u->s->ofr = bound_both(params[i], 1.0, 3.0);
    if ((i = u->mapping[1]) >= 0)
        u->s->dm_cc = bound_lower(params[i], 0.0);
}
static void sim_params_to(const simUser* u, f64* rstr params) {
    i32 i;
    /* <OPTIM ORDERING> */
    if ((i = u->mapping[0]) >= 0)
        params[i] = unbound_both(u->s->ofr, 1.0, 3.0);
    if ((i = u->mapping[1]) >= 0)
        params[i] = unbound_lower(u->s->dm_cc, 0.0);
}

static f64 sim_cost(const f64* rstr params, void* rstr user) {
    simUser* u = user;

    // Extract the given parameters.
    sim_params_from(u, params);

    // Simulate and evaluate.
    simState* s = u->s;
    sim_ulate(s, NO_FULL_OUTPUT);
    f64 cost = 0.0;
    cost += sqed(0.01 * (s->Thrust - s->target_Thrust)); // thrust target.
    cost -= sqed(0.025 * s->Isp); // higher Isp = goated.
    return cost;
}

static void sim_optimise(simState* rstr s) {
    assert(s->target_Thrust > 0.0, "invalid input: target_Thrust=%g",
            s->target_Thrust);

    simUser* u = &(simUser){ .s = s };

    // Setup the parameter mapping (to facilitate non-full optimisations).
    {
        i32 i = 0;
        #define push(x) do {                        \
                assert(i < PARAM_COUNT, "i=%d", i); \
                if ((x)) u->mapping[i] = u->N++;    \
                else     u->mapping[i] = -1;        \
                ++i;                                \
            } while (0)
        /* <OPTIM ORDERING> */
        push(s->optimise_ofr);
        push(s->optimise_dm_cc);
        #undef push
    }

    // If nothing to optimise, leave.
    if (u->N == 0)
        return;

    // Setup the total seeding params from the given state.
    f64 params[PARAM_COUNT]; // only first `u->N` elements used.
    assert(within(u->N, 0, PARAM_COUNT), "u->N=%d", u->N);
    sim_params_to(u, params);

    // Grab initial cost for funsies.
    f64 initial_cost = sim_cost(params, u);

    // Run the minimiser.
    f64 best_cost;
    u8 tmp[OPT_RUN_MEMSIZE(PARAM_COUNT)]; // worst-case sizing.
    enum { OPTIM_RUNS = 1 }; // several tries for max extraction?
    for (i32 i=0; i<OPTIM_RUNS; ++i) {
        i32 res = opt_run(sim_cost, u, u->N, tmp, 1e-8, 1e-8, params,
                &best_cost);
        if (!res) {
            printf("failed to optimise :((\n");
            return;
        }
    }
    printf("OPTIMISED :D\n");
    printf("    cost: $%g -> $%g\n", initial_cost, best_cost);
    printf("     ofr: %g\n", s->ofr);
    printf("   dm_cc: %g kg/s\n", s->dm_cc);
    printf("  Thrust: %g N\n", s->Thrust);
    printf("     Isp: %g s\n", s->Isp);
}
