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
from .chemical import *
from .ratpoly import *
from .space import *

__all__ = ["run", "main"]



def summary_1D(name, ratpoly, values, X, *, extra_reqs=()):
    approx = ratpoly(X)
    abserr = ratpoly.abs_error(values, X)
    relerr = ratpoly.rel_error(values, X)
    print(f"    /* max error of: */")
    print(f"    /*    abs {np.abs(abserr).max()*100:.3g}% */")
    print(f"    /*    rel {np.abs(relerr).max()*100:.3g}% */")
    print(f"    /* requires: */")
    print(f"    /*    x in [{X.min()}, {X.max()}] */")
    for x in extra_reqs:
        print(f"    /*    {x} */")
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
    abserr = np.abs(abs_error(values, approx))
    relerr = np.abs(rel_error(values, approx))
    Xmin, Xmax, Ymin, Ymax = surf.bounds()
    print(f"    /* max error of: */")
    print(f"    /*    abs {abserr.max()*100:.3g}% */")
    print(f"    /*    rel {relerr.max()*100:.3g}% */")
    print(f"    /* requires: */")
    print(f"    /*    x in [{Xmin}, {Xmax}] */")
    print(f"    /*    y in [{Ymin}, {Ymax}] */")
    for x in extra_reqs:
        print(f"    /*    {x} */")
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
    _, ax = new_plots(projection="3d")
    X, Y = Evenspace(*surf.bounds()).points(N**2, flatten=True)
    tri = matplotlib.tri.Triangulation(X, Y)
    if surf.masker is not None:
        surf.masker.triang(tri)
    ax.plot_trisurf(tri, surf.f(X, Y), cmap="viridis")
    if points:
        ax.scatter(*points)






def cea_AEAT_for(P, ofr, M_lerp_1_to_exit):
    P_exit = 101325.0
    gamma_tht = CEA["t_gamma"](P, ofr, 1.0)
    def isentropic_M_from_P_on_P0(P_on_P0, y):
        return (2/(y - 1)*(P_on_P0 ** ((1 - y)/y) - 1)) ** 0.5;
    M_exit = isentropic_M_from_P_on_P0(P_exit / (P*1e6), gamma_tht)
    def isentropic_A_on_Astar(M, y):
        n = 0.5*(y + 1)/(y - 1)
        return (2/(y + 1.0) + M*M/n/2)**n / M
    return isentropic_A_on_Astar(1 + (M_exit - 1)*M_lerp_1_to_exit, gamma_tht)

def cea(name, P, ofr):
    AEAT = cea_AEAT_for(P, ofr, 1.0)
    return CEA[name](P, ofr, AEAT)

def getattr_cea(cea_result, name):
    if name == "c_mach":
        return 0.0
    if name == "t_mach":
        return 1.0
    return getattr(cea_result, name)

def cea_approximation(our_name, cea_name, pidxs=None, qidxs=None, rel_only=False,
        points=200, spacing=1.25, max_error=0.015, blitz=2.0):
    def wrapped(what=""):
        print(f"approximating CEA {our_name}")
        f = lambda P, ofr: cea(cea_name, P, ofr)

        surf = Surfspace(f, 1.0, 5.0, 1.0, 3.0, N0=200)
        P, ofr, V = surf.points(
            1000 if what=="approximate" else points,
            spacing=spacing
        )

        evaluator = evaluator_rel_only if rel_only else evaluator_abs_only

        if what == "peep":
            peep_2D(surf, P, ofr, V)
        elif what == "backwards":
            RationalPolynomial.search_backwards(P, ofr, V, max_error=max_error,
                    evaluator=evaluator)
        elif what == "forwards":
            RationalPolynomial.search_forwards(P, ofr, V, blitz=blitz,
                    evaluator=evaluator)
        elif what == "approximate":
            rp = RationalPolynomial.approximate(pidxs, qidxs, P, ofr, V)
            print("found:", rp)
            summary_2D(f"CEA {our_name}", rp, surf)
        else:
            assert False, f"invalid what: {what}"
    return wrapped


find_cea_T_cc = cea_approximation("T_cc", "c_t", blitz=0.0,
        pidxs=[1, 3, 5], qidxs=[0, 2, 3, 4, 5])
find_cea_rho_cc = cea_approximation("rho_cc", "rho", blitz=2.0,
        pidxs=[1, 4, 5, 8], qidxs=[0, 1, 2, 4, 8, 9])
find_cea_gamma_tht = cea_approximation("gamma_tht", "t_gamma", rel_only=True,
        pidxs=[0, 1, 2, 3, 4, 5], qidxs=[0, 1, 2, 3, 4, 5, 7, 8, 9])
find_cea_Mw_tht = cea_approximation("Mw_tht", "t_mw", rel_only=True, blitz=0.0,
        pidxs=[2], qidxs=[0, 1, 2, 3, 4, 5])
find_cea_mu_tht = cea_approximation("mu_tht", "t_visc", blitz=0.0,
        pidxs=[2 ,1], qidxs=[0, 1, 2, 3, 4, 5])
find_cea_mu_exit = cea_approximation("mu_exit", "visc", blitz=0.0,
        pidxs=[2 ,1], qidxs=[0, 1, 2, 3, 4, 5])
find_cea_cp_tht = cea_approximation("cp_tht", "t_cp", blitz=0.0,
        pidxs=[2 ,1], qidxs=[0, 1, 2, 3, 4, 5])
