"""Stand-in for sim/bruv/c/deci.c, which doesn't build on macOS."""

import sys
from pathlib import Path

def shortpath(path):
    path = Path(path).resolve()
    cd = Path.cwd().resolve()
    try:
        path = path.relative_to(cd)
    except ValueError:
        pass
    return str(path)

def megabytes(path):
    return Path(path).stat().st_size / 1048576

def decimate(input_path, output_path, reduction):
    import pymeshlab

    print(f"decimating '{shortpath(input_path)}' -> "
            f"'{shortpath(output_path)}'")

    ms = pymeshlab.MeshSet()
    ms.load_new_mesh(str(input_path))
    before = ms.current_mesh().face_number()

    if reduction > 1:
        target = int(reduction)
    else:
        target = int(before * reduction)
    if target < 4:
        print("  error: target face count below a tetrahedron.")
        return False
    if target >= before:
        print(f"  note: target {target} >= current {before}, nothing to do.")
        return False

    ms.meshing_decimation_quadric_edge_collapse(
        targetfacenum=target,
        preservenormal=True,
        preservetopology=True,
        planarquadric=True,
        qualitythr=0.4,
    )
    ms.save_current_mesh(str(output_path), binary=True)
    after = ms.current_mesh().face_number()

    # Hausdorff against the source, so the deviation is known rather than hoped.
    ms.load_new_mesh(str(input_path))
    dev = ms.get_hausdorff_distance(sampledmesh=0, targetmesh=1,
            samplenum=300000)

    print(f"  faces  {before:,} -> {after:,}  ({after/before:.1%})")
    print(f"  size   {megabytes(input_path):.0f} MB -> "
            f"{megabytes(output_path):.0f} MB")
    print(f"  deviation from source: max {dev['max']*1000:.0f} um, "
            f"mean {dev['mean']*1000:.1f} um")
    return True

def main(argv):
    if len(argv) != 3:
        print("usage: deci.py <input.stl> <output.stl> "
                "<final size proportion>")
        return 2

    input_path = Path(argv[0])
    output_path = Path(argv[1])
    if not input_path.is_file():
        print(f"  error: no input at '{shortpath(input_path)}'")
        return 1
    try:
        reduction = float(argv[2])
    except ValueError:
        print("  error: invalid <final size proportion> (must be a number)")
        return 1
    if not reduction > 0:
        print("  error: invalid <final size proportion> (must be > 0)")
        return 1

    return 0 if decimate(input_path, output_path, reduction) else 1

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
