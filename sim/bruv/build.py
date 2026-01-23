"""
Compiles the sim library and cythonises the bridge module.
"""

import json
import shutil
import subprocess
import sys
import traceback
from pathlib import Path

import numpy as np
import setuptools
from Cython.Build import cythonize

from . import paths

__all__ = ["BuildError", "build"]




def _save_sim(cmd, deps, gcc_extra_args):
    data = {
        "command": cmd,
        "dependancies": [str(p) for p in deps],
        "gcc_extra_args": gcc_extra_args,
    }
    with paths.SIM_CACHE.open("w", encoding="utf-8") as f:
        json.dump(data, f)

def _load_sim():
    try:
        with paths.SIM_CACHE.open("r", encoding="utf-8") as f:
            data = json.load(f)
        if not isinstance(data, dict):
            raise BuildError()
        if set(data.keys()) != {"command", "dependancies", "gcc_extra_args"}:
            raise BuildError()
        for arr in data.values():
            if not isinstance(arr, list):
                raise BuildError()
            if not all(isinstance(s, str) for s in arr):
                raise BuildError()
        data["dependancies"] = [Path(p) for p in data["dependancies"]]
    except (BuildError, FileNotFoundError, json.JSONDecodeError):
        data = None
    return data



def _save_bridge(deps):
    data = {
        "dependancies": [str(p) for p in deps],
    }
    with paths.BRIDGE_CACHE.open("w", encoding="utf-8") as f:
        json.dump(data, f)

def _load_bridge():
    try:
        with paths.BRIDGE_CACHE.open("r", encoding="utf-8") as f:
            data = json.load(f)
        if not isinstance(data, dict):
            raise BuildError()
        if set(data.keys()) != {"dependancies"}:
            raise BuildError()
        for arr in data.values():
            if not isinstance(arr, list):
                raise BuildError()
            if not all(isinstance(s, str) for s in arr):
                raise BuildError()
        data["dependancies"] = [Path(p) for p in data["dependancies"]]
    except (BuildError, FileNotFoundError, json.JSONDecodeError):
        data = None
    return data



def _needa_sim(deps, built):
    # Check if sim has already been built and can be reused.

    # Check existance.
    if not all(p.is_file() for p in built):
        return True

    # Check that it was compiled in the same manner we intend to (and with the
    # same files).
    data = _load_sim()
    if data is None: # missing
        return True
    try:
        cmd, _ = _gcc_cmd()
    except BuildError:
        cmd = None # yeah nah
    if data["command"] != cmd:
        return True
    if set(data["dependancies"]) != set(deps):
        return True
    if len(data["gcc_extra_args"]) > 0: # any is nono.
        return True

    # Check that nothing has been modified.
    if paths.max_mtime(deps) > paths.max_mtime(built):
        return True

    # YIPEEE
    return False


def _needa_bridge(deps, built): # same deal as _needa_sim
    if not all(p.is_file() for p in built):
        return True
    # ensure at least one .pyd exists.
    if all((p.suffix != ".pyd") for p in built):
        return True

    data = _load_bridge()
    if data is None:
        return True
    if set(data["dependancies"]) != set(deps):
        return True

    if paths.max_mtime(deps) > paths.max_mtime(built):
        return True

    return False



