"""
Runs the static approximator, outputting all found approximations.
"""

import argparse
import itertools
import sys
from math import pi

import numpy as np
import matplotlib

from .. import paths
from .. import geez
from ..geez import new_figure, new_plots, new_window, no_window

from .cea import *
from .ipa import *
from .ethanol import *

from .ratpoly import *
from .space import *

__all__ = ["run", "main"]



def summary_1D(name, ratpoly, values, X, *, extra_reqs=()):
    approx = ratpoly(X)
    abserr = ratpoly.abs_error(values, X)
    relerr = ratpoly.rel_error(values, X)
    print(f"    /* rect-bounded rational polynomial approximation */")
    print(f"    /* max error of: */")
    print(f"    /*   abs {np.nanmax(np.abs(abserr))*100:.3g}% */")
    print(f"    /*   rel {np.nanmax(np.abs(relerr))*100:.3g}% */")
    if extra_reqs:
        print(f"    /* also requires: */")
        for x in extra_reqs:
            print(f"    /*   {x} */")
    print(f"    const f64 XLO = {X.min()};")
    print(f"    const f64 XHI = {X.max()};")
    print(ratpoly.code())
    _, axes = summary_1D.window.new_plots(cols=2, title=name)
    axes[0].plot(X, values, label="actual function")
    axes[0].plot(X, approx, label="approximation")
    axes[1].plot(X, 100*abserr, label="abs error")
    axes[1].plot(X, 100*relerr, label="rel error")
summary_1D.window = geez.no_window()

def summary_2D(name, ratpoly, surf, *, extra_reqs=()):
    X, Y = Evenspace(*surf.bounds()).points(400**2, flatten=True)
    if surf.masker is not None:
        mask = surf.masker.coords(X, Y)
        X = X[mask]
        Y = Y[mask]
    values = surf.f(X, Y)
    approx = ratpoly(X, Y)
    mask = np.isnan(values) | np.isnan(approx)
    values[mask] = np.nan
    approx[mask] = np.nan

    abserr = np.abs(abs_error(values, approx))
    relerr = np.abs(rel_error(values, approx))
    Xmin, Xmax, Ymin, Ymax = surf.bounds()
    print(f"    /* rect-bounded rational polynomial approximation */")
    print(f"    /* max error of: */")
    print(f"    /*   abs {np.nanmax(abserr)*100:.3g}% */")
    print(f"    /*   rel {np.nanmax(relerr)*100:.3g}% */")
    if extra_reqs:
        print(f"    /* also requires: */")
        for x in extra_reqs:
            print(f"    /*   {x} */")
    print(f"    const f64 XLO = {Xmin};")
    print(f"    const f64 XHI = {Xmax};")
    print(f"    const f64 YLO = {Ymin};")
    print(f"    const f64 YHI = {Ymax};")
    print(ratpoly.code())

    tri = matplotlib.tri.Triangulation(X, Y)
    if surf.masker is not None:
        surf.masker.triang(tri)

    fig, axes = summary_2D.window.new_plots(rows=2, cols=2, title=name)
    cont = axes[0,0].tricontourf(tri, values, levels=300, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 0])
    axes[0,0].set_title("actual function")
    axes[0,0].set_grid("none")
    cont = axes[0,1].tricontourf(tri, approx, levels=300, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 1])
    axes[0,1].set_title("approximation")
    axes[0,1].set_grid("none")
    cont = axes[1,0].tricontourf(tri, 100*abserr, levels=300, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 0])
    axes[1,0].set_title("abs error")
    axes[1,0].set_grid("none")
    cont = axes[1,1].tricontourf(tri, 100*relerr, levels=300, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 1])
    axes[1,1].set_title("rel error")
    axes[1,1].set_grid("none")
summary_2D.window = geez.no_window()


def peep_2D(surf, *points, N=200):
    _, ax = peep_2D.window.new_plots(projection="3d")
    X, Y = Evenspace(*surf.bounds()).points(N**2, flatten=True)
    tri = matplotlib.tri.Triangulation(X, Y)
    if surf.masker is not None:
        surf.masker.triang(tri)
    ax.plot_trisurf(tri, surf.f(X, Y), cmap="viridis")
    if points:
        ax.scatter(*points)
peep_2D.window = geez.no_window()




def isentropic_M_from_P_on_P0(P_on_P0, y):
    return (2/(y - 1)*(P_on_P0 ** ((1 - y)/y) - 1)) ** 0.5
def isentropic_A_on_Astar(M, y):
    n = 0.5*(y + 1)/(y - 1)
    return (2/(y + 1.0) + M*M/n/2)**n / M

