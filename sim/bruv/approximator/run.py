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
    abserr = ratpoly.abs_error(values, X, Y)
    relerr = ratpoly.rel_error(values, X, Y)
    print(f"    /* max error of: */")
    print(f"    /*    abs {np.abs(abserr).max()*100:.3g}% */")
    print(f"    /*    rel {np.abs(relerr).max()*100:.3g}% */")
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

    X_f = Evenspace(*surf.bounds()).points(1000)
    values_f = surf.f(X_f)

    ratpoly, _ = RationalPolynomial.approximate(
        values, X,
        idx_generator=IdxGenerator.just([0, 2], [0, 2, 3]),
        evaluator=EvaluatorAbsOnly(leave_when_better_than=0.012),
        printer=Printer()
    )
    print("found:", ratpoly)
    summary_1D(ratpoly, values_f, X_f)


def test2d():
    print("approximating e^(xy/10) + ln(x)")
    surf = Surfspace(
        lambda X, Y: np.exp(X*Y/10) + np.log(X), #+ 10*np.exp(-10*(X - 2)**2),
        1, 3,
        10, 20
    )

    X, Y, values = surf.points(100)
    # X, Y = Evenspace(*surf.bounds()).points(100)
    # values = surf.f(X, Y)

    X_f, Y_f = Evenspace(*surf.bounds()).points(100**2)
    values_f = surf.f(X_f, Y_f)

    _, ax = new_plots(projection="3d")
    ax.scatter(X, Y, values)

    ratpoly, _ = RationalPolynomial.approximate(
        values, X, Y,
        # idx_generator=IdxGenerator.infinite(ndim=2, blitz=2.0),
        idx_generator=IdxGenerator.just(list(range(20)), list(range(20))),
        evaluator=EvaluatorAbsOnly(leave_when_better_than=0.05),
        printer=Printer(),
    )
    print("found:", ratpoly)
    summary_2D(ratpoly, values_f, X_f, Y_f)




def _run():
    # test1d()
    test2d()






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
