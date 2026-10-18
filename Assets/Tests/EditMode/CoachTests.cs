using BlockDrop.Core;
using NUnit.Framework;

namespace BlockDrop.Tests
{
    public class CoachTests
    {
        [TestCase(0, 1, false, null)]
        [TestCase(1, 1, false, "NICE!")]
        [TestCase(1, 2, false, "NICE!")]
        [TestCase(2, 1, false, "GREAT!")]
        [TestCase(1, 3, false, "GREAT!")]
        [TestCase(3, 1, false, "EXCELLENT!")]
        [TestCase(1, 4, false, "EXCELLENT!")]
        [TestCase(4, 1, false, "AMAZING!")]
        [TestCase(1, 5, false, "AMAZING!")]
        [TestCase(5, 1, false, "UNBELIEVABLE!")]
        [TestCase(1, 7, false, "UNBELIEVABLE!")]
        [TestCase(1, 1, true, "PERFECT CLEAR!")]
        public void Praise_follows_the_bigger_of_lines_and_combo(int lines, int combo, bool empty, string word)
            => Assert.AreEqual(word, Praise.For(lines, combo, empty).Word);

        [Test]
        public void Praise_is_never_shown_without_a_clear()
            => Assert.IsFalse(Praise.For(0, 9, true).Any);

        [Test]
        public void Bigger_praise_is_drawn_bigger()
        {
            Assert.Greater(Praise.For(3, 1, false).Size, Praise.For(2, 1, false).Size);
            Assert.Greater(Praise.For(1, 1, true).Size, Praise.For(5, 1, false).Size);
        }

        [Test]
        public void Every_praise_word_is_translated()
        {
            for (int r = 1; r <= 6; r++)
            {
                string word = Praise.For(r <= 5 ? r : 1, 1, r == 6).Word;
                if (word == null) continue;
                Assert.IsTrue(Loc.Table.ContainsKey(word), word);
            }
            Assert.IsTrue(Loc.Table.ContainsKey("NICE!"));
        }

        [Test]
        public void Tutorial_walks_drag_clear_combo_done()
        {
            int s = 0;
            s = Guide.AfterClear(s); Assert.AreEqual(0, s);
            s = Guide.AfterPlace(s); Assert.AreEqual(1, s);
            s = Guide.AfterPlace(s); Assert.AreEqual(1, s);
            s = Guide.AfterClear(s); Assert.AreEqual(2, s);
            s = Guide.AfterWait(s, 1f); Assert.AreEqual(2, s);
            s = Guide.AfterWait(s, 4f); Assert.AreEqual(Guide.Done, s);
        }

        [Test]
        public void Skip_ends_the_tutorial_from_any_step_and_only_classic_shows_it()
        {
            for (int s = 0; s < Guide.Done; s++)
            {
                Assert.IsTrue(Guide.Active(s, true));
                Assert.IsFalse(Guide.Active(s, false));
                Assert.IsFalse(Guide.Active(Guide.Skip(), true));
            }
            Assert.IsFalse(Guide.Active(Guide.Done, true));
        }

        [Test]
        public void Guide_has_words_for_each_step_in_every_language()
        {
            foreach (var lang in new[] { Lang.En, Lang.Es, Lang.Pt, Lang.Ne })
            {
                Loc.Current = lang;
                for (int s = 0; s < Guide.Done; s++) Assert.IsNotEmpty(Coach.T(Guide.Say(s)));
                Assert.IsNotEmpty(Coach.T("SKIP"));
                Assert.IsNotEmpty(Coach.F("STEP {0} OF {1}", 1, 3));
            }
            Loc.Current = Lang.En;
            Loc.Current = Lang.Es;
            Assert.AreEqual("SALTAR", Coach.T("SKIP"));
            Loc.Current = Lang.En;
        }

        [Test]
        public void Example_move_shows_only_while_the_player_must_act()
        {
            Assert.IsTrue(Guide.ShowExample(0));
            Assert.IsTrue(Guide.ShowExample(1));
            Assert.IsFalse(Guide.ShowExample(2));
            Assert.IsFalse(Guide.ShowExample(Guide.Done));
        }

        [Test]
        public void Goal_reports_stars_and_distance_to_the_next_one()
        {
            Assert.AreEqual(0, Goal.StarsEarned(50, 100, 200, 300));
            Assert.AreEqual(2, Goal.StarsEarned(250, 100, 200, 300));
            Assert.AreEqual(3, Goal.StarsEarned(300, 100, 200, 300));
            Assert.AreEqual(50, Goal.ToNextStar(50, 100, 200, 300));
            Assert.AreEqual(1, Goal.ToNextStar(199, 100, 200, 300));
            Assert.AreEqual(0, Goal.ToNextStar(300, 100, 200, 300));
        }

