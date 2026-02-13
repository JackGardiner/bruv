"""
Various important paths + manip.
"""

import functools
import os
import shutil
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent


def shortstr(path):
    path = Path(path).resolve()
    if path.is_relative_to(ROOT):
        path = path.relative_to(ROOT)
    return repr(path.as_posix())


def subfiles(directory, ext="", recursive=True, as_str=False, as_rel=False):
    directory = Path(directory)
    pattern = "**/"*bool(recursive) + "*" + ext
    files = [path for path in directory.glob(pattern) if path.is_file()]
    parts = lambda p: p.relative_to(directory).parts
    # Ignore hidden.
    files = [path for path in files
            if all(not part.startswith(".") for part in parts(path))]
    # Ignore files inside pycaches.
    files = [path for path in files
             if ("__pycache__" not in parts(path))]
    # Transform to requested format.
    if as_rel:
        files = [x.relative_to(directory) for x in files]
    if as_str:
        files = [str(x) for x in files]
    return files

def subdirs(directory, recursive=True, as_str=False, as_rel=False):
    directory = Path(directory)
    pattern = "**/"*bool(recursive) + "*"
    dirs = [path for path in directory.glob(pattern) if path.is_dir()]
    parts = lambda p: p.relative_to(directory).parts
    # Ignore hidden.
    dirs = [path for path in dirs
            if all(not part.startswith(".") for part in parts(path))]
    # Note we dont ignore pycaches.
    # Transform to requested format.
    if as_rel:
        dirs = [x.relative_to(directory) for x in dirs]
    if as_str:
        dirs = [str(x) for x in dirs]
    return dirs


def wipe(directory, missing_ok=False, only_contents=False):
    directory = Path(directory)
    if not directory.exists():
        if not missing_ok:
            raise FileNotFoundError("cannot wipe missing directory: "
                                   f"{shortstr(directory)}")
        return
    if not directory.is_dir():
        raise NotADirectoryError("cannot wipe non-directory: "
                                f"{shortstr(directory)}")
    if not only_contents:
        shutil.rmtree(directory)
    else:
        for entry in directory.iterdir():
            if entry.is_dir():
                shutil.rmtree(entry)
            else:
                entry.unlink()


def max_mtime(files):
    files = [Path(p) for p in files]
    return max([(-1 if not p.is_file() else p.stat().st_mtime) for p in files])


class pushd:
    """
    Can be used as a context or as a function decorator. Defaults to project
    root.
    """

    def __init__(self, path=None):
        # Default to repo root.
        if path is None:
            path = ROOT
        self.path = Path(path).resolve()

    def __call__(self, func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            with self:
                return wrapper.func(*args, **kwargs)
        wrapper.func = func
        return wrapper

    def __enter__(self):
        self._old_dir = os.getcwd()
        os.chdir(str(self.path))
    def __exit__(self, etype, evalue, etb):
        os.chdir(self._old_dir)


GCC = "gcc"
# if gcc isnt on path, you may edit this to correctly point to the executable.


BRUV = ROOT / "bruv"

SIM = BRUV / "sim"

BRIDGE = BRUV / "bridge"

BIN = ROOT / "bin"
BIN_SIM = BIN / "sim"
BIN_BRIDGE = BIN / "bridge"

SIM_PREPRO = BIN_SIM / "sim.i"
SIM_DISAS  = BIN_SIM / "sim.s"
SIM_OBJ    = BIN_SIM / "sim.o"

SIM_LIB_NAME = "sim"
if sys.platform == "win32":
    SIM_LIB = BIN_SIM / f"{SIM_LIB_NAME}.dll"
    SIM_STUBS = BIN_SIM / f"{SIM_LIB_NAME}.lib"
elif sys.platform == "darwin":
    SIM_LIB = BIN_SIM / f"lib{SIM_LIB_NAME}.dylib"
    SIM_STUBS = None
else:
    SIM_LIB = BIN_SIM / f"lib{SIM_LIB_NAME}.so"
    SIM_STUBS = None

BRIDGE_MODULE_NAME = "bridge_cythonised"

SIM_CACHE = BIN_SIM / "cache.json"
BRIDGE_CACHE = BIN_BRIDGE / "cache.json"

PATHS_PY = BRUV / "paths.py"
BUILD_PY = BRUV / "build.py"
BRIDGE_H = BRIDGE / "bridge.h"


def sim_deps():
    paths = subfiles(SIM)
    paths.append(BUILD_PY) # always depends on builder.
    paths.append(PATHS_PY) # builder depends on paths.
    paths.append(BRIDGE_H) # depends on bridge.h also.
    for p in paths:
        if not p.is_file():
            raise FileNotFoundError(f"missing sim dependancy: {shortstr(p)}")
    return paths

def bridge_deps():
    paths = subfiles(BRIDGE)
    paths.append(BUILD_PY) # always depends on builder.
    paths.append(PATHS_PY) # builder depends on paths.
    for p in paths:
        if not p.is_file():
            raise FileNotFoundError(f"missing bridge dependancy: {shortstr(p)}")
    return paths


def sim_built(): # not necessarily all existent.
    paths = [SIM_CACHE]
    paths.append(SIM_LIB)
    if SIM_STUBS is not None:
        paths.append(SIM_STUBS)
    return paths

def bridge_built(): # not necessarily all existent.
    paths = [BRIDGE_CACHE]
    # Finding the "correct" compiled bridge file exists is lowk hard, so instead
    # we just check for any .pyd in the right spot.
    paths += subfiles(BIN_BRIDGE, ext=".pyd", recursive=False)
    return paths
