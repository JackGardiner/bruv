#include "stress.h"

#include "assertion.h"
#include "cea.h"
#include "maths.h"
#include "material.h"
#include "relations.h"


void stress_sim(const simState* s, const Contour* cnt,
        const thermalStation* thermal_stns, i32 thermal_N, stressStation* stns,
        i32 N) {

    ceaFit* fit_gamma = &(ceaFit){0};
    cea_fit_gamma(fit_gamma, s->P0_cc, s->ofr, s->M_exit);

    for (i32 i=0; i<N; ++i) {
        f64 z = cnt->z_exit * (i/(f64)(N - 1));
        f64 r = cnt_r(cnt, z);

        f64 drdz;
        f64 d2rdz; {
            // A B C D E
            //     ^ we r here smile.
            if (i == 0) {
                // Forward difference at the start.
                f64 zD = cnt->z_exit * ((i + 1)/(f64)(N - 1));
                f64 zE = cnt->z_exit * ((i + 2)/(f64)(N - 1));
                f64 rD = cnt_r(cnt, zD);
                f64 rE = cnt_r(cnt, zE);
                drdz = (rD - r) / (zD - z);
                d2rdz = (rE - 2.0*rD + r) / (zE - zD) / (zD - z);
            } else if (i == N - 1) {
                // Backward difference at the end.
                f64 zB = cnt->z_exit * ((i - 1)/(f64)(N - 1));
                f64 zA = cnt->z_exit * ((i - 2)/(f64)(N - 1));
                f64 rB = cnt_r(cnt, zB);
                f64 rA = cnt_r(cnt, zA);
                drdz = (r - rB) / (z - zB);
                d2rdz = (r - 2.0*rB + rA) / (z - zB) / (zB - zA);
            } else {
                // Central difference for the rest.
                f64 zB = cnt->z_exit * ((i - 1)/(f64)(N - 1));
                f64 zD = cnt->z_exit * ((i + 1)/(f64)(N - 1));
                f64 rB = cnt_r(cnt, zB);
                f64 rD = cnt_r(cnt, zD);
                drdz = (rD - rB) / (zD - zB);
                d2rdz = (rD - 2.0*r + rB) / (zD - z) / (z - zB);
            }
        }
        // goated.
        f64 Rm = r;
        f64 Rh = (nearzero(d2rdz)) ? INF
               : cbed(sqrt(1.0 + sqed(drdz))) / abs(d2rdz);
        (void)Rm;
        (void)Rh;

        SpecificHeatRatio* shr_g = &(SpecificHeatRatio){0};
        f64 M_g;
        isentropic_shr_M(shr_g, &M_g, z < cnt->z_tht, sqed(r/cnt->R_tht),
                fit_gamma, s->gamma_tht /* good guess */);
        f64 P_g = s->P0_cc * isentropic_P_on_P0(M_g, shr_g);

        f64 P_c;
        f64 T_wg;
        f64 T_wc; {
            f64 t = z / cnt->z_exit;
            t *= thermal_N - 1;
            i32 k = min(max((i32)t, 0), thermal_N - 2);
            t -= k;
            P_c = lerp(thermal_stns[k].P_c, thermal_stns[k + 1].P_c, t);
            T_wg = lerp(thermal_stns[k].T_wg, thermal_stns[k + 1].T_wg, t);
            T_wc = lerp(thermal_stns[k].T_wc, thermal_stns[k + 1].T_wc, t);
        }

        f64 th_iw = cnt_th_iw(cnt, z);
        f64 th_ow = s->th_ow;
        f64 th_chnl = cnt_th_chnl(cnt, z);
        f64 wi_chnl = cnt_wi_chnl(cnt, z);
        f64 wi_web = cnt_wi_web(cnt, z);


        // Firstly do start-up with only coolant pressure (assume pressure drop
        // calcs are mostly the same).
        {
            f64 T = 20.0 + 273.15;
            f64 Ys = CuCr1Zr_Ys(T);

            stns[i].startup.sigma = 0.5*P_c*sqed(wi_chnl/min(th_iw, th_ow));
            stns[i].startup.SF = Ys / stns[i].startup.sigma;
        }

        {
            f64 th_eff = th_iw + th_ow + wi_web*th_chnl/(wi_web + wi_chnl);
            f64 Ys = CuCr1Zr_Ys(T_wg);
            f64 E = CuCr1Zr_E(T_wg);
            f64 pois = CuCr1Zr_pois(T_wg);
            f64 alpha = CuCr1Zr_alpha(T_wg);

            f64 sigmah_pressure = P_g*r/th_eff;
            f64 sigmah_thermal = E*alpha*(T_wg - T_wc)*0.5/(1.0 - pois);
            f64 sigmah_bending = 0.5*(P_c - P_g)*sqed(wi_chnl/th_iw);
            f64 sigmah = sigmah_bending + sigmah_thermal + sigmah_pressure;
            f64 sigmam = E*alpha*(T_wg - T_wc);

            stns[i].firing.sigmah_pressure = sigmah_pressure;
            stns[i].firing.sigmah_thermal = sigmah_thermal;
            stns[i].firing.sigmah_bending = sigmah_bending;
            stns[i].firing.sigmah = sigmah;
            stns[i].firing.sigmam = sigmam;
            stns[i].firing.sigma_vm = sqrt(sqed(sigmah) + sqed(sigmam)
                                         - sigmah*sigmam);
            stns[i].firing.Ys = Ys;
            stns[i].firing.SF = Ys / stns[i].firing.sigma_vm;
        }
    }
}
