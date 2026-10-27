"""Draws Block Drop's level-map art from scratch (no stock images, no third-party assets), so it is ours to ship.
Run: python3 tools/art/make_map_art.py  -> Assets/Resources/Map/*.png (+ .meta files with sprite-friendly import settings)."""
# Note: the six bg_*.png backdrops are now Canva-made (1080x1920 resized to 720x1280); rerunning this script would overwrite them with the drawn fallback.
import hashlib, math, os, random
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Map")
S = 4  # supersampling

THEMES = {  # name: (sky top, sky bottom, hill far, hill near)
    "meadow": ("#8fd9ff", "#c9f5a8", "#7fcf74", "#4fae62"),
    "dusk":   ("#ffb3d1", "#ffe3c4", "#e48fbd", "#c06aa0"),
    "lagoon": ("#6fd3ff", "#d3f6ff", "#4bb8d6", "#2f8fb8"),
    "night":  ("#3b2a8c", "#8a6ee0", "#5b46b8", "#3d2f94"),
    "dunes":  ("#ffd98a", "#fff1c9", "#f0b860", "#d9953f"),
    "frost":  ("#b9ece0", "#f0fffb", "#8fd6c7", "#5fb8a8"),
}

def hexc(h, a=255):
    h = h.lstrip("#"); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))