def cea_AEAT_for(P, ofr, M_lerp_1_to_exit):
    P_exit = 101325.0
    gamma_tht = CEA["t_gamma"](P, ofr, 1.0)
    M_exit = isentropic_M_from_P_on_P0(P_exit / (P*1e6), gamma_tht)
    return isentropic_A_on_Astar(1 + (M_exit - 1)*M_lerp_1_to_exit, gamma_tht)

CEA_M_lowm = 0.1
CEA_M_midm = 0.9
def cea(name, P, ofr):
    if name.startswith("lowm_"):
        AEAT = cea_AEAT_for(P, ofr, CEA_M_lowm)
        name = name[len("lowm_"):]
    elif name.startswith("midm_"):
        AEAT = cea_AEAT_for(P, ofr, CEA_M_midm)
        name = name[len("midm_"):]
    else:
        AEAT = cea_AEAT_for(P, ofr, 1.0)
    return CEA[name](P, ofr, AEAT)

def getattr_cea(cea_result, name):
    if name == "c_mach":
        return 0.0
    if name == "t_mach":
        return 1.0
    return getattr(cea_result, name)

LUT_FOR_ALL = True

def find_approximation(get_surf, type_name, var_name, pidxs=None,
        qidxs=None, rel_only=False, points=200, spacing=1.25, max_error=0.015,
        blitz=2.0, lut=None, extra_reqs=()):

    if LUT_FOR_ALL:
        lut = (80, 80)

    def wrapped(what=""):
        if what != "get":
            print(f"approximating {type_name} {var_name}")

        surf = get_surf() # evaluate now rather than on init.

        if not (lut is not None and what in {"get", "approximate"}):
            X, Y, V = surf.points(
                1000 if what=="approximate" else points,
                spacing=spacing
            )
        if lut is not None:
            tbl = JustGimmeATable(surf, f"{type_name.lower()}_{var_name}", *lut)

        evaluator = evaluator_rel_only if rel_only else evaluator_abs_only

        if what == "peep":
            peep_2D(surf, X, Y, V)
            print("peeped")
        elif what == "backwards":
            RationalPolynomial.search_backwards(X, Y, V, max_error=max_error,
                    evaluator=evaluator)
        elif what == "forwards":
            RationalPolynomial.search_forwards(X, Y, V, blitz=blitz,
                    evaluator=evaluator)
        elif what == "approximate":
            if lut is not None:
                tbl.run(extra_reqs=extra_reqs)
                return
            rp = RationalPolynomial.approximate(pidxs, qidxs, X, Y, V)
            print("found:", rp)
            summary_2D(f"{type_name} {var_name}", rp, surf,
                    extra_reqs=extra_reqs)
        elif what == "get":
            if lut is not None:
                return tbl
            return RationalPolynomial.approximate(pidxs, qidxs, X, Y, V)
        else:
            assert False, f"invalid what: {what}"
    return wrapped

def cea_approximation(our_name, cea_name, **kwargs):
    f = lambda P, ofr: cea(cea_name, P, ofr)
    surf = lambda: Surfspace(f, 1.0, 5.0, 1.0, 3.0, N0=200)
    return find_approximation(surf, "CEA", our_name, **kwargs)


find_cea_Isp = cea_approximation("Isp", "isp", rel_only=True, blitz=0.0,
        max_error=0.03,
        pidxs=[1, 2, 4, 8, 13], qidxs=[0, 1, 2])

find_cea_T_cc = cea_approximation("T_cc", "c_t", blitz=0.0,
        pidxs=[1, 3, 5], qidxs=[0, 2, 3, 4, 5])
find_cea_rho_cc = cea_approximation("rho_cc", "rho", blitz=2.0,
        pidxs=[1, 4, 5, 8], qidxs=[0, 1, 2, 4, 8, 9])

find_cea_Mw_tht = cea_approximation("Mw_tht", "t_mw", rel_only=True, blitz=0.0,
        pidxs=[2], qidxs=[0, 1, 2, 3, 4, 5])

find_cea_gamma_cc = cea_approximation("gamma_cc", "c_gamma")
find_cea_gamma_tht = cea_approximation("gamma_tht", "t_gamma", rel_only=True,
        pidxs=[0, 1, 2, 3, 4, 5], qidxs=[0, 1, 2, 3, 4, 5, 7, 8, 9])
