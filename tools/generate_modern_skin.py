"""
Generates the textures of uScript's "Modern" canvas theme.

Every texture has the same file name, pixel size and 9-slice layout as its Classic twin in
_GUI/uScriptDefault, so graphs keep exactly the same node sizes and positions in both themes.

Run from the repository root:  python3 tools/generate_modern_skin.py
Writes PNGs (and Unity .meta files for new ones) to
uScript_Dev/Assets/uScript/uScriptEditor/Editor/_GUI/uScriptModern.
"""

import os
import uuid

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "uScript_Dev", "Assets", "uScript", "uScriptEditor", "Editor", "_GUI")
CLASSIC = os.path.join(ROOT, "uScriptDefault")
MODERN = os.path.join(ROOT, "uScriptModern")
SS = 8  # supersampling factor for smooth edges

# ---------------------------------------------------------------- palette
CANVAS = (24, 27, 36)
TEXT = (230, 233, 240)

FLOW = (90, 170, 255)
EVENT_FLOW = (255, 170, 60)
TYPES = {
    "default": (200, 205, 215),
    "string": (90, 210, 120),
    "bool": (240, 80, 90),
    "float": (80, 130, 255),
    "int": (60, 200, 230),
    "vector3": (240, 200, 100),
    "gameobject": (190, 100, 255),
    "object": (245, 160, 240),
}
SELECT = (235, 240, 250)
SELECT_BLUE = (95, 170, 255)

NODES = {
    #               header           body            border
    "default":    ((62, 68, 88),   (35, 38, 50),   (70, 77, 96)),
    "event":      ((214, 128, 28), (58, 44, 30),   (240, 160, 50)),
    "selected":   ((68, 76, 100),  (38, 42, 56),   SELECT_BLUE),
    "deprecated": ((170, 50, 50),  (55, 35, 38),   (220, 80, 80)),
}


def mix(color, other, amount):
    """amount of color, the rest other."""
    return tuple(int(round(c * amount + o * (1 - amount))) for c, o in zip(color, other))


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def box(x0, y0, x1, y1):
    return [x0 * SS, y0 * SS, x1 * SS - 1, y1 * SS - 1]


def shadow(w, h, shape_box, radius, offset=(1.5, 2.0), blur=2.2, alpha=150, ellipse=False):
    layer = canvas(w, h)
    d = ImageDraw.Draw(layer)
    x0, y0, x1, y1 = shape_box
    b = box(x0 + offset[0], y0 + offset[1], x1 + offset[0], y1 + offset[1])
    if ellipse:
        d.ellipse(b, fill=(0, 0, 0, alpha))
    else:
        d.rounded_rectangle(b, radius=radius * SS, fill=(0, 0, 0, alpha))
    return layer.filter(ImageFilter.GaussianBlur(blur * SS))


def save(img, relative):
    path = os.path.join(MODERN, relative)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    write_meta(path)


def write_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return  # keep the GUID stable once it exists
    with open(meta, "w") as f:
        f.write(
            "fileFormatVersion: 2\n"
            "guid: %s\n"
            "TextureImporter:\n"
            "  serializedVersion: 2\n"
            "  mipmaps:\n"
            "    enableMipMap: 0\n"
            "  isReadable: 0\n"
            "  textureFormat: -3\n"
            "  maxTextureSize: 512\n"
            "  textureSettings:\n"
            "    filterMode: 1\n"
            "    aniso: 0\n"
            "    mipBias: -1\n"
            "    wrapMode: 1\n"
            "  nPOTScale: 0\n"
            "  textureType: 5\n"
            "  alphaIsTransparency: 1\n" % uuid.uuid4().hex)


def folder_meta(path):
    meta = path + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w") as f:
            f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  userData: \n" % uuid.uuid4().hex)


# ---------------------------------------------------------------- nodes (25x39, border 9,15,23,15)
def node(kind):
    header, body, border = NODES[kind]
    w, h = 25, 39
    shape = (1, 1, 19, 32)    # same opaque area as Classic
    header_bottom = 18        # Classic title bar is rows 1-17
    r = 4
    img = canvas(w, h)
    img.alpha_composite(shadow(w, h, shape, r, alpha=170 if kind != "event" else 120))
    d = ImageDraw.Draw(img)
    width = 2 if kind == "selected" else 1
    d.rounded_rectangle(box(*shape), radius=r * SS, fill=border + (255,))
    inner = (shape[0] + width, shape[1] + width, shape[2] - width, shape[3] - width)
    d.rounded_rectangle(box(*inner), radius=(r - width * 0.6) * SS, fill=body + (255,))
    # header: fill the top of the inner area, square bottom edge
    top = canvas(w, h)
    td = ImageDraw.Draw(top)
    td.rounded_rectangle(box(*inner), radius=(r - width * 0.6) * SS, fill=header + (255,))
    mask = canvas(w, h)
    ImageDraw.Draw(mask).rectangle(box(0, 0, w, header_bottom), fill=(255, 255, 255, 255))
    top.putalpha(Image.composite(top.getchannel("A"), Image.new("L", top.size, 0), mask.getchannel("A")))
    img.alpha_composite(top)
    # thin line under the title bar
    d.rectangle(box(inner[0], header_bottom, inner[2], header_bottom + 1), fill=mix(border, body, 0.6) + (255,))
    return finish(img, w, h)


