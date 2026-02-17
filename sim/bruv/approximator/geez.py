"""
Plotting and data viewing.
"""

import argparse
import contextlib
from pathlib import Path

import tkinter
import tkinter.ttk
import numpy as np
import matplotlib.axes
import matplotlib.pyplot as plt
import matplotlib.ticker
import matplotlib.backends.backend_tkagg

from .. import paths

__all__ = ["FIGURE_DIRECTORY", "DEFAULT_FIGSIZE",
           "new_figure", "new_window", "no_window",
           "instance"]


FIGURE_DIRECTORY = paths.APPROXIMATOR_FIGS
DEFAULT_FIGSIZE = (8, 6)

new_figure = lambda path=None, rows=1, cols=1, *, overwrite=False, title=None, \
                    **kwargs: "fig", "axes"
# ax.plot = lambda X, Y, *args, label=None, legended=None, inlined_at_x=None, \
#                  inlined_at_y=None, inlined_offset=0.0, **kwargs: None
# ax.default_legend_location = str
# ax.set_grid = lambda axis=["both", "x", "y", "none"][0], \
#                      minor=["both", "x", "y", "none"][0]: None
# ax.origin_axes = lambda axis=["both", "x", "y", "none"][0]: None
# ax.ticks_every = lambda *, xmajor=[None, float, "none", "auto"][0], \
#                         xminor=[None, float, "none", "auto"][0], \
#                         ymajor=[None, float, "none", "auto"][0], \
#                         yminor=[None, float, "none", "auto"][0]: None
# ax.mark_point = lambda x, y, text=None, ha=0, va=1, tha=None, tva=None, \
#                        col=None, m_size=6, m_shape="o", \
#                        e_col=None, e_th=0, \
#                        t_col=None, t_size=10, bold=False, box=True, \
#                        rotation="horizontal": None

new_window = lambda title=None: "window"
# window.new_figure = <mimic of new_figure, where each figure is in a new tab>

no_window = lambda: "no_window" # exists only to maintain api with window.
# no_window.new_figure = <mimic of new_figure, identically>

instance = lambda view=True, save=False: "context"
# with instance():
#    ... # all figure functions are valid ONLY HERE



