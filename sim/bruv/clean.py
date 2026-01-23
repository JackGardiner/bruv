"""
Deletes any directories and files created from compiling.
"""

import traceback
import sys

from . import paths

__all__ = ["clean"]

def clean():

    def del_dir(path):
        if path.exists() and path.is_dir():
            print(f"Deleting directory: {paths.shortstr(path)}")
            try:
                paths.wipe(path)
            except Exception:
                print("  error: exception log:")
                traceback.print_exc()
            return True
        return False

    def del_file(path):
        if path.exists() and path.is_file():
            print(f"Deleting file: {paths.shortstr(path)}")
            try:
                path.unlink()
            except Exception:
                print("  error: exception log:")
                traceback.print_exc()
            return True
        return False

    # Del bin and any pycaches.
    dirs = [paths.BIN] + [p for p in paths.subdirs(paths.BRUV)
                          if p.name == "__pycache__"]
    files = [] # no explicit files.
    deleted = 0
    deleted += sum(map(del_dir, dirs))
    deleted += sum(map(del_file, files))
    if deleted == 0:
        print("Nothing to clean.")


if __name__ == "__main__":
    if len(sys.argv) > 1:
        raise RuntimeError("bruv.clean does not have command line args")
    sys.exit(clean())