# ---------------------------------------------------------------- sockets (13x13)
def socket(color, out=False, state=""):
    w = h = 13
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    c = 6.5
    r = 4.6 if state == "" else 5.6
    ring = 1.4
    outline = (20, 22, 30, 255) if state == "" else SELECT + (255,)
    d.ellipse(box(c - r - ring, c - r - ring, c + r + ring, c + r + ring), fill=outline)
    fill = color if state != "_connecting" else mix(color, (255, 255, 255), 0.7)
    d.ellipse(box(c - r, c - r, c + r, c + r), fill=fill + (255,))
    if out:
        # outputs get a small dark centre so their direction reads at a glance
        d.ellipse(box(c - 1.6, c - 1.6, c + 1.6, c + 1.6), fill=(20, 22, 30, 230))
    return finish(img, w, h)


# ---------------------------------------------------------------- variables (57x57, border 25,31,25,31)
def variable(ring_color, fill=None, selected=False):
    w = h = 57
    shape = (0.5, 0.5, 50.5, 50.5)
    img = canvas(w, h)
    img.alpha_composite(shadow(w, h, shape, 0, offset=(2, 2.5), blur=2.5, alpha=150, ellipse=True))
    d = ImageDraw.Draw(img)
    if selected:
        d.ellipse(box(*shape), fill=SELECT + (255,))
        inset = 3.2
    else:
        inset = 0
    ring = 2.4
    s = (shape[0] + inset, shape[1] + inset, shape[2] - inset, shape[3] - inset)
    d.ellipse(box(*s), fill=ring_color + (255,))
    s2 = (s[0] + ring, s[1] + ring, s[2] - ring, s[3] - ring)
    d.ellipse(box(*s2), fill=(fill or mix(ring_color, CANVAS, 0.24)) + (255,))
    return finish(img, w, h)


# ---------------------------------------------------------------- property nodes (19x19/19x20, border 6,12,6,12)
def prop(color, size, selected=False):
    w, h = size
    shape = (1, 1, 13, 13 + (h - 19))
    img = canvas(w, h)
    img.alpha_composite(shadow(w, h, shape, 3, alpha=150))
    d = ImageDraw.Draw(img)
    border = SELECT if selected else color
    d.rounded_rectangle(box(*shape), radius=3.5 * SS, fill=border + (255,))
    d.rounded_rectangle(box(shape[0] + 1.3, shape[1] + 1.3, shape[2] - 1.3, shape[3] - 1.3), radius=2.5 * SS,
                        fill=mix(color, CANVAS, 0.22) + (255,))
    return finish(img, w, h)


# ---------------------------------------------------------------- comments / selection box (13x13, border 6)
def comment(selected=False):
    w = h = 13
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(box(0, 0, 13, 13), radius=4.5 * SS, fill=(255, 255, 255, 235 if selected else 120))
    d.rounded_rectangle(box(1.2 if not selected else 2, 1.2 if not selected else 2, 11.8 if not selected else 11, 11.8 if not selected else 11),
                        radius=3.5 * SS, fill=(255, 255, 255, 34))
    # punch the centre so only the outline is strong
    return finish(img, w, h)


def selectionbox():
    w = h = 13
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(box(0, 0, 13, 13), radius=3 * SS, fill=SELECT_BLUE + (230,))
    d.rounded_rectangle(box(1, 1, 12, 12), radius=2.2 * SS, fill=SELECT_BLUE + (40,))
    return finish(img, w, h)


# ---------------------------------------------------------------- external connection (61x59, border 26,34,26,32)
def external(selected=False):
    w, h = 61, 59
    shape = (1, 1, 53, 52)
    img = canvas(w, h)
    img.alpha_composite(shadow(w, h, shape, 0, offset=(2, 2.5), blur=2.5, alpha=150, ellipse=True))
    d = ImageDraw.Draw(img)
    if selected:
        d.ellipse(box(*shape), fill=SELECT + (255,))
        shape = (shape[0] + 3.2, shape[1] + 3.2, shape[2] - 3.2, shape[3] - 3.2)
    d.ellipse(box(*shape), fill=NODES["event"][2] + (255,))
    d.ellipse(box(shape[0] + 2.4, shape[1] + 2.4, shape[2] - 2.4, shape[3] - 2.4), fill=NODES["event"][1] + (255,))
    return finish(img, w, h)


