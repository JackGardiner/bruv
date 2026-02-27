"""
Runs the static approximator, outputting all found approximations.
"""

import argparse
import sys
from math import pi

import numpy as np

from .. import paths
from .. import geez
from ..geez import new_figure, new_window, no_window
from .cea import *
from .ratpoly import *
from .space import *

__all__ = ["run", "main"]



def summary_1D(ratpoly, values, X, *, extra_reqs=()):
    approx = ratpoly(X)
    abserr = ratpoly.abs_error(values, X)
    relerr = ratpoly.rel_error(values, X)
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
    approx = ratpoly(X, Y)
    abserr = ratpoly.abs_error(values, X, Y)
    relerr = ratpoly.rel_error(values, X, Y)
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
    axes[0, 0].set_grid("none")
    cont = axes[0, 1].contourf(X, Y, approx, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 1])
    axes[0, 1].set_title("approximation")
    axes[0, 1].set_grid("none")
    cont = axes[1, 0].contourf(X, Y, 100*abserr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 0])
    axes[1, 0].set_title("abs error")
    axes[1, 0].set_grid("none")
    cont = axes[1, 1].contourf(X, Y, 100*relerr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 1])
    axes[1, 1].set_title("rel error")
    axes[1, 1].set_grid("none")

def compare_2D(xlo, xhi, ylo, yhi, f, g):
    X = linspace(100, xlo, xhi)
    Y = linspace(100, ylo, yhi)
    X, Y = meshgrid(X, Y)
    values = f(X, Y)
    approx = g(X, Y)

    abserr = abs_error(values, approx)
    relerr = rel_error(values, approx)

    fig, axes = new_figure(rows=2, cols=2)
    cont = axes[0, 0].contourf(X, Y, values, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 0])
    axes[0, 0].set_title("actual function")
    axes[0, 0].set_grid("none")
    cont = axes[0, 1].contourf(X, Y, approx, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[0, 1])
    axes[0, 1].set_title("approximation")
    axes[0, 1].set_grid("none")
    cont = axes[1, 0].contourf(X, Y, 100*abserr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 0])
    axes[1, 0].set_title("abs error")
    axes[1, 0].set_grid("none")
    cont = axes[1, 1].contourf(X, Y, 100*relerr, levels=100, cmap="viridis")
    fig.colorbar(cont, ax=axes[1, 1])
    axes[1, 1].set_title("rel error")
    axes[1, 1].set_grid("none")



def test1d():
    print("approximating e^x")
    X = linspace(100, 1, 4)
    values = np.exp(X)

    ratpoly, _ = RationalPolynomial.approximate(
        values, X,
        # idx_generator=IdxGenerator.infinite(dims=1),
        idx_generator=IdxGenerator.just((0, 2), (0, 2, 3)),
        evaluator=EvaluatorAbsOnly(leave_when_better_than=0.012),
        printer=Printer()
    )
    print("found:", ratpoly)
    summary_1D(ratpoly, values, X)



def test2d():
    print("approximating e^(xy/20) + ln(x)")
    X = linspace(20, 1, 3)
    Y = linspace(20, 10, 20)
    X, Y = meshgrid(X, Y)
    values = np.exp(X*Y/20) + np.log(X)

    ratpoly, _ = RationalPolynomial.approximate(
        values, X, Y,
        # idx_generator=IdxGenerator.infinite(dims=1, blitz=2.0),
        idx_generator=IdxGenerator.just((0, 2, 4, 5), (0, 1, 4)),
        evaluator=EvaluatorAbsOnly(leave_when_better_than=0.03),
        printer=Printer(),
    )
    print("found:", ratpoly)
    summary_2D(ratpoly, values, X, Y)




def _run():
    f = lambda x, y: 1 + (x - 2)**2 + (y + 3)**2
    xlo = 1.0
    xhi = 4.0
    ylo = -5.0
    yhi = -1.0

    xN = 20
    yN = 50

    print("ND Lookup:")
    X = linspace(xN, xlo, xhi)
    Y = linspace(yN, ylo, yhi)
    X, Y = meshgrid(X, Y)
    values = f(X, Y)
    ndlut = LookupTable(values,
        LookupTable.linear_lookup(xlo, xhi),
        LookupTable.linear_lookup(ylo, yhi),
    )
    compare_2D(xlo, xhi, ylo, yhi, f, ndlut)


    print("ND lookup min/max biased to be exact")
    xlookup = RationalPolynomial(
        Polynomial(2, [0, 1, 3], [(5.0 - 1)/4, -4.0/4, 1.0/4]),
        Polynomial.one(2),
    )
    ylookup = RationalPolynomial(
        Polynomial(2, [0, 2, 5], [9.0/4, 6.0/4, 1.0/4]),
        Polynomial.one(2),
    )
    values = np.array([
        [1.0 + 0.0, 5.0 + 0.0],
        [1.0 + 4.0, 5.0 + 4.0],
    ])
    print(xlookup)
    print(ylookup)
    print(values)
    ndlut = LookupTable(values, xlookup, ylookup)
    compare_2D(xlo, xhi, ylo, yhi, f, ndlut)



    print("ND lookup start/end biased to be exact")
    # YO we can add a tiny inc to y to not have equal start and end points.
    xlookup = RationalPolynomial(
        Polynomial(2, [0, 1, 3], [(5.0 - 2)/3, -4.0/3, 1.0/3]),
        Polynomial.one(2),
    )
    ylookup = RationalPolynomial(
        Polynomial(2, [0, 2, 5], [(9.0 - 3.995)/0.004, 6.0/0.004, 1.0/0.004]),
        Polynomial.one(2),
    )
    X = linspace(2, xlo, xhi)
    Y = linspace(2, ylo, yhi)
    X, Y = meshgrid(X, Y)
    values = f(X, Y) + 0.001*Y # tiny inc
    print(xlookup)
    print(ylookup)
    print(values)
    ndlut = LookupTable(values, xlookup, ylookup)
    compare_2D(xlo, xhi, ylo, yhi, f, ndlut)






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