class BrAxes(matplotlib.axes.Axes):
    name = "br_axes"

    _AXIS_X = 1
    _AXIS_Y = 2
    _AXIS_LOOKUP = {
        "none": 0,
        "x": _AXIS_X,
        "y": _AXIS_Y,
        "both": _AXIS_X | _AXIS_Y,
    }
    _AXIS_NAME = {v: k for k, v in _AXIS_LOOKUP.items()}

    # Private/updating existing:

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.set_grid()
        self.default_legend_location = "best"
        self._black_axis_x = None
        self._black_axis_y = None
        self._locked_aspect = False

    def _check_for_label(self, artist):
        if hasattr(artist, "get_label"):
            label = artist.get_label()
            if label and not label.startswith("_"):
                handles, labels = self.get_legend_handles_labels()
                if labels:
                    self.legend(handles, labels,
                            loc=self.default_legend_location)
    def add_line(self, line, *args, **kwargs):
        result = super().add_line(line, *args, **kwargs)
        self._check_for_label(line)
        return result
    def add_patch(self, patch, *args, **kwargs):
        result = super().add_patch(patch, *args, **kwargs)
        self._check_for_label(patch)
        return result
    def add_collection(self, collection, *args, **kwargs):
        result = super().add_collection(collection, *args, **kwargs)
        self._check_for_label(collection)
        return result
    def add_artist(self, artist, *args, **kwargs):
        result = super().add_artist(artist, *args, **kwargs)
        self._check_for_label(artist)
        return result

    def set_aspect(self, *args, **kwargs):
        if self._locked_aspect:
            raise ValueError("cannot change locked aspect (which is locked due "
                    "to inline labelling)")
        super().set_aspect(*args, **kwargs)

    def plot(self, X, Y, *args, label=None, legended=None, inlined_at_x=None,
            inlined_at_y=None, inlined_offset=0.0, **kwargs):
        if inlined_at_x is not None and inlined_at_y is not None:
            raise ValueError("must specify only one x or y to inline label at")
        inlined = (inlined_at_x is not None) or (inlined_at_y is not None)
        if legended is None and label is not None:
            legended = not inlined
        legended = bool(legended)
        if (legended or inlined) and label is None:
            raise ValueError("cannot legend or inline label without a label")
        # Maybe be a little extra permissive.
        # if (not legended and not inlined) and label is not None:
        #     raise ValueError("no reason to label without legended or inlined")
        if legended:
            kwargs["label"] = label
        lines = super().plot(X, Y, *args, **kwargs)
        if inlined:
            x, y, a = self._get_coords_angle(X, Y, x=inlined_at_x,
                    y=inlined_at_y, off=inlined_offset)
            self.annotate(label,
                    xy=(x, y),
                    rotation=a, rotation_mode="anchor",
                    ha="center", va="center",
                    color=lines[0].get_color(),
                    fontsize=10, fontweight="bold",
                    bbox=dict(facecolor="white", edgecolor="white",
                        boxstyle="round,pad=0.1"),
                )
        return lines

    def _get_coords_angle(self, X, Y, x=None, y=None, off=0.0):
        assert len(X) == len(Y)
        assert (x is None) != (y is None)
        assert (x == x) and (y == y)
        aspect = self.get_aspect()
        if not isinstance(aspect, float):
            raise ValueError("cannot inline label without fixing an aspect "
                    "first (via .set_aspect)")
        self._locked_aspect = True
        swap = False
        aspecty = aspect
        aspectx = 1.0
        if x is None:
            swap = True
            X, Y = Y, X
            aspectx, aspecty = aspecty, aspectx
            x = y
            y = None
            off = -off
        mask = (~isnan(X)) & (~isnan(Y))
        assert mask.any()
        X = X[mask]
        Y = Y[mask]
        assert (np.diff(X) > 0).all(), "not monotonically increasing"
        y = interp(x, X, Y)
        i = idxof(X, x)
        if (i == 0 and x < X[i]) or (i == len(X)-1 and x > X[i]) or len(X) == 1:
            dy = 1.0 if swap else 0.0
            dx = 0.0 if swap else 1.0
        else:
            if (x < X[i] and i > 0) or (i == len(X)-1):
                i -= 1
            dy = Y[i + 1] - Y[i]
            dx = X[i + 1] - X[i]
        x += off * (-aspecty*dy/sqrt(dx**2 + dy**2))
        y += off * (+aspectx*dx/sqrt(dx**2 + dy**2))
        angle = atand2(aspecty*dy, aspectx*dx)
        if swap:
            x, y = y, x
            angle = 90.0 - angle
        return x, y, angle


    # Public:

    def set_grid(self, axis="both", minor="both"):
        nulloc = matplotlib.ticker.NullLocator
        autloc = matplotlib.ticker.AutoLocator
        autminloc = matplotlib.ticker.AutoMinorLocator
        if axis not in self._AXIS_LOOKUP:
            raise ValueError(f"unrecognised axis: {repr(axis)}")
        if minor not in self._AXIS_LOOKUP:
            raise ValueError(f"unrecognised minor: {repr(minor)}")
        axis = self._AXIS_LOOKUP[axis]
        minor = self._AXIS_LOOKUP[minor]
        minor &= axis
        self.grid(False, axis="both", which="both")
        def isdflt(loc):
            if not isinstance(loc, (nulloc, autloc, autminloc)):
                return False
            if getattr(loc, "br_not_default", False):
                return False
            return True
        if isdflt(self.xaxis.get_minor_locator()):
            xminloc = autminloc() if (minor & self._AXIS_X) else nulloc()
            self.xaxis.set_minor_locator(xminloc)
        if isdflt(self.yaxis.get_minor_locator()):
            yminloc = autminloc() if (minor & self._AXIS_Y) else nulloc()
            self.yaxis.set_minor_locator(yminloc)
        if not axis:
            return
        self.grid(True, axis=self._AXIS_NAME[axis],
                which="major", lw=1.0, ls="-", alpha=0.8)
        if not minor:
            return
        self.grid(True, axis=self._AXIS_NAME[minor],
                which="minor", lw=0.6, ls="--", alpha=0.5)

    def origin_axes(self, axis="both"):
        if axis not in self._AXIS_LOOKUP:
            raise ValueError(f"unrecognised axis: {repr(axis)}")
        axis = self._AXIS_LOOKUP[axis]
        style = dict(ls="-", lw=1.1, alpha=0.9, color="black")
        if self._black_axis_x is not None:
            self._black_axis_x.remove()
            self._black_axis_x = None
        if self._black_axis_y is not None:
            self._black_axis_y.remove()
            self._black_axis_y = None
        if axis & self._AXIS_X:
            self._black_axis_x = self.axhline(0.0, **style)
        if axis & self._AXIS_Y:
            self._black_axis_y = self.axvline(0.0, **style)

    def ticks_every(self, *, xmajor=None, xminor=None, ymajor=None, yminor=None):
        mulloc = matplotlib.ticker.MultipleLocator
        nulloc = matplotlib.ticker.NullLocator
        autloc = matplotlib.ticker.AutoLocator
        autminloc = matplotlib.ticker.AutoMinorLocator
        def set_loc(ax, loc, isminor):
            if loc is None:
                return
            if isinstance(loc, str):
                if loc == "none":
                    loc = nulloc()
                elif loc == "auto":
                    loc = autminloc() if isminor else autloc()
                else:
                    raise ValueError(f"unrecognised loc: {repr(loc)}")
            else:
                loc = mulloc(loc)
            loc.br_not_default = True
            setter = ax.set_minor_locator if isminor else set_major_locator
            setter(loc)
        set_loc(self.xaxis, xmajor, isminor=False)
        set_loc(self.xaxis, xminor, isminor=True)
        set_loc(self.yaxis, ymajor, isminor=False)
        set_loc(self.yaxis, yminor, isminor=True)

    def mark_point(self, x, y, text=None, ha=0, va=1, tha=None, tva=None,
            col=None, m_size=6, m_shape="o",
            e_col=None, e_th=0,
            t_col=None, t_size=10, bold=False, box=True,
            rotation="horizontal"):
        if e_col is None:
            e_col = col if m_shape in {"_", "|"} else "black"
        if m_size > 0:
            handle, = self.plot(x, y,
                    linestyle="",
                    marker=m_shape,
                    markersize=m_size,
                    markerfacecolor=col,
                    markeredgecolor=e_col,
                    markeredgewidth=e_th,
                )
        else:
            handle = None
        if text is not None:
            if t_col is None:
                if handle is None:
                    t_col = "black" if col is None else col
                else:
                    t_col = handle.get_markerfacecolor()
            omag = 8 if box else 5
            bbox = None if not box else dict(
                    boxstyle="round", fc="white", ec="grey", alpha=0.8,
                )
            if tha is None:
                tha = ["center", "right", "left"][(ha < 0) + 2*(ha > 0)]
            if tva is None:
                tva = ["center", "top", "bottom"][(va < 0) + 2*(va > 0)]
            self.annotate(text,
                    xy=(x, y),
                    xytext=(omag*ha, omag*va),
                    textcoords="offset points",
                    color=t_col,
                    ha=tha,
                    va=tva,
                    fontsize=t_size,
                    fontweight="bold" if bold else None,
                    bbox=bbox,
                    rotation=rotation,
                )