def mini(kind):
    header, body, border = NODES[kind]
    img = Image.new("RGBA", (7, 9), border + (255,))
    ImageDraw.Draw(img).rectangle([1, 1, 5, 7], fill=body + (255,))
    ImageDraw.Draw(img).rectangle([1, 1, 5, 3], fill=header + (255,))
    return img


def main():
    os.makedirs(os.path.join(MODERN, "elements"), exist_ok=True)
    folder_meta(MODERN)
    folder_meta(os.path.join(MODERN, "elements"))

    save(Image.new("RGB", (16, 16), CANVAS), "uscript_background.png")

    for kind in ("default", "event", "selected", "deprecated"):
        save(node(kind), "elements/uscript_node_%s.png" % kind)
    for kind in ("default", "event", "selected"):
        save(mini(kind), "elements/uscript_node_%s_mini.png" % kind)

    for owner, io_color in (("default", FLOW), ("event", EVENT_FLOW)):
        for state in ("", "_connecting", "_selected"):
            save(socket(io_color, state=state), "elements/uscript_node_%s_socket_io%s.png" % (owner, state))
            for type_name, color in TYPES.items():
                save(socket(color, state=state), "elements/uscript_node_%s_socket_variable_%s%s.png" % (owner, type_name, state))
                save(socket(color, out=True, state=state), "elements/uscript_node_%s_socket_out_variable_%s%s.png" % (owner, type_name, state))

    for type_name, color in TYPES.items():
        save(variable(color), "elements/uscript_variable_%s.png" % type_name)
    save(variable((185, 120, 255), fill=(46, 30, 72)), "elements/uscript_variable_owner.png")
    save(variable(SELECT, fill=(46, 50, 64)), "elements/uscript_variable_selected.png")

    # Property nodes keep the Classic sizes, which differ by one pixel between types
    for name in os.listdir(os.path.join(CLASSIC, "elements")):
        if name.startswith("uscript_property_") and name.endswith(".png"):
            type_name = name[len("uscript_property_"):-4]
            size = Image.open(os.path.join(CLASSIC, "elements", name)).size
            if type_name == "selected":
                save(prop((150, 156, 172), size, selected=True), "elements/" + name)
            else:
                save(prop(TYPES.get(type_name, TYPES["default"]), size), "elements/" + name)

    save(comment(), "elements/uscript_comment.png")
    save(comment(selected=True), "elements/uscript_comment_selected.png")
    save(selectionbox(), "elements/uscript_selectionbox.png")
    save(external(), "elements/uscript_externalconnection.png")
    save(external(selected=True), "elements/uscript_externalconnection_selected.png")

    # Every Classic element must have a Modern twin, with the same size
    classic = {n for n in os.listdir(os.path.join(CLASSIC, "elements")) if n.endswith(".png")}
    modern = {n for n in os.listdir(os.path.join(MODERN, "elements")) if n.endswith(".png")}
    missing = sorted(classic - modern)
    assert not missing, "No Modern texture for: %s" % missing
    for n in classic:
        a = Image.open(os.path.join(CLASSIC, "elements", n)).size
        b = Image.open(os.path.join(MODERN, "elements", n)).size
        assert a == b, "%s: Classic %s, Modern %s" % (n, a, b)
    print("Modern skin written:", len(modern), "elements")


if __name__ == "__main__":
    main()


# ---------------------------------------------------------------- title-bar icons (white line art, tinted in code)
ICON = 24          # pixels; drawn at 12x12 on screen, so it stays sharp on high-DPI displays
LINE = 2.0         # line width in icon pixels


def icon_canvas():
    return Image.new("RGBA", (ICON * SS, ICON * SS), (0, 0, 0, 0))


def P(x, y):
    return (x * SS, y * SS)


def line(d, pts, width=LINE):
    d.line([P(*q) for q in pts], fill=(255, 255, 255, 255), width=int(width * SS), joint="curve")
    for q in (pts[0], pts[-1]):
        r = width / 2
        d.ellipse([P(q[0] - r, q[1] - r), P(q[0] + r, q[1] + r)], fill=(255, 255, 255, 255))


def ring(d, cx, cy, r, width=LINE):
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], outline=(255, 255, 255, 255), width=int(width * SS))