def _gcc_cmd(extra_args=()):

    # gcc args used for compiling.
    comp_args = [
        "-std=c11",

        # Include the src dir (which (should) be the working dir). Note that
        # using the dot allows for tidier `__FILE__` macros and such.
        "-iquote", ".",

        "-m64",
        "-O3",
        "-fipa-pta",
        "-fwhole-program", # possible because of godfile compilation. must mark
                           # entrypoints/program boundaries as
                           # __externally_visible__

        "-march=native", # build+tune for this machine, since builds are never
        "-mtune=native", #   distributed anywhere.
        "-fPIC",

        "-masm=intel",

        "-fsigned-char", # Required by the program. No real reason, just helpful
                         # to choose one.

        "-fmax-errors=5",
        "-Werror",

        "-Wall", "-Wextra", "-Wpedantic",
        "-Wcast-qual", # don't cast away const.
        "-Wcast-align=strict", # don't accidentally assume overalignment.
        "-Wduplicated-cond", # why not enabled by all or extra?
        "-Winvalid-pch", # we don't use any pch right now but.
        "-Winvalid-utf8", # should probs write real unicode.
        "-Wundef", # don't accidentally redefine macros.
        "-Wwrite-strings", # at least pretend strings are in ro.
        "-Wswitch-enum", # could take or leave tbh but generally useful.
        "-Winit-self", # don't refer to a symbol you just overwrote.
        "-Wstrict-prototypes",  # uh, let me
        "-Wmissing-prototypes", #   be clear.  - obama
        "-Wmissing-declarations", # remember to put static on local functions.
        "-Wold-style-definition", # no K&R thank you.
        "-Wimplicit-fallthrough=5", # must use `fallthrough()` to indicate it.
        "-Wmissing-noreturn", # no-return should be marked.
        "-Wdate-time", # to ensure deterministic binary.
        "-Wvla", # no vlas.
        "-Walloca", # no alloca.
        "-Walloc-zero", # no zero-sized allcoations.
        # "-Walloc-size",  um apparently this one just doesn't even exist?

        "-Wno-variadic-macros", # we allow the va args in macros to be named, it
                                # just makes the definition and documentation
                                # much nicer.

        "-Wno-alloc-size-larger-than", # bruh its just buggy and gives false-
                                       # postives (doesn't recognise `assert` in
                                       # some cases, such as disassembling).
        "-Wno-mismatched-dealloc", # very much would like for this to work (it
                                   # doesn't with lto so i thought god file would
                                   # fix it) but some bug (very likely inlining/
                                   # const_proping the allocating functions)
                                   # causes so many false positives that it's no
                                   # longer useful.

        "-fno-ident", # gootbye gcc signature.
        "-fno-exceptions",
        "-fno-unwind-tables",
        "-fno-asynchronous-unwind-tables",

        # Configure maths options to generally disregard ieee-754 in favour of
        # speed, but keep nan and infinity.
        "-fno-math-errno", # we'll never access it (prommy).
        "-fno-unsafe-math-optimizations",
        "-fassociative-math", # ye sure.
        "-freciprocal-math", # ye sure.
        "-fno-finite-math-only", # keep nan and inf.
        "-fno-signed-zeros", # newsflash negative zero IS zero.
        "-fno-trapping-math",
        "-fno-rounding-math", # we dont care about rounding modes.
        "-fno-signaling-nans",
        "-fno-fp-int-builtin-inexact", # ngl ion get it.
    ]

    # gcc args used for linking.
    if sys.platform == "win32":
        link_args = [
            "-shared",
            # need stubs for dyn linking on windows.
            f"-Wl,--out-implib,{paths.SIM_STUBS}",
        ]
    elif sys.platform == "darwin":
        link_args = [
            "-dynamiclib",
        ]
    else:
        link_args = [
            "-shared",
        ]

    # gcc args used for extra (pattern kinda breaks down here).
    extra_args = [arg.strip() for arg in extra_args if arg.strip()]

    # Default to compile+link.
    directive = []
    out = paths.SIM_LIB

    # Check for alternative directives.
    DIRECTIVE_ARGSS = {"-E", "-S", "-c"}
    prepro = "-E" in extra_args
    disas = "-S" in extra_args
    nolink = "-c" in extra_args
    given_directives = [prepro, disas, nolink]
    extra_args = [x for x in extra_args if x not in DIRECTIVE_ARGSS]

    if sum(given_directives) > 1:
        print("\nerror: cannot multiple directives (-E/-S/-c)")
        raise BuildError()
    if prepro:
        directive = ["-E"]
        out = paths.SIM_PREPRO
    elif disas:
        directive = ["-S"]
        out = paths.SIM_DISAS
    elif nolink:
        directive = ["-c"]
        out = paths.SIM_OBJ

    # Compile and link (dm) in one step (word dont check dm).
    cmd = [
        paths.GCC,
        "-fdiagnostics-color=always",

        *comp_args,

        *directive,
        "-x", "c", # Since we use stdin, gotta specify the language as c.
        "-",       # Read source code from stdin.

        *link_args,

        "-o", str(out),

        *extra_args
    ]

    # Any directive other than none results in no sim lib.
    builds_lib = sum(given_directives) == 0

    return cmd, builds_lib

def _build_sim(deps, gcc_extra_args=()):
    cmd, builds_lib = _gcc_cmd(gcc_extra_args)
    print(f">> {" ".join(cmd)}\n")

    # Ensure output dir.
    paths.BIN_SIM.mkdir(parents=True, exist_ok=True)

    # Make the godfile source code, which just #include's every source file and
    # is the only translation unit we ever use. The benefits of this compared to
    # something like lto are:
    # - Faster (YIPPEEEE).
    # - Much better for disassembling, since it shows the asm that will actually
    #     be in the final library.
    # - Doesn't need any caching system (though the previous cache system was
    #     like a son to me :c you will be missed bob the builder)
    # - It just feels better than lto. lto always feels a little dodgy yaknow.
    # - The way lto is setup (at least on gcc) means that some warnings never
    #     trigger (specifically the 0-sized alloc and array-bounds warnings, but
    #     there are probably more).
    srcs = [p for p in deps if p.suffix == ".c"]
    srcs = sorted(srcs) # just arbitrary but consistent ordering.
    if not srcs:
        print("error: must have at-least one source sim (.c) file\n")
        raise BuildError()
    def to_include(p):
        path = p.relative_to(paths.SIM).as_posix()
        path = json.dumps(path)
        return f"#include {path}\n"
    godfile = "".join(to_include(p) for p in srcs)


    _save_sim(cmd, deps, gcc_extra_args) # stash me.

    # Execute gcc (with working dir in the source files (required)).
    proc = subprocess.Popen(
        cmd,
        bufsize=-1, cwd=paths.SIM, text=True,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        stdin=subprocess.PIPE
    )
    proc.stdin.write(godfile) # hand the godfile over.
    proc.stdin.close()
    output, _ = proc.communicate() # blocks, and note stderr==stdout.

    # Treat any output as a failure. This is so that any warnings which sneak
    # through the "-Werror" sledgehammer are still treated as errors by us.
    if proc.returncode or output:
        print("error: when running gcc:")
        print(output)
        print()
        raise BuildError()

    return builds_lib


