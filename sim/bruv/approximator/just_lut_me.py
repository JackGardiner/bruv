"""
Runs the static approximator, outputting all found approximations.
"""

import argparse
import functools
import itertools
import sys
from math import pi, prod

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



def lerp(a, b, t):
    return a + (b - a)*t




def secant_root_find(func, x0, x1, tol=1e-4, max_iter=15, minx=1.0001):
    f0 = func(x0)
    if abs(f0) < tol:
        return x0
    f1 = func(x1)
    if abs(f1) < tol:
        return x1
    for _ in range(max_iter):
        if f1 == f0:
            break # Prevent division by zero if the function flatlines
        # Secant step
        x2 = x1 - f1 * (x1 - x0) / (f1 - f0)
        # Physics constraint: Supersonic area ratio must remain > 1.0
        x2 = max(x2, minx)
        f2 = func(x2)
        if abs(f2) < tol:
            return x2
        # Shift variables for the next iteration
        x0, f0 = x1, f1
        x1, f1 = x2, f2
    return x1 # Return the best approximation if max_iters is reached



def isentropic_M_from_P_on_P0(P_on_P0, y):
    return (2/(y - 1)*(P_on_P0 ** ((1 - y)/y) - 1)) ** 0.5
def isentropic_A_on_Astar(M, y):
    n = 0.5*(y + 1)/(y - 1)
    return (2/(y + 1.0) + M*M/n/2)**n / M

@np.vectorize
def AEAT_mach(P, ofr, M_exit):
    """ Returns the AEAT which gets the given exit mach number. """
    # Get an initial guess using some assumptions.
    gamma_tht = CEA["t_gamma"](P, ofr, 1.0)
    AEAT_guess = isentropic_A_on_Astar(M_exit, gamma_tht)
    # Root find the exact area ratio
    return secant_root_find(
        # Use relative error so a tolerance like 1e-4 means 0.01% error.
        func=lambda AEAT: (CEA["mach"](P, ofr, AEAT) / M_exit) - 1.0,
        x0=AEAT_guess,
        x1=lerp(1, AEAT_guess, 0.95), # slight perturb.
        tol=1e-5, # Solve to <0.01%
        max_iter=10
    )

@np.vectorize
def AEAT_perfexp(P, ofr, P_exit=101325.0):
    """ Returns the AEAT which is a perfectly expanded nozzle. """
    # Get an initial guess using some assumptions.
    gamma_tht = CEA["t_gamma"](P, ofr, 1.0)
    M_exit_guess = isentropic_M_from_P_on_P0(P_exit / (P*1e6), gamma_tht)
    AEAT_guess = isentropic_A_on_Astar(M_exit_guess, gamma_tht)
    # Root find the exact area ratio
    return secant_root_find(
        # Use relative error so a tolerance like 1e-4 means 0.01% error.
        func=lambda AEAT: (CEA["p"](P, ofr, AEAT) / P_exit) - 1.0,
        x0=AEAT_guess,
        x1=lerp(1, AEAT_guess, 0.95), # slight perturb.
        tol=1e-5, # Solve to <0.01%
        max_iter=10
    )

@np.vectorize
def AEAT_perfexp_guess(P, ofr, P_exit=101325.0):
    """ Returns the AEAT which is a perfectly expanded nozzle. """
    # Get an initial guess using some assumptions.
    gamma_tht = CEA["t_gamma"](P, ofr, 1.0)
    M_exit_guess = isentropic_M_from_P_on_P0(P_exit / (P*1e6), gamma_tht)
    return isentropic_A_on_Astar(M_exit_guess, gamma_tht)

@np.vectorize
def M_exit_perfexp(P, ofr, P_exit=101325.0):
    """ Returns the M_exit of a perfectly expanded nozzle. """
    return CEA["mach"](P, ofr, AEAT_perfexp(P, ofr, P_exit))




