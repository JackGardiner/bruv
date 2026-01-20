#pragma once
#include "br.h"

// Da state array.
typedef struct brState {
    double hi;
} brState;

// Errors handled via failed asserts. Called required to setup assertion failed
// handling.
void br_sim(brState* rstr s);
