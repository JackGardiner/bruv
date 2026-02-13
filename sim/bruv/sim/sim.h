#pragma once
#include "br.h"

#include "../bridge/bridge.h"


// Canonical intpretation of the state array.
#define SIM_INTERPRETATION  \
    X(hi,   f64,  C_INPUT)  \
    X(bye,  f64,  C_OUTPUT) \
    X(size, i64,  C_INPUT)  \
    X(data, u16*, C_INPUT | C_INPUT_DATA | C_OUTPUT_DATA)


// Da state array.
typedef struct simState {
    #define X(name, type, flags) type name;
    SIM_INTERPRETATION
    #undef X
} simState;

// Returns the hash of the canonical interpretation of the state array.
c_IH sim_interpretation_hash(void);

// Errors handled via failed asserts. Called required to setup assertion failed
// handling.
void sim_execute(simState* rstr s);