def save(img, name):
    img.save(os.path.join(OUT, name + ".png"), optimize=True)
    guid = hashlib.md5(("blockdrop-map-" + name).encode()).hexdigest()
    with open(os.path.join(OUT, name + ".png.meta"), "w") as f:
        f.write(f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""")

def down(img, w, h): return img.resize((w, h), Image.LANCZOS)

def backdrop(name, top, bottom, far, near):
    W, H = 256, 512
    img = Image.new("RGBA", (W * S, H * S))
    d = ImageDraw.Draw(img)
    for y in range(H * S):
        d.line([(0, y), (W * S, y)], fill=lerp(hexc(top), hexc(bottom), y / (H * S)))
    rng = random.Random(name)
    for _ in range(5):  # soft clouds
        cx, cy, r = rng.randint(0, W) * S, rng.randint(20, 300) * S, rng.randint(14, 30) * S
        cl = Image.new("RGBA", img.size, (0, 0, 0, 0)); cd = ImageDraw.Draw(cl)
        for k in range(4): cd.ellipse([cx + (k * r * 0.7) - r, cy - r * 0.5, cx + (k * r * 0.7) + r, cy + r * 0.5], fill=(255, 255, 255, 70))
        img = Image.alpha_composite(img, cl.filter(ImageFilter.GaussianBlur(S * 3)))
    for base, col, amp in ((380, far, 26), (440, near, 34)):  # rolling hills
        hl = Image.new("RGBA", img.size, (0, 0, 0, 0)); hd = ImageDraw.Draw(hl)
        ph = rng.random() * 6
        pts = [(x * S, int((base + amp * math.sin(x / 40 + ph)) * S)) for x in range(0, W + 1, 4)]
        hd.polygon(pts + [(W * S, H * S), (0, H * S)], fill=hexc(col))
        img = Image.alpha_composite(img, hl)
    for _ in range(26):  # sparkles
        x, y = rng.randint(0, W) * S, rng.randint(0, 340) * S
        r = rng.randint(1, 2) * S
        sp = Image.new("RGBA", img.size, (0, 0, 0, 0)); ImageDraw.Draw(sp).ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 140))
        img = Image.alpha_composite(img, sp)
    save(down(img, W, H).convert("RGBA"), "bg_" + name)

def sprite(name, size, draw_fn):
    img = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(img), size * S)
    save(down(img, size, size), name)

def g(v, a=255): return (v, v, v, a)  # grey tones: the map tints tree and house with the chapter colour, so the shading survives

def prop(name, size, draw_fn, outline=(60, 60, 70), glow=None):
    """Props are drawn big, then given a soft ground shadow and a dark outline so they sit in the painted scenery."""
    n = size * S
    body = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(body), n)
    alpha = body.split()[3]
    edge = alpha.filter(ImageFilter.MaxFilter(2 * S + 1))  # about 2 px of outline once scaled down
    ol = Image.new("RGBA", (n, n), outline + (255,)); ol.putalpha(edge)
    sh = Image.new("RGBA", (n, n), (0, 0, 0, 0)); ImageDraw.Draw(sh).ellipse([n * .16, n * .86, n * .84, n * .99], fill=(30, 20, 50, 110))
    img = Image.alpha_composite(sh.filter(ImageFilter.GaussianBlur(S * 2)), ol)
    if glow:
        gl = Image.new("RGBA", (n, n), (0, 0, 0, 0)); ImageDraw.Draw(gl).ellipse([n * .12, n * .0, n * .88, n * .56], fill=glow)
        img = Image.alpha_composite(img, gl.filter(ImageFilter.GaussianBlur(S * 5)))
    img = Image.alpha_composite(img, body)
    save(down(img, size, size), name)

def tree(d, n):
    d.rounded_rectangle([n * .43, n * .52, n * .57, n * .93], n * .04, fill=g(150))
    d.rounded_rectangle([n * .5, n * .52, n * .57, n * .93], n * .03, fill=g(110))           # shaded side of the trunk
    for cx, cy, r, c in ((.5, .36, .3, 130), (.36, .5, .2, 150), (.66, .5, .2, 140), (.5, .26, .22, 175)):
        d.ellipse([n * (cx - r), n * (cy - r), n * (cx + r), n * (cy + r)], fill=g(c))     # dark base blobs
    for cx, cy, r, c in ((.46, .3, .2, 205), (.34, .45, .12, 215), (.62, .42, .13, 195)):
        d.ellipse([n * (cx - r), n * (cy - r), n * (cx + r), n * (cy + r)], fill=g(c))     # sunlit leaves
    for cx, cy in ((.4, .22), (.55, .2), (.3, .42)):
        d.ellipse([n * (cx - .05), n * (cy - .035), n * (cx + .05), n * (cy + .035)], fill=g(250))  # highlights

def house(d, n):
    d.rectangle([n * .62, n * .14, n * .72, n * .36], fill=g(120))                         # chimney
    d.rectangle([n * .2, n * .45, n * .8, n * .9], fill=g(225))                            # wall
    d.rectangle([n * .62, n * .45, n * .8, n * .9], fill=g(185))                           # wall in shade
    d.polygon([(n * .1, n * .5), (n * .5, n * .1), (n * .9, n * .5)], fill=g(140))         # roof
    d.polygon([(n * .1, n * .5), (n * .5, n * .1), (n * .5, n * .5)], fill=g(175))         # lit roof plane
    d.line([(n * .1, n * .5), (n * .5, n * .1), (n * .9, n * .5)], fill=g(95), width=int(n * .025))
    d.rectangle([n * .1, n * .5, n * .9, n * .54], fill=g(105))                            # eave
    d.rounded_rectangle([n * .42, n * .6, n * .58, n * .9], n * .03, fill=g(110))          # door
    d.ellipse([n * .53, n * .74, n * .56, n * .77], fill=g(235))
    for x0 in (.26, .64):
        d.rectangle([n * x0, n * .57, n * (x0 + .12), n * .71], fill=g(255))               # window frame
        d.rectangle([n * (x0 + .015), n * .585, n * (x0 + .105), n * .695], fill=(143, 208, 255, 255))
        d.line([(n * (x0 + .06), n * .585), (n * (x0 + .06), n * .695)], fill=g(255), width=int(n * .015))

def lamp(d, n):
    d.rounded_rectangle([n * .46, n * .36, n * .54, n * .93], n * .025, fill=hexc("#7d8190"))
    d.rounded_rectangle([n * .46, n * .36, n * .49, n * .93], n * .015, fill=hexc("#b9bdca"))   # metal highlight
    d.rounded_rectangle([n * .38, n * .88, n * .62, n * .95], n * .03, fill=hexc("#5f6270"))   # base
    d.rounded_rectangle([n * .34, n * .28, n * .66, n * .38], n * .03, fill=hexc("#5f6270"))   # lamp holder
    d.ellipse([n * .3, n * .06, n * .7, n * .42], fill=hexc("#ffd95a"))                         # bulb
    d.ellipse([n * .35, n * .1, n * .65, n * .36], fill=hexc("#fff3a8"))
    d.ellipse([n * .4, n * .13, n * .52, n * .24], fill=hexc("#ffffff"))                        # glint

def node(d, n):  # glossy button: white so the screen can tint it
    d.ellipse([n * .04, n * .1, n * .96, n * .98], fill=(0, 0, 0, 90))
    d.ellipse([n * .04, n * .04, n * .96, n * .92], fill=hexc("#a8a8a8"))
    d.ellipse([n * .08, n * .06, n * .92, n * .86], fill=hexc("#ffffff"))
    d.ellipse([n * .2, n * .12, n * .8, n * .45], fill=(255, 255, 255, 255))
    d.ellipse([n * .12, n * .5, n * .88, n * .84], fill=(215, 215, 215, 255))

def star(fill):
    def f(d, n):
        pts = [(n * (.5 + (.46 if i % 2 == 0 else .2) * math.sin(i * math.pi / 5)), n * (.54 - (.46 if i % 2 == 0 else .2) * math.cos(i * math.pi / 5))) for i in range(10)]
        d.polygon(pts, fill=hexc("#ffcf3a") if fill else (255, 255, 255, 70), outline=hexc("#c98a00") if fill else (255, 255, 255, 140))
    return f

def crown(d, n):
    d.polygon([(n * .1, n * .8), (n * .06, n * .3), (n * .3, n * .52), (n * .5, n * .2), (n * .7, n * .52), (n * .94, n * .3), (n * .9, n * .8)], fill=hexc("#ffcf3a"), outline=hexc("#c98a00"))
    d.rectangle([n * .1, n * .78, n * .9, n * .9], fill=hexc("#f0a800"))
    for x, c in ((.3, "#ff4d6d"), (.5, "#46d6ff"), (.7, "#ff4d6d")): d.ellipse([n * (x - .05), n * .7, n * (x + .05), n * .8], fill=hexc(c))

def lock(d, n):
    d.arc([n * .28, n * .1, n * .72, n * .6], 180, 360, fill=hexc("#e8e8e8"), width=int(n * .09))
    d.rounded_rectangle([n * .2, n * .4, n * .8, n * .9], n * .08, fill=hexc("#e8e8e8"))
    d.ellipse([n * .44, n * .56, n * .56, n * .68], fill=hexc("#8a8a8a"))

def ribbon():
    W, H = 512, 96
    img = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.polygon([(0, H * .25 * S), (50 * S, H * .25 * S), (50 * S, H * .95 * S), (0, H * .95 * S), (24 * S, H * .6 * S)], fill=hexc("#6a2fb0"))
    d.polygon([(W * S, H * .25 * S), ((W - 50) * S, H * .25 * S), ((W - 50) * S, H * .95 * S), (W * S, H * .95 * S), ((W - 24) * S, H * .6 * S)], fill=hexc("#6a2fb0"))
    d.rounded_rectangle([34 * S, 4 * S, (W - 34) * S, H * .86 * S], 12 * S, fill=hexc("#9b4dff"))
    d.rounded_rectangle([34 * S, 4 * S, (W - 34) * S, H * .42 * S], 12 * S, fill=hexc("#b77dff"))
    save(down(img, W, H), "ribbon")

os.makedirs(OUT, exist_ok=True)
for i, (k, v) in enumerate(THEMES.items()): backdrop(k, *v)
prop("tree", 128, tree); prop("house", 128, house); prop("lamp", 128, lamp, outline=(70, 60, 90), glow=(255, 230, 120, 140))
for n, fn in (("node", node), ("star_on", star(True)), ("star_off", star(False)), ("crown", crown), ("lock", lock)):
    sprite(n, 128 if n == "node" else 64, fn)
ribbon()
print(sorted(os.listdir(OUT)))
