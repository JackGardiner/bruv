"""
Builds (if needed) and runs bruv.
"""

import sys

from . import build
from . import frontend

def main():
    # Compile me (if needed).
    try:
        build.build(must=False)
    except build.BuildError:
        return 2
    # Run me.
    return frontend.run()

if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv does not have command line args")
    sys.exit(main())
