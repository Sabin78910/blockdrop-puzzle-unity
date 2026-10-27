using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>Rank ladders for Classic, Daily and VS AI: the block on the result screen and the RANKS overview.</summary>
    public sealed partial class GameController
    {
        private RankResult _rankResult;
        private float _kingAsked = -99f;

        private static string TrackName(RankTrack t) => t == RankTrack.Classic ? "CLASSIC" : t == RankTrack.Daily ? "DAILY" : t == RankTrack.Level ? "LEVELS" : "VS AI";

        /// <summary>Called once when a Classic, Daily or VS AI game ends.</summary>
        private void ApplyRank()
        {
            _rankResult = null;
            if (!Career) return;
            if (_mode == Mode.Classic) _rankResult = _meta.RankClassic(Score);
            else if (_mode == Mode.Daily) _rankResult = _meta.RankDaily(Score, Today);
            else if (_mode == Mode.VsAi) _rankResult = _meta.RankVs(_aiLevel, _versus.Winner);
            else if (_mode == Mode.Level && _level.State == LevelState.Won) _rankResult = _meta.RankLevel(_levelStarsBefore, _level.Stars);
            if (_mode == Mode.Classic || _mode == Mode.Daily) OnlineService.SubmitRank(_mode == Mode.Classic ? RankTrack.Classic : RankTrack.Daily, MainSeat.Board);
            else if (_mode == Mode.Level && _level.State == LevelState.Won) OnlineService.SubmitRankLevel(_level.Definition.Number, _level.Stars);
            else if (_mode == Mode.VsAi && _versus.Winner >= 0) OnlineService.SubmitRankVs(_aiLevel, _versus.Winner == 0);
            var r = _rankResult;
            if (r == null || !r.Promoted) return;
            Toast(Loc.F("RANK UP: {0}!   +{1} ●", Ranks.Display(r.StepAfter), r.Coins), Visuals.Hex(Ranks.TierColor(r.StepAfter)));
            _fx.Win();
            Confetti(r.Hammers > 0 ? 70 : 40);
        }

        /// <summary>Badge, rank name and a bar that fills from the old points to the new ones. Tap for the overview.</summary>
        private void DrawRankBlock(float w, float h, float t, float boxY = -1f, float inset = 0.1f)
        {
            var r = _rankResult;
            if (r == null) return;
            var box = new Rect(w * inset, boxY < 0 ? h * 0.55f : boxY, w * (1f - 2f * inset), h * 0.1f);
            float k = NewLook.ReducedMotion ? 1f : Mathf.Clamp01((t - 0.6f) / 1.1f);
            int shown = Mathf.RoundToInt(Mathf.Lerp(r.Before, r.After, k));
            int step = Ranks.StepOf(r.Track, shown);
            var col = Visuals.Hex(Ranks.TierColor(step));
            Disc(new Rect(box.x, box.y + box.height * 0.08f, box.height * 0.5f, box.height * 0.5f), col);
            Outlined(new Rect(box.x, box.y + box.height * 0.08f, box.height * 0.5f, box.height * 0.5f), (Ranks.TierOf(step) + 1).ToString(), new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false }, Color.white, 1.5f);
            var left = new GUIStyle(_body) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            float tx = box.x + box.height * 0.6f;
            Outlined(new Rect(tx, box.y, box.width * 0.5f, box.height * 0.34f), Ranks.Display(step), left, col, 1.5f);
            string gain = r.AlreadyScored ? "" : r.Delta > 0 ? $"+{r.Delta} RP" : r.Delta < 0 ? $"{r.Delta} RP" : "0 RP";
            Outlined(new Rect(box.xMax - box.width * 0.35f, box.y, box.width * 0.35f, box.height * 0.34f), gain, new GUIStyle(left) { alignment = TextAnchor.MiddleRight }, r.Delta < 0 ? Visuals.Hex("FF8FA3") : Visuals.Gold, 1.5f);
            var bar = new Rect(tx, box.y + box.height * 0.4f, box.xMax - tx, box.height * 0.16f);
            Panel(bar, _barBackStyle);
            float f = Ranks.Fill(r.Track, shown);
            if (f > 0.02f) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * f), bar.height), Swatch(col));
            string note;
            if (r.AlreadyScored) note = Loc.L("Daily rank counts once a day. Come back tomorrow!");
            else if (r.Capped) note = Loc.L("This AI is too easy for your rank. Try a harder one!");
            else if (k >= 1f && r.Promoted) note = Loc.F("RANK UP!  +{0} ●{1}", r.Coins, r.Hammers > 0 ? Loc.L("  +1 HAMMER  +1 SHUFFLE") : "");
            else if (r.Track == RankTrack.Level && r.Delta == 0) note = Loc.L("Earn more stars on a level to gain RP.");
            else if (Ranks.ToNext(r.Track, shown) > 0) note = Loc.F("{0} RP to {1}", Ranks.ToNext(r.Track, shown), Ranks.Display(step + 1));
            else note = Loc.L("You are a Legend!");
            Outlined(new Rect(box.x, box.y + box.height * 0.62f, box.width, box.height * 0.36f), note, new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false }, k >= 1f && r.Promoted ? Visuals.Hex("7CF8FF") : Color.white, 1.2f);
            if (GUI.Button(box, GUIContent.none, GUIStyle.none)) ShowMenu(Screen.Ranks);
        }

        /// <summary>One line for a mode picker: "GOLD II  ·  120 RP to GOLD I".</summary>
        private string RankLine(RankTrack t)
        {
            int p = _meta.RankPoints[(int)t], s = Ranks.StepOf(t, p);
            return Ranks.ToNext(t, p) > 0 ? Loc.F("{0}   ·   {1} RP to {2}", Ranks.Display(s), Ranks.ToNext(t, p), Ranks.Display(s + 1)) : Ranks.Display(s);
        }

        /// <summary>KING is decided by the server (top 100) and needs a linked account; the client never claims it.</summary>
        private void DrawKingLine(float w, float h)
        {
            var st = new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false };
            if (OnlineService.IsKing) Outlined(new Rect(w * 0.05f, h * 0.83f, w * 0.9f, h * 0.035f), Loc.F("KING  #{0}", OnlineService.KingRank), st, Visuals.Gold, 1.5f);
            else if (_meta.RankStep(RankTrack.Classic) >= Ranks.TopStep || _meta.RankStep(RankTrack.Daily) >= Ranks.TopStep || _meta.RankStep(RankTrack.VsAi) >= Ranks.TopStep)
                Outlined(new Rect(w * 0.03f, h * 0.83f, w * 0.94f, h * 0.035f), Loc.L("Link your Google account to chase the KING title (top 100)."), st, Visuals.Hex("7CF8FF"), 1.2f);
        }

        private void DrawRanks(float w, float h)
        {
            if (Time.unscaledTime - _kingAsked > 30f) { _kingAsked = Time.unscaledTime; OnlineService.RefreshKing(); }
            Outlined(new Rect(0, h * 0.06f, w, h * 0.08f), "RANKS", _h2, Visuals.Hex("7CF8FF"), 3);
            Outlined(new Rect(w * 0.05f, h * 0.135f, w * 0.9f, h * 0.05f), "Play to climb. Every new rank pays coins, and every new tier a hammer and a shuffle.", _small, new Color(1, 1, 1, 0.9f), 1.2f);
            string[] tops = { "FFC24B", "4FD8FF", "B98CFF", "4BE38A" }, bottoms = { "F08A1C", "1C8FE0", "7A3FE0", "1FA45B" };
            for (int i = 0; i < 4; i++)
            {
                var track = (RankTrack)i;
                int p = _meta.RankPoints[i], s = Ranks.StepOf(track, p);
                var col = Visuals.Hex(Ranks.TierColor(s));
                var card = BeginShopCard(new Rect(w * 0.05f, h * (0.19f + i * 0.16f), w * 0.9f, h * 0.148f), i, tops[i], bottoms[i], false);
                Outlined(new Rect(card.x + w * 0.04f, card.y + h * 0.008f, card.width * 0.5f, h * 0.04f), TrackName(track), new GUIStyle(_small) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false }, Color.white, 1.2f);
                Disc(new Rect(card.x + w * 0.04f, card.y + h * 0.05f, h * 0.06f, h * 0.06f), col);
                Outlined(new Rect(card.x + w * 0.04f, card.y + h * 0.05f, h * 0.06f, h * 0.06f), (Ranks.TierOf(s) + 1).ToString(), new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false }, Color.white, 1.5f);
                var left = new GUIStyle(_body) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                float tx = card.x + w * 0.04f + h * 0.07f;
                Outlined(new Rect(tx, card.y + h * 0.045f, card.xMax - tx - w * 0.04f, h * 0.04f), Ranks.Display(s) + $"   {p} RP", left, Color.white, 1.5f);
                var bar = new Rect(tx, card.y + h * 0.09f, card.xMax - tx - w * 0.04f, h * 0.02f);
                Panel(bar, _barBackStyle);
                float f = Ranks.Fill(track, p);
                if (f > 0.02f) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * f), bar.height), Swatch(col));
                string next = Ranks.ToNext(track, p) > 0 ? Loc.F("{0} RP to {1}", Ranks.ToNext(track, p), Ranks.Display(s + 1)) : Loc.L("Top rank reached!");
                Outlined(new Rect(card.x + w * 0.04f, card.y + h * 0.118f, card.width - w * 0.08f, h * 0.025f), next, new GUIStyle(_small) { wordWrap = false, alignment = TextAnchor.MiddleLeft }, Color.white, 1f);
                bool tapped = GUI.Button(card, GUIContent.none, GUIStyle.none);
                EndShopCard();
                if (tapped)
                {
                    if (track == RankTrack.Classic) { if (!ResumeRun(Mode.Classic)) StartMode(Mode.Classic); }
                    else if (track == RankTrack.Daily) StartMode(Mode.Daily);
                    else if (track == RankTrack.Level) ShowMenu(Screen.LevelSelect);
                    else ShowMenu(Screen.AiSelect);
                }
            }
            DrawKingLine(w, h);
            Outlined(new Rect(w * 0.05f, h * 0.862f, w * 0.9f, h * 0.03f), "Tap a mode to play it.", _small, new Color(1, 1, 1, 0.9f), 1.2f);
            if (Btn(Row(w, h * 0.9f, h * 0.06f, 0.25f), "BACK", "5A4FA8", "3A2F80")) GoBack();
        }
    }
}
