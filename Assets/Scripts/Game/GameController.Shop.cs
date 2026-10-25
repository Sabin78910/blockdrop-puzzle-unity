using System.Collections.Generic;
using System.Globalization;
using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>The shop: one hero pack, three tier cards with a visible coin bonus, rich bundle cards and two
    /// single items. Cards slide in one after another, press with a small bounce and pay out a coin burst.
    /// Everything is drawn from shapes, so it needs no art files.</summary>
    public sealed partial class GameController
    {
        private float _shopOpened = -10f, _shopBurstAt = -10f;
        private Vector2 _shopBurstFrom;
        private readonly Dictionary<string, GUIStyle> _shopStyles = new Dictionary<string, GUIStyle>();

        private GUIStyle ShopStyle(string top, string bottom)
        {
            string key = top + bottom;
            if (!_shopStyles.TryGetValue(key, out var st))
                _shopStyles[key] = st = Sliced(Visuals.RoundedRect(96, 96, 32, Visuals.Hex(top), Visuals.Hex(bottom), 0.1f), 34);
            return st;
        }

        private static float CoinsPerDollar(Product p) =>
            float.TryParse(p.Price.TrimStart('$'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 ? p.Coins / d : 0f;

        private static bool IsCoinPack(Product p) => p.Consumable && p.Coins > 0 && p.Hammers == 0 && p.Shuffles == 0 && p.Themes.Length == 0;

        /// <summary>How many more coins a pack gives per dollar than the smallest pack, as a whole percent.</summary>
        private static int CoinBonusPercent(Product p)
        {
            var baseRate = CoinsPerDollar(Store.Products[0]);
            return baseRate <= 0 ? 0 : Mathf.RoundToInt((CoinsPerDollar(p) / baseRate - 1f) * 100f);
        }

        /// <summary>A card: slides in with its turn, shrinks a touch while pressed, optional soft glow. Returns the
        /// rect to draw into. Call <see cref="EndShopCard"/> afterwards to restore the colour.</summary>
        private Rect BeginShopCard(Rect r, int order, string top, string bottom, bool glow, bool dim = false, string glowHex = "FFD959")
        {
            float t = Time.unscaledTime - _shopOpened - order * 0.07f;
            float k = Mathf.Clamp01(t * 4f), a = Mathf.Clamp01(t * 5f);
            if (NewLook.ReducedMotion) { k = 1f; a = 1f; }
            r.y += (1f - EaseOutBack(k)) * _fitSafe.height * 0.05f;
            bool pressed = Input.GetMouseButton(0) && r.Contains(ToGui(Input.mousePosition)) && !NewLook.ReducedMotion;
            if (pressed) r = new Rect(r.x + r.width * 0.015f, r.y + r.height * 0.03f, r.width * 0.97f, r.height * 0.94f);
            float alpha = a * (dim ? 0.7f : 1f);
            if (glow && Event.current.type == EventType.Repaint)
            {
                float pulse = NewLook.ReducedMotion ? 0.5f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
                float grow = _fitSafe.height * (0.004f + 0.004f * pulse);
                var gc = Visuals.Hex(glowHex); var old = GUI.color; GUI.color = new Color(gc.r, gc.g, gc.b, alpha * (0.25f + 0.25f * pulse));
                ShopStyle("FFFFFF", "FFFFFF").Draw(new Rect(r.x - grow, r.y - grow, r.width + grow * 2, r.height + grow * 2), false, false, false, false);
                GUI.color = old;
            }
            GUI.color = new Color(1, 1, 1, alpha);
            Panel(r, ShopStyle(top, bottom));
            return r;
        }

        private static void EndShopCard() => GUI.color = Color.white;

        /// <summary>A rounded pill with a short label: badges, bonus tags and the coin counter.</summary>
        private void Pill(Rect r, string text, string top, string bottom, Color textColor)
        {
            Panel(r, ShopStyle(top, bottom));
            Outlined(r, text, new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(8, Mathf.RoundToInt(_small.fontSize * 0.8f)) }, textColor, 1f);
        }

        private static void Disc(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color; GUI.color = new Color(c.r, c.g, c.b, c.a * old.a);
            GUI.DrawTexture(r, Visuals.Circle.texture); GUI.color = old;
        }

        /// <summary>A little pyramid of gold coins (1, 3, 6 or 10) that grows with the pack.</summary>
        private void CoinPile(Rect area, int rows)
        {
            float d = Mathf.Min(area.width / rows, area.height / (1f + (rows - 1) * 0.72f));
            float totalH = d * (1f + (rows - 1) * 0.72f);
            float y0 = area.y + (area.height - totalH) / 2f;
            for (int row = rows - 1; row >= 0; row--)          // top coin first, so lower rows overlap it
            {
                int n = row + 1;
                float x0 = area.center.x - n * d / 2f;
                for (int i = 0; i < n; i++)
                {
                    var c = new Rect(x0 + i * d + d * 0.04f, y0 + row * d * 0.72f, d * 0.92f, d * 0.92f);
                    Disc(new Rect(c.x - d * 0.03f, c.y + d * 0.04f, c.width, c.height), Visuals.Hex("A8620A"));
                    Disc(c, Visuals.Hex("FFB627"));
                    Disc(new Rect(c.x + c.width * 0.12f, c.y + c.height * 0.12f, c.width * 0.76f, c.height * 0.76f), Visuals.Hex("FFE27A"));
                    Disc(new Rect(c.x + c.width * 0.3f, c.y + c.height * 0.3f, c.width * 0.4f, c.height * 0.4f), Visuals.Hex("FFC93C"));
                }
            }
        }

        /// <summary>One gold coin, the same look as the piles.</summary>
        private static void GoldCoin(Rect c)
        {
            float d = c.height;
            Disc(new Rect(c.x - d * 0.03f, c.y + d * 0.05f, c.width, c.height), Visuals.Hex("A8620A"));
            Disc(c, Visuals.Hex("FFB627"));
            Disc(new Rect(c.x + c.width * 0.12f, c.y + c.height * 0.12f, c.width * 0.76f, c.height * 0.76f), Visuals.Hex("FFE27A"));
            Disc(new Rect(c.x + c.width * 0.3f, c.y + c.height * 0.3f, c.width * 0.4f, c.height * 0.4f), Visuals.Hex("FFC93C"));
        }


        /// <summary>The contents of a product as a wrapped line of items joined by "+", with a real gold coin where
        /// an amount of coins goes. The font shrinks until every line fits the box.</summary>
        private void DrawContentChips(Rect box, List<string> parts, Color color)
        {
            for (float f = 0.8f; f >= 0.4f; f -= 0.05f)
            {
                var st = new GUIStyle(_small) { wordWrap = false, alignment = TextAnchor.MiddleLeft, fontSize = Mathf.Max(8, Mathf.RoundToInt(_small.fontSize * f)) };
                float lh = st.CalcSize(new GUIContent("Ag")).y, cd = lh * 0.85f, plus = st.CalcSize(new GUIContent(" + ")).x;
                var items = new List<(string text, bool coin, float width)>();
                foreach (var part in parts)
                {
                    bool coin = part.EndsWith(" ●");
                    string text = coin ? part.Substring(0, part.Length - 2) : part;
                    items.Add((text, coin, st.CalcSize(new GUIContent(text)).x + (coin ? cd * 1.2f : 0f)));
                }
                // lay out: wrap before an item that does not fit
                var pos = new List<(float x, int line)>();
                float x = 0; int line = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    float need = items[i].width + (i < items.Count - 1 ? plus : 0f);
                    if (x > 0 && x + need > box.width) { x = 0; line++; }
                    pos.Add((x, line)); x += need;
                }
                if ((line + 1) * lh > box.height && f > 0.4f) continue;
                for (int i = 0; i < items.Count; i++)
                {
                    var (ix, il) = pos[i];
                    var r = new Rect(box.x + ix, box.y + il * lh, items[i].width + plus, lh);
                    Outlined(r, items[i].text, st, color, 1f);
                    if (items[i].coin) GoldCoin(new Rect(r.x + st.CalcSize(new GUIContent(items[i].text)).x + cd * 0.1f, r.y + (lh - cd) / 2f, cd, cd));
                    if (i < items.Count - 1) Outlined(new Rect(r.x + items[i].width, r.y, plus, lh), " + ", st, color, 1f);
                }
                return;
            }
        }

        private void PriceButton(Rect r, Product p, int index, bool hero)
        {
            bool owned = !p.Consumable && (_meta.Purchases & (1 << index)) != 0;
            if (owned) { Pill(r, "OWNED", "2EE59D", "13A86E", Color.white); return; }
            GUI.enabled = _store.Ready;
            if (Btn(r, _store.LocalPrice(p.Id) ?? p.Price, hero ? "FFD54A" : "4BE38A", hero ? "D99A00" : "1FA45B", hero ? 0.85f : 0.7f)) { _shopBurstFrom = r.center; Buy(p); }
            GUI.enabled = true;
        }


        private void CoinPill(float w, float h)
        {
            var coinPill = new Rect(w * 0.34f, h * 0.103f, w * 0.32f, h * 0.036f);
            Panel(coinPill, _barBackStyle);
            GoldCoin(new Rect(coinPill.x + coinPill.height * 0.15f, coinPill.y + coinPill.height * 0.12f, coinPill.height * 0.76f, coinPill.height * 0.76f));
            Outlined(new Rect(coinPill.x + coinPill.height * 0.9f, coinPill.y, coinPill.width - coinPill.height, coinPill.height), _meta.Coins.ToString("N0", CultureInfo.InvariantCulture), new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter }, Visuals.Gold, 1.2f);
        }

        private static readonly string[] TierTop = { "4FD8FF", "B98CFF", "FFD54A", "3DE0C0" };
        private static readonly string[] TierBottom = { "1B7FD6", "6A33D1", "E08A00", "138F78" };

        private void DrawThemes(float w, float h)
        {
            Outlined(new Rect(0, h * 0.03f, w, h * 0.075f), "THEMES", _h2, Visuals.Hex("FF8FAB"), 3);
            CoinPill(w, h);
            string[] tabs = { "STARTER", "PREMIUM", "LEGENDARY", "EXCLUSIVE" };
            float tw = w * 0.88f / tabs.Length;
            for (int k = 0; k < tabs.Length; k++)
            {
                bool on = (int)_themeTab == k;
                if (Btn(new Rect(w * 0.06f + k * tw, h * 0.155f, tw - w * 0.01f, h * 0.05f), tabs[k], on ? TierTop[k] : "5A4FA8", on ? TierBottom[k] : "3A2F80", 0.55f)) _themeTab = (ThemeTier)k;
            }
            int tier = (int)_themeTab, row = 0;
            for (int t = 0; t < PlayerMeta.Themes.Length; t++)
            {
                var th = PlayerMeta.Themes[t];
                if (th.Tier != _themeTab) continue;
                bool owned = _meta.Owns(t), selected = _meta.ThemeIndex == t, afford = _meta.Coins >= th.Price;
                bool locked = !owned && (!th.Buyable || !afford);
                var card = BeginShopCard(new Rect(w * 0.05f, h * (0.225f + row * 0.118f), w * 0.9f, h * 0.108f), row, selected ? "2EE59D" : TierTop[tier], selected ? "13805A" : TierBottom[tier], selected || th.Tier == ThemeTier.Legendary, locked, selected ? "7CF8C0" : "FFD959");
                row++;
                Outlined(new Rect(card.x + w * 0.04f, card.y + h * 0.005f, w * 0.5f, h * 0.036f), Loc.L(th.Name).ToUpperInvariant(), new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = false }, Color.white, 1.5f);
                if (th.Tagline != "") Outlined(new Rect(card.x + w * 0.04f, card.y + h * 0.039f, w * 0.5f, h * 0.024f), Loc.L(th.Tagline), new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = Mathf.Max(8, Mathf.RoundToInt(_small.fontSize * 0.85f)) }, new Color(1, 1, 1, 0.85f), 1f);
                // a little board in the theme's own colours
                float sw = h * 0.028f;
                var board = new Rect(card.x + w * 0.04f, card.y + h * 0.067f, 5 * (sw + 4) + 6, sw + 6);
                Panel(board, ShopStyle(th.Panel, th.Panel));
                for (int k = 0; k < 5; k++) Panel(new Rect(board.x + 3 + k * (sw + 4), board.y + 3, sw, sw), Swatch(Visuals.Hex(th.Blocks[k])));
                var btn = new Rect(card.xMax - w * 0.32f, card.y + h * 0.034f, w * 0.28f, h * 0.056f);
                if (selected) Pill(new Rect(btn.x, btn.y + btn.height * 0.1f, btn.width, btn.height * 0.8f), "IN USE", "FFFFFF", "C9F7E4", Visuals.Hex("0E6B48"));
                else if (!owned && !th.Buyable)
                {
                    float need = th.ChapterReward > 0 ? th.ChapterReward : th.StarsReward;
                    float have = th.ChapterReward > 0 ? Mathf.Min(Collection.CompletedPictures(_progress), th.ChapterReward) : Mathf.Min(_progress.TotalStars, th.StarsReward);
                    Panel(new Rect(btn.x, btn.y + btn.height * 0.55f, btn.width, btn.height * 0.3f), _barBackStyle);
                    if (have > 0) Panel(new Rect(btn.x, btn.y + btn.height * 0.55f, Mathf.Max(btn.height * 0.3f, btn.width * have / need), btn.height * 0.3f), _barFillStyle);
                    Outlined(new Rect(btn.x, btn.y - btn.height * 0.1f, btn.width, btn.height * 0.6f), ExclusiveProgress(th), new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter }, Color.white, 1.2f);
                }
                else if (Btn(btn, owned ? "USE" : $"{th.Price} ●", owned ? "6E8BFF" : afford ? "FFD54A" : "6C6590", owned ? "3A4FE0" : afford ? "D99A00" : "463F66", 0.75f))
                {
                    if (_meta.BuyOrSelect(t)) { ApplyTheme(); SaveMeta(); _fx.Star(); if (!owned) { Confetti(th.Tier == ThemeTier.Legendary ? 70 : 25); _shopBurstFrom = btn.center; _shopBurstAt = Time.unscaledTime; } }
                    else _fx.Invalid();
                }
                EndShopCard();
            }
            DrawShopCoinBurst(w, h);
            float by = h * 0.86f, bw = w * 0.4f;
            if (Btn(new Rect(w * 0.07f, by, bw, h * 0.07f), "BACK", "5A4FA8", "3A2F80")) GoBack();
            if (Btn(new Rect(w * 0.53f, by, bw, h * 0.07f), "SHOP", "FFD54A", "D99A00")) ShowMenu(Screen.Shop);
        }

        private void DrawShop(float w, float h)
        {
            Outlined(new Rect(0, h * 0.03f, w, h * 0.075f), "SHOP", _h2, Visuals.Gold, 3);
            CoinPill(w, h);
            if (!_store.Ready)
            {
                string note = Loc.L("Purchases open when Block Drop launches on Google Play.");
                var noteStyle = new GUIStyle(_small) { wordWrap = false, alignment = TextAnchor.MiddleCenter };
                while (noteStyle.fontSize > 8 && noteStyle.CalcSize(new GUIContent(note)).x > w * 0.88f) noteStyle.fontSize--;
                Outlined(new Rect(w * 0.06f, h * 0.142f, w * 0.88f, h * 0.026f), note, noteStyle, new Color(1, 1, 1, 0.75f), 1f);
            }

            var P = Store.Products;
            // heights as fractions of the screen; scaled down together if the safe area is short
            float heroH = 0.135f, tierH = 0.175f, labelH = 0.024f, royalH = 0.085f, legendsH = 0.115f, singleH = 0.105f, gap = 0.008f;
            float need = heroH + tierH + labelH + royalH + legendsH + singleH + gap * 5;
            float avail = 0.845f - 0.172f, s = Mathf.Min(1f, avail / need);
            float y = h * 0.172f, x = w * 0.05f, fw = w * 0.9f;
            if (_shopOpened < 0f) _shopOpened = Time.unscaledTime;
            int order = 0;

            // ---- hero: the chest, the pack most people pick ----
            var hero = P[1]; int heroIdx = 1;
            var hr = BeginShopCard(new Rect(x, y, fw, h * heroH * s), order++, "FF8FAB", "C23B73", true);
            CoinPile(new Rect(hr.x + hr.width * 0.03f, hr.y + hr.height * 0.1f, hr.width * 0.27f, hr.height * 0.8f), 3);
            Pill(new Rect(hr.x + hr.width * 0.34f, hr.y + hr.height * 0.08f, hr.width * 0.3f, hr.height * 0.19f), hero.Badge, "FFE27A", "FFB627", Visuals.Hex("5A2A00"));
            var amountStyle = new GUIStyle(_h2) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = Mathf.RoundToInt(_h2.fontSize * 0.9f) };
            string amount = hero.Coins.ToString("N0", CultureInfo.InvariantCulture);
            var amountRect = new Rect(hr.x + hr.width * 0.33f, hr.y + hr.height * 0.28f, hr.width * 0.4f, hr.height * 0.4f);
            Outlined(amountRect, amount, amountStyle, Color.white, 2.5f);
            float cd = amountRect.height * 0.7f, aw = Mathf.Min(amountStyle.CalcSize(new GUIContent(amount)).x, amountRect.width - cd);
            GoldCoin(new Rect(amountRect.x + aw + cd * 0.15f, amountRect.y + (amountRect.height - cd) / 2f, cd, cd));
            Outlined(new Rect(hr.x + hr.width * 0.34f, hr.y + hr.height * 0.68f, hr.width * 0.42f, hr.height * 0.22f), Loc.F("+{0}% MORE", CoinBonusPercent(hero)), new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleLeft }, Visuals.Hex("FFF3B0"), 1.2f);
            PriceButton(new Rect(hr.xMax - hr.width * 0.28f, hr.y + hr.height * 0.28f, hr.width * 0.25f, hr.height * 0.44f), hero, heroIdx, true);
            EndShopCard();
            y += h * (heroH * s + gap);

            // ---- three tier cards: a growing pile and a visible bonus ----
            var tiers = new[] { (0, "4FD8FF", "1B7FD6", 1), (2, "B98CFF", "6A33D1", 4), (3, "FFD54A", "E08A00", 4) };
            float cw = (fw - 2 * w * 0.015f) / 3f, th = h * tierH * s;
            int[] rows = { 2, 3, 4 };
            for (int k = 0; k < 3; k++)
            {
                var (idx, top, bottom, _) = tiers[k];
                var p = P[idx];
                var cr = BeginShopCard(new Rect(x + k * (cw + w * 0.015f), y, cw, th), order++, top, bottom, p.Badge != "");
                CoinPile(new Rect(cr.x + cr.width * 0.12f, cr.y + cr.height * 0.1f, cr.width * 0.76f, cr.height * 0.38f), rows[k]);
                Outlined(new Rect(cr.x, cr.y + cr.height * 0.48f, cr.width, cr.height * 0.16f), p.Coins.ToString("N0", CultureInfo.InvariantCulture), new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter }, Color.white, 1.8f);
                int bonus = CoinBonusPercent(p);
                if (bonus > 0) Pill(new Rect(cr.x + cr.width * 0.2f, cr.y + cr.height * 0.64f, cr.width * 0.6f, cr.height * 0.11f), Loc.F("+{0}% MORE", bonus), "FFFFFF", "DCD3FF", Visuals.Hex("3A1B8F"));
                else Pill(new Rect(cr.x + cr.width * 0.2f, cr.y + cr.height * 0.64f, cr.width * 0.6f, cr.height * 0.11f), "STARTER", "FFFFFF", "DCD3FF", Visuals.Hex("3A1B8F"));
                PriceButton(new Rect(cr.x + cr.width * 0.08f, cr.y + cr.height * 0.78f, cr.width * 0.84f, cr.height * 0.19f), p, idx, false);
                if (p.Badge != "") Pill(new Rect(cr.x + cr.width * 0.12f, cr.y - h * 0.008f, cr.width * 0.76f, h * 0.02f), p.Badge, "FF4FD8", "C21A9B", Color.white);
                EndShopCard();
            }
            y += th + h * gap;

            // ---- bundles ----
            Outlined(new Rect(0, y, w, h * labelH * s), "BUNDLES", new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter }, Visuals.Hex("C9A7FF"), 1.2f);
            y += h * (labelH * s + gap * 0.5f);
            foreach (var (idx, top, bottom, hh) in new[] { (5, "6A33D1", "C98A12", royalH), (6, "3A1B8F", "FF7A1C", legendsH) })
            {
                var p = P[idx];
                bool owned = (_meta.Purchases & (1 << idx)) != 0;
                var br = BeginShopCard(new Rect(x, y, fw, h * hh * s), order++, top, bottom, true, owned);
                float textW = br.width * 0.62f;
                Outlined(new Rect(br.x + br.width * 0.04f, br.y + br.height * 0.05f, textW, br.height * 0.3f), Loc.L(p.Title).ToUpperInvariant(), new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = false }, Color.white, 1.5f);
                DrawContentChips(new Rect(br.x + br.width * 0.04f, br.y + br.height * 0.36f, textW, br.height * 0.6f), Store.ContentParts(p), Visuals.Hex("FFF3B0"));
                if (p.Badge != "") Pill(new Rect(br.xMax - br.width * 0.3f, br.y + br.height * 0.07f, br.width * 0.27f, br.height * 0.2f), p.Badge, "FFE27A", "FFB627", Visuals.Hex("5A2A00"));
                PriceButton(new Rect(br.xMax - br.width * 0.3f, br.y + br.height * 0.34f, br.width * 0.27f, br.height * 0.44f), p, idx, true);
                EndShopCard();
                y += h * (hh * s + gap);
            }

            // ---- singles ----
            float sw = (fw - w * 0.015f) / 2f, sh = h * singleH * s;
            foreach (var (k, idx, top, bottom) in new[] { (0, 4, "FF6B6B", "B82E4A"), (1, 7, "FFC24B", "E07A10") })
            {
                var p = P[idx];
                var sr = BeginShopCard(new Rect(x + k * (sw + w * 0.015f), y, sw, sh), order++, top, bottom, false, !p.Consumable && (_meta.Purchases & (1 << idx)) != 0);
                float pad = sr.width * 0.06f;
                var icon = new Rect(sr.x + pad, sr.y + (sr.height - sr.height * 0.58f) / 2f, sr.height * 0.58f, sr.height * 0.58f);
                Disc(icon, new Color(0, 0, 0, 0.25f));
                if (idx == 4)
                {
                    // "AD" with a small cross badge: no ads
                    Outlined(new Rect(icon.x, icon.y, icon.width, icon.height), "AD", new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter }, Color.white, 1.2f);
                    var badge = new Rect(icon.xMax - icon.width * 0.38f, icon.yMax - icon.width * 0.38f, icon.width * 0.42f, icon.width * 0.42f);
                    Disc(badge, Visuals.Hex("FFFFFF"));
                    Outlined(badge, "×", new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter }, Visuals.Hex("C73A3A"), 0.6f);
                }
                else if (Event.current.type == EventType.Repaint)
                {
                    // a hammer (handle and head) beside three little blocks: hammers and shuffles
                    var old = GUI.color; var white = new Color(1, 1, 1, old.a);
                    GUI.color = white;
                    float u = icon.width;
                    GUI.DrawTexture(new Rect(icon.x + u * 0.2f, icon.y + u * 0.3f, u * 0.3f, u * 0.14f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(icon.x + u * 0.31f, icon.y + u * 0.44f, u * 0.08f, u * 0.3f), Texture2D.whiteTexture);
                    GUI.color = new Color(1, 0.9f, 0.5f, old.a);
                    GUI.DrawTexture(new Rect(icon.x + u * 0.58f, icon.y + u * 0.28f, u * 0.14f, u * 0.14f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(icon.x + u * 0.66f, icon.y + u * 0.44f, u * 0.14f, u * 0.14f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(icon.x + u * 0.54f, icon.y + u * 0.58f, u * 0.14f, u * 0.14f), Texture2D.whiteTexture);
                    GUI.color = old;
                }
                float tx = icon.xMax + pad, tw2 = sr.xMax - pad - tx;
                Outlined(new Rect(tx, sr.y + sr.height * 0.1f, tw2, sr.height * 0.3f), Loc.L(p.Title).ToUpperInvariant(), new GUIStyle(_small) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = Mathf.Max(8, Mathf.RoundToInt(_small.fontSize * 0.75f)) }, Color.white, 1.1f);
                PriceButton(new Rect(tx, sr.y + sr.height * 0.48f, tw2, sr.height * 0.38f), p, idx, false);
                EndShopCard();
            }

            DrawShopCoinBurst(w, h);
            float by = h * 0.86f, bw = w * 0.4f;
            if (Btn(new Rect(w * 0.07f, by, bw, h * 0.07f), "BACK", "5A4FA8", "3A2F80")) GoBack();
            GUI.enabled = _store.Ready;
            if (Btn(new Rect(w * 0.53f, by, bw, h * 0.07f), "RESTORE", "6E8BFF", "3A4FE0", 0.8f)) _store.Restore(id => { var p = Store.Find(id); if (p != null && _meta.Grant(p)) { SaveMeta(); ApplyTheme(); } });
            GUI.enabled = true;
        }

        /// <summary>Gold coins toss up from the price button after a purchase. Skipped with reduced motion.</summary>
        private void DrawShopCoinBurst(float w, float h)
        {
            float age = Time.unscaledTime - _shopBurstAt;
            if (age < 0f || age > 1.4f || NewLook.ReducedMotion || Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < 18; i++)
            {
                float ang = (i / 18f) * Mathf.PI * 2f + i * 0.7f, speed = h * (0.25f + 0.2f * ((i * 37) % 10) / 10f);
                var pos = _shopBurstFrom + new Vector2(Mathf.Cos(ang) * speed * age, Mathf.Sin(ang) * speed * age - h * 0.22f * age + h * 0.3f * age * age);
                float d = h * 0.022f * (1f - age / 1.6f);
                var old = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(1.4f - age));
                Disc(new Rect(pos.x - d / 2, pos.y - d / 2, d, d), Visuals.Hex("FFC93C"));
                GUI.color = old;
            }
        }
    }
}