find_cea_gamma_lowm = cea_approximation("gamma_lowm", "lowm_gamma",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_lowm})"])
find_cea_gamma_midm = cea_approximation("gamma_midm", "midm_gamma",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_midm})"])
find_cea_gamma_exit = cea_approximation("gamma_exit", "gamma")

find_cea_cp_cc = cea_approximation("cp_cc", "c_cp",
        blitz=3.0,
        pidxs=[0, 1, 2, 4, 5, 6, 8], qidxs=[0, 1, 2, 4, 6, 8, 9])
find_cea_cp_tht = cea_approximation("cp_tht", "t_cp",
        blitz=3.0, points=300, spacing=1.3,
        pidxs=[0, 1, 3, 5, 6, 7, 8, 9, 11, 12, 14], qidxs=[0, 1, 2, 3, 4, 5])
find_cea_cp_lowm = cea_approximation("cp_lowm", "lowm_cp",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_lowm})"])
find_cea_cp_midm = cea_approximation("cp_midm", "midm_cp",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_midm})"],
        blitz=3.0, points=300, spacing=1.3,
        pidxs=[0, 1, 2, 4, 5, 6, 7], qidxs=[0, 1, 2, 4, 5, 8, 9, 10, 11, 12, 14],
        # give up
        lut=(20, 40))
find_cea_cp_exit = cea_approximation("cp_exit", "cp",
        blitz=3.0, max_error=0.045, points=400, spacing=1.25,
        pidxs=[1, 2, 3, 4, 5, 6, 7, 8],
        qidxs=[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
               19, 20, 21, 22, 24, 25, 26, 27],
        # its so over
        lut=(15, 100))

find_cea_mu_cc = cea_approximation("mu_cc", "c_visc",
        blitz=3.0,
        pidxs=[1, 4, 5], qidxs=[0, 1, 2, 5])
find_cea_mu_tht = cea_approximation("mu_tht", "t_visc",
        blitz=3.0,
        pidxs=[0, 2, 4, 5], qidxs=[0, 1, 2, 5])
find_cea_mu_lowm = cea_approximation("mu_lowm", "lowm_visc",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_lowm})"])
find_cea_mu_midm = cea_approximation("mu_midm", "midm_visc",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_midm})"],
        blitz=3.0,
        pidxs=[0, 4, 8, 9], qidxs=[0, 1, 2, 4, 5])
find_cea_mu_exit = cea_approximation("mu_exit", "visc",
        blitz=3.0, max_error=0.01, points=300, spacing=1.3,
        pidxs=[0, 4, 6, 8, 9, 12, 13], qidxs=[0, 1, 2, 3, 4, 5, 7])

find_cea_Pr_cc = cea_approximation("Pr_cc", "c_pran",
        blitz=5.0,
        pidxs=[0, 1, 2, 4, 5], qidxs=[0, 1, 2, 3, 5, 7, 8, 9, 12, 14])
find_cea_Pr_tht = cea_approximation("Pr_tht", "t_pran",
        blitz=5.0,
        pidxs=[0, 1, 2, 4, 5], qidxs=[0, 1, 2, 3, 5, 7, 8, 9, 12, 13, 14])
find_cea_Pr_lowm = cea_approximation("Pr_lowm", "lowm_pran",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_lowm})"])
find_cea_Pr_midm = cea_approximation("Pr_midm", "midm_pran",
        extra_reqs=[f"'M_midm' as: lerp(1, M_exit, {CEA_M_midm})"],
        blitz=5.0,
        pidxs=[2, 3, 4, 5, 9], qidxs=[0, 2, 5, 9, 12, 13],
        # we down
        lut=(10, 40))
find_cea_Pr_exit = cea_approximation("Pr_exit", "pran",
        blitz=5.0,
        pidxs=[], qidxs=[], # genuinely never gonna happen
        # :((
        lut=(10, 60))


@Masker
def ipa_masker(T, P):
    # Psat check is way worse. thermo.Chemical is pretty shithouse.
    # https://www.desmos.com/calculator/laxpd5p1zh
    if False:
        A = -1.41
        B = -2.582e-3
        C = 1.97e-3
        max_T = (A + P) / (B + C*P)
    else:
        A = 18.75
        B = 417.5
        max_T = A*P + B
    return T <= max_T
ipa_masker.as_string = "x <= 18.75 * y + 410.6"

def ipa_approximation(our_name, ipa_name, **kwargs):
    f = lambda *args: IPA[ipa_name](*args)
    surf = lambda: Surfspace(f, 250.0, 500.0, 2.0, 7.0, N0=200,
            masker=ipa_masker)
    if "extra_reqs" not in kwargs:
        kwargs["extra_reqs"] = []
    kwargs["extra_reqs"] = list(kwargs["extra_reqs"])
    kwargs["extra_reqs"].append(ipa_masker.as_string)
    return find_approximation(surf, "IPA", our_name, **kwargs)


