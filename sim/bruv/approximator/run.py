"""
Runs the static approximator, outputting all found approximations.
"""

import argparse
import sys

from .. import paths

from . import geez
from .ratpoly import *
from .geez import new_figure, new_window, no_window
from .space import *

__all__ = ["run", "main"]


def _run():
    print("testing, blow me please")
    _, ax = new_figure("feel the tension", title="Feel the Tension.")
    X = linspace(100, 0, 1)
    ax.plot(X, X**2 - X + 3)

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
