from __future__ import annotations

import argparse
import math
import re
import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent


# Output file name -> text in the label.
LABELS = {
    "SWG-1_8": "S1/8",
    "SWG-1_4": "S1/4",
    "SWG-3_8": "S3/8",
    "SWG-1_2": "S1/2",
    "SWG-3_4": "S3/4",
    "BSPP-1_8": "G1/8",
    "BSPP-1_4": "G1/4",
    "BSPP-3_8": "G3/8",
    "BSPP-1_2": "G1/2",
    "BSPP-3_4": "G3/4",
    "NPT-1_8": "N1/8",
    "NPT-1_4": "N1/4",
    "NPT-3_8": "N3/8",
    "NPT-1_2": "N1/2",
    "NPT-3_4": "N3/4",
    "AN-6": "AN6",
    "UNF-9_16": "FILL",
    "UNF-1-1_16": "MOL",
    "UNS-1-1_16": "RS",
    "THRU-3_8": "AVI",
}

HEIGHT = 1024 # PNG height, width is from the label.

FONTS = [
    "/usr/share/fonts/truetype/roboto/RobotoMono-Bold.ttf",
    "/usr/share/fonts/truetype/noto/NotoSansMono-Bold.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf",
    "/usr/share/fonts/truetype/liberation/LiberationMono-Bold.ttf",
    "/System/Library/Fonts/Menlo.ttc",
    "C:/Windows/Fonts/consolab.ttf",
]

# Fraction geometry, as ratios of the main font size unless noted.
FRAC_SCALE = 0.56    # numerator/denominator size relative to main
FRAC_CELLS = 2       # cells a fraction occupies, whatever the digit count
FRAC_TOP = 1.06      # fraction apex, as a ratio of main cap height
BAR_W = 0.34         # horizontal run of the diagonal
NUM_TUCK = 0.09      # diagonal overlap into the numerator
DEN_TUCK = 0.09      # denominator overlap into the diagonal

FRACTION_RE = re.compile(r"(\d+)/(\d+)")
BREAK = "<>"         # fraction breaker: takes no space and draws nothing



@dataclass
class Text:
    s: str

@dataclass
class Frac:
    num: str
    den: str

def tokenise(name: str) -> list:
    """Split a label into runs of plain text and fractions.

    A fraction takes every digit up to the slash, so "1 1/16" written without
    the space would read as 11/16. BREAK divides the label first, which stops
    a numerator reaching back past it while occupying no width itself.
    """
    out = []
    for chunk in name.split(BREAK):
        i = 0
        for m in FRACTION_RE.finditer(chunk):
            if m.start() > i:
                out.append(Text(chunk[i:m.start()]))
            out.append(Frac(m.group(1), m.group(2)))
            i = m.end()
        if i < len(chunk):
            out.append(Text(chunk[i:]))
    return out



def stem_width(font: ImageFont.FreeTypeFont) -> int:
    """Thinnest vertical stroke among the digits, in pixels.

    Scans the row at mid cap height and takes the shortest run of ink: a stem
    gives its true thickness, a diagonal crossing the row can only read wider.
    Used for the fraction diagonal so it matches the digits it joins.
    """
    thin = font.size
    for ch in "0123456789":
        img = Image.new("L", (math.ceil(font.getlength(ch)) + 6,
                              math.ceil(font.size * 1.6)), 0)
        ImageDraw.Draw(img).text((3, img.height - 3), ch, font=font,
                                 fill=255, anchor="ls")
        bb = img.getbbox()
        if bb is None:
            continue
        row, px, run = (bb[1] + bb[3]) // 2, img.load(), 0
        for x in range(bb[0], bb[2] + 1):
            if x < bb[2] and px[x, row] > 127:
                run += 1
            elif run:
                thin, run = min(thin, run), 0
    return max(2, thin)


