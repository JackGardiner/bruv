#include "optim.h"

#include "maths.h"


// =========================================================================== //
// = OPTIMISER =============================================================== //
// =========================================================================== //

#define OPT_MAXITERS_ (100000)


static f64 opt_1D_cost_(opt_cost_f cost, void* rstr user, i64 count,
        f64* rstr tmp, const f64* rstr r, const f64* rstr m, f64 phi) {
    for (i64 i=0; i<count; ++i)
        tmp[i] = r[i] + phi*m[i];
    return cost(tmp, user);
}
#define get_1D_cost(phi) \
    ( opt_1D_cost_(cost, user, count, tmp, r, m, (phi)) )


void opt_bracket1D(opt_cost_f cost, void* rstr user, i64 count, void* rstr tmp,
        const f64* rstr r, const f64* rstr m, f64 phi0, f64 phistep,
        f64* rstr philo, f64* rstr phihi) {

    f64 phia = phi0;
    f64 phib = phi0 + phistep;
    f64 fa = get_1D_cost(phia);
    f64 fb = get_1D_cost(phib);

    // ooo might be flat.
    if (nearto(fa, fb)) {
        // Check some other point (not exact midpoint as a justin caseme).
        f64 phic = phia + (phib - phia)/PHI;
        f64 fc = get_1D_cost(phic);
        // Its flat :(
        if (nearto(fc, fa) && nearto(fc, fb)) {
            *philo = phi0;
            *phihi = phi0;
            return;
        }
    }

    // Ensure a->b is downhill.
    if (fb > fa) {
        phistep = -phistep;
        swap(phia, phib);
        swap(fa, fb);
    }

    // Expand until we climb again, then we know a->b->c is concave up.
    for (i32 iter=0; iter<OPT_MAXITERS_; ++iter) /* safety */ {
        phistep *= PHI;
        f64 phic = phib + phistep;
        f64 fc = get_1D_cost(phic);

        if (fc > fb) {
            *philo = min(phia, phic);
            *phihi = max(phia, phic);
            return;
        }
        phia = phib;
        phib = phic;
        fa = fb;
        fb = fc;
    }
    // Not found.
    *philo = NAN;
    *phihi = NAN;
}


f64 opt_run1D(opt_cost_f cost, void* rstr user, i64 count, void* rstr tmp,
        const f64* rstr r, const f64* rstr m, f64 philo, f64 phihi,
        f64 rtol, f64 atol,
        f64* rstr best_cost) {

    f64 phi0 = 0.5*(philo + phihi); // first best (init to guess).
    f64 phi1 = phi0; // second best.
    f64 phi2 = phi0; // third best.
    f64 f0 = get_1D_cost(phi0);
    f64 f1 = f0;
    f64 f2 = f0;

    f64 Dphi = 0.0; // step size.
    f64 Dphi_baseline = 0.0; // kiiinda prev step size.

    for (i32 iter=0; iter<OPT_MAXITERS_; ++iter) /* safety */ {
        f64 tol = rtol*(phihi - philo) + atol/2;

        // If bounds collapsed, good to exit.
        if ((phihi - philo) < 4*tol)
            break;

        f64 phimid = 0.5*(philo + phihi);

        // Do parabolic step if last step was large enough. Note this will never
        // trigger on first loop.
        if (abs(Dphi_baseline) > tol) {
            // Idk some formula.
            f64 term0 = (phi0 - phi1) * (f0 - f2);
            f64 term1 = (phi0 - phi2) * (f0 - f1);
            f64 term2 = 2*(term0 - term1);
            if (!nearzero(term2)) {
                Dphi = (phi0 - phi1)*term0 - (phi0 - phi2)*term1;
                Dphi /= term2;

                // Accept parabolic step only if:
                i32 inside = within(phi0 + Dphi, philo, phihi);
                i32 small_enough = (abs(Dphi) < 0.5*abs(Dphi_baseline));

                // Sneak this update in.
                Dphi_baseline = Dphi;

                if (inside && small_enough) {
                    // If too close to boundary, take a small step away.
                    i32 inside_tol = within(phi0 + Dphi
                            , philo + 2*tol
                            , phihi - 2*tol);
                    if (!inside_tol)
                        Dphi = (phi0 < phimid) ? +tol : -tol;

                    goto SET_Dphis;
                    // ^ dont fallthrough.
                }
                /* fallthrough */
            }
            /* fallthrough */
        }

        // Golden ratio step. Move toward larger subinterval.
        if (phi0 >= phimid)
            Dphi = philo - phi0;
        else
            Dphi = phihi - phi0;
        Dphi_baseline = Dphi; // unscaled.
        Dphi *= 0.38196601125010515/* phi^-2 */;
        goto SET_Dphis;

      SET_Dphis:;
        // Don't make too small a step.
        if (abs(Dphi) < tol)
            Dphi = (Dphi < 0) ? -tol : tol;

        // Evaluate new point.
        f64 phinew = phi0 + Dphi;
        f64 fnew = get_1D_cost(phinew);

        // Update points.

        if (fnew <= f0) {
            // New point is best (or equal).

            // Shrink bracket.
            if (Dphi < 0)
                phihi = phi0;
            else
                philo = phi0;

            // Update next bests.
            phi2 = phi1;
            phi1 = phi0;
            phi0 = phinew;
            f2 = f1;
            f1 = f0;
            f0 = fnew;
        } else {
            // New point is not best.

            // Shrink bracket.
            if (Dphi < 0)
                philo = phinew;
            else
                phihi = phinew;

            // Update next bests.
            if ((fnew <= f1) || (phi1 == phi0)) {
                phi2 = phi1;
                phi1 = phinew;
                f2 = f1;
                f1 = fnew;
            } else if ((fnew <= f2) || (phi2 == phi0) || (phi2 == phi1)) {
                phi2 = phinew;
                f2 = fnew;
            }
        }
    }
    // Return best.
    if (best_cost)
        *best_cost = f0;
    return phi0;
}




