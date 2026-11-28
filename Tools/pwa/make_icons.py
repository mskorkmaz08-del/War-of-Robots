"""Makes the install icons for the web app (PWA) and the Android app from the game's app icon.

Run from the repository root:  python3 Tools/pwa/make_icons.py [menu-screenshot.png]

- TemplateData/icon-192.png, icon-512.png: plain icons (browser tabs, desktop installs)
- TemplateData/icon-maskable-512.png: the robot shrunk into the safe circle, so Android
  can cut the icon into a circle, squircle or teardrop without clipping the guns or feet
- TemplateData/screenshot-wide.jpg: shown in Android's install sheet (optional argument)
- Assets/Art/AppIconAdaptive*.png: Android 8+ adaptive icon layers (native app)
"""
import sys
from PIL import Image, ImageFilter

SRC = "Assets/Art/AppIcon.png"
TPL = "Assets/WebGLTemplates/WarOfRobots/TemplateData/"
BG = (10, 14, 18)


def padded(icon, scale, size=1024):
    """The icon shrunk to `scale` and centred on the same dark steel colour, with a soft
    edge so no square seam shows."""
    inner = int(size * scale)
    small = icon.resize((inner, inner), Image.LANCZOS)
    base = Image.new("RGB", (size, size), BG)
    mask = Image.new("L", (inner, inner), 0)
    edge = max(8, inner // 14)
    mask.paste(255, (edge, edge, inner - edge, inner - edge))
    mask = mask.filter(ImageFilter.GaussianBlur(edge / 2))
    off = (size - inner) // 2
    base.paste(small, (off, off), mask)
    return base


def main():
    icon = Image.open(SRC).convert("RGB")
    icon.resize((192, 192), Image.LANCZOS).save(TPL + "icon-192.png", optimize=True)
    icon.resize((512, 512), Image.LANCZOS).save(TPL + "icon-512.png", optimize=True)
    # Maskable safe zone is the centre circle of 80% diameter; the robot's guns and
    # feet reach the corners, so 0.72 keeps them inside.
    padded(icon, 0.72).resize((512, 512), Image.LANCZOS).save(TPL + "icon-maskable-512.png", optimize=True)
    # Android adaptive icon: 108 dp layers, only the centre 66 dp is always visible.
    padded(icon, 0.60).save("Assets/Art/AppIconAdaptiveBack.png", optimize=True)
    Image.new("RGBA", (1024, 1024), (0, 0, 0, 0)).save("Assets/Art/AppIconAdaptiveFront.png", optimize=True)
    # Round launcher icon (older Android): circle cut, so a little padding.
    padded(icon, 0.80).save("Assets/Art/AppIconRound.png", optimize=True)
    if len(sys.argv) > 1:
        shot = Image.open(sys.argv[1]).convert("RGB")
        shot.save(TPL + "screenshot-wide.jpg", quality=86, optimize=True, progressive=True)


if __name__ == "__main__":
    main()