find_ipa_rho = ipa_approximation("rho", "rho", blitz=0.0,
        pidxs=[0, 1, 2], qidxs=[0, 1, 2, 3, 4])
find_ipa_cp = ipa_approximation("cp", "Cp", blitz=1.0,
        pidxs=[0, 1, 3, 6, 10], qidxs=[0])
find_ipa_mu = ipa_approximation("mu", "mu", spacing=1.15, blitz=2.0,
        pidxs=[0, 1, 3, 4, 6], qidxs=[0, 3, 6])
find_ipa_k = ipa_approximation("k", "k", blitz=0.0,
        pidxs=[0], qidxs=[0, 1, 2, 3])




@Masker
def ethanol_masker(T, P):
    A = 16.5
    B = 416.5
    max_T = A*P + B
    return T <= max_T
ethanol_masker.as_string = "x <= 16.5 * y + 409.9"

def ethanol_approximation(our_name, ethanol_name, **kwargs):
    f = lambda *args: Ethanol[ethanol_name](*args)
    surf = lambda: Surfspace(f, 250.0, 500.0, 2.0, 7.0, N0=200,
            masker=ethanol_masker)
    if "extra_reqs" not in kwargs:
        kwargs["extra_reqs"] = []
    kwargs["extra_reqs"] = list(kwargs["extra_reqs"])
    kwargs["extra_reqs"].append(ethanol_masker.as_string)
    return find_approximation(surf, "Ethanol", our_name, **kwargs)


find_ethanol_rho = ethanol_approximation("rho", "rho")
find_ethanol_cp = ethanol_approximation("cp", "Cp")
find_ethanol_mu = ethanol_approximation("mu", "mu")
find_ethanol_k = ethanol_approximation("k", "k")




def cea_property_along(X_name, Y_name, P0_cc, ofr):
    AEAT = cea_AEAT_for(P0_cc, ofr, 1.0)
    X = []
    Y = []
    for aeat in np.linspace(1.0, AEAT, 100):
        cea_res = CEA(P0_cc, ofr, aeat)
        X.append(getattr_cea(cea_res, X_name))
        Y.append(getattr_cea(cea_res, Y_name))
    pairs = sorted(zip(X, Y))
    X, Y = zip(*pairs)

    X = np.array(X)
    Y = np.array(Y)

    X0 = np.linspace(X[0], X[len(X) - 1], 30)
    Y0 = np.interp(X0, X, Y)
    X0 = np.insert(X0, 0, getattr_cea(cea_res, f"c_{X_name}"))
    Y0 = np.insert(Y0, 0, getattr_cea(cea_res, f"c_{Y_name}"))

    pairs0 = sorted(zip(X0, Y0))
    X0, Y0 = zip(*pairs0)

    return X0, Y0

def cea_compare_along(X_name, Y_name, approx):
    P0_ccs = np.linspace(1, 5, 5)
    ofrs = np.linspace(1.0, 3.0, 8)
    P0_ccs = np.linspace(3.3, 3.7, 3)
    ofrs = np.linspace(1.2, 2.0, 6)

    cmap = matplotlib.cm.get_cmap("tab10", len(ofrs))

    _, ax = new_plots(cols=len(P0_ccs))
    if not isinstance(ax, np.ndarray):
        ax = [ax]
    for i, P0_cc in enumerate(P0_ccs):
        for j, ofr in enumerate(ofrs):
            x1, y1 = approx(P0_cc, ofr)
            x2, y2 = cea_property_along(X_name, Y_name, P0_cc, ofr)
            ax[i].set_title(f"P={P0_cc:.3g}")
            ax[i].plot(x1, y1, label=f"APPROX ofr={ofr:.3g}",
                    color=cmap(j))
            ax[i].scatter(x2, y2, label=f"ACTUAL ofr={ofr:.3g}",
                    marker="o", facecolors="none", color=cmap(j))
            if len(x1) == 0:
                ax[i].plot(x2, y2, "--", color=cmap(j))



