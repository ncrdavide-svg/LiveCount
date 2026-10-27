"""Generates assets/LiveCount.ico (and a PNG preview).

Same family as LiveCue's icon (same dark tile, palette and proportions): a stopwatch whose
ring is the countdown - green time remaining (LiveCue's GO green), an amber "last stretch"
segment (LiveCue's current-cue amber) and the elapsed part in dim grey. Small sizes
(<= 32 px) use a simplified drawing so it stays readable in the taskbar and title bar.

Usage: python tools/make_icon.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT_ICO = ROOT / "assets" / "LiveCount.ico"
OUT_PNG = ROOT / "assets" / "LiveCount.png"

SUPERSAMPLE = 1024  # drawn at this size, then downscaled with Lanczos for smooth edges

# Palette shared with LiveCue.
TILE_TOP = (52, 57, 66)
TILE_BOTTOM = (22, 24, 28)
DIM = (98, 106, 122)
AMBER = (255, 179, 0)
GO_TOP = (102, 222, 128)
GO_BOTTOM = (34, 160, 70)


def vertical_gradient(size, top, bottom):
    gradient = Image.new("RGBA", (1, size))
    for y in range(size):
        t = y / (size - 1)
        gradient.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,))
    return gradient.resize((size, size))


def rounded_mask(size, box, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=radius, fill=255)
    return mask


def arc_mask(size, center, radius, width, start, end, round_caps=True):
    """Mask of a ring segment from 'start' to 'end' degrees (0 = 3 o'clock, clockwise)."""
    import math

    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    cx, cy = center
    box = (cx - radius, cy - radius, cx + radius, cy + radius)
    d.arc(box, start, end, fill=255, width=round(width))
    if round_caps:
        r_mid = radius - width / 2
        for a in (start, end):
            x = cx + r_mid * math.cos(math.radians(a))
            y = cy + r_mid * math.sin(math.radians(a))
            d.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), fill=255)
    return mask


def draw(simplified: bool) -> Image.Image:
    s = SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    # Tile: identical to LiveCue's.
    margin = s * 0.03
    tile_box = (margin, margin, s - margin, s - margin)
    img.paste(vertical_gradient(s, TILE_TOP, TILE_BOTTOM), (0, 0), rounded_mask(s, tile_box, s * 0.2))
    d = ImageDraw.Draw(img)

    if simplified:
        center, radius, width = (s * 0.5, s * 0.52), s * 0.36, s * 0.15
    else:
        center, radius, width = (s * 0.5, s * 0.555), s * 0.32, s * 0.105
        # Stopwatch crown and stem, in LiveCue's dim bar grey.
        cx = s * 0.5
        top = center[1] - radius
        d.rounded_rectangle((cx - s * 0.035, top - s * 0.075, cx + s * 0.035, top + s * 0.01), radius=s * 0.012, fill=DIM)
        d.rounded_rectangle((cx - s * 0.10, top - s * 0.135, cx + s * 0.10, top - s * 0.065), radius=s * 0.035, fill=DIM)

    # Ring: the whole dial runs clockwise from 12 o'clock (-90 deg).
    # Elapsed part (dim), then the remaining time: amber last stretch, then green.
    start = -90
    elapsed_end = start + 360 * 0.28
    amber_end = elapsed_end + 360 * 0.12
    gap = 9 if not simplified else 12  # small visual gap between segments

    track = arc_mask(s, center, radius, width, start + gap / 2, elapsed_end - gap / 2)
    img.paste(Image.new("RGBA", (s, s), DIM + (255,)), (0, 0), track)
    amber = arc_mask(s, center, radius, width, elapsed_end + gap / 2, amber_end - gap / 2)
    img.paste(Image.new("RGBA", (s, s), AMBER + (255,)), (0, 0), amber)
    green = arc_mask(s, center, radius, width, amber_end + gap / 2, start + 360 - gap / 2)
    img.paste(vertical_gradient(s, GO_TOP, GO_BOTTOM), (0, 0), green)

    if not simplified:
        # Hand pointing at the boundary between elapsed and remaining time, amber pivot.
        import math

        cx, cy = center
        hand_len = radius - width - s * 0.04
        a = math.radians(elapsed_end)
        hx, hy = cx + hand_len * math.cos(a), cy + hand_len * math.sin(a)
        d.line((cx, cy, hx, hy), fill=(235, 238, 242), width=round(s * 0.03))
        r = s * 0.015
        d.ellipse((hx - r, hy - r, hx + r, hy + r), fill=(235, 238, 242))
        p = s * 0.045
        d.ellipse((cx - p, cy - p, cx + p, cy + p), fill=AMBER)

    return img


def main():
    full = draw(simplified=False)
    small = draw(simplified=True)

    sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
    frames = [(small if size <= 32 else full).resize((size, size), Image.LANCZOS) for size in sizes]

    OUT_ICO.parent.mkdir(parents=True, exist_ok=True)
    frames[-1].save(OUT_ICO, format="ICO", sizes=[(f.width, f.height) for f in frames], append_images=frames[:-1])
    full.resize((512, 512), Image.LANCZOS).save(OUT_PNG)
    small.resize((128, 128), Image.LANCZOS).save(OUT_PNG.with_name("LiveCount-small.png"))
    print(f"Wrote {OUT_ICO} and {OUT_PNG}")


if __name__ == "__main__":
    main()
