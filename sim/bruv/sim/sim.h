#pragma once
#include "br.h"

#include "../bridge/bridge.h"


// Canonical interpretation of the state array.
#define SIM_INTERPRETATION                                      \
    X(Lstar, f64, C_INPUT)                                      \
    X(R_cc, f64, C_INPUT)                                       \
    X(L_cc, f64, C_OUTPUT)                                      \
    X(R_tht, f64, C_OUTPUT)                                     \
    X(R_exit, f64, C_OUTPUT)                                    \
    X(z_tht, f64, C_OUTPUT)                                     \
    X(z_exit, f64, C_OUTPUT)                                    \
    X(A_tht, f64, C_OUTPUT)                                     \
    X(AEAT, f64, C_OUTPUT)                                      \
    X(NLF, f64, C_INPUT)                                        \
    X(phi_conv, f64, C_INPUT)                                   \
    X(phi_div, f64, C_OUTPUT)                                   \
    X(phi_exit, f64, C_OUTPUT)                                  \
                                                                \
    X(prop_fc, f64, C_INPUT)                                    \
    X(helix_angle, f64, C_INPUT | C_OUTPUT)                     \
    X(th_pdms, f64, C_INPUT)                                    \
    X(k_pdms, f64, C_INPUT)                                     \
    X(th_iw, f64, C_INPUT | C_OUTPUT)                           \
    X(th_ow, f64, C_INPUT | C_OUTPUT)                           \
    X(no_chnl, i64, C_INPUT)                                    \
    X(th_chnl, f64, C_INPUT | C_OUTPUT)                         \
    X(prop_chnl, f64, C_INPUT | C_OUTPUT)                       \
    X(wi_web, f64, C_OUTPUT)                                    \
    X(wi_chnl, f64, C_OUTPUT)                                   \
    X(psi_chnl, f64, C_OUTPUT)                                  \
    X(eps_chnl, f64, C_INPUT)                                   \
    X(Pr_fu, f64, C_INPUT)                                      \
    X(T_fu0, f64, C_INPUT)                                      \
    X(P_fu0, f64, C_OUTPUT)                                     \
    X(T_fu1, f64, C_OUTPUT)                                     \
    X(P_fu1, f64, C_OUTPUT)                                     \
                                                                \
    X(ofr, f64, C_INPUT | C_OUTPUT)                             \
    X(dm_cc, f64, C_INPUT | C_OUTPUT)                           \
    X(dm_ox, f64, C_OUTPUT)                                     \
    X(dm_fu, f64, C_OUTPUT)                                     \
    X(P_exit, f64, C_INPUT)                                     \
    X(M_exit, f64, C_OUTPUT)                                    \
    X(gamma_exit, f64, C_OUTPUT)                                \
    X(P0_cc, f64, C_INPUT)                                      \
    X(T0_cc, f64, C_OUTPUT)                                     \
    X(rho0_cc, f64, C_OUTPUT)                                   \
    X(gamma_tht, f64, C_OUTPUT)                                 \
    X(Mw_tht, f64, C_OUTPUT)                                    \
    X(Isp, f64, C_OUTPUT)                                       \
    X(Thrust, f64, C_OUTPUT)                                    \
    X(efficiency, f64, C_OUTPUT)                                \
                                                                \
    X(min_SF, f64, C_OUTPUT)                                    \
    X(possible_system, i64, C_OUTPUT)                           \
                                                                \
    X(out_count, i64, C_INPUT)                                  \
    X(out_z, f64*, C_INPUT | C_OUTPUT_DATA)                     \
    X(out_r, f64*, C_INPUT | C_OUTPUT_DATA)                     \
    X(out_M_g, f64*, C_INPUT | C_OUTPUT_DATA)                   \
    X(out_T_g, f64*, C_INPUT | C_OUTPUT_DATA)                   \
    X(out_P_g, f64*, C_INPUT | C_OUTPUT_DATA)                   \
    X(out_rho_g, f64*, C_INPUT | C_OUTPUT_DATA)                 \
    X(out_gamma_g, f64*, C_INPUT | C_OUTPUT_DATA)               \
    X(out_cp_g, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_mu_g, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_Pr_g, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_T_c, f64*, C_INPUT | C_OUTPUT_DATA)                   \
    X(out_P_c, f64*, C_INPUT | C_OUTPUT_DATA)                   \
    X(out_T_gw, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_T_pdms, f64*, C_INPUT | C_OUTPUT_DATA)                \
    X(out_T_wg, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_T_wc, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_q, f64*, C_INPUT | C_OUTPUT_DATA)                     \
    X(out_h_g, f64*, C_INPUT | C_OUTPUT_DATA)                   \
    X(out_h_c, f64*, C_INPUT | C_OUTPUT_DATA)                   \
    X(out_vel_c, f64*, C_INPUT | C_OUTPUT_DATA)                 \
    X(out_rho_c, f64*, C_INPUT | C_OUTPUT_DATA)                 \
    X(out_ff_c, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_Re_c, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_Pr_c, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(out_startup_sigma, f64*, C_INPUT | C_OUTPUT_DATA)         \
    X(out_startup_Ys, f64*, C_INPUT | C_OUTPUT_DATA)            \
    X(out_startup_SF, f64*, C_INPUT | C_OUTPUT_DATA)            \
    X(out_sigmah_pressure, f64*, C_INPUT | C_OUTPUT_DATA)       \
    X(out_sigmah_thermal, f64*, C_INPUT | C_OUTPUT_DATA)        \
    X(out_sigmah_bending, f64*, C_INPUT | C_OUTPUT_DATA)        \
    X(out_sigmah, f64*, C_INPUT | C_OUTPUT_DATA)                \
    X(out_sigmam, f64*, C_INPUT | C_OUTPUT_DATA)                \
    X(out_sigma_vm, f64*, C_INPUT | C_OUTPUT_DATA)              \
    X(out_Ys, f64*, C_INPUT | C_OUTPUT_DATA)                    \
    X(out_SF, f64*, C_INPUT | C_OUTPUT_DATA)                    \
    X(out_xtra, f64*, C_INPUT | C_OUTPUT_DATA)                  \
                                                                \
    X(export_count, i64, C_INPUT)                               \
    X(export_z, f64*, C_INPUT | C_OUTPUT_DATA)                  \
    X(export_helix_angle, f64*, C_INPUT | C_OUTPUT_DATA)        \
    X(export_th_chnl, f64*, C_INPUT | C_OUTPUT_DATA)            \
    X(export_psi_chnl, f64*, C_INPUT | C_OUTPUT_DATA)           \
    X(export_th_iw, f64*, C_INPUT | C_OUTPUT_DATA)              \
                                                                \
    X(target_Thrust, f64, C_INPUT)                              \
    X(optimise_ofr, i64, C_INPUT)                               \
    X(optimise_dm_cc, i64, C_INPUT)                             \
    X(optimise_helix_angle, i64, C_INPUT)                       \
    X(optimise_th_iw, i64, C_INPUT)                             \
    X(optimise_th_ow, i64, C_INPUT)                             \
    X(optimise_th_chnl, i64, C_INPUT)                           \
    X(optimise_prop_chnl, i64, C_INPUT)                         \


// Da state array.
typedef struct simState {
    #define X(name, type, flags) type name;
    SIM_INTERPRETATION
    #undef X
} simState;

// Returns the hash of the canonical interpretation of the state array.
c_IH sim_interpretation_hash(void);

// Simulation entrypoint. Errors are handled via asserts, caller is required to
// setup assertion failed handling.
void sim_execute(simState* rstr s);
