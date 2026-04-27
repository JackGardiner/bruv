"""
Compiles the sim library and cythonises the bridge module.
"""

import json
import os
import subprocess
import sys
import traceback
from pathlib import Path

from . import build
from . import paths

__all__ = ["build_deci"]



def build_deci(gcc_extra_args=()):
    # Very similar to ./build.py::_build_sim

    os.system("")

    out_paths = {
        "final": paths.DECI_EXE,
        "prepro": paths.DECI_PREPRO,
        "disas": paths.DECI_DISAS,
        "obj": paths.DECI_OBJ,
    }
    cmd, builds_final, out = build._gcc_cmd(gcc_extra_args, out_paths=out_paths,
            dynamic_lib=False)
    print(f">> {' '.join(cmd)}\n")

    out.parent.mkdir(parents=True, exist_ok=True)

    srcs = [p for p in paths.subfiles(paths.SIM) if p.suffix == ".c"]
    srcs = sorted(srcs)
    if not srcs:
        print("error: must have at least one source sim (.c) file\n")
        raise build.BuildError()
    def to_include(p):
        path = p.relative_to(paths.SIM).as_posix()
        path = json.dumps(path)
        return f"#include {path}\n"
    godfile = "".join(to_include(p) for p in srcs)

    proc = subprocess.Popen(
        cmd,
        bufsize=-1, cwd=paths.SIM, text=True,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        stdin=subprocess.PIPE
    )
    proc.stdin.write(godfile)
    proc.stdin.close()
    output, _ = proc.communicate()

    if proc.returncode or output:
        print("error: when running gcc:")
        print(output)
        print()
        raise build.BuildError()

    print(f"Built deci at: {paths.shortstr(out)}\n")


if __name__ == "__main__":
    try:
        build_deci(sys.argv[1:])
        sys.exit(0)
    except build.BuildError:
        sys.exit(1)