def fit_quad(x0, y0, x1, y1, x2, y2):
    # Determinant of the Vandermonde matrix
    det = x0**2 * (x1 - x2) \
        - x1**2 * (x0 - x2) \
        + x2**2 * (x0 - x1)
    assert det != 0

    a = y0 * (x1 - x2) \
      - y1 * (x0 - x2) \
      + y2 * (x0 - x1)
    b = x0**2 * (y1 - y2) \
      - x1**2 * (y0 - y2) \
      + x2**2 * (y0 - y1)
    c = x0**2 * (x1*y2 - x2*y1) \
      - x1**2 * (x0*y2 - x2*y0) \
      + x2**2 * (x0*y1 - x1*y0)
    a /= det
    b /= det
    c /= det
    return a, b, c

def cea_quadfit_function_of_M(M_midm, M_exit, V_cc, V_tht, V_midm, V_exit):
    a, b, c = fit_quad(1.0, V_tht, M_midm, V_midm, M_exit, V_exit)
    return lambda M: np.where(
        M > 1,
        a*M**2 + b*M + c,
        M * V_tht + (1 - M) * V_cc
    )

def cea_quadfit(name, P0_cc, ofr):
    M_exit = cea("mach", P0_cc, ofr)
    M_midm = 1 + (M_exit - 1) / 2
    V_cc = cea(f"c_{name}", P0_cc, ofr)
    V_tht = cea(f"t_{name}", P0_cc, ofr)
    V_exit = cea(name, P0_cc, ofr)
    V_midm = CEA[name](P0_cc, ofr, cea_AEAT_for(P0_cc, ofr, CEA_M_midm))

    M = np.linspace(1.0, M_exit, 100)
    M = np.insert(M, 0, 0.0)
    M = np.insert(M, 1, 0.5)
    f = cea_quadfit_function_of_M(M_midm, M_exit, V_cc, V_tht, V_midm, V_exit)
    return M, f(M)

def cea_quadfit_real(P0_cc, ofr, get_gamma, get_cc, get_tht, get_midm, get_exit):
    gamma_tht = get_gamma(P0_cc, ofr)
    M_exit = isentropic_M_from_P_on_P0(101325.0 / (P0_cc*1e6), gamma_tht);
    M_midm = 1 + (M_exit - 1) * CEA_M_midm
    V_cc = get_cc(P0_cc, ofr)
    V_tht = get_tht(P0_cc, ofr)
    V_midm = get_midm(P0_cc, ofr)
    V_exit = get_exit(P0_cc, ofr)

    M = np.linspace(1.0, M_exit, 100)
    M = np.insert(M, 0, 0.0)
    M = np.insert(M, 1, 0.5)
    f = cea_quadfit_function_of_M(M_midm, M_exit, V_cc, V_tht, V_midm, V_exit)
    return M, f(M)


def fit_cubic(x0, y0, x1, y1, x2, y2, x3, y3):
    x0_2 = x0 * x0
    x1_2 = x1 * x1
    x2_2 = x2 * x2
    x3_2 = x3 * x3

    x0_3 = x0_2 * x0
    x1_3 = x1_2 * x1
    x2_3 = x2_2 * x2
    x3_3 = x3_2 * x3

    # Determinant of the Vandermonde matrix
    den = x0_3 * (x1_2 * (x2 - x3) - x2_2 * (x1 - x3) + x3_2 * (x1 - x2)) \
        - x1_3 * (x0_2 * (x2 - x3) - x2_2 * (x0 - x3) + x3_2 * (x0 - x2)) \
        + x2_3 * (x0_2 * (x1 - x3) - x1_2 * (x0 - x3) + x3_2 * (x0 - x1)) \
        - x3_3 * (x0_2 * (x1 - x2) - x1_2 * (x0 - x2) + x2_2 * (x0 - x1))

    assert den != 0

    a = y0 * (x1_2 * (x2 - x3) - x2_2 * (x1 - x3) + x3_2 * (x1 - x2)) \
      - y1 * (x0_2 * (x2 - x3) - x2_2 * (x0 - x3) + x3_2 * (x0 - x2)) \
      + y2 * (x0_2 * (x1 - x3) - x1_2 * (x0 - x3) + x3_2 * (x0 - x1)) \
      - y3 * (x0_2 * (x1 - x2) - x1_2 * (x0 - x2) + x2_2 * (x0 - x1))

    b = x0_3 * (y1 * (x2 - x3) - y2 * (x1 - x3) + y3 * (x1 - x2)) \
      - x1_3 * (y0 * (x2 - x3) - y2 * (x0 - x3) + y3 * (x0 - x2)) \
      + x2_3 * (y0 * (x1 - x3) - y1 * (x0 - x3) + y3 * (x0 - x1)) \
      - x3_3 * (y0 * (x1 - x2) - y1 * (x0 - x2) + y2 * (x0 - x1))

    c = x0_3 * (x1_2 * (y2 - y3) - x2_2 * (y1 - y3) + x3_2 * (y1 - y2)) \
      - x1_3 * (x0_2 * (y2 - y3) - x2_2 * (y0 - y3) + x3_2 * (y0 - y2)) \
      + x2_3 * (x0_2 * (y1 - y3) - x1_2 * (y0 - y3) + x3_2 * (y0 - y1)) \
      - x3_3 * (x0_2 * (y1 - y2) - x1_2 * (y0 - y2) + x2_2 * (y0 - y1))

    d = x0_3 * (x1_2 * (x2 * y3 - x3 * y2) - x2_2 * (x1 * y3 - x3 * y1) \
                + x3_2 * (x1 * y2 - x2 * y1)) \
      - x1_3 * (x0_2 * (x2 * y3 - x3 * y2) - x2_2 * (x0 * y3 - x3 * y0) \
                + x3_2 * (x0 * y2 - x2 * y0)) \
      + x2_3 * (x0_2 * (x1 * y3 - x3 * y1) - x1_2 * (x0 * y3 - x3 * y0) \
                + x3_2 * (x0 * y1 - x1 * y0)) \
      - x3_3 * (x0_2 * (x1 * y2 - x2 * y1) - x1_2 * (x0 * y2 - x2 * y0) \
                + x2_2 * (x0 * y1 - x1 * y0))

    a /= den
    b /= den
    c /= den
    d /= den

    return a, b, c, d

