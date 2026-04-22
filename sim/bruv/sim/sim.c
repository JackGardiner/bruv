#include "sim.h"

#include "assertion.h"
#include "maths.h"

#include "cea.h"
#include "contour.h"
#include "ethanol.h"
#include "ipa.h"
#include "optim.h"
#include "relations.h"
#include "stress.h"
#include "thermal.h"



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

static void sim_full_outputs(simState* rstr s, const Contour* cnt,
        const thermalStation* thermal_stns, i32 thermal_N,
        const stressStation* stress_stns, i32 stress_N);

static void sim_ulate(simState* rstr s, i32 full_output) {

    /* Input validation. */

    s->possible_system = 1;

    assert(s->Lstar > 0.0, "invalid input: Lstar=%g", s->Lstar);
    assert(s->R_cc > 0.0, "invalid input: R_cc=%g", s->R_cc);
    assert(0.0 < s->NLF && s->NLF <= 1.0, "invalid input: NLF=%g", s->NLF);
    assert(-PI_2 <= s->phi_conv && s->phi_conv < 0.0,
                "invalid input: phi_conv=%g", s->phi_conv);

    assert(0.0 <= s->helix_angle && s->helix_angle < PI_2,
            "invalid input: helix_angle=%g", s->helix_angle);
    assert(s->th_pdms > 0.0, "invalid input: th_pdms=%g", s->th_pdms);
    assert(s->k_pdms > 0.0, "invalid input: k_pdms=%g", s->k_pdms);
    assert(s->th_iw > 0.0, "invalid input: th_iw=%g", s->th_iw);
    assert(s->th_ow > 0.0, "invalid input: th_ow=%g", s->th_ow);
    assert(s->no_chnl > 0, "invalid input: no_chnl=%g", s->no_chnl);
    assert(s->th_chnl > 0.0, "invalid input: th_chnl=%g", s->th_chnl);
    assert(s->prop_chnl > 0.0, "invalid input: prop_chnl=%g", s->prop_chnl);
    assert(s->eps_chnl >= 0.0, "invalid input: eps_chnl=%g", s->eps_chnl);
    assert(s->T_fu0 > 0.0, "invalid input: T_fu0=%g", s->T_fu0);
    assert(s->Pr_fu > 1.0, "invalid input: Pr_fu=%g", s->Pr_fu);

    assert(s->ofr > 0.0, "invalid input: ofr=%g", s->ofr);
    assert(s->dm_cc > 0.0, "invalid input: dm_cc=%g", s->dm_cc);
    assert(s->P_exit > 0.0, "invalid input: P_exit=%g", s->P_exit);
    assert(s->P0_cc > 0.0, "invalid input: P0_cc=%g", s->P0_cc);

    // Our CEA approximations assume ideally expanded at sea level.
    assert(nearto(s->P_exit, 101325.0), "invalid input: exit pressure must be "
            "sea-level atmospheric, got %g", s->P_exit);


    /* Combustion */

    s->T0_cc = cea_T0_cc(s->P0_cc, s->ofr);
    s->rho0_cc = cea_rho0_cc(s->P0_cc, s->ofr);

    s->gamma_tht = cea_gamma_tht(s->P0_cc, s->ofr);
    s->Mw_tht = cea_Mw_tht(s->P0_cc, s->ofr);
    SpecificHeatRatio* shr_tht = get_shr(s->gamma_tht);

    s->M_exit = isentropic_M_from_P_on_P0(s->P_exit / s->P0_cc, shr_tht);
    f64 P_exit = s->P0_cc * isentropic_P_on_P0(s->M_exit, shr_tht);
    assert(nearto(P_exit, s->P_exit),
            "failed to find perfectly expanded nozzle?");
    s->gamma_exit = cea_gamma_exit(s->P0_cc, s->ofr);

    s->A_tht = s->dm_cc / s->P0_cc
             * sqrt(s->T0_cc * GAS_CONSTANT / s->Mw_tht / shr_tht->y)
             * pow(0.5*(shr_tht->y + 1.0), shr_tht->n);
    // TODO: ^ move to relations.

    s->AEAT = isentropic_A_on_Astar(s->M_exit, shr_tht);
    // TODO: ^ fixed point iterate

    s->dm_fu = s->dm_cc / (s->ofr + 1.0);
    s->dm_ox = s->dm_cc - s->dm_fu;

    s->Isp = cea_Isp(s->P0_cc, s->ofr);
    s->Thrust = s->Isp * s->dm_cc * STANDARD_GRAVITY;


    /* Geometry */

    Contour* cnt = &(Contour){0};
    { // Get chamber contour and find chamber cyl length.
        f64 V_subsonic = s->A_tht*s->Lstar;
        f64 A_cc = PI * sqed(s->R_cc);
        s->L_cc = V_subsonic / A_cc * 0.8; // guess.
        init_cnt(cnt, s->R_cc, s->L_cc, s->A_tht, s->AEAT, s->NLF, s->phi_conv,
                s->th_iw, s->helix_angle, s->th_chnl, s->no_chnl, s->prop_chnl);
        // Fix cc straight length in one iteration (newton-raphson with a linear
        // section up-to the root, so onebang it).
        f64 DL_cc = (V_subsonic - cnt_V_subsonic(cnt)) / A_cc;
        cnt_change_length(cnt, DL_cc);
        f64 V = cnt_V_subsonic(cnt);
        assert(nearto(V, V_subsonic),
                "failed to size chamber? V_subonic=%g vs %g", V_subsonic, V);
        s->possible_system &= cnt->possible;
    }

    s->R_tht = cnt->R_tht;
    s->R_exit = cnt->R_exit;
    s->z_tht = cnt->z_tht;
    s->z_exit = cnt->z_exit;
    s->phi_div = cnt->phi_div;
    s->phi_exit = cnt->phi_exit;

    s->wi_web = cnt_wi_web(cnt, cnt->z_tht);
    s->wi_chnl = cnt_wi_chnl(cnt, cnt->z_tht);
    s->psi_chnl = cnt_psi_chnl(cnt, cnt->z_tht);


    /* Post-construction tweaks. */

    s->efficiency = cos(s->phi_exit) // divergent exhaust.
                  * 0.9; // estimated viscous+combustion losses.
    s->Isp *= s->efficiency;
    s->Thrust *= s->efficiency;


    /* Thermals. */

    i32 thermal_N = 500;
    thermalStation* thermal_stns = malloc(thermal_N * sizeof(thermalStation));

    // Set target fuel injector pressure.
    s->P_fu1 = s->Pr_fu * s->P0_cc;
    // Guess pressure drop at 5 bar.
    s->P_fu0 = s->P_fu1 + 5e5;
    // Fixed point iterate to dial in ipa manifold pressure.
    for (i32 iter=0; /* true */; ++iter) {
        enum { MAX_ITERS = 20 };

        i32 possible = thermal_sim(s, cnt, thermal_stns, thermal_N);
        f64 T_fu1 = thermal_stns[0].T_c;
        f64 P_fu1 = thermal_stns[0].P_c;
        f64 diff = iterstep(&s->P_fu0, s->P_fu1 + s->P_fu0 - P_fu1);
        if (diff < 1.0 || iter >= MAX_ITERS) {
            s->possible_system &= possible;
            s->T_fu1 = T_fu1;
            s->P_fu1 = P_fu1;
            break;
        }
    }


    /* Stresses. */

    i32 stress_N = 200;
    stressStation* stress_stns = malloc(stress_N * sizeof(stressStation));

    stress_sim(s, cnt, thermal_stns, thermal_N, stress_stns, stress_N);

    s->min_SF = +1e6;
    for (i32 i=0; i<stress_N; ++i)
        s->min_SF = min(s->min_SF, stress_stns[i].firing.SF);


    /* Outputs */

    if (!full_output || s->out_count <= 0)
        return;

    sim_full_outputs(s, cnt, thermal_stns, thermal_N, stress_stns, stress_N);
}