plt.rcParams.update({
    # yeah just can it.
    "figure.max_open_warning": 0,

    # Makes ticks bigger and bidirectional.
    "xtick.major.size": 6.0,
    "xtick.minor.size": 3.0,
    "ytick.major.size": 6.0,
    "ytick.minor.size": 3.0,
    "xtick.direction": "inout",
    "ytick.direction": "inout",
})
_figures = {}
_windows = []
_root = None
def _br_on_window_close(win):
    global _root
    _windows.remove(win)
    for fig in win.br_figures:
        plt.close(fig)
    win.destroy()
    if not _windows:
        _root.destroy()
        _root = None

def _new_window(window_title=None):
    global _root
    if _root is None:
        _root = tkinter.Tk()
        _root.withdraw()
    if window_title is None:
        window_title = f"Window {len(_windows) + 1}"
    win = tkinter.Toplevel()
    win.withdraw()
    _windows.append(win)
    win.title(window_title)
    win.protocol("WM_DELETE_WINDOW", lambda w=win: _br_on_window_close(w))
    win.br_figures = []
    win.br_notebook = None
    return win

def _new_figure(master, path=None, rows=1, cols=1, *, overwrite_ok=False,
        **kwargs):
    assert path is None or isinstance(path, str)
    if "figsize" not in kwargs:
        figsize_x, figsize_y = DEFAULT_FIGSIZE
        figsize_x *= cols
        figsize_y *= rows
        kwargs["figsize"] = (figsize_x, figsize_y)
    if "subplot_kw" not in kwargs:
        kwargs["subplot_kw"] = {}
    assert "axes_class" not in kwargs["subplot_kw"]
    kwargs["subplot_kw"]["axes_class"] = BrAxes
    fig, axes = plt.subplots(rows, cols, **kwargs)
    if path:
        path = (FIGURE_DIRECTORY / path).resolve()
        if path in _figures and not overwrite_ok:
            raise KeyError(f"figure named {repr(path)} already exists")
    else:
        path = len(_figures)
    _figures[path] = fig

    canvas = matplotlib.backends.backend_tkagg.FigureCanvasTkAgg(
            fig, master=master)
    canvas.draw()
    canvas.get_tk_widget().pack(fill="both", expand=True)
    toolbar = matplotlib.backends.backend_tkagg.NavigationToolbar2Tk(
            canvas, master, pack_toolbar=False)
    toolbar.update()
    toolbar.pack(side="top", fill="x")

    return fig, axes

