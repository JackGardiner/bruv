"""
Proxy module for the bridge cython, lazy-loading all symbols.
"""

import importlib
import os
import sys

from .. import paths


_bridge_module = None # cache loaded bridge.

def _load_bridge():
    """
    Finds and loads the cythonised bridge.
    """
    global _bridge_module
    if _bridge_module is not None:
        return _bridge_module

    howtobuild = "run 'py -m bruv.build'"

    # Check bin dirs exist.
    if not paths.BIN_SIM.is_dir() or not paths.BIN_BRIDGE.is_dir():
        raise ImportError(f"bin directories missing, {howtobuild}")

    # Find the actual compiled bridge file.
    bridge_paths = [p for p in paths.bridge_built() if p.suffix == ".pyd"]
    if not bridge_paths:
        raise ImportError(f"cythonised bridge not found, {howtobuild}")
    if len(bridge_paths) > 1:
        raise ImportError(f"multiple cythonised bridges found, {howtobuild}")
    bridge_path = str(bridge_paths[0].resolve())

    # Windows needs sim lib dir added to be able to find dll.
    if sys.platform == "win32" and hasattr(os, "add_dll_directory"):
        os.add_dll_directory(str(paths.BIN_SIM.resolve()))

    # Do the actual import.
    name = f"bruv.bridge.{paths.BRIDGE_MODULE_NAME}"
    spec = importlib.util.spec_from_file_location(name, bridge_path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)

    # Cache me.
    _bridge_module = module
    return _bridge_module


# Mimic the cythonised bridge:

# The only symbols exported from the cython:
__all__ = ["Interpretation", "State"]

def __getattr__(name):
    if name not in __all__:
        raise AttributeError(f"module {__name__} has no attribute {name}")
    module = _load_bridge()
    value = getattr(module, name)
    globals()[name] = value # cache into this module.
    return value

def __dir__():
    # This modules dir and any valid lookups.
    return sorted(set(globals().keys()) | set(__all__))
