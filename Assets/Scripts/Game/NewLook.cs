using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>The 1.1 look (design spec "Block Drop design"): lit jewel blocks, a deeper jewel background,
    /// Fredoka and Nunito type, a feedback ladder that scales with the clear, and a reduced-motion path.
    /// Everything sits behind one switch so the release build can ship with it off and turn it on later.
    /// The rules it uses (ladder, springs, near miss, defaults) live in Core/Feel.cs and are unit tested.</summary>
    public static class NewLook
    {
        private const string Pref = "newLook", MotionPref = "motion";

        /// <summary>-1 means the player never chose, so <see cref="Look.NewLookDefault"/> decides.</summary>
        public static bool On => Look.NewLook(PlayerPrefs.GetInt(Pref, -1));

        public static void Toggle() { PlayerPrefs.SetInt(Pref, On ? 0 : 1); PlayerPrefs.Save(); }

        // ---------- reduced motion ----------

        private static float _systemScale = -1f;

        /// <summary>Android "remove animations" sets animator_duration_scale to 0. Read once; 1 elsewhere.</summary>
        private static float SystemAnimatorScale()
        {
            if (_systemScale >= 0f) return _systemScale;
            _systemScale = 1f;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var global = new AndroidJavaClass("android.provider.Settings$Global");
                _systemScale = global.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f);
            }
            catch (System.Exception) { _systemScale = 1f; }