class _Window:
    def __init__(self, win):
        notebook = tkinter.ttk.Notebook(win)
        notebook.pack(fill="both", expand=True)
        win.br_notebook = notebook
        self._win = win

    def new_figure(self, *args, title=None, **kwargs):
        if title is None:
            title = f"Tab {len(self._win.br_figures) + 1}"
        tab = tkinter.ttk.Frame(self._win.br_notebook)
        self._win.br_notebook.add(tab, text=title)
        fig, axes = _new_figure(tab, *args, **kwargs)
        self._win.br_figures.append(fig)
        return fig, axes


def new_figure(*args, title=None, **kwargs):
    win = _new_window(title)
    fig, axes = _new_figure(win, *args, **kwargs)
    win.br_figures.append(fig)
    return fig, axes

def new_window(title=None):
    return _Window(_new_window(title))

class _NoWindow:
    def new_figure(self, *args, **kwargs):
        return new_figure(*args, **kwargs)
def no_window():
    return _NoWindow()


def _post():
    for fig in _figures.values():
        fig.tight_layout()
    to_remove = []
    for win in _windows:
        if win.br_notebook is not None:
            tabs = win.br_notebook.tabs()
            if len(tabs) == 0:
                warnings.warn("a window has no figures, closing it...")
                win.destroy()
                to_remove.append(win)
    for win in to_remove:
        _windows.remove(win)

def _save():
    count = len([x for x in _figures if isinstance(x, Path)])
    if not count:
        print("No figures to save.")
        return
    print("Saving all figures...")
    FIGURE_DIRECTORY.mkdir(parents=True, exist_ok=True)
    i = 0
    maxlen = len(str(count))
    for path, fig in _figures.items():
        if not isinstance(path, Path):
            continue
        i += 1
        name = repr(str(path.relative_to(FIGURE_DIRECTORY)))
        idx = f"{i:>{maxlen}}/{count:>{maxlen}}"
        print(f"Saving {idx}: {name}...\r", end="")
        # cranked dpi.
        fig.savefig(path, dpi=300, bbox_inches="tight")
        print(f" Saved {idx}: {name}   \n", end="")

def _show():
    for win in _windows:
        win.deiconify()
        win.lift()
        win.focus_force()
        if win.br_notebook is not None:
            tabs = win.br_notebook.tabs()
            if len(tabs) > 0:
                win.br_notebook.select(tabs[-1])
    if _root is not None:
        if len(_windows) > 0:
            _root.mainloop()


@contextlib.contextmanager
def instance(view=True, save=False):
    yield
    _post()
    if save:
        _save()
    if view:
        _show()