def cea_cubefit_function_of_M(M_lowm, M_midm, M_exit, V_cc, V_tht, V_lowm,
        V_midm, V_exit):
    a, b, c, d = fit_cubic(
        1.0, V_tht,
        M_lowm, V_lowm,
        M_midm, V_midm,
        M_exit, V_exit
    )
    return lambda M: np.where(
        M > 1,
        ((a*M + b)*M + c)*M + d,
        M * (a + b + c + d) + (1 - M) * V_cc
    )

def cea_cubefit_real(P0_cc, ofr, get_gamma, get_cc, get_tht, get_lowm, get_midm,
        get_exit):
    gamma_tht = get_gamma(P0_cc, ofr)
    M_exit = isentropic_M_from_P_on_P0(101325.0 / (P0_cc*1e6), gamma_tht);
    M_lowm = 1 + (M_exit - 1) * CEA_M_lowm
    M_midm = 1 + (M_exit - 1) * CEA_M_midm
    V_cc = get_cc(P0_cc, ofr)
    V_tht = get_tht(P0_cc, ofr)
    V_lowm = get_lowm(P0_cc, ofr)
    V_midm = get_midm(P0_cc, ofr)
    V_exit = get_exit(P0_cc, ofr)

    M = np.linspace(1.0, M_exit, 100)
    M = np.insert(M, 0, 0.0)
    M = np.insert(M, 1, 0.5)
    f = cea_cubefit_function_of_M(M_lowm, M_midm, M_exit, V_cc, V_tht,
            V_lowm, V_midm, V_exit)
    return M, f(M)


def check_cea_gamma_along():
    get_gamma = find_cea_gamma_tht(what="get")
    get_cc = find_cea_gamma_cc(what="get")
    get_tht = find_cea_gamma_tht(what="get")
    get_lowm = find_cea_gamma_lowm(what="get")
    get_midm = find_cea_gamma_midm(what="get")
    get_exit = find_cea_gamma_exit(what="get")
    def approx(P0_cc, ofr):
        return cea_cubefit_real(P0_cc, ofr, get_gamma, get_cc, get_tht, get_lowm,
                get_midm, get_exit)
    cea_compare_along("mach", "gamma", approx)
def check_cea_cp_along():
    get_gamma = find_cea_gamma_tht(what="get")
    get_cc = find_cea_cp_cc(what="get")
    get_tht = find_cea_cp_tht(what="get")
    get_lowm = find_cea_cp_lowm(what="get")
    get_midm = find_cea_cp_midm(what="get")
    get_exit = find_cea_cp_exit(what="get")
    def approx(P0_cc, ofr):
        return cea_cubefit_real(P0_cc, ofr, get_gamma, get_cc, get_tht, get_lowm,
                get_midm, get_exit)
    cea_compare_along("mach", "cp", approx)