class Renderer:
    def __init__(self, font_path: str, size: int):
        self.size = size
        self.main = ImageFont.truetype(font_path, size)
        self.small = ImageFont.truetype(font_path, max(1, round(size * FRAC_SCALE)))
        self.cell = self.main.getlength("0")
        self.small_cell = self.small.getlength("0")
        self.cap_h = -self.main.getbbox("H", anchor="ls")[1]
        self.small_cap_h = -self.small.getbbox("H", anchor="ls")[1]
        self.apex = FRAC_TOP * self.cap_h
        self.bar_w = BAR_W * size
        self.stroke = stem_width(self.small)
        self._boxes: dict[str, tuple | None] = {}

    def frac_ink(self, tok: Frac) -> float:
        """Natural drawn width of a fraction, before cell allocation."""
        return (len(tok.num) * self.small_cell - NUM_TUCK * self.size
                + self.bar_w - DEN_TUCK * self.size
                + len(tok.den) * self.small_cell)

    def frac_cells(self, tok: Frac) -> int:
        """Two cells, or more if this fraction's ink genuinely needs it."""
        return max(FRAC_CELLS, math.ceil(self.frac_ink(tok) / self.cell - 1e-6))

    def advance(self, tokens) -> float:
        return self.cell * sum(len(t.s) if isinstance(t, Text) else self.frac_cells(t)
                               for t in tokens)

    def draw(self, d: ImageDraw.ImageDraw, tokens, x: float, baseline: float):
        for tok in tokens:
            if isinstance(tok, Text):
                d.text((x, baseline), tok.s, font=self.main, fill=255, anchor="ls")
                x += len(tok.s) * self.cell
                continue

            # Centre the fraction inside its cell allocation.
            alloc = self.frac_cells(tok) * self.cell
            fx = x + (alloc - self.frac_ink(tok)) / 2.0

            num_base = baseline - (self.apex - self.small_cap_h)
            d.text((fx, num_base), tok.num, font=self.small, fill=255, anchor="ls")
            fx += len(tok.num) * self.small_cell - NUM_TUCK * self.size

            d.line([(fx, baseline), (fx + self.bar_w, baseline - self.apex)],
                   fill=255, width=self.stroke)
            fx += self.bar_w - DEN_TUCK * self.size

            d.text((fx, baseline), tok.den, font=self.small, fill=255, anchor="ls")
            x += alloc

    def measure(self, text: str):
        """Ink box (left, top, right, bottom) relative to the pen origin at
        x=0 on the baseline; up is negative. None if nothing is drawn.

        Measured rather than taken from font metrics: side bearings differ
        glyph to glyph (a mono "1" is mostly whitespace, a "6" is not) and a
        fraction never fills its cells, so advance-width centring is visibly
        off.
        """
        if text not in self._boxes:
            pad = max(8, math.ceil(self.size))
            scratch = Image.new("L", (math.ceil(self.advance(tokenise(text))) + 2 * pad,
                                      math.ceil(3 * self.size) + 2 * pad), 0)
            base = pad + 2 * self.size
            self.draw(ImageDraw.Draw(scratch), tokenise(text), pad, base)
            bb = scratch.getbbox()
            self._boxes[text] = None if bb is None else (
                bb[0] - pad, bb[1] - base, bb[2] - pad, bb[3] - base)
        return self._boxes[text]


def band(r: Renderer, texts):
    """Ink top and bottom across the whole batch, relative to the baseline."""
    boxes = [b for b in (r.measure(t) for t in texts) if b]
    if not boxes:
        sys.exit("Nothing to draw.")
    return min(b[1] for b in boxes), max(b[3] for b in boxes)


def find_font() -> str:
    for p in FONTS:
        if Path(p).is_file():
            return p
    sys.exit("No monospace font found. Edit FONTS at the top of this script.")


def get_renderer(font_path: str, texts) -> Renderer:
    """Largest font size whose batch ink band fits HEIGHT. Ink scales linearly
    with font size, so estimate from one measurement, then nudge."""
    ref = Renderer(font_path, 120)
    top, bot = band(ref, texts)
    fs = max(8, int(120 * HEIGHT / (bot - top)))
    while fs > 8:
        r = Renderer(font_path, fs)
        top, bot = band(r, texts)
        if bot - top <= HEIGHT:
            return r
        fs -= 1
    sys.exit("Nothing fits. Raise HEIGHT.")


def main():
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--names", nargs="+", default=None,
                    help="render only these entries of LABELS (default: all)")
    parser.add_argument("--png", "-p", action="store_true",
                    help="write all PNGs (not exclusive with TGAs)")
    parser.add_argument("--no-tga", "-n", action="store_true",
                    help="don't write any TGAs")
    args = parser.parse_args()

    if args.no_tga and not args.png:
        parser.error("must give '-p'/'--png' if giving '-n'/'--no-tga'")

    names = args.names or list(LABELS)
    unknown = [n for n in names if n not in LABELS]
    if unknown:
        sys.exit("Not in LABELS: " + ", ".join(unknown)
                 + "\nAvailable: " + ", ".join(LABELS))

    r = get_renderer(find_font(), [LABELS[n] for n in names])

    # One baseline for the batch, with the batch's ink band centred in HEIGHT,
    # so every label keeps its true height relative to the others.
    top, bot = band(r, [LABELS[n] for n in names])
    baseline = (HEIGHT - (bot - top)) / 2.0 - top

    widths = []
    for n in names:
        box = r.measure(LABELS[n])
        ink_w = box[2] - box[0]
        img = Image.new("L", (math.ceil(ink_w), HEIGHT), 0)
        r.draw(ImageDraw.Draw(img), tokenise(LABELS[n]),
               (img.width - ink_w) / 2.0 - box[0], baseline)
        if args.png:
            img.save(HERE / (n + ".png"))
        if not args.no_tga:
            img.save(HERE / (n + ".tga"), format="TGA", rle=False)
        widths.append(img.width)

    print(f"Wrote {len(names)} {'PNGs' if args.png else 'TGAs'}  "
          f"({HEIGHT}px tall, {min(widths)}-{max(widths)}px wide, "
          f"font size {r.size}, thinnest stroke {r.stroke}px "
          f"= {r.stroke / HEIGHT:.3f} of the height)")


if __name__ == "__main__":
    main()