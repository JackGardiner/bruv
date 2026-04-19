#include "sim.h"

#include "assertion.h"
#include "maths.h"

#include "cea.h"
#include "contour.h"
#include "ethanol.h"
#include "ipa.h"
#include "optim.h"
#include "relations.h"



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

    assert(0.0 <= s->helix_angle && s->helix_angle < PI_2,
            "invalid input: helix_angle=%g", s->helix_angle);
    assert(s->th_iw > 0.0, "invalid input: th_iw=%g", s->th_iw);
    assert(s->th_chnl > 0.0, "invalid input: th_chnl=%g", s->th_chnl);
    assert(s->wi_chnl > 0.0, "invalid input: wi_chnl=%g", s->wi_chnl);
    assert(s->eps_chnl >= 0.0, "invalid input: eps_chnl=%g", s->eps_chnl);
    assert(s->T_fu0 > 0.0, "invalid input: T_fu0=%g", s->T_fu0);

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

    f64 M_exit = isentropic_M_from_P_on_P0(s->P_exit / s->P0_cc, shr_tht);
    f64 P_exit = s->P0_cc * isentropic_P_on_P0(M_exit, shr_tht);
    assert(nearto(P_exit, s->P_exit),
            "failed to find perfectly expanded nozzle?");

    ceaFit* fit_gamma = &(ceaFit){0};
    ceaFit* fit_cp = &(ceaFit){0};
    ceaFit* fit_mu = &(ceaFit){0};
    ceaFit* fit_Pr = &(ceaFit){0};
    cea_fit_gamma(fit_gamma, s->P0_cc, s->ofr, M_exit);
    cea_fit_cp(fit_cp, s->P0_cc, s->ofr, M_exit);
    cea_fit_mu(fit_mu, s->P0_cc, s->ofr, M_exit);
    cea_fit_Pr(fit_Pr, s->P0_cc, s->ofr, M_exit);

    s->A_tht = s->dm_cc / s->P0_cc
             * sqrt(s->T0_cc * GAS_CONSTANT / s->Mw_tht / shr_tht->y)
             * pow(0.5*(shr_tht->y + 1.0), shr_tht->n);
    // TODO: ^ move to relations.

    s->AEAT = isentropic_A_on_Astar(M_exit, shr_tht);
    // TODO: ^ fixed point iterate

    s->dm_fu = s->dm_cc / (s->ofr + 1.0);
    s->dm_ox = s->dm_cc - s->dm_fu;

    s->Isp = cea_Isp(s->P0_cc, s->ofr);
    s->Thrust = s->Isp * s->dm_cc * STANDARD_GRAVITY;


    /* Geometry */

    f64 cos_helix = cos(s->helix_angle);

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



    /* Cooling channels. */

    i32 possible_system = 1;

    typedef struct Station {
        f64 T_wg;
        f64 T_wc;
        f64 T_c;
        f64 P_c;
    } Station;

    i32 N = 5000;
    Station* stns = malloc(N * sizeof(Station));

    stns[N - 1] = (Station){
        .T_wg = NAN,
        .T_wc = NAN,
        .T_c = s->T_fu0,
        .P_c = 45e5,
    };
    // March from nozzle exit to injector face.
    for (i32 i=N - 1; i>0; --i) {
        f64 zA = cnt->z_exit * (i/(f64)(N - 1));
        f64 zB = cnt->z_exit * ((i - 1)/(f64)(N - 1));
        f64 rA = cnt_r(cnt, zA);
        f64 rB = cnt_r(cnt, zB);

        // Combustion gas properties:
        f64 dm_g = s->dm_cc;
        f64 T0_g = s->T0_cc;
        f64 A_g = PI*sqed(rA);
        SpecificHeatRatio* shr_g = &(SpecificHeatRatio){0};
        f64 M_g;
        isentropic_shr_M(shr_g, &M_g, zA < cnt->z_tht, A_g/s->A_tht, fit_gamma,
                s->gamma_tht /* good guess */);
        f64 y1M22_g = get_y1M22(M_g, shr_g);
        f64 T_g = s->T0_cc * isentropicx_T_on_T0(y1M22_g, shr_g);
        f64 cp_g = cea_sample(fit_cp, M_g);
        f64 mu_g = cea_sample(fit_mu, M_g);
        f64 Pr_g = cea_sample(fit_Pr, M_g);
        assert(cp_g > 0.0, "nonphysical property, cp_g: %g", cp_g);
        assert(mu_g > 0.0, "nonphysical property, mu_g: %g", mu_g);
        assert(Pr_g > 0.0, "nonphysical property, Pr_g: %g", Pr_g);
        f64 cbrt_Pr_g = cbrt(Pr_g);

        // Coolant properties:
        f64 T_c = stns[i].T_c;
        f64 P_c = stns[i].P_c;
        assert(T_c > 0.0, "nonphysical property, T_c: %g", T_c);
        assert(P_c > 0.0, "nonphysical property, P_c: %g", P_c);
        f64 ipa_P_c = min(max(P_c, 1.001*IPA_MIN_P), 0.999*IPA_MAX_P);
        f64 ipa_T_c = min(max(T_c, 1.001*IPA_MIN_T), 0.999*ipa_max_T(ipa_P_c));
        possible_system &= (ipa_P_c == P_c) && (ipa_T_c == T_c);
        f64 A_c = s->wi_chnl*s->th_chnl // ~approx as rectangle.
                * s->no_chnl;
        f64 HD_c = 2.0*s->wi_chnl*s->th_chnl/(s->wi_chnl + s->th_chnl);
        f64 rho_c = ipa_rho(ipa_T_c, ipa_P_c);
        f64 cp_c = ipa_cp(ipa_T_c, ipa_P_c);
        f64 mu_c = ipa_mu(ipa_T_c, ipa_P_c);
        f64 k_c = ipa_k(ipa_T_c, ipa_P_c);
        assert(rho_c > 0.0, "nonphysical property, rho_c: %g", rho_c);
        assert(cp_c > 0.0, "nonphysical property, cp_c: %g", cp_c);
        assert(mu_c > 0.0, "nonphysical property, mu_c: %g", mu_c);
        assert(k_c > 0.0, "nonphysical property, k_c: %g", k_c);
        f64 dm_c = s->dm_fu;
        f64 G_c = dm_c/A_c;
        f64 vel_c = G_c/rho_c;
        f64 Re_c = G_c*HD_c/mu_c;
        f64 Pr_c = cp_c*mu_c/k_c;
        f64 ff_c = friction_factor_colebrook(Re_c, HD_c, s->eps_chnl);
        f64 Nu_c = nusselt_dittus_boelter(Re_c, Pr_c, 1);
        f64 h_c = Nu_c*k_c/HD_c;
        assert(ff_c > 0.0, "nonphysical property, ff_c: %g", ff_c);
        assert(Nu_c > 0.0, "nonphysical property, Nu_c: %g", Nu_c);
        assert(h_c > 0.0, "nonphysical property, h_c: %g", h_c);


        // Wall heat/temperature numerical search:
        f64 q;
        f64 T_wg = 0.2*T_c + 0.8*T_g; // initial guess.
        f64 T_wc = 0.8*T_c + 0.2*T_g; // initial guess.
        // note a far initial guess helps prevent a huge (mis)step further.
        for (i32 iter=0; /* true */; ++iter) {
            enum { MAX_ITERS = 1000 };

            assert(T_wg > 0.0, "nonphysical property, T_wg: %g", T_wg);
            assert(T_wc > 0.0, "nonphysical property, T_wc: %g", T_wc);
            T_wg = min(T_wg, T_g);
            T_wc = max(T_wc, T_c);

            // Adiabatic wall temperature.
            f64 adiabatic_T_wg = T_g * (1.0 + cbrt_Pr_g*y1M22_g);

            // Bartz equation for convection coefficient.
            f64 h_g; {
                // Use properties evauluated at the eckert temperature.
                f64 T_gw = 0.5*T_wg + 0.28*T0_g + 0.22*adiabatic_T_wg;
                f64 M_gw = mach_for_temperature(T_gw / T0_g, fit_gamma);
                // upstream curvature?
                f64 Rcurvature_tht = 1.5*cnt->R_tht;
                f64 bartz_gamma_g = cea_sample(fit_gamma, M_gw);
                f64 bartz_cp_g = cea_sample(fit_cp, M_gw);
                f64 bartz_mu_g = cea_sample(fit_mu, M_gw);
                f64 bartz_Pr_g = cea_sample(fit_Pr, M_gw);
                f64 bartz_y1M22_g = 0.5*(bartz_gamma_g - 1.0)*sqed(M_g);
                f64 w = 0.6; // common estimate.
                h_g = 0.026
                    * pow(bartz_mu_g, 0.2)
                    * bartz_cp_g
                    * pow(bartz_Pr_g, -0.6)
                    * pow(dm_g/s->A_tht, 0.8)
                    * pow(0.5/Rcurvature_tht/cnt->R_tht, 0.1)
                    * pow(s->A_tht/A_g, 0.9)
                    * pow(0.5*T_wg/T0_g*(1.0 + bartz_y1M22_g) + 0.5, 0.2*w - 0.8)
                    * pow(1.0 + bartz_y1M22_g, -0.2*w);
            }

            // Convection between boundary layer and wall.
            f64 q_convective = h_g * (adiabatic_T_wg - T_wg);
            assert(q_convective >= 0.0,
                    "coolant should not heat cc gases, q_convective=%g",
                    q_convective);

            // Simple radiation.
            f64 emissivity_g = 0.15; // common for combustion products.
            f64 q_radiative = emissivity_g * STEFAN_BOLTZMAN_CONSTANT
                            * (sqed(sqed(T_g)) - sqed(sqed(T_wg)));
            assert(q_radiative >= 0.0,
                    "coolant should not heat cc gases, q_radiative=%g",
                    q_radiative);

            q = q_convective + q_radiative;
            assert(q >= 0.0, "coolant should not heat cc gases, q=%g", q);

            f64 k_iw = 320.0; // single datapoint smile.

            // Model web as a fin.
            f64 wi_web = TWOPI*rA/s->no_chnl - s->wi_chnl;
            f64 eta_web; {
                f64 term = s->th_chnl * sqrt(h_c / k_iw / wi_web);
                f64 exp_twoterm = exp2(LOG2E*2.0*term);
                f64 tanh_term = (exp_twoterm - 1.0)
                              / (exp_twoterm + 1.0);
                eta_web = tanh_term / term;
            }

            // Cylindrical conductor resistance.
            f64 Rth_iw = rA * LN2*log2(1.0 + s->th_iw/rA) / k_iw;
            // Fin + convective resistance.
            f64 Rth_c = TWOPI*rA / h_c
                      / s->no_chnl / (s->wi_chnl + 2.0*eta_web*s->th_chnl)
                      * cos_helix;

            // Heat balance to find wall temperatures at each side.
            f64 new_T_wg = T_c + q*(Rth_iw + Rth_c);
            f64 new_T_wc = T_c + q*Rth_c;
            f64 diff_T_wg = iterstep(&T_wg, new_T_wg);
            f64 diff_T_wc = iterstep(&T_wc, new_T_wc);
            if (max(diff_T_wg, diff_T_wc) < 1e-4)
                break;

            if (iter >= MAX_ITERS) {
                possible_system = 0;
                T_wg = NAN;
                T_wc = NAN;
                q = 8e3; // guess to try wrangle ok data.
                break;
            }
        }
        possible_system &= (T_wg < T_g) || nearto(T_wg, T_g);
        possible_system &= (T_wc > T_c) || nearto(T_wc, T_c);

        stns[i].T_wg = T_wg;
        stns[i].T_wc = T_wc;

        f64 Dell = hypot(rB - rA, zB - zA);

        f64 contact_area = PI*(rA + rB)*Dell;
        stns[i - 1].T_c = T_c + q*contact_area/dm_c/cp_c;
        assert(stns[i - 1].T_c > 0.0, "nonphysical property, T_c: %g",
                stns[i - 1].T_c);

        f64 DP_c = 0.5*rho_c*sqed(vel_c)*ff_c*(zA - zB)/HD_c
                 / cos_helix;
        stns[i - 1].P_c = P_c - DP_c;
        assert(stns[i - 1].P_c > 0.0, "nonphysical property, P_c: %g",
                stns[i - 1].P_c);
    }
    stns[0].T_wg = NAN;
    stns[0].T_wc = NAN;

    printf("possible system: %s\n", (possible_system) ? "true" : "false");

    // TODO: initial fu/ox pressures.



    /* Outputs */

    if (!full_output || s->out_count <= 0)
        return;

    assert(s->out_count > 20, "output contour array is too small (%lld)",
            s->out_count);
    assert(s->out_z, "output array 'out_z' is null");
    assert(s->out_r, "output array 'out_r' is null");
    assert(s->out_M_g, "output array 'out_M_g' is null");
    assert(s->out_T_g, "output array 'out_T_g' is null");
    assert(s->out_P_g, "output array 'out_P_g' is null");
    assert(s->out_T_c, "output array 'out_T_c' is null");
    assert(s->out_P_c, "output array 'out_P_c' is null");

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

        f64 t_c = (z - 0.0) / (cnt->z_exit - 0.0);
        t_c *= N - 1;
        i32 i_c = min(max((i32)t_c, 0), N - 2);
        t_c -= i_c;

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
        s->out_T_c[i] = lerp(stns[i_c].T_c, stns[i_c + 1].T_c, t_c);
        s->out_P_c[i] = lerp(stns[i_c].P_c, stns[i_c + 1].P_c, t_c);
        s->out_T_wg[i] = lerp(stns[i_c].T_wg, stns[i_c + 1].T_wg, t_c);
        s->out_T_wc[i] = lerp(stns[i_c].T_wc, stns[i_c + 1].T_wc, t_c);
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