#endif
            return _systemScale;
        }

        public static bool ReducedMotion => On && Feel.ReducedMotion(PlayerPrefs.GetInt(MotionPref, -1), SystemAnimatorScale());

        public static void ToggleMotion() { PlayerPrefs.SetInt(MotionPref, ReducedMotion ? 0 : 1); PlayerPrefs.Save(); }

        // ---------- type ----------

        private static Font _display, _body;
        private static bool _fontsTried;

        /// <summary>Fredoka Bold for headings, Nunito ExtraBold for everything else (both SIL OFL 1.1, see Resources/Fonts).
        /// Null if the files are missing, which falls back to the default font.</summary>
        public static Font Display { get { LoadFonts(); return _display; } }
        public static Font Body { get { LoadFonts(); return _body; } }

        private static void LoadFonts()
        {
            if (_fontsTried) return;
            _fontsTried = true;
            _display = Resources.Load<Font>("Fonts/Fredoka-Bold");
            _body = Resources.Load<Font>("Fonts/Nunito-ExtraBold");
        }

        // ---------- block patterns: a shape on each colour, opt in ----------

        private const string PatternPref = "patterns";
        private static readonly Sprite[] _patterns = new Sprite[6];

        public static bool PatternsOn => Look.Patterns(PlayerPrefs.GetInt(PatternPref, -1));

        public static void TogglePatterns() { PlayerPrefs.SetInt(PatternPref, PatternsOn ? 0 : 1); PlayerPrefs.Save(); }

        /// <summary>The mark drawn over a block of this colour, or null when it gets none. Dark and untinted,
        /// so it reads on every theme.</summary>
        public static Sprite Pattern(int colorIndex)
        {
            var kind = Look.PatternFor(colorIndex);
            if (kind == BlockPattern.None) return null;
            int k = (int)kind;
            return _patterns[k] ??= Make(BuildPattern(kind, 128), 128);
        }

        private static Texture2D BuildPattern(BlockPattern kind, int n)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            float aa = 1.5f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                    float d;   // signed distance to the shape, negative inside
                    switch (kind)
                    {
                        case BlockPattern.Dot: d = new Vector2(u, v).magnitude - 0.13f; break;
                        case BlockPattern.Ring: d = Mathf.Abs(new Vector2(u, v).magnitude - 0.16f) - 0.045f; break;
                        case BlockPattern.Cross: d = Mathf.Min(Mathf.Max(Mathf.Abs(u) - 0.05f, Mathf.Abs(v) - 0.2f), Mathf.Max(Mathf.Abs(u) - 0.2f, Mathf.Abs(v) - 0.05f)); break;
                        case BlockPattern.Diamond: d = (Mathf.Abs(u) + Mathf.Abs(v) - 0.2f) * 0.7071f; break;
                        default:   // diagonal stripes inside the block face
                            float s = (u + v) / 0.1414f;
                            d = Mathf.Max((Mathf.Abs(s - Mathf.Round(s)) - 0.25f) * 0.1414f, Sdf(u + 0.5f, v + 0.5f, 0.22f) + 0.14f);
                            break;
                    }
                    px[y * n + x] = new Color(0f, 0f, 0f, 0.38f * Mathf.Clamp01(0.5f - d / aa));
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        // ---------- board pulse: the board dips and springs back on a clear ----------

        private static Spring _pulse = new Spring(1f);

        public static void Pulse(FeedbackTier tier)
        {
            if (ReducedMotion || tier < FeedbackTier.Great) return;
            _pulse.Velocity -= 0.25f + 0.12f * (int)tier;
        }

        public static void Tick(Transform board)
        {
            if (!On || ReducedMotion) { _pulse.Snap(1f); board.localScale = Vector3.one; return; }
            _pulse.Step(1f, Time.unscaledDeltaTime, 5f, 0.55f);
            board.localScale = Vector3.one * Mathf.Clamp(_pulse.Value, 0.9f, 1.08f);
        }

        // ---------- art ----------

        private static Sprite _tile, _gloss, _background;

        public static readonly Color CameraBackground = Visuals.Hex("14104F");

        /// <summary>Lit jewel block, grayscale so the theme tints it: a light face from the top-left,
        /// a crisp bright rim on top, a deeper shaded lip at the bottom and a soft contact shadow.</summary>
        public static Sprite Tile => _tile ??= Make(BuildTile(256), 256);

        private static Texture2D BuildTile(int n)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float d = Sdf(u, v, 0.22f);
                    float a = Mathf.Clamp01(0.5f - d * n);
                    float face = 0.84f + 0.24f * v - 0.08f * u;                  // light from the top-left
                    float lip = Mathf.Clamp01(1f - (v - 0.02f) / 0.12f);         // bottom lip
                    float rimTop = Mathf.Clamp01(1f - Mathf.Abs(v - 0.91f) / 0.03f) * Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) / 0.30f); // bright top edge
                    float edge = Mathf.Clamp01(1f + d / 0.05f);                  // 1 at the outline, 0 inside
                    float shade = face - lip * 0.28f + rimTop * 0.25f - edge * 0.08f;
                    px[y * n + x] = new Color(Mathf.Clamp01(shade), Mathf.Clamp01(shade), Mathf.Clamp01(shade), a);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>A sharper specular: a soft band across the top and a small hot spot, not tinted.</summary>
        public static Sprite Gloss => _gloss ??= Make(BuildGloss(256), 256);

        private static Texture2D BuildGloss(int n)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    if (Sdf(u, v, 0.22f) > -0.07f) { px[y * n + x] = Color.clear; continue; }
                    float band = Mathf.Clamp01(1f - Mathf.Abs(v - 0.80f) / 0.08f) * Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) / 0.36f);
                    float spot = Mathf.Clamp01(1f - new Vector2(u - 0.26f, v - 0.78f).magnitude / 0.07f);
                    px[y * n + x] = new Color(1, 1, 1, band * 0.38f + spot * spot * 0.9f);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>Jewel night sky: a violet glow at the top fading to deep navy (#6A47E8 → #302298 → #14104F).
        /// Same size and pixels-per-unit as the old background, so the camera fit is unchanged.</summary>
        public static Sprite Background => _background ??= Make(BuildBackground(512, 1024), 512);

        private static Texture2D BuildBackground(int w, int h)
        {
            var t = NewTex(w, h);
            t.filterMode = FilterMode.Bilinear;
            Color glow = Visuals.Hex("6A47E8"), mid = Visuals.Hex("302298"), deep = Visuals.Hex("14104F");
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1), v = (float)y / (h - 1);
                    float r = new Vector2((u - 0.5f) / 1.2f, (1f - v) / 0.7f).magnitude;  // radial from top centre
                    Color c = r < 0.45f ? Color.Lerp(glow, mid, r / 0.45f) : Color.Lerp(mid, deep, Mathf.Clamp01((r - 0.45f) / 0.55f));
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>Swaps every sprite already in the scene between the two looks, so the switch takes effect
        /// without rebuilding anything. New sprites pick the look up from <see cref="Visuals"/>.</summary>
        public static void Apply(Transform world, Camera cam)
        {
            bool on = On;
            Visuals.UseNewLook = on;
            Sprite tile = on ? Tile : Visuals.OldTile, gloss = on ? Gloss : Visuals.OldGloss, bg = on ? Background : Visuals.OldBackground;
            foreach (var sr in world.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null) continue; // empty overlays (marks) must not match a look not built yet
                if (sr.sprite == Visuals.OldTile || sr.sprite == _tile) sr.sprite = tile;
                else if (sr.sprite == Visuals.OldGloss || sr.sprite == _gloss) sr.sprite = gloss;
                else if (sr.sprite == Visuals.OldBackground || sr.sprite == _background) sr.sprite = bg;
            }
            cam.backgroundColor = on ? CameraBackground : Visuals.Hex("1A1150");
        }

        private static float Sdf(float u, float v, float r)
        {
            float qx = Mathf.Abs(u - 0.5f) - (0.5f - r), qy = Mathf.Abs(v - 0.5f) - (0.5f - r);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };

        private static Sprite Make(Texture2D t, float ppu) =>
            Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
    }
}