def icons():
    shapes = {}

    def add(name):
        def wrap(fn):
            img = icon_canvas()
            fn(ImageDraw.Draw(img))
            shapes[name] = img.resize((ICON, ICON), Image.LANCZOS)
            return fn
        return wrap

    @add("event")
    def _(d):
        d.polygon([P(13.5, 2), P(5, 13.5), P(11, 13.5), P(9.5, 22), P(19, 9.5), P(12.5, 9.5), P(15, 2)], fill=(255, 255, 255, 255))

    @add("compare")
    def _(d):
        line(d, [(5, 9), (19, 9)]); line(d, [(5, 15), (19, 15)]); line(d, [(15, 4), (9, 20)])

    @add("set")
    def _(d):
        d.rounded_rectangle([P(4, 4), P(20, 20)], radius=3.5 * SS, outline=(255, 255, 255, 255), width=int(LINE * SS))
        line(d, [(8, 12.5), (11, 15.5), (16.5, 9)])

    @add("math")
    def _(d):
        line(d, [(4, 8), (11, 8)]); line(d, [(7.5, 4.5), (7.5, 11.5)])
        line(d, [(13, 16), (20, 16)])
        line(d, [(14, 4.5), (19.5, 11.5)]); line(d, [(19.5, 4.5), (14, 11.5)])
        d.ellipse([P(6.5, 14), P(8.5, 16)], fill=(255, 255, 255, 255)); d.ellipse([P(6.5, 17.5), P(8.5, 19.5)], fill=(255, 255, 255, 255))
        line(d, [(4, 16.8), (11, 16.8)], 1.6)

    @add("list")
    def _(d):
        for y in (6, 12, 18):
            d.ellipse([P(3.5, y - 1.6), P(6.7, y + 1.6)], fill=(255, 255, 255, 255))
            line(d, [(9.5, y), (20.5, y)])

    @add("string")
    def _(d):
        line(d, [(5, 5), (19, 5)]); line(d, [(12, 5), (12, 20)])

    @add("signal")
    def _(d):
        d.ellipse([P(10, 10), P(14, 14)], fill=(255, 255, 255, 255))
        d.arc([P(5.5, 5.5), P(18.5, 18.5)], 200, 340, fill=(255, 255, 255, 255), width=int(LINE * SS))
        d.arc([P(5.5, 5.5), P(18.5, 18.5)], 20, 160, fill=(255, 255, 255, 255), width=int(LINE * SS))
        d.arc([P(1.5, 1.5), P(22.5, 22.5)], 210, 330, fill=(255, 255, 255, 255), width=int(LINE * SS))
        d.arc([P(1.5, 1.5), P(22.5, 22.5)], 30, 150, fill=(255, 255, 255, 255), width=int(LINE * SS))

    @add("timer")
    def _(d):
        ring(d, 12, 13, 8.5); line(d, [(12, 13), (12, 8.5)]); line(d, [(12, 13), (15.5, 15)]); line(d, [(9.5, 2.5), (14.5, 2.5)])

    @add("loop")
    def _(d):
        d.arc([P(4, 4), P(20, 20)], 40, 330, fill=(255, 255, 255, 255), width=int(LINE * SS))
        d.polygon([P(20.5, 3.5), P(20.5, 10.5), P(14, 9)], fill=(255, 255, 255, 255))

    @add("debug")
    def _(d):
        d.rounded_rectangle([P(4, 4), P(20, 20)], radius=3 * SS, outline=(255, 255, 255, 255), width=int(LINE * SS))
        line(d, [(8, 9), (11, 12), (8, 15)]); line(d, [(13, 15.5), (16.5, 15.5)])

    @add("logic")
    def _(d):
        line(d, [(4, 12), (10, 12)]); line(d, [(10, 12), (15, 6.5), (20, 6.5)]); line(d, [(10, 12), (15, 17.5), (20, 17.5)])

    @add("input")
    def _(d):
        d.rounded_rectangle([P(7, 2.5), P(17, 21.5)], radius=5 * SS, outline=(255, 255, 255, 255), width=int(LINE * SS))
        line(d, [(12, 6), (12, 10)])

    @add("variable")
    def _(d):
        d.rounded_rectangle([P(3, 6), P(21, 18)], radius=6 * SS, outline=(255, 255, 255, 255), width=int(LINE * SS))
        d.ellipse([P(10, 10), P(14, 14)], fill=(255, 255, 255, 255))

    @add("action")
    def _(d):
        d.polygon([P(7, 4), P(19.5, 12), P(7, 20)], outline=(255, 255, 255, 255), width=int(LINE * SS))

    @add("nested")
    def _(d):
        d.rounded_rectangle([P(3, 3), P(15, 15)], radius=2.5 * SS, outline=(255, 255, 255, 255), width=int(LINE * SS))
        d.rounded_rectangle([P(9, 9), P(21, 21)], radius=2.5 * SS, outline=(255, 255, 255, 255), width=int(LINE * SS))

    return shapes


def write_icons():
    folder = os.path.join(MODERN, "icons")
    os.makedirs(folder, exist_ok=True)
    folder_meta(folder)
    shapes = icons()
    for name, img in shapes.items():
        save(img, "icons/uscript_icon_%s.png" % name)
    print("Icons written:", ", ".join(sorted(shapes)))


if __name__ == "__main__":
    write_icons()
