"""Regression gate for the generator."""

import argparse
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASELINE_DIR = os.path.join(ROOT, "config", "grain")
BASELINE = os.path.join(BASELINE_DIR, "baseline-0.5mm.json")  # fast-gate default
TOL = 0.005

WATCH = ("out_volume_mL", "fin_volume_mL", "out_fill", "fin_fill",
         "out_mass_g", "fin_mass_g", "out_bounds_mm")


def newest(variant, record_dir=None):
    """Newest record for `variant` by mtime, not by (hash) filename."""
    d = record_dir or os.path.join(ROOT, "config", "grain")
    names = [n for n in os.listdir(d)
             if n.startswith(f"grain-{variant}-") and n.endswith(".json")]
    if not names:
        raise FileNotFoundError(f"no grain-{variant}-*.json in {d}")
    names.sort(key=lambda n: os.path.getmtime(os.path.join(d, n)))
    chosen = names[-1]
    if len(names) > 1:
        print(f"  ({variant}: {len(names)} candidate records in {d}, "
              f"picked newest by mtime -> {chosen})")
    with open(os.path.join(d, chosen)) as fh:
        return json.load(fh)


def _drift(a, b):
    """Worst-element relative drift, scalars or lists."""
    if isinstance(a, (list, tuple)):
        return max(abs(bi / ai - 1) if ai else 0.0 for ai, bi in zip(a, b))
    return abs(b / a - 1) if a else 0.0


def _fmt(v):
    if isinstance(v, (list, tuple)):
        return "[" + ", ".join(f"{x:.3f}" for x in v) + "]"
    return f"{v:.5f}"


def resolve_baseline(name_or_path):
    """Resolve a --baseline argument to a baseline file path."""
    if name_or_path is None:
        return BASELINE
    looks_like_a_path = (os.sep in name_or_path
                         or name_or_path.endswith(".json"))
    if looks_like_a_path:
        return name_or_path
    return os.path.join(BASELINE_DIR, f"baseline-{name_or_path}.json")


def capture(baseline_path=None, record_dir=None):
    path = resolve_baseline(baseline_path)
    data = {v: newest(v, record_dir=record_dir) for v in ("std", "base")}
    with open(path, "w") as fh:
        json.dump(data, fh, indent=2)
    print(f"Baseline written to {path}")
    for v, rec in data.items():
        print(f"  {v}: {rec['out_volume_mL']:.3f} mL, "
              f"fill {rec['out_fill']:.4%}, mass {rec['out_mass_g']:.2f} g")


def check(baseline_path=None, record_dir=None):
    baseline_path = resolve_baseline(baseline_path)
    if not os.path.exists(baseline_path):
        print(f"No baseline at {baseline_path}; run --capture first.")
        return 1
    with open(baseline_path) as fh:
        base = json.load(fh)
    fails = 0
    for variant, ref in base.items():
        rec = newest(variant, record_dir=record_dir)
        # drift is meaningless across voxel sizes, so that's its own failure
        ref_vox, rec_vox = ref.get("voxel_size"), rec.get("voxel_size")
        if ref_vox != rec_vox:
            fails += 1
            print(f"  FAIL {variant:5s} voxel_size MISMATCH: baseline "
                  f"{ref_vox} mm vs this run {rec_vox} mm -- NOT COMPARABLE. "
                  "Regenerate the baseline (--capture) at this voxel size, "
                  "or re-run the generator at the baseline's voxel size, "
                  "before trusting any drift number below.")
        for key in WATCH:
            a, b = ref[key], rec[key]
            drift = _drift(a, b)
            flag = "FAIL" if drift > TOL else "ok  "
            if drift > TOL:
                fails += 1
            print(f"  {flag} {variant:5s} {key:16s} "
                  f"{_fmt(a):>12s} -> {_fmt(b):>12s}  ({drift:+.3%})")
    print("  REGRESSION CLEAN" if not fails else f"  {fails} DRIFTED")
    return 1 if fails else 0


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--baseline", default=None, metavar="TAG_OR_PATH",
                     help="Which baseline to capture to / check against: a "
                          "resolution tag matching config/grain/baseline-"
                          "<tag>.json (e.g. 0.25mm), or an explicit path. "
                          "Defaults to the 0.5 mm fast-gate baseline.")
    args = ap.parse_args()
    if args.capture:
        capture(baseline_path=args.baseline)
        sys.exit(0)
    sys.exit(check(baseline_path=args.baseline))
