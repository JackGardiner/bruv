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

def summary_2D(ratpoly, values, X, Y, *, extra_reqs=(), trimasker=None):
    values = values.ravel()
    X = X.ravel()
    Y = Y.ravel()

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

    X, Y, values = surf.points(100)

    # RationalPolynomial.search_forwards(X, Y, values,
    #         blitz=3.0, print_all_below=0.02)
    # RationalPolynomial.search_backwards(X, Y, values, max_error=0.02)

    ratpoly, _ = RationalPolynomial.approximate(
        [0, 1, 3, 17], [1, 3, 4, 6, 7, 8, 10, 12], X, Y, values,
    )
    print("found:", ratpoly)
    X_f, Y_f = Evenspace(*surf.bounds()).points(100**2)
    values_f = surf.f(X_f, Y_f)
    summary_2D(ratpoly, values_f, X_f, Y_f)




def find_T_cc():
    f = lambda P, ofr: CEA["c_t"](P, ofr, 1.0)

    surf = Surfspace(f, 0.1, 10.0, 0.8, 8.0, N0=100)
    P, ofr, T_cc = surf.points(800, spacing=1.0, batch_size=1)

    # _, ax = win.new_plots(projection="3d", title="0.1 0.8")
    # ax.scatter(P, ofr, T_cc)

    # surf = Surfspace(f, 0.01, 10.0, 0.1, 8.0, N0=100)
    # P, ofr, T_cc = surf.points(400, spacing=1.0, batch_size=1)

    # _, ax = win.new_plots(projection="3d", title="0.01 0.1")
    # ax.scatter(P, ofr, T_cc)
    # return

    # RationalPolynomial.search_backwards(P, ofr, T_cc, max_error=0.02)
    # RationalPolynomial.search_forwards(P, ofr, T_cc, blitz=4.0, print_all_below=0.01)
    # return

    ratpoly, _ = RationalPolynomial.approximate(
        [0, 4, 5, 19], [1, 4, 5, 8, 9, 13, 19], P, ofr, T_cc
    )
    print("found:", ratpoly)
    P_f, ofr_f = Evenspace(*surf.bounds()).points(100**2)
    T_cc_f = f(P_f, ofr_f)
    summary_2D(ratpoly, T_cc_f, P_f, ofr_f)



def _run():
    # test1d()
    # test2d()
    find_T_cc()





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