summary_window = None

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
        self.tbl = self.tbl.astype(np.float32)

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

        X, Y = Evenspace(*self.surf.bounds()).points(2**2, flatten=False)
        values = self.surf.f(X, Y)
        approx = self(X, Y)
        mask = np.isnan(values) | np.isnan(approx)
        values[mask] = np.nan
        approx[mask] = np.nan
        abserr = np.abs(abs_error(values, approx))
        relerr = np.abs(rel_error(values, approx))

        fig, axes = summary_window.new_plots(rows=2, cols=2,
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
        lines.append(f"    #include \"tbl/{self.name}.i\"")
        lines.append(f"")

        print("\n".join(lines))

        # now write the table tile.
        lines = [
            f"/* LUT for {self.name} */",
            f"const f64 XLO = {xlo};",
            f"const f64 XHI = {xhi};",
            f"const f64 YLO = {ylo};",
            f"const f64 YHI = {yhi};",
            f"enum {{ XLEN = {rows},",
            f"       YLEN = {cols}, }};",
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




class JustGimmeATable3D:
    def __init__(self, surf, name, size_P, size_ofr, size_AEAT):
        self.surf = surf
        self.name = name
        self.shape = (size_P, size_ofr, size_AEAT)
        X = np.linspace(surf.xlo, surf.xhi, size_P)
        Y = np.linspace(surf.ylo, surf.yhi, size_ofr)
        Z = np.linspace(surf.zlo, surf.zhi, size_AEAT)
        X, Y, Z = np.meshgrid(X, Y, Z, indexing="ij")
        if surf.masker is not None:
            mask = surf.masker.coords(X, Y)
            X[~mask] = np.nan
            Y[~mask] = np.nan
            Z[~mask] = np.nan
        self.tbl = surf.f(X, Y, Z)
        self.tbl = self.tbl.flatten("C") # rowmajor.
        self.tbl = self.tbl.astype(np.float32)

    def __call__(self, x, y, z):
        xlo, xhi, ylo, yhi, zlo, zhi = self.surf.bounds()
        x = (x - xlo) / (xhi - xlo)
        y = (y - ylo) / (yhi - ylo)
        z = (z - zlo) / (zhi - zlo)
        x *= self.shape[0] - 1
        y *= self.shape[1] - 1
        z *= self.shape[2] - 1
        i = np.floor(x).astype(int)
        j = np.floor(y).astype(int)
        k = np.floor(z).astype(int)
        i = np.maximum(i, 0)
        j = np.maximum(j, 0)
        k = np.maximum(k, 0)
        i = np.minimum(i, self.shape[0] - 2)
        j = np.minimum(j, self.shape[1] - 2)
        k = np.minimum(k, self.shape[2] - 2)
        t = x - i
        u = y - j
        v = z - k
        c000 = self.tbl[(   i *self.shape[1] + j  )*self.shape[2] + k  ]
        c001 = self.tbl[(   i *self.shape[1] + j  )*self.shape[2] + k+1]
        c010 = self.tbl[(   i *self.shape[1] + j+1)*self.shape[2] + k  ]
        c011 = self.tbl[(   i *self.shape[1] + j+1)*self.shape[2] + k+1]
        c100 = self.tbl[((1+i)*self.shape[1] + j  )*self.shape[2] + k  ]
        c101 = self.tbl[((1+i)*self.shape[1] + j  )*self.shape[2] + k+1]
        c110 = self.tbl[((1+i)*self.shape[1] + j+1)*self.shape[2] + k  ]
        c111 = self.tbl[((1+i)*self.shape[1] + j+1)*self.shape[2] + k+1]
        c00 = lerp(c000, c001, v)
        c01 = lerp(c010, c011, v)
        c10 = lerp(c100, c101, v)
        c11 = lerp(c110, c111, v)
        c0 = lerp(c00, c01, u)
        c1 = lerp(c10, c11, u)
        return lerp(c0, c1, t)

    def run(self, extra_reqs=()):
        lines = []

        xlo, xhi, ylo, yhi, zlo, zhi = self.surf.bounds()

        cols = [0.5, 2.0, 3.0, 4.0]
        fig, axes = summary_window.new_plots(rows=3, cols=len(cols),
                title=f"LUT {self.name}")
        for i, AEAT in enumerate(cols):
            X, Y = Evenspace(xlo, xhi, ylo, yhi).points(2**2, flatten=False)
            values = self.surf.f(X, Y, AEAT)
            approx = self(X, Y, AEAT)
            mask = np.isnan(values) | np.isnan(approx)
            values[mask] = np.nan
            approx[mask] = np.nan
            abserr = np.abs(abs_error(values, approx))
            # relerr = np.abs(rel_error(values, approx))

            cont = axes[0,i].contourf(X, Y, values, levels=300, cmap="viridis")
            fig.colorbar(cont, ax=axes[0,i])
            axes[0,i].set_title("actual function")
            axes[0,i].set_grid("none")
            cont = axes[1,i].contourf(X, Y, approx, levels=300, cmap="viridis")
            fig.colorbar(cont, ax=axes[1,i])
            axes[1,i].set_title("approximation")
            axes[1,i].set_grid("none")
            cont = axes[2,i].contourf(X, Y, 100*abserr, levels=300, cmap="viridis")
            fig.colorbar(cont, ax=axes[2,i])
            axes[2,i].set_title("abs error %")
            axes[2,i].set_grid("none")

        X, Y, Z = Evenspace(*self.surf.bounds()).points(2**3, flatten=False)
        values = self.surf.f(X, Y, Z)
        approx = self(X, Y, Z)
        mask = np.isnan(values) | np.isnan(approx)
        values[mask] = np.nan
        approx[mask] = np.nan
        abserr = np.abs(abs_error(values, approx))
        relerr = np.abs(rel_error(values, approx))


        lines.extend([
            f"    /* evenly-spaced flattened (C-ordered) 3D LUT */",
            f"    /* max error of: */",
            f"    /*   abs {np.nanmax(abserr)*100:.3g}% */",
            f"    /*   rel {np.nanmax(relerr)*100:.3g}% */",
        ])
        if extra_reqs:
            lines.append(f"    /* also requires: */")
            for x in extra_reqs:
                lines.append(f"    /*   {x} */")
        lines.append(f"    #include \"tbl/{self.name}.i\"")
        lines.append(f"")

        print("\n".join(lines))

        # now write the table tile.
        lines = [
            f"/* LUT for {self.name} */",
            f"const f64 XLO = {xlo};",
            f"const f64 XHI = {xhi};",
            f"const f64 YLO = {ylo};",
            f"const f64 YHI = {yhi};",
            f"const f64 ZLO = {zlo};",
            f"const f64 ZHI = {zhi};",
            f"enum {{ XLEN = {self.shape[0]},",
            f"       YLEN = {self.shape[1]},",
            f"       ZLEN = {self.shape[2]}, }};",
        ]
        lines.append(f"static const f32 tbl[{self.shape[0]*self.shape[1]*self.shape[2]}] = {{")

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




def find_approximation(get_surf, type_name, var_name, size, extra_reqs=()):
    def wrapped(what=""):
        if what == "approximate":
            print(f"approximating {type_name} {var_name}")

        surf = get_surf() # evaluate now rather than on init.
        if len(size) == 2:
            tbl = JustGimmeATable(surf, f"{type_name.lower()}_{var_name}", *size)
        else:
            tbl = JustGimmeATable3D(surf, f"{type_name.lower()}_{var_name}", *size)

        if what == "get":
            return tbl
        elif what == "approximate":
            tbl.run(extra_reqs=extra_reqs)
        else:
            assert False, f"invalid what: {what}"
    return wrapped



SIZE2 = (100, 100)
SIZE3_SUP = (60, 60, 100)
SIZE3_SUB = (60, 60, 100)

def cea_approximation2(our_name, cea_name, size=SIZE2, f=None, **kwargs):
    if f is None:
        f = lambda P, ofr: CEA[cea_name](P, ofr, 1.0)
    surf = lambda: Evenspace(1.0, 6.0, 0.5, 3.0).setf(f)
    return find_approximation(surf, "CEA", our_name, size, **kwargs)

def cea_approximation3(our_name, cea_name, size=SIZE3_SUP, only="sup", **kwargs):
    f = lambda P, ofr, AEAT: CEA[cea_name](P, ofr, AEAT)
    surf = lambda: Evenspace(1.0, 6.0, 0.5, 3.0,
            0.1 if only=="sub" else 1.0,
            1.0 if only=="sub" else 10.0,
        ).setf(f)
    return find_approximation(surf, "CEA", our_name, size, **kwargs)

find_cea_perfexp_AEAT = cea_approximation2("perfexp_AEAT", "perfexp_AEAT",
        f=AEAT_perfexp)

find_cea_Ivac = cea_approximation3("Ivac", "ivac", only="sup")

find_cea_sup_T = cea_approximation3("sup_T", "t", SIZE3_SUP, only="sup")
find_cea_sup_P = cea_approximation3("sup_P", "p", SIZE3_SUP, only="sup")
find_cea_sup_rho = cea_approximation3("sup_rho", "rho", SIZE3_SUP, only="sup")
find_cea_sup_M = cea_approximation3("sup_M", "mach", SIZE3_SUP, only="sup")
find_cea_sup_a = cea_approximation3("sup_a", "son", SIZE3_SUP, only="sup")
find_cea_sup_gamma = cea_approximation3("sup_gamma", "gamma", SIZE3_SUP, only="sup")
find_cea_sup_cp = cea_approximation3("sup_cp", "cp", SIZE3_SUP, only="sup")
find_cea_sup_mu = cea_approximation3("sup_mu", "visc", SIZE3_SUP, only="sup")
find_cea_sup_Pr = cea_approximation3("sup_Pr", "pran", SIZE3_SUP, only="sup")

find_cea_sub_T = cea_approximation3("sub_T", "t", SIZE3_SUB, only="sub")
find_cea_sub_P = cea_approximation3("sub_P", "p", SIZE3_SUB, only="sub")
find_cea_sub_rho = cea_approximation3("sub_rho", "rho", SIZE3_SUB, only="sub")
find_cea_sub_M = cea_approximation3("sub_M", "mach", SIZE3_SUB, only="sub")
find_cea_sub_a = cea_approximation3("sub_a", "son", SIZE3_SUB, only="sub")
find_cea_sub_gamma = cea_approximation3("sub_gamma", "gamma", SIZE3_SUB, only="sub")
find_cea_sub_cp = cea_approximation3("sub_cp", "cp", SIZE3_SUB, only="sub")
find_cea_sub_mu = cea_approximation3("sub_mu", "visc", SIZE3_SUB, only="sub")
find_cea_sub_Pr = cea_approximation3("sub_Pr", "pran", SIZE3_SUB, only="sub")

find_cea_cc_T = cea_approximation2("cc_T", "c_t")
find_cea_cc_P = cea_approximation2("cc_P", "c_p")
find_cea_cc_rho = cea_approximation2("cc_rho", "c_rho")
find_cea_cc_a = cea_approximation2("cc_a", "c_son")
find_cea_cc_gamma = cea_approximation2("cc_gamma", "c_gamma")
find_cea_cc_cp = cea_approximation2("cc_cp", "c_cp")
find_cea_cc_mu = cea_approximation2("cc_mu", "c_visc")
find_cea_cc_Pr = cea_approximation2("cc_Pr", "c_pran")



def _run():

    from contextlib import contextmanager
    import time
    @contextmanager
    def timer(title, count=None):
        start = time.perf_counter()
        try:
            yield
        finally:
            dt = time.perf_counter() - start
            if count is None:
                print(f"{title}: {dt:.6f} s")
            elif dt > 0:
                per = dt / count
                n_3h = int(count * (3 * 3600) / dt)
                print(f"{title}: {dt:.6f} s "
                      f"({count} iters; {per*1e3:.1f} ms/iter; "
                      f"{n_3h:,} iters in 3 hr)")
            else:
                print(f"{title}: {dt:.6f} s "
                      f"({count} iters; too fast to extrapolate)")


    # _, ax = geez.new_plots()
    # P = np.linspace(1.0, 6.0, 50)
    # ofr = 1.4
    # ax.plot(P, AEAT_perfexp(P, ofr))
    # ax.plot(P, AEAT_perfexp_guess(P, ofr), ls="--")
    # return

    # with timer("AEAT", count=prod(SIZE2)):
    #     find_cea_perfexp_AEAT(what="approximate")
    # with timer("Ivac", count=prod(SIZE3_SUP)):
    #     find_cea_Ivac(what="approximate")
    # with timer("T", count=prod(SIZE3_SUP)):
    #     find_cea_T(what="approximate")

    with timer("AEAT"):
        find_cea_perfexp_AEAT(what="approximate")

    with timer("SUPER"):
        find_cea_Ivac(what="approximate")

        find_cea_sup_T(what="approximate")
        find_cea_sup_P(what="approximate")
        find_cea_sup_rho(what="approximate")
        find_cea_sup_M(what="approximate")
        find_cea_sup_a(what="approximate")
        find_cea_sup_gamma(what="approximate")
        find_cea_sup_cp(what="approximate")
        find_cea_sup_mu(what="approximate")
        find_cea_sup_Pr(what="approximate")

    with timer("SUB"):
        find_cea_sub_T(what="approximate")
        find_cea_sub_P(what="approximate")
        find_cea_sub_rho(what="approximate")
        find_cea_sub_M(what="approximate")
        find_cea_sub_a(what="approximate")
        find_cea_sub_gamma(what="approximate")
        find_cea_sub_cp(what="approximate")
        find_cea_sub_mu(what="approximate")
        find_cea_sub_Pr(what="approximate")

    with timer("CC"):
        find_cea_cc_T(what="approximate")
        find_cea_cc_P(what="approximate")
        find_cea_cc_rho(what="approximate")
        find_cea_cc_a(what="approximate")
        find_cea_cc_gamma(what="approximate")
        find_cea_cc_cp(what="approximate")
        find_cea_cc_mu(what="approximate")
        find_cea_cc_Pr(what="approximate")




def run(view=True, save=False):
    global summary_window
    with CEA.configure("LOx", "IPA"), IPA.configure(), Ethanol.configure():
        with geez.instance(view, save):
            # stoud redirect nested to not capture meta output.
            with paths.splice_stdout(paths.APPROXIMATOR_OUTPUT, view, save):
                summary_window = geez.new_window("summary", emptyok=True)
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
