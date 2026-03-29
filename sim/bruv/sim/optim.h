#pragma once
#include "br.h"



// ========================= //
//         OPTIMISER         //
// ========================= //

typedef f64 opt_cost_f(const f64* rstr params, void* rstr user);


// Find an interval [`philo`, `phihi`] around `phi0` (not necessarily enclosing
// it), using steps proportional to `phistep`. This interval will be nan if
// bracketing failed, otherwise it will contain a local minimum of
// `cost(r + phi*m)` over `phi`.
// - `tmp` must point to `OPT_BRACKET1D_MEMSIZE(count)` bytes.
// - `r` and `m` must point to `count` elements, representing state vector
//      constants.
void opt_bracket1D(opt_cost_f cost, void* rstr user, i64 count, void* rstr tmp,
        const f64* rstr r, const f64* rstr m, f64 phi0, f64 phistep,
        f64* rstr philo, f64* rstr phihi);
#define OPT_BRACKET1D_MEMSIZE(count) (8*(count))


// Bracketed 1D function minimiser (Brent's method). Finds `phi` in
// [`philo`, `phihi`] s.t. `cost(r + phi*m)` is locally minimised.
// - `tmp` must point to `OPT_RUN1D_MEMSIZE(count)` bytes.
// - `r` and `m` must point to `count` elements, representing state vector
//      constants.
// - If `best_cost` is not null, it will be set to the minimised cost.
f64 opt_run1D(opt_cost_f cost, void* rstr user, i64 count, void* rstr tmp,
        const f64* rstr r, const f64* rstr m, f64 philo, f64 phihi,
        f64 rtol, f64 atol,
        f64* rstr best_cost);
#define OPT_RUN1D_MEMSIZE(count) (8*(count))


// Seeded N-dimensional function minimiser (Powell's method). Takes `x` as a seed
// for the initial state and in-place modifies it until a local minimum is found
// (or it fails). On success, guarantees that the most recent call to `cost` was
// with the optimal `x`.
// - `tmp` must point to `OPT_RUN_MEMSIZE(count)` bytes.
// - `x` must point to `count` elements, as a seeding state vector.
// - If `best_cost` is not null, it will be set to the minimised cost.
// - Returns non-zero if a minimum was successfully (approximately) found, zero
//      otherwise (failure).
i32 opt_run(opt_cost_f cost, void* rstr user, i64 count, void* rstr tmp,
        f64 ftol, f64 xtol, f64* rstr x, f64* rstr best_cost);
#define OPT_RUN_MEMSIZE(count) (8*(count)*((count) + 4))



// ========================== //
//       LEAPS & BOUNDS       //
// ========================== //

// Maps `param` from (-inf, +inf) to (`lower`, `upper`).
f64 bound_both(f64 param, f64 lower, f64 upper);
// Maps `param` from (-inf, +inf) to (`lower`, +inf).
f64 bound_lower(f64 param, f64 lower);
// Maps `param` from (-inf, +inf) to (-inf, `upper`).
f64 bound_upper(f64 param, f64 upper);

// Un-maps `value` from (`lower`, `upper`) to (-inf, +inf).
f64 unbound_both(f64 value, f64 lower, f64 upper);
// Un-maps `value` from (`lower`, +inf) to (-inf, +inf).
f64 unbound_lower(f64 value, f64 lower);
// Un-maps `value` from (-inf, `upper`) to (-inf, +inf).
f64 unbound_upper(f64 value, f64 upper);