i32 opt_run(opt_cost_f cost, void* rstr user, i64 count, void* rstr tmp,
        f64 ftol, f64 xtol, f64* rstr x, f64* rstr best_cost) {
    /* first `count` elements used for temp state vectors. */
    f64* xprev    = (f64*)tmp + 1*count;
    f64* netdir   = (f64*)tmp + 2*count;
    i64* sorting  = (i64*)tmp + 3*count;
    f64* searches = (f64*)tmp + 4*count;

    // `xprev` left uninitialised.
    // `netdir` left uninitialised.

    // `searches` is a list of `count` state vectors, each representing a
    // direction in the x-space. It initially starts with a set of spanning
    // orthogonal vectors.
    memset(searches, 0, 8*count*count);
    for (i64 i=0; i<count; ++i)
        searches[i*count + i] = 1.0;

    // Maintains an ordering of the search directions. This ordering is initially
    // identity, but changes as directions are replaced and pushed to the back.
    for (i64 i=0; i<count; ++i)
        sorting[i] = i;


    // Main loop.
    f64 prev_cost = cost(x, user);
    memcpy(xprev, x, 8*count);
    f64 prev_netdir_mag = 100.0;
    i32 was_reset = 0;

    for (i32 iter=0; iter<OPT_MAXITERS_; ++iter) /* safety */ {

        // Firstly, minimise along each search, adding to the previous searches
        // improvements.
        f64 last_cost = prev_cost;
        f64 best_Dcost = 0.0;
        i64 best_dir = 0;
        for (i64 dir=0; dir<count; ++dir) {
            f64* m = searches + count*sorting[dir];

            f64 philo, phihi;
            opt_bracket1D(cost, user, count, tmp,
                    x, m, 0.0, min(1.0, prev_netdir_mag/4),
                    &philo, &phihi
                );
            if (isnan(philo + phihi))
                return 0;

            f64 new_cost;
            f64 phi = opt_run1D(cost, user, count, tmp,
                    x, m, philo, phihi,
                    0.0, xtol,
                    &new_cost
                );
            for (i64 i=0; i<count; ++i)
                x[i] += phi*m[i];

            f64 Dcost = last_cost - new_cost; // since cost decreases.
            if (Dcost > best_Dcost) {
                best_dir = dir;
                best_Dcost = Dcost;
            }
            last_cost = new_cost;
        }

        // Calculate the direction travelled from the initial `x` to now.
        f64 netdir_mag = 0.0;
        for (i64 i=0; i<count; ++i) {
            netdir[i] = x[i] - xprev[i];
            netdir_mag += sqed(netdir[i]);
        }
        netdir_mag = sqrt(netdir_mag);
        if (netdir_mag < xtol) {
            // Force genuine zero.
            netdir_mag = 0.0;
            memset(netdir, 0, 8*count);
        } else {
            for (i64 i=0; i<count; ++i)
                netdir[i] /= netdir_mag;
        }


        f64 new_cost;
        if (netdir_mag == 0.0) {
            // No change.
            new_cost = prev_cost;
        } else {
            // Accelerate search by doing an additional line search along this
            // new direction.
            f64 philo, phihi;
            opt_bracket1D(cost, user, count, tmp,
                    x, netdir, 0.0, min(1.0, prev_netdir_mag/4),
                    &philo, &phihi
                );
            if (isnan(philo + phihi))
                return 0;

            f64 phi = opt_run1D(cost, user, count, tmp,
                    x, netdir, philo, phihi,
                    0.0, xtol,
                    &new_cost
                );
            for (i64 i=0; i<count; ++i)
                x[i] += phi*netdir[i];

            // Replace the most-useful search vector with this new one.
            memcpy(searches + count*sorting[best_dir], netdir, 8*count);

            // Update the sorting to push this direction to the back.
            i64 idx = sorting[best_dir];
            for (i64 i=best_dir; i<count - 1; ++i)
                sorting[i] = sorting[i + 1];
            sorting[count - 1] = idx;
        }

        // Check the basis hasn't collapsed any dimensions.
        i32 collapsed = 0;
        for (i64 i=0; i<count; ++i) {
            for (i64 j=i + 1; j<count; ++j) {
                f64 cosbeta = 0.0;
                for (i64 k=0; k<count; ++k)
                    cosbeta += searches[i*count + k]*searches[j*count + k];
                if (cosbeta > 0.995) { // oooo too close.
                    collapsed = 1;
                    goto END_COLLAPSED;
                }
            }
        }
      END_COLLAPSED:;

        // If this iter is collapsed and it wasnt just reset to orthonormal, dont
        // even check convergence.
        was_reset = collapsed && !was_reset; // dont reset twice in a row.
        if (was_reset) {
            // Reset to orthonormal basis.
            memset(searches, 0, 8*count*count);
            for (i64 i=0; i<count; ++i)
                searches[i*count + i] = 1.0;
            for (i64 i=0; i<count; ++i)
                sorting[i] = i;
        } else {
            // Check for convergence.
            if ((abs(new_cost - prev_cost) <= ftol) && (netdir_mag <= xtol)) {
                if (best_cost)
                    *best_cost = new_cost;
                return 1;
            }
        }

        // Update for next iter.
        prev_cost = new_cost;
        memcpy(xprev, x, 8*count);
        prev_netdir_mag = netdir_mag;
        /* `was_reset` already updated. */

        // Go again.
    }
    // failed :(
    return 0;
}



