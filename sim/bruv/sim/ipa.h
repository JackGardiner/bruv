#pragma once
#include "br.h"

// thermo.Chemical isopropanol approximations.

#define IPA_MIN_T (250.0)
f64 ipa_max_T(f64 P);
#define IPA_MIN_P (2.0e6)
#define IPA_MAX_P (7.0e6)

f64 ipa_rho(f64 T, f64 P);
f64 ipa_cp(f64 T, f64 P);
f64 ipa_mu(f64 T, f64 P);
f64 ipa_k(f64 T, f64 P);
