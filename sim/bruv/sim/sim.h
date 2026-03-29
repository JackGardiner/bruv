#pragma once
#include "br.h"

#include "../bridge/bridge.h"


// Canonical interpretation of the state array.
#define SIM_INTERPRETATION                      \
    X(Lstar, f64, C_INPUT)                      \
    X(R_cc, f64, C_INPUT)                       \
    X(L_cc, f64, C_OUTPUT)                      \
    X(R_tht, f64, C_OUTPUT)                     \
    X(R_exit, f64, C_OUTPUT)                    \
    X(z_tht, f64, C_OUTPUT)                     \
    X(z_exit, f64, C_OUTPUT)                    \
    X(A_tht, f64, C_OUTPUT)                     \
    X(AEAT, f64, C_OUTPUT)                      \
    X(NLF, f64, C_INPUT)                        \
    X(phi_conv, f64, C_INPUT)                   \
    X(phi_div, f64, C_OUTPUT)                   \
    X(phi_exit, f64, C_OUTPUT)                  \
                                                \
    X(ofr, f64, C_INPUT)                        \
    X(dm_cc, f64, C_INPUT)                      \
    X(dm_ox, f64, C_OUTPUT)                     \
    X(dm_fu, f64, C_OUTPUT)                     \
    X(P_exit, f64, C_INPUT)                     \
    X(P0_cc, f64, C_INPUT)                      \
    X(T0_cc, f64, C_OUTPUT)                     \
    X(gamma_tht, f64, C_OUTPUT)                 \
    X(Mw_tht, f64, C_OUTPUT)                    \
    X(Thrust, f64, C_OUTPUT)                    \
    X(Isp, f64, C_OUTPUT)                       \
                                                \
    X(out_count, i64, C_INPUT)                  \
    X(out_z, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_r, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_M, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_T, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_P, f64*, C_INPUT | C_OUTPUT_DATA)     \
                                                \
    X(optimise, i64, C_INPUT)                   \
    X(target_Thrust, f64, C_INPUT)              \
    X(forced_ofr, f64, C_INPUT)                 \
    X(forced_dm_cc, f64, C_INPUT)               \


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
