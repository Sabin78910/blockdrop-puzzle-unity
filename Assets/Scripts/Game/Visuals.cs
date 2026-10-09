using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>All art is generated in code at high resolution: crisp on any screen, no asset files.
    /// Style: glossy bevelled "jewel" blocks on a deep blue-violet gradient (genre-standard look).</summary>
    public static class Visuals
    {
        public static readonly Color[] BlockColors =
        {
            Hex("FF4D6D"), // ruby
            Hex("FFB627"), // amber
            Hex("2EE59D"), // emerald
            Hex("2EC4F1"), // sky
            Hex("A66CFF"), // amethyst
            Hex("6B7390"), // stone (level obstacles)
        };

        public static readonly Color BoardPanel = Hex("101845");
        public static readonly Color EmptySlot = Hex("1E2A6B");
        public static readonly Color Gold = Hex("FFD54A");

        private static Sprite _tile, _gloss, _slot, _panel, _circle, _background;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        // Signed distance to a rounded square in [0,1]² (negative inside).
        private static float RoundedSquareSdf(float u, float v, float r)
        {
            float qx = Mathf.Abs(u - 0.5f) - (0.5f - r), qy = Mathf.Abs(v - 0.5f) - (0.5f - r);
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        private static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        }

        /// <summary>Grayscale bevelled block (tinted by SpriteRenderer.color): bright top-left rim,
        /// shaded bottom-right edge, soft vertical gradient.</summary>
        public static Sprite Tile => _tile ??= MakeSprite(BuildTile(256), 256);

        private static Texture2D BuildTile(int n)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            const float r = 0.2f, bevel = 0.11f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float d = RoundedSquareSdf(u, v, r);
                    float a = Mathf.Clamp01(0.5f - d * n);
                    float depth = Mathf.Clamp01(-d / bevel); // 0 at edge → 1 inside
                    float shade = 0.80f + 0.16f * v;           // lighter towards the top
                    float rim = 1f - depth;
                    // light from the top-left: top/left rims bright, bottom/right rims dark
                    float dir = (v - 0.5f) - (u - 0.5f);
                    shade += rim * (dir > 0 ? 0.22f : -0.32f) * Mathf.Abs(dir) * 2.2f;
                    shade -= rim * 0.05f;
                    shade = Mathf.Clamp01(shade);
                    px[y * n + x] = new Color(shade, shade, shade, a);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>White glossy highlight laid over each block (not tinted).</summary>
        public static Sprite Gloss => _gloss ??= MakeSprite(BuildGloss(256), 256);

        private static Texture2D BuildGloss(int n)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    if (RoundedSquareSdf(u, v, 0.2f) > -0.06f) { px[y * n + x] = Color.clear; continue; }
                    float ex = (u - 0.42f) / 0.36f, ey = (v - 0.76f) / 0.13f;
                    float e = 1f - (ex * ex + ey * ey);
                    float spark = Mathf.Clamp01(1f - new Vector2(u - 0.24f, v - 0.80f).magnitude / 0.06f);
                    float alpha = Mathf.Clamp01(e) * 0.55f + spark * 0.8f;
                    px[y * n + x] = new Color(1, 1, 1, alpha);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>Recessed empty board slot (inner shadow at the top).</summary>
        public static Sprite Slot => _slot ??= MakeSprite(BuildSlot(128), 128);

        private static Texture2D BuildSlot(int n)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float d = RoundedSquareSdf(u, v, 0.22f);
                    float a = Mathf.Clamp01(0.5f - d * n);
                    float shade = Mathf.Clamp01(0.82f + 0.18f * (1 - v) - (v > 0.82f ? 0.25f : 0f));
                    px[y * n + x] = new Color(shade, shade, shade, a);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>9-slice rounded panel (white, tinted per use).</summary>
        public static Sprite Panel => _panel ??= BuildPanelSprite();

        private static Sprite BuildPanelSprite()
        {
            var t = RoundedRect(128, 128, 36, Color.white, Color.white, 0f);
            return Sprite.Create(t, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128, 0, SpriteMeshType.FullRect, new Vector4(40, 40, 40, 40));
        }

        public static Sprite Circle => _circle ??= MakeSprite(BuildCircle(64), 64);

        private static Texture2D BuildCircle(int n)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude / (n / 2f);
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 2.5f));
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>Deep violet → royal blue gradient with soft light orbs.</summary>
        public static Sprite Background => _background ??= MakeSprite(BuildBackground(512, 1024), 512);

        private static Texture2D BuildBackground(int w, int h)
        {
            var t = NewTex(w, h);
            t.filterMode = FilterMode.Bilinear;
            Color top = Hex("2A0E61"), mid = Hex("3B2FB0"), bottom = Hex("0B5FD6");
            var orbs = new[] { (0.18f, 0.82f, 0.22f), (0.85f, 0.62f, 0.28f), (0.3f, 0.25f, 0.3f), (0.9f, 0.12f, 0.2f) };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                Color c = v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bottom, mid, v * 2f);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    Color p = c;
                    foreach (var (ox, oy, rad) in orbs)
                    {
                        float d = new Vector2((u - ox) * 0.5f, v - oy).magnitude / rad;
                        p += new Color(0.35f, 0.4f, 1f) * Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 0.12f;
                    }
                    float vig = new Vector2(u - 0.5f, (v - 0.5f) * 0.7f).magnitude;
                    p *= 1f - Mathf.Clamp01(vig - 0.35f) * 0.6f;
                    p.a = 1;
                    px[y * w + x] = p;
                }
            }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        private static Sprite MakeSprite(Texture2D t, float ppu)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Rounded rectangle with a vertical gradient and a darker bottom "lip" (chunky button look).</summary>
        public static Texture2D RoundedRect(int w, int h, int radius, Color top, Color bottom, float lip)
        {
            var t = NewTex(w, h);
            t.mipMapBias = -0.5f;
            var px = new Color[w * h];
            int lipPx = Mathf.RoundToInt(h * lip);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - w / 2f) - (w / 2f - radius);
                    float qy = Mathf.Abs(y + 0.5f - h / 2f) - (h / 2f - radius);
                    float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    float v = (float)y / (h - 1);
                    Color c = Color.Lerp(bottom, top, v);
                    if (y < lipPx) c *= 0.72f;                                         // 3D lip
                    if (y > h - radius * 0.9f && y < h - radius * 0.45f && Mathf.Abs(x - w / 2f) < w / 2f - radius)
                        c = Color.Lerp(c, Color.white, 0.25f);                         // top highlight
                    c.a = a * top.a;
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }
    }
}
