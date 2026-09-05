"""How sensitive is the bubble-escape throat criterion to surface tension?"""

RHO, G = 750.0, 9.81   # molten paraffin, kg/m3


def min_throat_mm(sigma_mNm, bubble_h_mm, aspect=0.5):
    """Smallest throat radius (mm) a bubble of that height can pass."""
    sig = sigma_mNm * 1e-3
    h = bubble_h_mm * 1e-3
    r_b = aspect * h
    drive = RHO * G * h + 2.0 * sig / r_b
    return 2.0 * sig / drive * 1e3


if __name__ == "__main__":
    sigmas = (7.1, 15, 20, 25, 28, 30, 35)
    heights = (1, 2, 5, 10)
    print("Minimum passable throat radius, mm")
    print("  sigma is in mN/m; 7.1 is the rejected figure, 20-30 the adopted band")
    print()
    print(f"  {'sigma':>7} |" + "".join(f"{f'h={h}mm':>9}" for h in heights))
    print("  " + "-" * (9 + 9 * len(heights)))
    for s in sigmas:
        mark = "  <- rejected" if s == 7.1 else ("  <- adopted" if s == 25 else "")
        row = "".join(f"{min_throat_mm(s, h):9.2f}" for h in heights)
        print(f"  {s:7.1f} |{row}{mark}")
    print()
    lo, hi = 20.0, 30.0
    print(f"  Spread across the adopted {lo:.0f}-{hi:.0f} mN/m band:")
    for h in heights:
        print(f"    h={h:2d}mm: {min_throat_mm(hi, h)/min_throat_mm(lo, h):.2f}x")