// =========================================================================== //
// = LEAPS & BOUNDS ========================================================== //
// =========================================================================== //

// https://www.desmos.com/calculator/suklzv879l

f64 bound_both(f64 param, f64 lower, f64 upper) {
    f64 term = 2.0*LOG2E*(lower + upper - 2.0*param) / (upper - lower);
    return lower + (upper - lower) / (exp2(term) + 1.0);
}
f64 bound_lower(f64 param, f64 lower) {
    return lower + log2(exp2(param - lower) + 1.0);
}
f64 bound_upper(f64 param, f64 upper) {
    return upper - log2(exp2(upper - param) + 1.0);
}

f64 unbound_both(f64 value, f64 lower, f64 upper) {
    assert(within(value, lower, upper), "value=%g, lower=%g, upper=%g", value,
            lower, upper);
    f64 term = (upper - lower) / (value - lower) - 1.0;
    return 0.5*(lower + upper - (upper - lower)/2.0/LOG2E * log2(term));
}
f64 unbound_lower(f64 value, f64 lower) {
    assert(value > lower, "value=%g, lower=%g", value, lower);
    return lower + log2(exp2(value - lower) - 1.0);
}
f64 unbound_upper(f64 value, f64 upper) {
    assert(value < upper, "value=%g, upper=%g", value, upper);
    return upper - log2(exp2(upper - value) - 1.0);
}