        [Test]
        public void Goal_bar_fill_is_clamped_and_low_moves_warn()
        {
            Assert.AreEqual(0f, Goal.Fill(-5, 300));
            Assert.AreEqual(0.5f, Goal.Fill(150, 300), 0.001f);
            Assert.AreEqual(1f, Goal.Fill(900, 300));
            Assert.AreEqual(0f, Goal.Fill(10, 0));
            Assert.IsTrue(Goal.LowMoves(3));
            Assert.IsFalse(Goal.LowMoves(4));
        }

        [Test]
        public void Chapter_ends_every_ten_levels()
        {
            Assert.IsFalse(LevelCelebration.IsChapterEnd(0));
            Assert.IsFalse(LevelCelebration.IsChapterEnd(9));
            Assert.IsTrue(LevelCelebration.IsChapterEnd(10));
            Assert.IsTrue(LevelCelebration.IsChapterEnd(250));
            Assert.AreEqual(1, LevelCelebration.Chapter(10));
            Assert.AreEqual(2, LevelCelebration.Chapter(11));
        }

        [Test]
        public void Headline_is_stable_per_level_and_every_word_is_translated()
        {
            for (int level = 1; level <= 40; level++)
                for (int stars = 1; stars <= 3; stars++)
                {
                    string h = LevelCelebration.Headline(level, stars);
                    Assert.AreEqual(h, LevelCelebration.Headline(level, stars));
                    Assert.IsTrue(Coach.Table.ContainsKey(h), h);
                }
            Assert.AreNotEqual(LevelCelebration.Headline(1, 3), LevelCelebration.Headline(2, 3));
            Assert.AreEqual(LevelCelebration.Headline(1, 1), LevelCelebration.Headline(1, 0));
        }

        [Test]
        public void Confetti_grows_with_stars_and_chapter_ends_and_stops_under_reduced_motion()
        {
            Assert.Greater(LevelCelebration.Confetti(3, false, false), LevelCelebration.Confetti(1, false, false));
            Assert.Greater(LevelCelebration.Confetti(1, true, false), LevelCelebration.Confetti(1, false, false));
            Assert.AreEqual(0, LevelCelebration.Confetti(3, true, true));
            Assert.AreEqual(0, LevelCelebration.SecondBurst(3, true, true));
            Assert.AreEqual(0, LevelCelebration.SecondBurst(2, false, false));
            Assert.Greater(LevelCelebration.SecondBurst(3, false, false), 0);
            Assert.Greater(LevelCelebration.SecondBurst(1, true, false), LevelCelebration.SecondBurst(3, false, false));
        }

        [Test]
        public void Star_record_needs_an_earlier_win_with_fewer_stars()
        {
            Assert.IsFalse(LevelCelebration.StarRecord(0, 3));
            Assert.IsTrue(LevelCelebration.StarRecord(1, 2));
            Assert.IsFalse(LevelCelebration.StarRecord(3, 3));
        }

        [Test]
        public void Coins_count_up_without_overshooting()
        {
            Assert.AreEqual(0, LevelCelebration.CountUp(15, 0f));
            Assert.AreEqual(7, LevelCelebration.CountUp(15, 0.5f));
            Assert.AreEqual(15, LevelCelebration.CountUp(15, 1f));
            Assert.AreEqual(15, LevelCelebration.CountUp(15, 9f));
            Assert.AreEqual(0, LevelCelebration.CountUp(0, 5f));
        }

        [Test]
        public void New_celebration_text_is_translated()
        {
            foreach (var key in new[] { "CHAPTER {0} COMPLETE!", "NEW STAR RECORD!", "NEXT: LEVEL {0}", "SCORE {0}   +{1} ●" })
                Assert.IsTrue(Coach.Table.ContainsKey(key), key);
        }

        [Test]
        public void Settings_text_is_translated_into_both_languages()
        {
            foreach (var key in new[] { "SETTINGS", "MORE WAYS TO PLAY", "OPTIONS" })
            {
                Assert.IsTrue(Coach.Table.ContainsKey(key), key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(Coach.Table[key].es) || string.IsNullOrWhiteSpace(Coach.Table[key].pt), key);
            }
        }
    
        [Test]
        public void Level_goal_is_met_only_while_moves_remain()
        {
            var level = new LevelSession(1);
            Assert.IsFalse(level.GoalMet);
            level.Seat.Board.RestoreScore(level.Definition.TargetScore, 0, 0, 0);
            Assert.IsTrue(level.GoalMet);
            Assert.AreEqual(LevelState.Playing, level.State);
        }

        [Test]
        public void Trophies_list_unwon_closest_first()
        {
            var meta = new PlayerMeta();
            var unwon = TrophyBoard.Unwon(meta);
            Assert.AreEqual(PlayerMeta.Achievements.Length, unwon.Count);
            for (int i = 1; i < unwon.Count; i++)
                Assert.GreaterOrEqual(TrophyBoard.Progress(meta, unwon[i - 1]), TrophyBoard.Progress(meta, unwon[i]));
            Assert.AreEqual(1, new System.Collections.Generic.HashSet<int>(unwon).Count == unwon.Count ? 1 : 0);
        }
}
}
