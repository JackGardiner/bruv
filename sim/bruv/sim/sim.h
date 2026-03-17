#pragma once
#include "br.h"

#include "../bridge/bridge.h"


// Canonical interpretation of the state array.
#define SIM_INTERPRETATION                      \
    X(L_cc, f64, C_INPUT)                       \
    X(R_cc, f64, C_INPUT)                       \
    X(R_tht, f64, C_INPUT)                      \
    X(R_exit, f64, C_OUTPUT)                    \
    X(AEAT, f64, C_INPUT)                       \
    X(NLF, f64, C_INPUT)                        \
    X(phi_conv, f64, C_INPUT)                   \
    X(phi_div, f64, C_OUTPUT)                   \
    X(phi_exit, f64, C_OUTPUT)                  \
                                                \
    X(out_count, i64, C_INPUT)                  \
    X(out_z, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_r, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_M, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_T, f64*, C_INPUT | C_OUTPUT_DATA)     \
    X(out_P, f64*, C_INPUT | C_OUTPUT_DATA)     \
                                                \
    X(cnt_r_conv, f64, C_OUTPUT)                \
    X(cnt_z0, f64, C_OUTPUT)                    \
    X(cnt_r0, f64, C_OUTPUT)                    \
    X(cnt_z1, f64, C_OUTPUT)                    \
    X(cnt_r1, f64, C_OUTPUT)                    \
    X(cnt_z2, f64, C_OUTPUT)                    \
    X(cnt_r2, f64, C_OUTPUT)                    \
    X(cnt_z3, f64, C_OUTPUT)                    \
    X(cnt_r3, f64, C_OUTPUT)                    \
    X(cnt_z4, f64, C_OUTPUT)                    \
    X(cnt_r4, f64, C_OUTPUT)                    \
    X(cnt_z5, f64, C_OUTPUT)                    \
    X(cnt_r5, f64, C_OUTPUT)                    \
    X(cnt_z6, f64, C_OUTPUT)                    \
    X(cnt_r6, f64, C_OUTPUT)                    \
    X(cnt_para_az, f64, C_OUTPUT)               \
    X(cnt_para_bz, f64, C_OUTPUT)               \
    X(cnt_para_cz, f64, C_OUTPUT)               \
    X(cnt_para_ar, f64, C_OUTPUT)               \
    X(cnt_para_br, f64, C_OUTPUT)               \
    X(cnt_para_cr, f64, C_OUTPUT)               \


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