static void sim_full_outputs(simState* rstr s, const Contour* cnt,
        const thermalStation* thermal_stns, i32 thermal_N,
        const stressStation* stress_stns, i32 stress_N) {
    assert(s->out_count > 20, "output array is too small (%lld)", s->out_count);
    assert(s->out_z, "null output array: out_z");
    assert(s->out_r, "null output array: out_r");
    assert(s->out_M_g, "null output array: out_M_g");
    assert(s->out_T_g, "null output array: out_T_g");
    assert(s->out_P_g, "null output array: out_P_g");
    assert(s->out_rho_g, "null output array: out_rho_g");
    assert(s->out_gamma_g, "null output array: out_gamma_g");
    assert(s->out_cp_g, "null output array: out_cp_g");
    assert(s->out_mu_g, "null output array: out_mu_g");
    assert(s->out_Pr_g, "null output array: out_Pr_g");
    assert(s->out_T_c, "null output array: out_T_c");
    assert(s->out_P_c, "null output array: out_P_c");
    assert(s->out_T_gw, "null output array: out_T_gw");
    assert(s->out_T_pdms, "null output array: out_T_pdms");
    assert(s->out_T_wg, "null output array: out_T_wg");
    assert(s->out_T_wc, "null output array: out_T_wc");
    assert(s->out_q, "null output array: out_q");
    assert(s->out_h_g, "null output array: out_h_g");
    assert(s->out_h_c, "null output array: out_h_c");
    assert(s->out_vel_c, "null output array: out_vel_c");
    assert(s->out_rho_c, "null output array: out_rho_c");
    assert(s->out_ff_c, "null output array: out_ff_c");
    assert(s->out_Re_c, "null output array: out_Re_c");
    assert(s->out_Pr_c, "null output array: out_Pr_c");
    assert(s->out_startup_SF, "null output array: out_startup_SF");
    assert(s->out_sigmah_pressure, "null output array: out_sigmah_pressure");
    assert(s->out_sigmah_thermal, "null output array: out_sigmah_thermal");
    assert(s->out_sigmah_bending, "null output array: out_sigmah_bending");
    assert(s->out_sigmah, "null output array: out_sigmah");
    assert(s->out_sigmam, "null output array: out_sigmam");
    assert(s->out_sigma_vm, "null output array: out_sigma_vm");
    assert(s->out_Ys, "null output array: out_Ys");
    assert(s->out_SF, "null output array: out_SF");
    assert(s->out_xtra, "null output array: out_xtra");


    assert(s->export_count > 20, "export array is too small (%lld)",
            s->export_count);
    assert(s->export_z, "null export array: export_z");
    assert(s->export_helix_angle, "null export array: export_helix_angle");
    assert(s->export_th_chnl, "null export array: export_th_chnl");
    assert(s->export_psi_chnl, "null export array: export_psi_chnl");


    ceaFit* fit_gamma = &(ceaFit){0};
    ceaFit* fit_cp = &(ceaFit){0};
    ceaFit* fit_mu = &(ceaFit){0};
    ceaFit* fit_Pr = &(ceaFit){0};
    cea_fit_gamma(fit_gamma, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_cp(fit_cp, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_mu(fit_mu, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_Pr(fit_Pr, s->P0_cc, s->ofr, s->M_exit);

    for (i64 i=0; i<s->out_count; ++i) {
        f64 z = lerpidx(0.0, cnt->z_exit, i, s->out_count);
        f64 r = cnt_r(cnt, z);
        f64 A_on_Astar = sqed(r) / sqed(cnt->R_tht);
        SpecificHeatRatio* shr_g = &(SpecificHeatRatio){0};
        f64 M_g;
        isentropic_shr_M(shr_g, &M_g, z < cnt->z_tht, A_on_Astar, fit_gamma,
                s->gamma_tht /* good guess */);
        f64 y1M22 = get_y1M22(M_g, shr_g);
        f64 T_g = s->T0_cc * isentropicx_T_on_T0(y1M22, shr_g);
        f64 P_g = s->P0_cc * isentropicx_P_on_P0(y1M22, shr_g);
        f64 rho_g = s->rho0_cc * isentropicx_rho_on_rho0(y1M22, shr_g);
        f64 cp_g = cea_sample(fit_cp, M_g);
        f64 mu_g = cea_sample(fit_mu, M_g);
        f64 Pr_g = cea_sample(fit_Pr, M_g);


        s->out_z[i] = z;
        s->out_r[i] = r;
        s->out_M_g[i] = M_g;
        s->out_T_g[i] = T_g;
        s->out_P_g[i] = P_g;
        s->out_rho_g[i] = rho_g;
        s->out_gamma_g[i] = shr_g->y;
        s->out_cp_g[i] = cp_g;
        s->out_mu_g[i] = mu_g;
        s->out_Pr_g[i] = Pr_g;
        {
            f64 t = z / cnt->z_exit;
            t *= thermal_N - 1;
            i32 k = min(max((i32)t, 0), thermal_N - 2);
            t -= k;
            typeof(thermal_stns) stns = thermal_stns;
            s->out_T_c[i] = lerp(stns[k].T_c, stns[k + 1].T_c, t);
            s->out_P_c[i] = lerp(stns[k].P_c, stns[k + 1].P_c, t);
            s->out_T_gw[i] = lerp(stns[k].T_gw, stns[k + 1].T_gw, t);
            s->out_T_pdms[i] = lerp(stns[k].T_pdms, stns[k + 1].T_pdms, t);
            s->out_T_wg[i] = lerp(stns[k].T_wg, stns[k + 1].T_wg, t);
            s->out_T_wc[i] = lerp(stns[k].T_wc, stns[k + 1].T_wc, t);
            s->out_q[i] = lerp(stns[k].q, stns[k + 1].q, t);
            s->out_h_g[i] = lerp(stns[k].h_g, stns[k + 1].h_g, t);
            s->out_h_c[i] = lerp(stns[k].h_c, stns[k + 1].h_c, t);
            s->out_vel_c[i] = lerp(stns[k].vel_c, stns[k + 1].vel_c, t);
            s->out_rho_c[i] = lerp(stns[k].rho_c, stns[k + 1].rho_c, t);
            s->out_ff_c[i] = lerp(stns[k].ff_c, stns[k + 1].ff_c, t);
            s->out_Re_c[i] = lerp(stns[k].Re_c, stns[k + 1].Re_c, t);
            s->out_Pr_c[i] = lerp(stns[k].Pr_c, stns[k + 1].Pr_c, t);
            s->out_xtra[i] = lerp(stns[k].xtra, stns[k + 1].xtra, t);
        }
        {
            f64 t = z / cnt->z_exit;
            t *= stress_N - 1;
            i32 k = min(max((i32)t, 0), stress_N - 2);
            t -= k;
            typeof(stress_stns) stns = stress_stns;
            s->out_startup_SF[i] = lerp(stns[k].startup.SF,
                    stns[k + 1].startup.SF, t);
            s->out_sigmah_pressure[i] = lerp(
                    stns[k].firing.sigmah_pressure,
                    stns[k + 1].firing.sigmah_pressure,
                    t);
            s->out_sigmah_thermal[i] = lerp(
                    stns[k].firing.sigmah_thermal,
                    stns[k + 1].firing.sigmah_thermal,
                    t);
            s->out_sigmah_bending[i] = lerp(
                    stns[k].firing.sigmah_bending,
                    stns[k + 1].firing.sigmah_bending,
                    t);
            s->out_sigmah[i] = lerp(
                    stns[k].firing.sigmah,
                    stns[k + 1].firing.sigmah,
                    t);
            s->out_sigmam[i] = lerp(
                    stns[k].firing.sigmam,
                    stns[k + 1].firing.sigmam,
                    t);
            s->out_sigma_vm[i] = lerp(
                    stns[k].firing.sigma_vm,
                    stns[k + 1].firing.sigma_vm,
                    t);
            s->out_Ys[i] = lerp(
                    stns[k].firing.Ys,
                    stns[k + 1].firing.Ys,
                    t);
            s->out_SF[i] = lerp(
                    stns[k].firing.SF,
                    stns[k + 1].firing.SF,
                    t);
        }
    }


    for (i64 i=0; i<s->export_count; ++i) {
        f64 z = lerpidx(0.0, cnt->z_exit, i, s->export_count);
        s->export_z[i] = z;
        s->export_helix_angle[i] = cnt_helix_angle(cnt, z);
        s->export_th_chnl[i] = cnt_th_chnl(cnt, z);
        s->export_psi_chnl[i] = cnt_psi_chnl(cnt, z);
    }
}



// =========================================================================== //
// = OPTIMISATION ============================================================ //
// =========================================================================== //

typedef struct simParms {
    /* <OPTIM ORDERING> */
    f64 ofr;
    f64 dm_cc;
    f64 helix_angle;
    f64 th_iw;
    f64 th_ow;
    f64 th_chnl;
    f64 prop_chnl;
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
        u->s->ofr = bound_both(params[i], 1.0, 2.0);
    if ((i = u->mapping[1]) >= 0)
        u->s->dm_cc = bound_lower(params[i], 0.05);
    if ((i = u->mapping[2]) >= 0)
        u->s->helix_angle = bound_both(params[i], 0.0, 0.8*PI_2);
    if ((i = u->mapping[3]) >= 0)
        u->s->th_iw = bound_both(params[i], 0.8e-3, 2.0e-3);
    if ((i = u->mapping[4]) >= 0)
        u->s->th_ow = bound_lower(params[i], 0.3e-3);
    if ((i = u->mapping[5]) >= 0)
        u->s->th_chnl = bound_lower(params[i], 0.3e-3);
    if ((i = u->mapping[6]) >= 0)
        u->s->prop_chnl = bound_both(params[i], 0.1, 1.0);
}
static void sim_params_to(const simUser* u, f64* rstr params) {
    i32 i;
    /* <OPTIM ORDERING> */
    if ((i = u->mapping[0]) >= 0)
        params[i] = unbound_both(u->s->ofr, 1.0, 2.0);
    if ((i = u->mapping[1]) >= 0)
        params[i] = unbound_lower(u->s->dm_cc, 0.05);
    if ((i = u->mapping[2]) >= 0)
        params[i] = unbound_both(u->s->helix_angle, 0.0, 0.8*PI_2);
    if ((i = u->mapping[3]) >= 0)
        params[i] = unbound_both(u->s->th_iw, 0.8e-3, 2.0e-3);
    if ((i = u->mapping[4]) >= 0)
        params[i] = unbound_lower(u->s->th_ow, 0.3e-3);
    if ((i = u->mapping[5]) >= 0)
        params[i] = unbound_lower(u->s->th_chnl, 0.3e-3);
    if ((i = u->mapping[6]) >= 0)
        params[i] = unbound_both(u->s->prop_chnl, 0.1, 1.0);
}

static f64 sim_cost(const f64* rstr params, void* rstr user) {
    simUser* u = user;

    // Extract the given parameters.
    sim_params_from(u, params);

    // Simulate and evaluate.
    simState* s = u->s;
    sim_ulate(s, NO_FULL_OUTPUT);
    f64 cost = 0.0;
    cost += 1e2*sqed(s->Thrust - s->target_Thrust); // thrust target.
    cost -= sqed(s->Isp); // higher Isp = goated.
    // safety for everyone.
    cost += (s->min_SF < 1.0)
          ? 1e8
          : 100.0 / sqed(sqed(s->min_SF));
    cost += 1e8*(s->possible_system == 0);
    cost += sqed(s->th_iw);
    cost -= sqed(s->helix_angle);
    cost += sqed(1e3*s->th_chnl);
    cost += sqed(s->P_fu0/1e4);
    f64 min_feature = +INF;
    min_feature = min(min_feature, s->th_chnl);
    min_feature = min(min_feature, s->wi_web);
    min_feature = min(min_feature, s->wi_chnl);
    min_feature = min(min_feature, s->psi_chnl);
    cost += (min_feature < 0.5e-3)
          ? 32.0 - 48000.0*min_feature
          : 1.0e-3 / cbed(min_feature);
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
        push(s->optimise_helix_angle);
        push(s->optimise_th_iw);
        push(s->optimise_th_ow);
        push(s->optimise_th_chnl);
        push(s->optimise_prop_chnl);
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
    enum { OPTIM_RUNS = 3 }; // several tries for max extraction?
    for (i32 i=0; i<OPTIM_RUNS; ++i) {
        i32 res = opt_run(sim_cost, u, u->N, tmp, 1e-6, 1e-6, params,
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
