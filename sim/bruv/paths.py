"""
Various important paths + manip.
"""

import functools
import os
import shutil
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
def fromroot(rel):
    return ROOT / rel


def subfiles(directory, ext="", as_str=False, as_rel=False):
    files = [p for p in directory.glob("**/*" + ext) if p.is_file()]
    # ignore some things.
    files = [p for p in files if ("__pycache__" not in p.resolve().parts)]
    files = [p for p in files if not p.name.startswith(".")]
    if as_rel:
        files = [x.relative_to(directory) for x in files]
    if as_str:
        files = [str(x) for x in files]
    return files


def wipe(directory, including_itself=True):
    if including_itself:
        shutil.rmtree(directory)
        return
    for entry in directory.iterdir():
        if entry.is_dir():
            shutil.rmtree(entry)
        else:
            entry.unlink()


def max_mtime(files):
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
        self.path = path

    def __call__(self, func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            with self:
                return wrapper.func(*args, **kwargs)
        wrapper.func = func
        return wrapper

    def __enter__(self):
        self._old_dir = Path.cwd()
        os.chdir(self.path)
    def __exit__(self, etype, evalue, etb):
        os.chdir(self._old_dir)


GCC = "gcc"
# if gcc isnt on path, you may edit this to correctly point to the executable.


SIM = fromroot("bruv/sim")

BRIDGE = fromroot("bruv/bridge")

BIN = fromroot("bin")
BIN_SIM = BIN / "sim"
BIN_BRIDGE = BIN / "bridge"

SIM_OBJ    = BIN_SIM / "sim.o"
SIM_DISAS  = BIN_SIM / "sim.s"
SIM_PREPRO = BIN_SIM / "sim.i"

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

SIM_CACHE = BIN_SIM / "cache.json"
BRIDGE_CACHE = BIN_BRIDGE / "cache.json"
