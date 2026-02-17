"""
Array/grid creation functions.
"""

import numpy as np

__all__ = ["meshgrid", "linspace", "arcspace"]


meshgrid = np.meshgrid

def concentrate(X, c, s):
    # https://www.desmos.com/calculator/nncnokstzq
    lo = X[0]
    hi = X[-1]
    assert lo <= hi
    if c is None:
        return X
    elif c == "hi":
        c = hi
    elif c == "lo":
        c = lo
    elif c == "mid":
        c = (lo + hi)/2
    assert lo <= c and c <= hi
    assert -1.0 <= s and s <= 1.0
    X = (X - lo) / (hi - lo)
    c = (c - lo) / (hi - lo)
    p = 1+s if s <= 0 else 1/(1-s)
    mask = (X <= c)
    if c == 1.0:
        X = c - c*((c-X)/c)**p
    elif c == 0.0:
        X = c + (1-c)*((X-c)/(1-c))**p
    else:
        with np.errstate(invalid="ignore"):
            X = np.where((X <= c)
                    , c - c*((c-X)/c)**p
                    , c + (1-c)*((X-c)/(1-c))**p
                )
    return X*(hi - lo) + lo


def linspace(N, lo, hi, *, c=None, s=0.0):
    X = np.linspace(lo, hi, N)
    return concentrate(X, c, s)

def arcspace(N, f, xmin, xmax, *, N0=2000, c=None, s=0.0):
    X0 = np.linspace(xmin, xmax, N0)
    Y0 = f(X0)
    xspan = xmax - xmin
    yspan = Y0.max() - Y0.min()
    scale = xspan / yspan if yspan != 0.0 else 1.0
    dy_dx = np.gradient(Y0, X0)
    integrand = np.sqrt(1 + (scale * dy_dx)**2)
    S0 = np.zeros_like(X0)
    S0[1:] = np.cumsum(0.5 * (integrand[1:] + integrand[:-1]) * np.diff(X0))
    S = np.linspace(S0[0], S0[-1], N)
    X = np.interp(S, S0, X0)
    return concentrate(X, c, s)
