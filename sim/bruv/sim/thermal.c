#include "thermal.h"

#include "cea.h"
#include "ethanol.h"
#include "ipa.h"
#include "maths.h"
#include "relations.h"


i32 thermal_sim(const simState* s, const Contour* cnt, thermalStation* stns,
        i32 N) {
    i32 possible_system = 1;

    ceaFit* fit_gamma = &(ceaFit){0};
    ceaFit* fit_cp = &(ceaFit){0};
    ceaFit* fit_mu = &(ceaFit){0};
    ceaFit* fit_Pr = &(ceaFit){0};
    cea_fit_gamma(fit_gamma, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_cp(fit_cp, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_mu(fit_mu, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_Pr(fit_Pr, s->P0_cc, s->ofr, s->M_exit);

    f64 cos_helix = cos(s->helix_angle);

    stns[N - 1] = (thermalStation){
        .T_wg = NAN,
        .T_wc = NAN,
        .T_c = s->T_fu0,
        .P_c = s->P_fu0,
    };
    // March from nozzle exit to injector face.
    for (i32 i=N - 1; i>-1; --i) {
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
            if (max(diff_T_wg, diff_T_wc) < 1e-2)
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

        // Do coolant property continuation if theres more channel left.
        if (i > 0) {
            f64 contact_area = PI*(rA + rB)*hypot(rB - rA, zB - zA);
            stns[i - 1].T_c = T_c + q*contact_area/dm_c/cp_c;
            assert(stns[i - 1].T_c > 0.0, "nonphysical property, T_c: %g",
                    stns[i - 1].T_c);

            f64 DP_c = 0.5*rho_c*sqed(vel_c)*ff_c*(zA - zB)/HD_c
                    / cos_helix;
            stns[i - 1].P_c = P_c - DP_c;
            assert(stns[i - 1].P_c > 0.0, "nonphysical property, P_c: %g",
                    stns[i - 1].P_c);
        }
    }

    return possible_system;
}
