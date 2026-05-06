"""
Various important paths + manip.
"""

import functools
import os
import shutil
import sys
from pathlib import Path

# Platform-specific locking
if os.name == "nt":
    import msvcrt
else:
    import fcntl


ROOT = Path(__file__).resolve().parent.parent


def shortstr(path):
    path = Path(path).resolve()
    if path.is_relative_to(ROOT):
        path = path.relative_to(ROOT)
    return repr(path.as_posix())


def isopen(file):
    return file is not None and not file.closed


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


class FileLock:
    def __init__(self, path):
        self.path = path
        self._fh = None

    def acquire(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._fh = open(self.path, "a+b")
        if os.name == "nt":
            self._fh.seek(0)
            msvcrt.locking(self._fh.fileno(), msvcrt.LK_LOCK, 1)
        else:
            fcntl.flock(self._fh.fileno(), fcntl.LOCK_EX)

    def release(self):
        if self._fh is None:
            return
        if not self._fh.closed:
            if os.name == "nt":
                self._fh.seek(0)
                msvcrt.locking(self._fh.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(self._fh.fileno(), fcntl.LOCK_UN)
            self._fh.close()
        self._fh = None

    def __enter__(self):
        self.acquire()
        return self
    def __exit__(self, exc_type, exc_val, exc_tb):
        self.release()


class splice_stdout:
    """
    Splices stdout to (optionally) be redirected to file.
    """

    def __init__(self, path, view=True, save=False):
        self.path = path
        self.view = view
        self.save = save
        self._fview = None
        self._fsave = None
        self._old_stdout = None
        self._buffer = ""

    def __enter__(self):
        self._fview = None
        self._fsave = None
        self._old_stdout = sys.stdout
        if self.view:
            self._fview = sys.stdout
        if self.save:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            self._fsave = open(self.path, "w", encoding="utf-8")
        sys.stdout = self
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        try:
            if isopen(self._fsave) and self._buffer:
                self._fsave.write(self._buffer)
                self._buffer = ""
        finally:
            if isopen(self._fsave):
                self._fsave.close()
            sys.stdout = self._old_stdout
            self._old_stdout = None
            self._fsave = None
            self._fview = None

    def write(self, text):
        if isopen(self._fview):
            self._fview.write(text)
        if isopen(self._fsave):
            lines = (self._buffer + text).split("\n")
            lines = [l.split("\r")[-1] for l in lines]
            self._fsave.write("".join(l + "\n" for l in lines[:-1]))
            self._buffer = lines[-1]

    def flush(self):
        if isopen(self._fview):
            self._fview.flush()
        if isopen(self._fsave):
            self._fsave.flush()



GCC = "gcc"
# if gcc isnt on path, you may edit this to correctly point to the executable.


BRUV = ROOT / "bruv"

C = BRUV / "c"

BRIDGE = BRUV / "bridge"

BIN = ROOT / "bin"
BIN_C = BIN / "c"
BIN_BRIDGE = BIN / "bridge"
BIN_APPROXIMATOR = BIN / "approximator"

OUT = ROOT / "out"

C_PREPRO = OUT / "c.i"
C_DISAS  = OUT / "c.s"
C_OBJ    = OUT / "c.o"

C_LIB_NAME = "c"
if sys.platform == "win32":
    C_LIB = BIN_C / f"{C_LIB_NAME}.dll"
    C_STUBS = BIN_C / f"{C_LIB_NAME}.lib"
elif sys.platform == "darwin":
    C_LIB = BIN_C / f"lib{C_LIB_NAME}.dylib"
    C_STUBS = None
else:
    C_LIB = BIN_C / f"lib{C_LIB_NAME}.so"
    C_STUBS = None
C_CACHE = BIN_C / "cache.json"

BRIDGE_MODULE_NAME = "bridge_cythonised"
BRIDGE_CACHE = BIN_BRIDGE / "cache.json"

APPROXIMATOR_OUTPUT = OUT / "approximator.txt"
APPROXIMATOR_FIGS = OUT / "approximator_figs"
APPROXIMATOR_TBLS = OUT / "tbl"

DECI_EXE    = OUT / "deci.exe"
DECI_PREPRO = OUT / "deci.i"
DECI_DISAS  = OUT / "deci.s"
DECI_OBJ    = OUT / "deci.o"


PATHS_PY = BRUV / "paths.py"
BUILD_PY = BRUV / "build.py"
BRIDGE_H = BRIDGE / "bridge.h"


def c_deps():
    paths = subfiles(C)
    paths.append(BUILD_PY) # always depends on builder.
    paths.append(PATHS_PY) # builder depends on paths.
    paths.append(BRIDGE_H) # depends on bridge.h also.
    for p in paths:
        if not p.is_file():
            raise FileNotFoundError(f"missing c dependancy: {shortstr(p)}")
    return paths

def bridge_deps():
    paths = subfiles(BRIDGE)
    paths.append(BUILD_PY) # always depends on builder.
    paths.append(PATHS_PY) # builder depends on paths.
    for p in paths:
        if not p.is_file():
            raise FileNotFoundError(f"missing bridge dependancy: {shortstr(p)}")
    return paths


def c_built(): # not necessarily all existent.
    paths = [C_CACHE]
    paths.append(C_LIB)
    if C_STUBS is not None:
        paths.append(C_STUBS)
    return paths

def bridge_built(): # not necessarily all existent.
    paths = [BRIDGE_CACHE]
    # Finding the "correct" compiled bridge file exists is lowk hard, so instead
    # we just check for any .pyd in the right spot.
    paths += subfiles(BIN_BRIDGE, ext=".pyd", recursive=False)
    return paths
