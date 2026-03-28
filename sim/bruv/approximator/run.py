"""
Runs the static approximator, outputting all found approximations.
"""

import argparse
import sys
from math import pi

import numpy as np
import matplotlib

from .. import paths
from .. import geez
from ..geez import new_figure, new_plots, new_window, no_window
from .cea import *
from .ratpoly import *
from .space import *

__all__ = ["run", "main"]



def summary_1D(ratpoly, values, X, *, extra_reqs=()):
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
    _, axes = new_plots(cols=2)
    axes[0].plot(X, values, label="actual function")
    axes[0].plot(X, approx, label="approximation")
    axes[1].plot(X, 100*abserr, label="abs error")
    axes[1].plot(X, 100*relerr, label="rel error")

def summary_2D(ratpoly, surf, *, extra_reqs=(), trimasker=None):
    X, Y = Evenspace(*surf.bounds()).points(400**2)
    values = surf.f(X, Y)

    approx = ratpoly(X, Y)
    abserr = np.abs(ratpoly.abs_error(values, X, Y))
    relerr = np.abs(ratpoly.rel_error(values, X, Y))
    print(f"    /* max error of: */")
    print(f"    /*    abs {abserr.max()*100:.3g}% */")
    print(f"    /*    rel {relerr.max()*100:.3g}% */")
    print(f"    /* requires: */")
    print(f"    /*    x in [{X.min()}, {X.max()}] */")
    print(f"    /*    y in [{Y.min()}, {Y.max()}] */")
    for x in extra_reqs:
        print(f"    /*    {x} */")
    print(ratpoly.code())

    tri = matplotlib.tri.Triangulation(X, Y)
    if trimasker is not None:
        Xtri = X[tri.triangles].T
        Ytri = Y[tri.triangles].T
        mask = trimasker(Xtri, Ytri)
        tri.set_mask(~mask)

    fig, axes = new_plots(rows=2, cols=2)
    cont = axes[0,0].tricontourf(tri, values, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 0])
    axes[0,0].set_title("actual function")
    axes[0,0].set_grid("none")
    cont = axes[0,1].tricontourf(tri, approx, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 1])
    axes[0,1].set_title("approximation")
    axes[0,1].set_grid("none")
    cont = axes[1,0].tricontourf(tri, 100*abserr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 0])
    axes[1,0].set_title("abs error")
    axes[1,0].set_grid("none")
    cont = axes[1,1].tricontourf(tri, 100*relerr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 1])
    axes[1,1].set_title("rel error")
    axes[1,1].set_grid("none")


def peep_2D(surf, *points):
    _, ax = new_plots(projection="3d")
    X, Y = Evenspace(*surf.bounds()).points(200**2, flatten=False)
    # ax.plot_surface(X, Y, surf.f(X, Y), cmap="viridis")
    ax.scatter(*points)



def test1d():
    print("approximating e^x")
    surf = Surfspace(np.exp, 1, 4)

    X, values = surf.points(100)

    # RationalPolynomial.search_forwards(X, values, print_all_below=0.0)
    # RationalPolynomial.search_backwards(X, values, max_error=0.01)

    ratpoly, _ = RationalPolynomial.approximate(
        [0, 2], [0, 2, 3], X, values,
    )
    print("found:", ratpoly)
    X_f = Evenspace(*surf.bounds()).points(1000)
    values_f = surf.f(X_f)
    summary_1D(ratpoly, values_f, X_f)


def test2d():
    print("approximating e^(xy/10) + ln(x)")
    surf = Surfspace(
        lambda X, Y: np.exp(X*Y/10) + np.log(X),
        1, 3,
        10, 20
    )

    X, Y, values = surf.points(200)

    # RationalPolynomial.search_forwards(X, Y, values,
    #         blitz=3.0, print_all_below=0.02)
    # RationalPolynomial.search_backwards(X, Y, values, max_error=0.02)

    RationalPolynomialSum.search_forwards(X, Y, values, blitz=3.0, starting_cost=20)
    ratpoly, app = RationalPolynomial.multiapproximate(
        [0, 1, 3, 17], [1, 3, 4, 6, 7, 8, 10, 12], X, Y, values,
    )
    print(evaluator_abs_only(values, app))
    print("found:", ratpoly)
    summary_2D(ratpoly, surf)




def cea_approximation(our_name, cea_name, pidxs=None, qidxs=None, rel_only=False,
        points=200, spacing=1.25, max_error=0.02, blitz=2.0):
    def wrapped(what=""):
        print(f"approximating {our_name}")
        f = lambda P, ofr: CEA[cea_name](P, ofr, 1.0)

        surf = Surfspace(f, 1.0, 5.0, 0.5, 3.0, N0=200)
        P, ofr, V = surf.points(
            1000 if what=="approximate" else points,
            spacing=spacing
        )

        evaluator = evaluator_rel_only if rel_only else evaluator_abs_only

        RationalPolynomialSum.search_forwards(P, ofr, V, blitz=blitz,
                evaluator=evaluator)

        if what == "peep":
            peep_2D(surf, P, ofr, V)
        elif what == "backwards":
            RationalPolynomial.search_backwards(P, ofr, V, max_error=max_error,
                    evaluator=evaluator)
        elif what == "forwards":
            RationalPolynomial.search_forwards(P, ofr, V, blitz=blitz,
                    evaluator=evaluator)
        elif what == "approximate":
            ratpoly, _ = RationalPolynomial.approximate(pidxs, qidxs, P, ofr, V)
            print("found:", ratpoly)
            summary_2D(ratpoly, surf)
        else:
            assert False
    return wrapped


find_T_cc = cea_approximation("T_cc", "c_t", blitz=3.0,
        pidxs=[0, 1, 2, 3, 4, 5], qidxs=[0, 1, 2, 3, 4, 5, 7])
find_gamma_tht = cea_approximation("gamma_tht", "t_gamma", rel_only=True,
        # pidxs=[0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
        # qidxs=[0, 1, 2, 3, 5, 6, 7, 8, 10, 11, 13, 14])
        pidxs=[0, 1, 2, 3, 4, 5],
        qidxs=[0, 1, 2, 3, 4, 5, 7, 8, 9, 11, 12, 13, 14])
find_Mw_tht = cea_approximation("Mw_tht", "t_mw", rel_only=True, blitz=3.0,
        # pidxs=[0, 5], qidxs=[0, 5, 8, 9])
        pidxs=[0, 2, 4, 5], qidxs=[1, 5])


def _run():

    find_T_cc(what="backwards")
    # [0, 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14], [0, 1, 2, 3, 5, 6, 7, 9, 10, 11, 12, 13, 14]
    find_gamma_tht(what="backwards")
    find_Mw_tht(what="fowards")



def run(view=True, save=False):
    with CEA.configure("LOx", "IPA"):
        with geez.instance(view, save):
            # stoud redirect nested to not capture meta output.
            with paths.splice_stdout(paths.APPROXIMATOR_OUTPUT, view, save):
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