def _build_bridge(deps):

    # Ensure output dir.
    paths.BIN_BRIDGE.mkdir(parents=True, exist_ok=True)

    # Force different cmdline args for the cython setup.
    old_argv = sys.argv[1:]
    sys.argv[1:] = [
        "build_ext",
        "--build-lib", str(paths.BIN_BRIDGE),
        "--inplace"
    ]
    try:

        # holy shit cython/setuptools/disttools whatever is a fucking huge pain
        # when trying to get it to just put all temporaries and results in bin.
        # like at one point it was literally mimicing the entire absolute path
        # given for the sources when creating temporaries with an explicit
        # build_dir. unreal. lets just copy the bridge source into bin and build
        # it there all pretend like.

        _save_bridge(deps) # stash me.

        # Copy the bridge dir into bin.
        inbridge = paths.subfiles(paths.BRIDGE)
        relled = {p: p.relative_to(paths.BRIDGE) for p in inbridge}
        copied = {p: paths.BIN_BRIDGE / relled[p] for p in inbridge}
        for p in inbridge:
            shutil.copy(p, copied[p])

        # Get source pyx files.
        pyxs = [str(p) for p in relled.values() if p.suffix == ".pyx"]
        pyxs = sorted(pyxs) # arbitrary but consistent ordering.
        if not pyxs:
            print("error: must have at-least one source bridge (.pyx) file\n")
            raise BuildError()

        with paths.pushd(paths.BIN_BRIDGE):
            if sys.platform == "win32":
                # on windows this will throw, and instead the lib path must be
                # manually added to PATH before importing (grim).
                runtime_library_dirs = {}
            else:
                runtime_library_dirs = {
                    "runtime_library_dirs": [str(paths.BIN_SIM)],
                }
            ext = setuptools.Extension(
                paths.BRIDGE_MODULE_NAME,
                sources=pyxs,
                include_dirs=[str(paths.BIN_BRIDGE), np.get_include()],
                libraries=[str(paths.SIM_LIB_NAME)], # points to stub on windows,
                                                     # shared lib on other.
                library_dirs=[str(paths.BIN_SIM)],
                **runtime_library_dirs,
            )
            setuptools.setup(ext_modules=cythonize([ext]))

        # okie now can delete the copied source (justin caseme).
        for p in copied.values():
            p.unlink()

        # Touch all files to ensure mtime is updated.
        for p in paths.subfiles(paths.BIN_BRIDGE):
            p.touch()

        print()
    except BuildError:
        raise
    except Exception:
        print("\nerror: when cythonising:")
        traceback.print_exc()
        print()
        raise BuildError()
    finally:
        sys.argv[1:] = old_argv # pop goes the weasel




class BuildError(RuntimeError):
    pass


def build(gcc_extra_args=(), must=True):
    # Recompute dependancies and previous build products.
    sim_deps = paths.sim_deps()
    bridge_deps = paths.bridge_deps()
    sim_built = paths.sim_built()
    bridge_built = paths.bridge_built()

    # Build sim lib then cythonise bridge. Note both may be able to re-use the
    # previous build (cython actually does some caching of its own but we help it
    # out since its still super slow (nevermind we have covered its entire face
    # in wool it has no idea whats going on)).

    try:
        if not must and not _needa_sim(sim_deps, sim_built):
            print("Using previously built sim library.") # no \n
        else:
            print("Rebuilding sim library...\n")
            if not _build_sim(sim_deps, gcc_extra_args):
                # may have been given args which dont build the actual library.
                print("that's all folks.")
                return
            print("Rebuilt sim library.\n")
    except BuildError:
        # dont leak half-baked goods.
        try:
            paths.wipe(paths.BIN_SIM)
        except Exception:
            pass
        raise

    try:
        if not must and not _needa_bridge(bridge_deps, bridge_built):
            print("Using previously cythonised bridge.\n")
        else:
            print("Cythonising bridge...\n")
            _build_bridge(bridge_deps)
            print("Cythonised bridge.\n")
    except BuildError:
        try:
            paths.wipe(paths.BIN_BRIDGE)
        except Exception:
            pass
        raise


if __name__ == "__main__":
    try:
        build(sys.argv[1:])
        sys.exit(0)
    except BuildError:
        sys.exit(1)
