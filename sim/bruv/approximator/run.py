"""
Runs the static approximator, outputting all found approximations.
"""

import argparse
import sys

import numpy as np

from .. import paths

from . import geez
from .ratpoly import *
from .geez import new_figure, new_window, no_window
from .space import *

__all__ = ["run", "main"]



def summary_1D(ratpoly, values, X, *, extra_reqs=()):
    approx = ratpoly.eval_coords(X)
    abserr = Evaluator(values).abs_error(approx)
    relerr = Evaluator(values).rel_error(approx)
    print(f"    /* max error of: */")
    print(f"    /*    abs {np.abs(abserr).max()*100:.4g}% */")
    print(f"    /*    rel {np.abs(relerr).max()*100:.4g}% */")
    print(f"    /* requires: */")
    print(f"    /*    x in [{X.min()}, {X.max()}] */")
    for x in extra_reqs:
        print(f"    /*    {x} */")
    print(ratpoly.code())
    _, axes = new_figure(cols=2)
    axes[0].plot(X, values, label="actual function")
    axes[0].plot(X, approx, label="approximation")
    axes[1].plot(X, 100*abserr, label="abs error")
    axes[1].plot(X, 100*relerr, label="rel error")

def summary_2D(ratpoly, values, X, Y, *, extra_reqs=()):
    approx = ratpoly.eval_coords(X, Y)
    abserr = Evaluator(values).abs_error(approx)
    relerr = Evaluator(values).rel_error(approx)
    print(f"    /* max error of: */")
    print(f"    /*    abs {np.abs(abserr).max()*100:.4g}% */")
    print(f"    /*    rel {np.abs(relerr).max()*100:.4g}% */")
    print(f"    /* requires: */")
    print(f"    /*    x in [{X.min()}, {X.max()}] */")
    print(f"    /*    y in [{Y.min()}, {Y.max()}] */")
    for x in extra_reqs:
        print(f"    /*    {x} */")
    print(ratpoly.code())

    fig, axes = new_figure(rows=2, cols=2)
    cont = axes[0, 0].contourf(X, Y, values, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 0])
    axes[0, 0].set_title("actual function")
    axes[0, 0].set_grid("none", "none")
    cont = axes[0, 1].contourf(X, Y, approx, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 1])
    axes[0, 1].set_title("approximation")
    axes[0, 1].set_grid("none", "none")
    cont = axes[1, 0].contourf(X, Y, 100*abserr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 0])
    axes[1, 0].set_title("abs error")
    axes[1, 0].set_grid("none", "none")
    cont = axes[1, 1].contourf(X, Y, 100*relerr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 1])
    axes[1, 1].set_title("rel error")
    axes[1, 1].set_grid("none", "none")



def test1d():
    print("approximating e^x")
    X = linspace(100, 1, 4)
    values = np.exp(X)

    ratpoly, _ = RationalPolynomial.approximate(
        values, X,
        idx_generator=IdxGenerator.infinite(dims=1),
        # idx_generator=IdxGenerator.just((0, 1), (0, 1, 2)),
        evaluator=Evaluator(values, leave_when_better_than=0.012),
        printer=Printer()
    )
    print("found:", ratpoly)
    summary_1D(ratpoly, values, X)



def test2d():
    print("approximating e^(xy/20) + ln(x)")
    X = linspace(100, 1, 3)
    Y = linspace(100, 10, 20)
    X, Y = meshgrid(X, Y)
    values = np.exp(X*Y/20) + np.log(X)

    ratpoly, _ = RationalPolynomial.approximate(
        values, X, Y,
        # idx_generator=IdxGenerator.infinite(dims=1, blitz=2.0),
        idx_generator=IdxGenerator.just((0, 2, 4), (0, 1, 4)),
        evaluator=Evaluator(values, leave_when_better_than=0.012),
        printer=Printer()
    )
    print("found:", ratpoly)
    summary_2D(ratpoly, values, X, Y)




def _run():
    test1d()
    print()
    test2d()





def run(view=True, save=False):
    with geez.instance(view, save):
        # Redirect nested in geez to not capture geez output.
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
