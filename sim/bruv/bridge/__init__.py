"""
Proxy module import for the bridge cython.
"""

import importlib as _importlib
import os as _os
import sys as _sys

from .. import paths as _paths


# Find the actual compiled bridge file.
_bridge_path = None
for _file in _paths.subfiles(_paths.BIN_BRIDGE):
    if _file.suffix == ".pyd":
        _bridge_path = str(_file.resolve())
        break
if _bridge_path is None:
    raise ImportError("Could not find compiled cython in "
                     f"{repr(_paths.BIN_BRIDGE)}.")

# Windows needs sim lib dir added to be able to find dll.
if _sys.platform == "win32" and hasattr(_os, "add_dll_directory"):
    _os.add_dll_directory(_paths.BIN_SIM)

# Load it explicitly (this less-than-one-year old project still does its imports
# the old fashioned way).
_spec = _importlib.util.spec_from_file_location("bruv.bridge", _bridge_path)
_bridge_module = _importlib.util.module_from_spec(_spec)

# Overwrite the bruv.bridge module with the cython only.
_sys.modules[__name__] = _bridge_module

_spec.loader.exec_module(_bridge_module)

# # Mimic being the module by smashing it into our globals.
# bridge_module.__name__ = __name__ # eh except name.
# bridge_module.__package__ = __package__ # eh except name.
# print(bridge_module.__dict__)
# globals().update(bridge_module.__dict__)