find_cea_cp_exit = cea_approximation("cp_exit", "cp", blitz=0.0,
        pidxs=[2 ,1], qidxs=[0, 1, 2, 3, 4, 5])
find_cea_Isp = cea_approximation("Isp", "isp", rel_only=True, blitz=0.0,
        max_error=0.03,
        pidxs=[1, 2, 4, 8, 13], qidxs=[0, 1, 2])



@Masker
def ipa_masker(T, P):
    # Psat check is way worse. thermo.Chemical is pretty shithouse.
    # https://www.desmos.com/calculator/beepq3klam
    A = -1.41
    B = -2.582e-3
    C = 1.97e-3
    max_T = (A + P) / (B + C*P)
    return T <= max_T
ipa_masker.as_string = "x < (y - 1.41) / (1.97e-3 * y - 2.582e-3)"

def ipa_approximation(our_name, ipa_name, pidxs=None, qidxs=None, rel_only=False,
        points=200, spacing=1.25, max_error=0.015, blitz=2.0):
    def wrapped(what=""):
        print(f"approximating IPA {our_name}")
        f = IPA[ipa_name]

        surf = Surfspace(f, 250.0, 500.0, 2.0, 7.0, N0=200, masker=ipa_masker)
        T, P, V = surf.points(
            1000 if what=="approximate" else points,
            spacing=spacing
        )

        evaluator = evaluator_rel_only if rel_only else evaluator_abs_only

        if what == "peep":
            peep_2D(surf, T, P, V)
        elif what == "backwards":
            RationalPolynomial.search_backwards(T, P, V, max_error=max_error,
                    evaluator=evaluator)
        elif what == "forwards":
            RationalPolynomial.search_forwards(T, P, V, blitz=blitz,
                    evaluator=evaluator)
        elif what == "approximate":
            rp = RationalPolynomial.approximate(pidxs, qidxs, T, P, V)
            print("found:", rp)
            summary_2D(f"IPA {our_name}", rp, surf,
                    extra_reqs=[ipa_masker.as_string])
        else:
            assert False, f"invalid what: {what}"
    return wrapped

find_ipa_rho = ipa_approximation("rho", "rho", blitz=0.0,
        pidxs=[0, 1, 2], qidxs=[0, 1, 2, 3, 4])
find_ipa_cp = ipa_approximation("cp", "Cp", blitz=1.0,
        pidxs=[0, 1, 3, 6, 10], qidxs=[0])
find_ipa_mu = ipa_approximation("mu", "mu", spacing=1.15, blitz=2.0,
        pidxs=[0, 1, 3, 4, 6], qidxs=[0, 3, 6])
find_ipa_k = ipa_approximation("k", "k", blitz=0.0,
        pidxs=[0], qidxs=[0, 1, 2, 3])




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

    cmap = matplotlib.cm.get_cmap("tab10", len(ofrs))

    _, ax = new_plots(cols=len(P0_ccs))
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
    V_midm = CEA[name](P0_cc, ofr, cea_AEAT_for(P0_cc, ofr, 0.5))

    M = np.linspace(1.0, M_exit, 100)
    M = np.insert(M, 0, 0.0)
    M = np.insert(M, 1, 0.5)
    f = cea_quadfit_function_of_M(M_midm, M_exit, V_cc, V_tht, V_midm, V_exit)
    return M, f(M)


def find_cea_cp_along():
    def approx(P0_cc, ofr):
        return cea_quadfit("cp", P0_cc, ofr)
    cea_compare_along("mach", "cp", approx)


def cea_mu_as_function_of_M(P0_cc, ofr, M_exit, mu_tht, mu_exit):
    a = (mu_exit - mu_tht) / (M_exit - 1)
    b = mu_tht - a
    return lambda M: a*M + b

def find_cea_mu_along():
    def approx(P0_cc, ofr):
        M_exit = cea("mach", P0_cc, ofr)
        mu_tht = cea("t_visc", P0_cc, ofr)
        mu_exit = cea("visc", P0_cc, ofr)
        M = np.linspace(1.0, M_exit, 100)
        M = np.concatenate((np.array([0.0]), M))
        mu = cea_mu_as_function_of_M(P0_cc, ofr, M_exit, mu_tht, mu_exit)
        return M, mu(M)
    cea_compare_along("mach", "visc", approx)



def find_cea_Pr_along():
    def approx(P0_cc, ofr):
        return cea_quadfit("pran", P0_cc, ofr)
    cea_compare_along("mach", "pran", approx)





def _run():
    # find_cea_T_cc(what="approximate")
    # find_cea_rho_cc(what="approximate")
    # find_cea_gamma_tht(what="approximate")
    # find_cea_Mw_tht(what="approximate")

    # find_cea_mu_tht(what="peep")
    # find_cea_mu_exit(what="peep")
    # find_cea_cp_tht(what="peep")
    # find_cea_cp_exit(what="peep")

    # find_cea_Isp(what="approximate")

    find_cea_cp_along()
    find_cea_mu_along()
    find_cea_Pr_along()

    # find_ipa_rho(what="approximate")
    # find_ipa_cp(what="approximate")
    # find_ipa_mu(what="approximate")
    # find_ipa_k(what="approximate")



def run(view=True, save=False):
    with CEA.configure("LOx", "IPA"), IPA.configure():
        with geez.instance(view, save):
            # stoud redirect nested to not capture meta output.
            with paths.splice_stdout(paths.APPROXIMATOR_OUTPUT, view, save):
                win = geez.new_window()
                summary_1D.window = win
                summary_2D.window = win
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
