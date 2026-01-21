
def main():
    # Compile me (if needed).
    from . import build
    try:
        build.build(must=False)
    except build.BuildError:
        return 2

    # Run me.
    from . import frontend
    frontend.run()

    return 0

if __name__ == "__main__":
    import sys as _sys
    if len(_sys.argv) > 1:
        raise RuntimeError("bruv does not have command line args")
    _sys.exit(main())
