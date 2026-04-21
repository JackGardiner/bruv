#include "thermal.h"

#include "assertion.h"
#include "cea.h"
#include "ethanol.h"
#include "ipa.h"
#include "maths.h"
#include "material.h"
#include "relations.h"


i32 thermal_sim(const simState* s, const Contour* cnt, thermalStation* stns,
        i32 N) {
    #define throw() return 0;

    i32 possible_system = 1;

    ceaFit* fit_gamma = &(ceaFit){0};
    ceaFit* fit_cp = &(ceaFit){0};
    ceaFit* fit_mu = &(ceaFit){0};
    ceaFit* fit_Pr = &(ceaFit){0};
    cea_fit_gamma(fit_gamma, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_cp(fit_cp, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_mu(fit_mu, s->P0_cc, s->ofr, s->M_exit);
    cea_fit_Pr(fit_Pr, s->P0_cc, s->ofr, s->M_exit);

    SpecificHeatRatio* shr_exit = get_shr(s->gamma_exit);

    f64 ell = 0.0;
    for (i32 i=0; i<1000; ++i) {
        f64 zA = cnt->z_exit * (i/(f64)(1000));
        f64 zB = cnt->z_exit * ((i + 1)/(f64)(1000));
        f64 rA = cnt_r(cnt, zA);
        f64 rB = cnt_r(cnt, zB);
        ell += hypot(rB - rA, zB - zA);
    }

    // Film cooling parameters.
    f64 T_film = 450.0;
    f64 cpl_film  = 3860.527052563261;
    f64 Hvap_film = 164045.7251946664;
    f64 cpv_film  = 2334.731417515824;

    stns[N - 1] = (thermalStation){
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
        f64 th_iw = cnt_th_iw(cnt, zA);
        f64 th_chnl = cnt_th_chnl(cnt, zA);
        f64 wi_web = cnt_wi_web(cnt, zA);
        f64 wi_chnl = cnt_wi_chnl(cnt, zA);
        f64 psi_chnl = cnt_psi_chnl(cnt, zA);
        f64 A_c = psi_chnl*th_chnl // ~approx as rectangle.
                * s->no_chnl;
        f64 HD_c = 2.0*psi_chnl*th_chnl/(psi_chnl + th_chnl);
        f64 rho_c = ipa_rho(ipa_T_c, ipa_P_c);
        f64 cp_c = ipa_cp(ipa_T_c, ipa_P_c);
        f64 mu_c = ipa_mu(ipa_T_c, ipa_P_c);
        f64 k_c = ipa_k(ipa_T_c, ipa_P_c);
        assert(rho_c > 0.0, "nonphysical property, rho_c: %g", rho_c);
        assert(cp_c > 0.0, "nonphysical property, cp_c: %g", cp_c);
        assert(mu_c > 0.0, "nonphysical property, mu_c: %g", mu_c);
        assert(k_c > 0.0, "nonphysical property, k_c: %g", k_c);
        f64 dm_c = s->dm_fu * 1.1 /* TODO: film cooling */;
        f64 G_c = dm_c/A_c;
        f64 vel_c = G_c/rho_c;
        f64 Re_c = G_c*HD_c/mu_c;
        f64 Pr_c = cp_c*mu_c/k_c;
        f64 ff_c = friction_factor_colebrook(Re_c, HD_c, s->eps_chnl);
        f64 Nu_c = nusselt_dittus_boelter(Re_c, Pr_c, 1);
        assert(ff_c > 0.0, "nonphysical property, ff_c: %g", ff_c);
        assert(Nu_c > 0.0, "nonphysical property, Nu_c: %g", Nu_c);


        // Adiabatic wall temperature.
        f64 adiabatic_T_wg = T_g * (1.0 + cbrt_Pr_g*y1M22_g);

        f64 filmcooled_T_wg; {
            f64 T = adiabatic_T_wg;

            f64 eta_c = 0.25;
            f64 f_friction = 0.035;
            f64 Vg_Vd = 1.2;
            f64 A_coeff = 0.37;
            f64 a = 2.0 * Vg_Vd / f_friction;
            f64 b = Vg_Vd - 1.0;
            /* TODO: film cooling */;
            f64 dm_film = 0.1 * s->dm_fu;
            f64 GcGg = dm_film / (dm_film + dm_g);
            f64 protected_T; {
                f64 b_term  = pow(b, cpv_film/cp_g);
                f64 H_avail = GcGg * eta_c * a * (1.0 + b_term);
                protected_T = (cpv_film * T
                                + H_avail * (cpl_film * T_film - Hvap_film))
                            / (cpv_film + H_avail * cpl_film);
            }
            f64 eta; {
                eta = min(max((T - protected_T) / (T - T_film), 0.0), 1.0);
                eta = min(max(eta, 0.0), 1.0);
                f64 B = (dm_g * cp_g) / (dm_film * cpl_film);
                f64 gl_part = 1.0 + A_coeff*B*ell / 2.0/s->R_cc;
                eta = lerp(eta, 1.0, 1.0/gl_part);
                eta = min(max(eta, 0.0), 1.0);
            }
            filmcooled_T_wg = lerp(T, T_film, eta);
        }


        f64 k_pdms = s->k_pdms;
        f64 th_pdms = s->th_pdms;

        f64 k_iw = CuCr1Zr_k();
        f64 k_web = CuCr1Zr_k();

        // Coolant convection coefficient.
        f64 h_c = Nu_c*k_c/HD_c;

        // Model web as a fin.
        f64 eta_web; {
            f64 term = sqrt(2.0 * h_c * wi_web / k_web) * th_chnl / wi_chnl;
            f64 exp_twoterm = exp2(LOG2E*2.0*term);
            f64 tanh_term = (exp_twoterm - 1.0)
                          / (exp_twoterm + 1.0);
            eta_web = tanh_term / term;
        }
        // Correct for fin.
        h_c *= (wi_chnl + 2.0*eta_web*th_chnl) / (wi_chnl + wi_web);

        // Cylindrical conductor resistance.
        f64 Rth_iw = rA * LN2*log2(1.0 + th_iw/rA) / k_iw;
        f64 Rth_pdms = rA * LN2*log2(1.0 + th_pdms/rA) / k_pdms;
        // Fin + convective resistance.
        f64 Rth_c = 1.0 / h_c;

        f64 prev_T_pdms;
        f64 prev_T_wg;
        f64 prev_T_wc;
        if (i == N - 1) {
            // Guess.
            prev_T_pdms = lerp(T_c, filmcooled_T_wg, 0.0);
            prev_T_wg = lerp(T_c, filmcooled_T_wg, 0.0);
            prev_T_wc = lerp(T_c, filmcooled_T_wg, 0.0);
        } else {
            prev_T_pdms = stns[i + 1].T_pdms;
            prev_T_wg = stns[i + 1].T_wg;
            prev_T_wc = stns[i + 1].T_wc;
        }

        // Wall heat/temperature numerical search:
        f64 q = NAN;
        f64 h_g = NAN;
        f64 T_pdms = prev_T_pdms;
        f64 T_wg = prev_T_wg;
        f64 T_wc = prev_T_wc;
        f64 old_T_pdms = T_pdms;
        f64 old_T_wg = T_wg;
        f64 old_T_wc = T_wc;

        for (i32 iter=0; /* true */; ++iter) {
            enum { MAX_ITERS = 300 };
            stns[i].xtra = iter;

            i32 possible_rn = 1;

            T_pdms = max(T_pdms, 250.0);
            T_wg = max(T_wg, 250.0);
            T_wc = max(T_wc, 250.0);

            if (i != N - 1) {
                f64 max_DTDz = 100e3;
                f64 max_DT = max_DTDz * abs(zB - zA);

                possible_rn &= (T_pdms > prev_T_pdms - max_DT);
                possible_rn &= (T_pdms < prev_T_pdms + max_DT);
                possible_rn &= (T_wg > prev_T_wg - max_DT);
                possible_rn &= (T_wg < prev_T_wg + max_DT);
                possible_rn &= (T_wc > prev_T_wc - max_DT);
                possible_rn &= (T_wc < prev_T_wc + max_DT);
                T_pdms = min(max(T_pdms, prev_T_pdms - max_DT),
                             prev_T_pdms + max_DT);
                T_wg = min(max(T_wg, prev_T_wg - max_DT), prev_T_wg + max_DT);
                T_wc = min(max(T_wc, prev_T_wc - max_DT), prev_T_wc + max_DT);
            }

            if (iter >= MAX_ITERS) {
                // possible_system = 0;
                // eh let it slide.
                break;
            }

            f64 diff_T_pdms = abs(T_pdms - old_T_pdms);
            f64 diff_T_wg = abs(T_wg - old_T_wg);
            f64 diff_T_wc = abs(T_wc - old_T_wc);
            f64 max_diff = max(diff_T_pdms, max(diff_T_wg, diff_T_wc));
            if (iter > 0 && possible_rn && max_diff < 5e-2)
                break;

            old_T_pdms = T_pdms;
            old_T_wg = T_wg;
            old_T_wc = T_wc;

            // Bartz equation for convection coefficient.
            {
                // Use properties evauluated at the eckert temperature.
                f64 T_gw = 0.5*T_pdms + 0.28*T0_g + 0.22*adiabatic_T_wg;
                f64 min_T = T0_g * isentropic_T_on_T0(s->M_exit, shr_exit);
                T_gw = min(max(T_gw, min_T), T0_g);
                f64 M_gw = mach_for_temperature(T_gw / T0_g, fit_gamma);
                assert(M_gw >= 0.0, "nonphysical property: M_gw=%g", M_gw);
                // M_gw = min(M_gw, s->M_exit);
                // upstream curvature?
                f64 Rcurvature_tht = 1.5*cnt->R_tht;
                f64 bartz_gamma_g = cea_sample(fit_gamma, M_gw);
                f64 bartz_cp_g = cea_sample(fit_cp, M_gw);
                f64 bartz_mu_g = cea_sample(fit_mu, M_gw);
                f64 bartz_Pr_g = cea_sample(fit_Pr, M_gw);
                f64 bartz_y1M22_g = 0.5*(bartz_gamma_g - 1.0)*sqed(M_g);
                assert(bartz_mu_g > 0.0, "nonphysical property, bartz_mu_g: %g",
                        bartz_mu_g);
                assert(bartz_cp_g > 0.0, "nonphysical property, bartz_cp_g: %g",
                        bartz_cp_g);
                assert(bartz_Pr_g > 0.0, "nonphysical property, bartz_Pr_g: %g",
                        bartz_Pr_g);
                f64 w = 0.6; // common estimate.
                h_g = 0.026
                    * pow(bartz_mu_g, 0.2)
                    * bartz_cp_g
                    * pow(bartz_Pr_g, -0.6)
                    * pow(dm_g/s->A_tht, 0.8)
                    * pow(0.5/Rcurvature_tht/cnt->R_tht, 0.1)
                    * pow(s->A_tht/A_g, 0.9)
                    * pow(0.5*filmcooled_T_wg/T0_g*(1.0 + bartz_y1M22_g) + 0.5,
                          0.2*w - 0.8)
                    * pow(1.0 + bartz_y1M22_g, -0.2*w);
            }

            // Convection between boundary layer and wall.
            f64 q_convective = h_g * (filmcooled_T_wg - T_pdms);

            // Simple radiation.
            f64 emissivity_g = 0.15; // common for combustion products.
            f64 q_radiative = emissivity_g * STEFAN_BOLTZMAN_CONSTANT
                            * (sqed(sqed(T_g)) - sqed(sqed(T_pdms)));

            q = q_convective + q_radiative;

            // Heat balance to find wall temperatures at each side.
            T_pdms = T_c + q*(Rth_pdms + Rth_iw + Rth_c);
            T_wg = T_c + q*(Rth_iw + Rth_c);
            T_wc = T_c + q*Rth_c;
        }

        stns[i].q = q;
        stns[i].h_g = h_g;
        stns[i].h_c = h_c;
        stns[i].vel_c = vel_c;
        stns[i].rho_c = rho_c;
        stns[i].ff_c = ff_c;
        stns[i].Re_c = Re_c;
        stns[i].Pr_c = Pr_c;
        stns[i].T_gw = filmcooled_T_wg;
        stns[i].T_pdms = T_pdms;
        stns[i].T_wg = T_wg;
        stns[i].T_wc = T_wc;

        f64 Dell = hypot(rB - rA, zB - zA);
        ell -= Dell;

        // Do coolant property continuation if theres more channel left.
        if (i > 0) {
            q = ifnan(q, 8e3); // try to wrangle some ok data.

            f64 contact_area = PI*(rA + rB)*Dell;
            stns[i - 1].T_c = T_c + q*contact_area/dm_c/cp_c;
            assert(stns[i - 1].T_c > 0.0, "nonphysical property, T_c: %g",
                    stns[i - 1].T_c);

            f64 DP_c = 0.5*rho_c*sqed(vel_c)*ff_c/HD_c * Dell;
            stns[i - 1].P_c = P_c - DP_c;
            if (stns[i - 1].P_c <= 0.0) {
                possible_system = 0;
                stns[i - 1].P_c = 1.0; // smile.
            }
        }
    }

    return possible_system;
}
