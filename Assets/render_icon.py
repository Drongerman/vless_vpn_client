"""
Сборка квадратных PNG/ICO без Cairo: геометрия совпадает с VlessVPN-icon.svg (масштаб 0.96 по X, 0.86 по Y от центра).
Запуск: python render_icon.py
"""
from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw

SIZE = 512
BG = (13, 17, 23, 255)
SHIELD_FILL = (15, 25, 35, 255)
CYAN = (0, 217, 255, 255)
GREEN = (46, 160, 67, 255)
TEXT = (230, 237, 243, 255)
RADIUS = 96


def T(x: float, y: float) -> tuple[float, float]:
    """Как transform translate(256,256) scale(0.96 0.86) translate(-256 -256)."""
    return 256 + (x - 256) * 0.96, 256 + (y - 256) * 0.86


def shield_polygon() -> list[tuple[float, float]]:
    # Упрощённый контур щита (как в SVG, без кривых Безье — для мелких размеров ок)
    return [
        T(256, 72),
        T(396, 132),
        T(396, 268),
        T(396, 360),
        T(320, 420),
        T(256, 448),
        T(192, 420),
        T(116, 360),
        T(116, 268),
        T(116, 132),
    ]


def inner_shield_line() -> list[tuple[float, float]]:
    return [
        T(256, 96),
        T(372, 146),
        T(372, 262),
        T(372, 340),
        T(310, 392),
        T(256, 416),
        T(202, 392),
        T(140, 340),
        T(140, 262),
        T(140, 146),
        T(256, 96),
    ]


def draw_rounded_rect(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int, int, int],
    radius: int,
    fill: tuple[int, int, int, int],
) -> None:
    draw.rounded_rectangle(xy, radius=radius, fill=fill)


def main() -> None:
    base = os.path.dirname(os.path.abspath(__file__))
    png_path = os.path.join(base, "VlessVPN-icon.png")
    ico_path = os.path.join(base, "VlessVPN-icon.ico")

    im = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)

    draw_rounded_rect(draw, (0, 0, SIZE - 1, SIZE - 1), RADIUS, BG)

    shield = [(int(round(p[0])), int(round(p[1]))) for p in shield_polygon()]
    draw.polygon(shield, fill=SHIELD_FILL, outline=CYAN, width=6)

    inner = [(int(round(p[0])), int(round(p[1]))) for p in inner_shield_line()]
    draw.line(inner, fill=(*CYAN[:3], 90), width=2)

    for cx, cy, r, fill in (
        (200, 220, 14, CYAN),
        (312, 220, 14, GREEN),
        (256, 290, 16, CYAN),
    ):
        tx, ty = T(cx, cy)
        rr = int(round(r * 0.92))
        bbox = (
            int(round(tx - rr)),
            int(round(ty - rr)),
            int(round(tx + rr)),
            int(round(ty + rr)),
        )
        draw.ellipse(bbox, fill=fill)

    # Линии VPN
    p1, p2, p3 = T(200, 220), T(256, 290), T(312, 220)
    draw.line(
        [(int(round(p1[0])), int(round(p1[1]))), (int(round(p2[0])), int(round(p2[1]))), (int(round(p3[0])), int(round(p3[1])))],
        fill=CYAN,
        width=8,
        joint="curve",
    )

    # Буква V
    va, vb, vc = T(220, 170), T(256, 240), T(292, 170)
    draw.line(
        [
            (int(round(va[0])), int(round(va[1]))),
            (int(round(vb[0])), int(round(vb[1]))),
            (int(round(vc[0])), int(round(vc[1]))),
        ],
        fill=TEXT,
        width=14,
        joint="curve",
    )

    im.save(png_path, "PNG", dpi=(96, 96))
    im.save(
        ico_path,
        format="ICO",
        sizes=[(256, 256), (64, 64), (48, 48), (32, 32), (16, 16)],
    )
    print("OK", png_path, ico_path, im.size)


if __name__ == "__main__":
    main()