def check_cea_mu_along():
    get_gamma = find_cea_gamma_tht(what="get")
    get_cc = find_cea_mu_cc(what="get")
    get_tht = find_cea_mu_tht(what="get")
    get_lowm = find_cea_mu_lowm(what="get")
    get_midm = find_cea_mu_midm(what="get")
    get_exit = find_cea_mu_exit(what="get")
    def approx(P0_cc, ofr):
        return cea_cubefit_real(P0_cc, ofr, get_gamma, get_cc, get_tht, get_lowm,
                get_midm, get_exit)
    cea_compare_along("mach", "visc", approx)
def check_cea_Pr_along():
    get_gamma = find_cea_gamma_tht(what="get")
    get_cc = find_cea_Pr_cc(what="get")
    get_tht = find_cea_Pr_tht(what="get")
    get_lowm = find_cea_Pr_lowm(what="get")
    get_midm = find_cea_Pr_midm(what="get")
    get_exit = find_cea_Pr_exit(what="get")
    def approx(P0_cc, ofr):
        return cea_cubefit_real(P0_cc, ofr, get_gamma, get_cc, get_tht, get_lowm,
                get_midm, get_exit)
    cea_compare_along("mach", "pran", approx)




class JustGimmeATable:
    # holy balls some are hard. just gimme a lookuptable.

    def __init__(self, surf, name, size_P, size_ofr):
        self.surf = surf
        self.name = name
        self.shape = (size_P, size_ofr)
        X = np.linspace(surf.xlo, surf.xhi, size_P)
        Y = np.linspace(surf.ylo, surf.yhi, size_ofr)
        X, Y = np.meshgrid(X, Y, indexing="ij")
        if surf.masker is not None:
            mask = surf.masker.coords(X, Y)
            X[~mask] = np.nan
            Y[~mask] = np.nan
        self.tbl = surf.f(X, Y)
        self.tbl = self.tbl.flatten("C") # rowmajor.

    def __call__(self, x, y):
        xlo, xhi, ylo, yhi = self.surf.bounds()
        x = (x - xlo) / (xhi - xlo)
        x *= self.shape[0] - 1
        i = np.floor(x).astype(int)
        i = np.maximum(i, 0)
        i = np.minimum(i, self.shape[0] - 2)
        t = x - i
        y = (y - ylo) / (yhi - ylo)
        y *= self.shape[1] - 1
        j = np.floor(y).astype(int)
        j = np.maximum(j, 0)
        j = np.minimum(j, self.shape[1] - 2)
        s = y - j
        c00 = self.tbl[i*self.shape[1] + j]
        c01 = self.tbl[i*self.shape[1] + j + 1]
        c10 = self.tbl[(i + 1)*self.shape[1] + j]
        c11 = self.tbl[(i + 1)*self.shape[1] + j + 1]
        c0 = c00 + s*(c01 - c00)
        c1 = c10 + s*(c11 - c10)
        return c0 + t*(c1 - c0)

    def run(self, extra_reqs=()):
        lines = []

        xlo, xhi, ylo, yhi = self.surf.bounds()
        rows, cols = self.shape

        X, Y = Evenspace(*self.surf.bounds()).points(400**2, flatten=False)
        values = self.surf.f(X, Y)
        approx = self(X, Y)
        mask = np.isnan(values) | np.isnan(approx)
        values[mask] = np.nan
        approx[mask] = np.nan
        abserr = np.abs(abs_error(values, approx))
        relerr = np.abs(rel_error(values, approx))

        fig, axes = summary_2D.window.new_plots(rows=2, cols=2,
                title=f"LUT {self.name}")
        cont = axes[0,0].contourf(X, Y, values, levels=300, cmap="viridis")
        fig.colorbar(cont, ax=axes[0, 0])
        axes[0,0].set_title("actual function")
        axes[0,0].set_grid("none")
        cont = axes[0,1].contourf(X, Y, approx, levels=300, cmap="viridis")
        fig.colorbar(cont, ax=axes[0, 1])
        axes[0,1].set_title("approximation")
        axes[0,1].set_grid("none")
        cont = axes[1,0].contourf(X, Y, 100*abserr, levels=300, cmap="viridis")
        fig.colorbar(cont, ax=axes[1, 0])
        axes[1,0].set_title("abs error")
        axes[1,0].set_grid("none")
        cont = axes[1,1].contourf(X, Y, 100*relerr, levels=300, cmap="viridis")
        fig.colorbar(cont, ax=axes[1, 1])
        axes[1,1].set_title("rel error")
        axes[1,1].set_grid("none")


        lines.extend([
            f"    /* evenly-spaced flattened (C-ordered) 2D LUT */",
            f"    /* max error of: */",
            f"    /*   abs {np.nanmax(abserr)*100:.3g}% */",
            f"    /*   rel {np.nanmax(relerr)*100:.3g}% */",
        ])
        if extra_reqs:
            lines.append(f"    /* also requires: */")
            for x in extra_reqs:
                lines.append(f"    /*   {x} */")
        lines.append(f"    const f64 XLO = {xlo};")
        lines.append(f"    const f64 XHI = {xhi};")
        lines.append(f"    const f64 YLO = {ylo};")
        lines.append(f"    const f64 YHI = {yhi};")
        lines.append(f"    enum {{ XLEN = {rows},")
        lines.append(f"           YLEN = {cols}, }};")
        lines.append(f"    #include \"tbl/{self.name}.i\"")
        lines.append(f"")

        print("\n".join(lines))

        # now write the table tile.
        lines = [
            f"/* LUT for {self.name} */"
        ]
        lines.append(f"static const f32 tbl[{rows * cols}] = {{")

        # x.01234567ennnf,
        # =17 diggies.
        entrylen = 17
        display_cols = (80 - 4) // entrylen

        n = len(self.tbl)

        for start in range(0, n, display_cols):
            chunk = self.tbl[start:start + display_cols]
            entries = []
            for k, val in enumerate(chunk):
                if val == val:
                    s = f"{val:.8e}f,"
                else:
                    s = "fNAN,"
                s += " " * (entrylen - len(s))
                entries.append(s)
            lines.append(" "*4 + "".join(entries))

        lines.append(f"}};")
        lines.append(f"")

        paths.APPROXIMATOR_TBLS.mkdir(parents=True, exist_ok=True)
        with open(paths.APPROXIMATOR_TBLS / f"{self.name}.i", "w") as f:
            f.write("\n".join(lines))






