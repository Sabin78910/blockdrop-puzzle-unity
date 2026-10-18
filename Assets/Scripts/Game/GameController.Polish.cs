using System.Collections;
using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>HUD polish, the first-run guide and the Home button. Lives beside the controller so the big file
    /// changes in a few lines only. Nothing here collects data: the tutorial flag is a local PlayerPref.</summary>
    public sealed partial class GameController
    {
        private GUIStyle _guideBody, _plateStyle;

        // ---------- first-run guide ----------

        private void AdvanceGuide(int next)
        {
            if (!Guide.Active(_tutorial, _mode == Mode.Classic) || next == _tutorial) return;
            _tutorial = next;
            _tutorialStepAt = Time.time;
            _hint = null;
            if (next >= Guide.Done) { PlayerPrefs.SetInt("tutorial", Guide.Done); PlayerPrefs.Save(); }
        }

        /// <summary>Per frame while playing: finish the last step, and keep a highlighted example move on the
        /// board until the player makes it (the same landing preview the HINT button shows).</summary>
        private void TickGuide()
        {
            if (!Guide.Active(_tutorial, _mode == Mode.Classic)) return;
            AdvanceGuide(Guide.AfterWait(_tutorial, Time.time - _tutorialStepAt));
            if (!Guide.ShowExample(_tutorial) || _dragging >= 0 || Time.time < _hintUntil) return;
            var seat = MainSeat;
            if (Bot.TryMove(seat.Board, seat.Dealer.Hand, BotLevel.Hard, _rng, out int i, out int x, out int y))
            { _hint = (i, x, y); _hintUntil = Time.time + 1.5f; }
        }

        private void DrawGuide(float w, float h)
        {
            if (!Guide.Active(_tutorial, _mode == Mode.Classic)) return;
            EnsurePolishStyles();
            var card = new Rect(w * 0.04f, h * 0.2f, w * 0.92f, h * 0.075f);
            Panel(card, _cardStyle);

            float face = card.height * 0.86f;
            DrawGuideFace(new Rect(card.x + card.height * 0.1f, card.y + (card.height - face) / 2f, face, face));

            float skipW = w * 0.2f, textX = card.x + face + card.height * 0.25f;
            var skip = new Rect(card.xMax - skipW - card.height * 0.12f, card.y + card.height * 0.22f, skipW, card.height * 0.56f);
            if (Btn(skip, Coach.T("SKIP"), "5A4FA8", "3A2F80", 0.6f)) { _fx.Pick(); AdvanceGuide(Guide.Skip()); return; }

            var say = new Rect(textX, card.y + card.height * 0.3f, skip.x - textX - card.height * 0.1f, card.height * 0.66f);
            Outlined(new Rect(say.x, card.y + card.height * 0.04f, say.width, card.height * 0.3f),
                $"{Coach.T("BLOCKY")}  ·  {Coach.F("STEP {0} OF {1}", _tutorial + 1, Guide.Done)}", new GUIStyle(_small) { fontSize = Mathf.RoundToInt(_small.fontSize * 0.8f), alignment = TextAnchor.MiddleLeft, wordWrap = false }, Visuals.Hex("C9A7FF"), 1f);
            Outlined(say, Coach.T(Guide.Say(_tutorial)), new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold }, Visuals.Hex("FFE27A"), 1.2f);

            // On step one a finger glides from the piece to its spot; the board's landing preview marks the spot.
            if (_tutorial == 0 && _dragging < 0)
            {
                float t = (Time.time * 0.7f) % 1f;
                var from = HandSlot(_hint.HasValue ? _hint.Value.hand : 1);
                var to = _hint.HasValue ? CellPos(_hint.Value.x + 1, _hint.Value.y + 1) : BoardCenter;
                var p = ToGui(Vector3.Lerp(_cam.WorldToScreenPoint(from), _cam.WorldToScreenPoint(to), Mathf.SmoothStep(0, 1, t)));
                float size = h * 0.05f;
                var old = GUI.color; GUI.color = new Color(1, 1, 1, 0.85f * Mathf.Sin(t * Mathf.PI));
                GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), Visuals.Circle.texture);
                GUI.color = old;
            }
        }

        /// <summary>Blocky, the guide: a friendly block with eyes. It bobs and blinks unless motion is reduced.</summary>
        private void DrawGuideFace(Rect r)
        {
            bool calm = NewLook.ReducedMotion;
            float bob = calm ? 0f : Mathf.Sin(Time.time * 3f) * r.height * 0.04f;
            r.y += bob;
            Panel(r, _guideBody);
            bool blink = !calm && (Time.time % 3.2f) < 0.12f;
            float eye = r.width * 0.2f, ey = r.y + r.height * 0.3f;
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            foreach (float fx in new[] { 0.24f, 0.56f })
            {
                float ex = r.x + r.width * fx, eh = blink ? eye * 0.2f : eye;
                GUI.color = Color.white; GUI.DrawTexture(new Rect(ex, ey + (eye - eh) / 2f, eye, eh), Visuals.Circle.texture);
                GUI.color = new Color(0.1f, 0.05f, 0.3f); if (!blink) GUI.DrawTexture(new Rect(ex + eye * 0.3f, ey + eye * 0.35f, eye * 0.45f, eye * 0.45f), Visuals.Circle.texture);
            }
            GUI.color = new Color(0.1f, 0.05f, 0.3f, 0.9f);
            GUI.DrawTexture(new Rect(r.x + r.width * 0.34f, r.y + r.height * 0.66f, r.width * 0.32f, r.height * 0.09f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void EnsurePolishStyles()
        {
            if (_guideBody != null) return;
            _guideBody = Sliced(Visuals.RoundedRect(96, 96, 28, Visuals.Hex("8FA6FF"), Visuals.Hex("4A5FE8"), 0.1f), 30);
            _plateStyle = Sliced(Visuals.RoundedRect(96, 96, 30, new Color(0.08f, 0.06f, 0.25f, 0.55f), new Color(0.05f, 0.04f, 0.18f, 0.55f), 0f), 32);
        }

        // ---------- in-game HUD ----------

        /// <summary>Extra HUD layers drawn before the existing text so they sit behind it: a plate under the score,
        /// the distance to the next star, a low-moves warning that is a symbol and not only a colour, and in
        /// Classic a thin bar showing how close the score is to the player's best.</summary>
        private void DrawHudPolish(float w, float h)
        {
            if (_mode == Mode.Versus || _mode == Mode.VsAi) return;
            EnsurePolishStyles();
            if (_mode == Mode.Level)
            {
                var d = _level.Definition;
                int score = Score;
                Panel(new Rect(w * 0.3f, h * 0.083f, w * 0.4f, h * 0.064f), _plateStyle);
                Panel(new Rect(w * 0.3f, h * 0.2f, w * 0.4f, h * 0.045f), _plateStyle);
                int toNext = Goal.ToNextStar(score, d.TargetScore, d.TwoStarScore, d.ThreeStarScore);
                Outlined(new Rect(0, h * 0.247f, w, h * 0.03f), toNext > 0 ? Coach.F("NEXT ★ IN {0}", toNext) : Coach.T("ALL STARS!"),
                    new GUIStyle(_small) { wordWrap = false, fontStyle = FontStyle.Bold }, toNext > 0 ? Color.white : Visuals.Gold, 1.2f);
                if (Goal.LowMoves(_level.MovesLeft) && Event.current.type == EventType.Repaint)
                {
                    float d2 = h * 0.034f, pulse = 1f + (NewLook.ReducedMotion ? 0f : 0.1f * Mathf.Sin(Time.time * 6f));
                    var r = new Rect(w * 0.3f - d2 * 0.4f, h * 0.2f + h * 0.0225f - d2 * pulse / 2f, d2 * pulse, d2 * pulse);
                    var old = GUI.color; GUI.color = Visuals.Hex("FF3D64"); GUI.DrawTexture(r, Visuals.Circle.texture); GUI.color = old;
                    Outlined(r, "!", new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false }, Color.white, 1f);
                }
                return;
            }
            Panel(new Rect(w * 0.3f, h * 0.093f, w * 0.4f, h * 0.095f), _plateStyle);
            if (_mode == Mode.Classic && _best > 0)
            {
                var bar = new Rect(w * 0.34f, h * 0.19f, w * 0.32f, h * 0.009f);
                float f = Goal.Fill(Score, _best);
                Panel(bar, _barBackStyle);
                if (f > 0.04f) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * f), bar.height), _barFillStyle);
            }
        }

        // ---------- level won ----------

        private int _levelCoins, _levelStarsBefore;

        /// <summary>A small glittering pop on every placed block, so each drop feels good, not only clears.
        /// Skipped with reduced motion.</summary>
        private void PlaceSparkle(Piece piece, int ox, int oy)
        {
            if (NewLook.ReducedMotion) return;
            var col = Color.Lerp(Visuals.BlockColors[piece.ColorIndex], Color.white, 0.55f);
            foreach (var (cx, cy) in piece.Cells)
            {
                var pos = CellPos(ox + cx, oy + cy);
                for (int k = 0; k < 2; k++)
                {
                    var spark = Rent(Visuals.Circle, col, pos, Random.Range(0.14f, 0.24f), 7);
                    var dir = Random.insideUnitCircle.normalized * Random.Range(1.2f, 3f);
                    _particles.Add(new Particle { Sr = spark, V = new Vector3(dir.x, dir.y + 0.8f, 0), Life = 0.45f, Max = 0.45f, Spin = 0, Grow = -0.3f });
                }
            }
        }

        /// <summary>The moment a level is won: confetti scaled to the stars (none under reduced motion), and a second
        /// burst for three stars or the end of a chapter. The win chime itself is played by the caller.</summary>
        private void StartLevelCelebration()
        {
            int stars = _level.Stars;
            bool end = LevelCelebration.IsChapterEnd(_level.Definition.Number), calm = NewLook.ReducedMotion;
            Confetti(LevelCelebration.Confetti(stars, end, calm));
            int more = LevelCelebration.SecondBurst(stars, end, calm);
            if (more > 0) StartCoroutine(SecondBurst(more));
        }

        private IEnumerator SecondBurst(int pieces)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            if (_screen != Screen.Result) yield break;
            _fx.Win();
            Confetti(pieces);
        }

        /// <summary>Headline banner above the result card ("PERFECT!", or "CHAPTER 2 COMPLETE!" on every tenth level),
        /// and a line when the player beat their old star count. It pops in; reduced motion shows it still.</summary>
        private void DrawLevelCelebration(float w, float h, float y, float t)
        {
            int num = _level.Definition.Number;
            bool end = LevelCelebration.IsChapterEnd(num);
            string head = end ? Coach.F("CHAPTER {0} COMPLETE!", LevelCelebration.Chapter(num)) : Coach.T(LevelCelebration.Headline(num, _level.Stars));
            float k = NewLook.ReducedMotion ? 1f : EaseOutBack(Mathf.Clamp01(t * 3f));
            var st = new GUIStyle(_h2) { wordWrap = false, fontSize = Mathf.Max(8, Mathf.RoundToInt(_h2.fontSize * (end ? 0.95f : 1.15f) * k)) };
            Outlined(new Rect(0, h * 0.045f, w, h * 0.09f), head, st, end ? Visuals.Hex("7CF8FF") : Visuals.Gold, 3);
            if (_pieceMsg == "" && LevelCelebration.StarRecord(_levelStarsBefore, _level.Stars))
                Outlined(new Rect(0, y + h * 0.262f, w, h * 0.036f), Coach.T("NEW STAR RECORD!"), new GUIStyle(_small) { wordWrap = false, fontStyle = FontStyle.Bold }, Visuals.Hex("7CF8FF"), 1.2f);
        }

        /// <summary>"SCORE 340   +15 ●" with the coins counting up after the stars have landed.</summary>
        private string LevelScoreLine(int score)
        {
            if (_levelCoins <= 0) return Loc.F("SCORE {0}", score);
            float since = NewLook.ReducedMotion ? 99f : Time.time - _resultTime - 0.9f;
            return Coach.F("SCORE {0}   +{1} ●", score, LevelCelebration.CountUp(_levelCoins, since));
        }

        // ---------- home and settings ----------

        /// <summary>The home screen shows only the rewarding choices; everything else lives under SETTINGS.
        /// Returns the y below the last button.</summary>
        private float DrawHomeButtons(float w, float h, float y)
        {
            float bh = h * 0.088f, gap = h * 0.016f, half = (w * 0.84f - gap) / 2f, x2 = w * 0.08f + half + gap;
            if (Btn(Row(w, y, bh * 1.2f), "PLAY", "4BE38A", "1FA45B", 1.4f)) StartMode(Mode.Classic);
            y += bh * 1.2f + gap;
            if (Btn(new Rect(w * 0.08f, y, half, bh), Loc.F("LEVELS {0}★", _progress.TotalStars), "FFC24B", "F08A1C", 0.95f)) ShowMenu(Screen.LevelSelect);
            if (Btn(new Rect(x2, y, half, bh), "DAILY", "4FD8FF", "1C8FE0", 0.95f)) StartMode(Mode.Daily);
            y += bh + gap;
            int ready = 0; for (int i = 0; i < 3; i++) if (_meta.IsComplete(i, Today) && !_meta.Claimed[i]) ready++;
            var missionsRect = new Rect(w * 0.08f, y, half, bh);
            if (Btn(missionsRect, "MISSIONS", "2EE59D", "13A86E", 0.95f)) ShowMenu(Screen.Missions);
            if (ready > 0) Badge(missionsRect, ready);
            if (Btn(new Rect(x2, y, half, bh), "THEMES", "FF8FAB", "D9466F", 0.95f)) ShowMenu(Screen.Themes);
            y += bh + gap * 1.5f;
            if (Btn(Row(w, y, bh * 0.75f, 0.22f), Coach.T("SETTINGS"), "5A4FA8", "3A2F80", 0.8f)) ShowMenu(Screen.Settings);
            return y + bh * 0.75f + gap;
        }

        /// <summary>The player name entry: on the home screen and in Settings, so the name is always one tap away.</summary>
        private void PlayerButton(Rect r)
        {
            string shown = _localName != "" ? _localName : OnlineService.Ready ? OnlineService.PlayerName.Split('#')[0] : Loc.L("Set your name");
            if (shown.Length > 16) shown = shown.Substring(0, 15) + "…";
            if (Btn(r, Loc.F("PLAYER: {0}", shown), "2B2470", "1C1650", 0.75f)) { _nameInput = _localName; _nameMsg = ""; ShowMenu(Screen.Profile); }
        }

        /// <summary>Everything that left the home screen: the other game modes, rankings, and the options.</summary>
        private void DrawSettings(float w, float h)
        {
            float bh = h * 0.07f, gap = h * 0.012f, half = (w * 0.84f - gap) / 2f, x1 = w * 0.08f, x2 = x1 + half + gap;
            var label = new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false };
            Outlined(new Rect(0, h * 0.04f, w, h * 0.07f), Coach.T("SETTINGS"), _h2, Color.white, 3);
            float y = h * 0.13f;
            Outlined(new Rect(0, y, w, h * 0.03f), Coach.T("MORE WAYS TO PLAY"), label, Visuals.Hex("C9A7FF"), 1.2f);
            y += h * 0.035f;
            if (Btn(new Rect(x1, y, half, bh), "VS AI", "B98CFF", "7A3FE0", 0.85f)) ShowMenu(Screen.AiSelect);
            if (Btn(new Rect(x2, y, half, bh), "2 PLAYERS", "FF7AB6", "E0347C", 0.85f)) StartMode(Mode.Versus);
            y += bh + gap;
            if (Btn(new Rect(x1, y, half, bh), "CHALLENGE", "6E8BFF", "3A4FE0", 0.85f)) { _codeError = ""; ShowMenu(Screen.Challenge); }
            if (Btn(new Rect(x2, y, half, bh), "RANKINGS", "FFD54A", "D99A00", 0.85f)) { OnlineService.Refresh(_boardTab); ShowMenu(Screen.Leaderboard); }
            y += bh + gap * 3f;

            Outlined(new Rect(0, y, w, h * 0.03f), Coach.T("OPTIONS"), label, Visuals.Hex("C9A7FF"), 1.2f);
            y += h * 0.035f;
            float sh = bh * 0.85f;
            if (Btn(new Rect(x1, y, half, sh), _fx.SoundOn ? "SOUND: ON" : "SOUND: OFF", "5A4FA8", "3A2F80", 0.75f))
            { _fx.SoundOn = !_fx.SoundOn; PlayerPrefs.SetInt("sound", _fx.SoundOn ? 1 : 0); }
            if (Btn(new Rect(x2, y, half, sh), _fx.HapticsOn ? "VIBRATE: ON" : "VIBRATE: OFF", "5A4FA8", "3A2F80", 0.75f))
            { _fx.HapticsOn = !_fx.HapticsOn; PlayerPrefs.SetInt("haptics", _fx.HapticsOn ? 1 : 0); }
            y += sh + gap;
            if (NewLook.On)
            {
                if (Btn(new Rect(x1, y, half, sh), NewLook.ReducedMotion ? "MOTION: REDUCED" : "MOTION: FULL", "5A4FA8", "3A2F80", 0.75f)) NewLook.ToggleMotion();
                if (Btn(new Rect(x2, y, half, sh), NewLook.PatternsOn ? "PATTERNS: ON" : "PATTERNS: OFF", "5A4FA8", "3A2F80", 0.75f)) NewLook.TogglePatterns();
                y += sh + gap;
            }
            PlayerButton(Row(w, y, sh, 0.08f));
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        // ---------- end of game ----------

        private bool HomeBtn(Rect r) => Btn(r, Coach.T("HOME"), "5A4FA8", "3A2F80");

        /// <summary>Level result, last row: LEVELS and HOME side by side so Home never needs a fourth row.</summary>
        private void LevelResultNav(float w, float y, float bh)
        {
            float x = w * 0.14f, full = w * 0.72f, gap = w * 0.02f, half = (full - gap) / 2f;
            if (Btn(new Rect(x, y, half, bh), "LEVELS", "5A4FA8", "3A2F80")) { ResetStars(); ShowMenu(Screen.LevelSelect); }
            if (HomeBtn(new Rect(x + half + gap, y, half, bh))) { ResetStars(); ShowMenu(); }
        }
    }
}