def _run():
    find_cea_Isp(what="approximate")

    find_cea_T_cc(what="approximate")
    find_cea_rho_cc(what="approximate")

    find_cea_Mw_tht(what="approximate")

    find_cea_gamma_cc(what="approximate")
    find_cea_gamma_tht(what="approximate")
    find_cea_gamma_lowm(what="approximate")
    find_cea_gamma_midm(what="approximate")
    find_cea_gamma_exit(what="approximate")

    find_cea_cp_cc(what="approximate")
    find_cea_cp_tht(what="approximate")
    find_cea_cp_lowm(what="approximate")
    find_cea_cp_midm(what="approximate")
    find_cea_cp_exit(what="approximate")

    find_cea_mu_cc(what="approximate")
    find_cea_mu_tht(what="approximate")
    find_cea_mu_lowm(what="approximate")
    find_cea_mu_midm(what="approximate")
    find_cea_mu_exit(what="approximate")

    find_cea_Pr_cc(what="approximate")
    find_cea_Pr_tht(what="approximate")
    find_cea_Pr_lowm(what="approximate")
    find_cea_Pr_midm(what="approximate")
    find_cea_Pr_exit(what="approximate")

    check_cea_gamma_along()
    check_cea_cp_along()
    check_cea_mu_along()
    check_cea_Pr_along()

    find_ipa_rho(what="approximate")
    find_ipa_cp(what="approximate")
    find_ipa_mu(what="approximate")
    find_ipa_k(what="approximate")

    find_ethanol_rho(what="approximate")
    find_ethanol_cp(what="approximate")
    find_ethanol_mu(what="approximate")
    find_ethanol_k(what="approximate")



def run(view=True, save=False):
    global peep_window
    with CEA.configure("LOx", "IPA"), IPA.configure(), Ethanol.configure():
        with geez.instance(view, save):
            # stoud redirect nested to not capture meta output.
            with paths.splice_stdout(paths.APPROXIMATOR_OUTPUT, view, save):
                win = geez.new_window("summary", emptyok=True)
                summary_1D.window = win
                summary_2D.window = win
                peep_2D.window = geez.new_window("peep", emptyok=True)
                _run()


def main():
    parser = argparse.ArgumentParser(
        description="Static function approximator, generates c source code "
                    "which can be used as an approximation of some mathematical"
                    "/data-derived function."
    )

    group = parser.add_mutually_exclusive_group()
    group.add_argument("-v", "--view", action="store_true",
            help="view output (default, unless saving)")
    group.add_argument("-n", "--no-view", action="store_true",
            help="do not view output")

    parser.add_argument("-s", "--save", action="store_true",
            help="save output to file, inc. figures")

    args = parser.parse_args()

    save = args.save
    view = (args.view | (not args.save)) & (not args.no_view)
    run(view, save)

if __name__ == "__main__":
    main()
