using System;
using System.Collections;
using System.Collections.Generic;
using BlockDrop.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BlockDrop.Game
{
    /// <summary>Builds the whole game at runtime: responsive portrait layout, glossy board, drag & drop
    /// with landing preview and line glow, particles, praise pop-ups, sounds, haptics, menus, levels,
    /// vs-AI, 2-player, challenge codes and online leaderboards. Fully playable offline.</summary>
    public sealed partial class GameController : MonoBehaviour
    {
        private enum Screen { Menu, LevelSelect, AiSelect, Playing, Result, Challenge, Leaderboard, Profile, Missions, Themes, Trophies, Collection, Settings, Shop, Ranks }
        private enum Mode { Classic, Daily, Level, Versus, VsAi, Challenge }

        private const int Size = 8;
        private static readonly Vector3 BoardCenter = new Vector3(0, 0.6f, 0);
        private static float _handDrop; // extra room on tall phones: the hand sits lower instead of leaving a gap
        private const float HandY = -6.1f, HandSpacing = 3.2f, HandScale = 0.64f, Lift = 2.1f;
        private static readonly Vector3 MiniCenter = new Vector3(3.15f, 6.9f, 0);
        private const float MiniCell = 0.27f;

        // ---------- state ----------
        private Screen _screen = Screen.Menu;
        private Mode _mode;
        private BotLevel _aiLevel = BotLevel.Medium;
        private Seat _single;
        private LevelSession _level;
        private VersusMatch _versus;
        private Progress _progress;
        private PlayerMeta _meta;
        private int _welcomeReward;
        private bool _devPreview; // five quick taps on the BEST line: every level opens and the streak card shows again (for checking on a device)
        private int _devTaps; private float _devTapAt;
        private float _flashUntil, _playSeconds;
        private bool _breakShown, _breakOpen;
        private int _tutorial; // 0 = drag, 1 = clear a line, 2 = combos, 3 = done
        private float _tutorialStepAt;
        private static string Today => PlayerMeta.Day(DateTime.Now);
        private bool _bestCelebrated, _nameSynced, _paused, _offerRevive, _firstSession;
        private float _introAt; // when the start card opened (unscaled time)
        private bool _offerContinue, _levelIntro, _boosterTray, _hammerMode; // level: the continue card and the start card
        private int _chapterPage, _bonusShown, _coinsEarned, _shuffles;
        private const int ContinueCost = 100, CoinsPerStar = 15, HelpingHandMoves = 3, HelpingHandAfter = 3;
        private readonly IAdProvider _ads = new BetaAdProvider();
        private readonly IStoreProvider _store = new BetaStoreProvider();
        private ThemeTier _themeTab;
        private SpriteRenderer _panel, _miniPanel;
        private readonly List<SpriteRenderer> _slots = new List<SpriteRenderer>();
        private AdPolicy _adPolicy;
        private TouchScreenKeyboard _kb;
        private string _pieceMsg = "";
        private string _kbTarget; // "code" or "name"
        private float _nextOnlineTry;
        private readonly List<(string text, Color color, float until)> _toasts = new List<(string, Color, float)>();
        private bool Career => _mode != Mode.Versus; // pass-and-play moves don't count toward one player's profile
        private int _best, _challengeSeed, _hintsLeft;
        private bool _aiThinking, _newBest;
        private float _shownScore, _resultTime, _turnBannerUntil;
        private string _codeInput = "", _codeError = "", _boardTab = OnlineService.LevelsBoard;
        private string _nameInput = "", _nameMsg = "", _localName = "";
        private readonly System.Random _rng = new System.Random();

        // ---------- scene objects ----------
        private Camera _cam;
        private Feedback _fx;
        private Transform _world;
        private SpriteRenderer _background;
        private GameObject _boardRoot, _miniRoot, _menuDeco;
        private SpriteRenderer[,] _tiles, _gloss, _ghosts, _miniTiles, _marks;
        private float[,] _pop;
        private readonly GameObject[] _handViews = new GameObject[Dealer.HandSize];
        private int _dragging = -1;
        private Vector3 _dragPos;
        private (int hand, int x, int y)? _hint;
        private float _hintUntil, _shake;

        private struct Particle { public SpriteRenderer Sr; public Vector3 V; public float Life, Max, Spin, Grow; }
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();

        private struct Floater { public string Text; public Vector3 World; public float Born, Life, Size; public Color Color; }
        private readonly List<Floater> _floaters = new List<Floater>();

        private struct Deco { public Transform T; public float Speed, Spin; }
        private readonly List<Deco> _decos = new List<Deco>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<GameController>() == null) new GameObject("BlockDrop").AddComponent<GameController>();
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            // The layout is portrait only; landscape squeezed the board and hand off screen.
            UnityEngine.Screen.autorotateToLandscapeLeft = false;
            UnityEngine.Screen.autorotateToLandscapeRight = false;
            UnityEngine.Screen.autorotateToPortrait = true;
            UnityEngine.Screen.autorotateToPortraitUpsideDown = true;
            _cam = Camera.main != null ? Camera.main : new GameObject("Main Camera").AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Visuals.Hex("1A1150");
            _fx = gameObject.AddComponent<Feedback>();
            _fx.SoundOn = PlayerPrefs.GetInt("sound", 1) == 1;
            _fx.HapticsOn = PlayerPrefs.GetInt("haptics", 1) == 1;
            _best = PlayerPrefs.GetInt("best", 0);
            _progress = Progress.Parse(PlayerPrefs.GetString("progress", ""));
            _localName = PlayerPrefs.GetString("name", "");
            string sysLang = Application.systemLanguage == SystemLanguage.Spanish ? "es" : Application.systemLanguage == SystemLanguage.Portuguese ? "pt" : System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ne" ? "ne" : "en";
            Loc.Current = Loc.FromCode(PlayerPrefs.GetString("lang", sysLang));
            _meta = PlayerMeta.Parse(PlayerPrefs.GetString("meta", ""));
            _adPolicy = AdPolicy.Parse(PlayerPrefs.GetString("ads", ""));
            int sessions = PlayerPrefs.GetInt("sessions", 0) + 1;
            PlayerPrefs.SetInt("sessions", sessions);
            _firstSession = sessions == 1;
            _welcomeReward = _meta.CheckIn(Today);
            Celebrate(_meta.Record(Stat.BestStreak, _meta.Streak));
            _tutorial = PlayerPrefs.GetInt("tutorial", 0);
            SaveMeta();
            _world = new GameObject("World").transform;
            BuildScene();
            FitCamera();
            ApplyTheme();
            NewLook.Apply(_world, _cam);
            ShowMenu();
            if (_welcomeReward > 0) { _fx.Win(); Confetti(40); }
            ResumeRun(Mode.Level);
            _ = OnlineService.EnsureReady(); // best effort; game is fully playable offline
        }

        // ================= scene building =================

        private SpriteRenderer NewSprite(string name, Sprite sprite, Transform parent, Vector3 pos, float scale, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingOrder = order; sr.color = color;
            return sr;
        }

        private static Vector3 CellPos(int x, int y) => BoardCenter + new Vector3(x - 3.5f, 3.5f - y, 0);

        private void BuildScene()
        {
            _background = NewSprite("Background", Visuals.Background, _world, Vector3.zero, 1, -100, Color.white);

            _boardRoot = new GameObject("Board");
            _boardRoot.transform.SetParent(_world, false);
            var panel = _panel = NewSprite("Panel", Visuals.Panel, _boardRoot.transform, BoardCenter, 1, -20, Visuals.BoardPanel);
            panel.drawMode = SpriteDrawMode.Sliced; panel.size = new Vector2(8.7f, 8.7f);
            var rim = NewSprite("Rim", Visuals.Panel, _boardRoot.transform, BoardCenter + new Vector3(0, -0.12f, 0), 1, -21, new Color(0, 0, 0, 0.45f));
            rim.drawMode = SpriteDrawMode.Sliced; rim.size = new Vector2(8.9f, 8.9f);

            _tiles = new SpriteRenderer[Size, Size];
            _gloss = new SpriteRenderer[Size, Size];
            _ghosts = new SpriteRenderer[Size, Size];
            _marks = new SpriteRenderer[Size, Size];
            _pop = new float[Size, Size];
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    var p = CellPos(x, y);
                    _slots.Add(NewSprite("slot", Visuals.Slot, _boardRoot.transform, p, 0.94f, -5, Visuals.EmptySlot));
                    _tiles[x, y] = NewSprite("tile", Visuals.Tile, _boardRoot.transform, p, 0.94f, 0, Color.white);
                    _gloss[x, y] = NewSprite("gloss", Visuals.Gloss, _tiles[x, y].transform, Vector3.zero, 1f, 1, Color.white);
                    _marks[x, y] = NewSprite("mark", null, _tiles[x, y].transform, Vector3.zero, 1f, 2, Color.white); // above the gloss
                    _ghosts[x, y] = NewSprite("ghost", Visuals.Tile, _boardRoot.transform, p, 0.94f, 2, Color.clear);
                }

            _miniRoot = new GameObject("MiniBoard");
            _miniRoot.transform.SetParent(_world, false);
            var mp = _miniPanel = NewSprite("MiniPanel", Visuals.Panel, _miniRoot.transform, MiniCenter, 1, -20, Visuals.BoardPanel);
            mp.drawMode = SpriteDrawMode.Sliced; mp.size = new Vector2(MiniCell * 8 + 0.3f, MiniCell * 8 + 0.3f);
            _miniTiles = new SpriteRenderer[Size, Size];
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    var p = MiniCenter + new Vector3((x - 3.5f) * MiniCell, (3.5f - y) * MiniCell, 0);
                    _miniTiles[x, y] = NewSprite("mini", Visuals.Tile, _miniRoot.transform, p, MiniCell * 0.92f, 0, Visuals.EmptySlot);
                }

            _menuDeco = new GameObject("MenuDeco");
            _menuDeco.transform.SetParent(_world, false);
            for (int i = 0; i < 18; i++)
            {
                var c = Visuals.BlockColors[i % 5]; c.a = 0.22f;
                var sr = NewSprite("deco", Visuals.Tile, _menuDeco.transform, new Vector3(Random.Range(-6f, 6f), Random.Range(-12f, 12f), 0), Random.Range(0.6f, 1.6f), -50, c);
                _decos.Add(new Deco { T = sr.transform, Speed = Random.Range(0.3f, 0.9f), Spin = Random.Range(-25f, 25f) });
            }
        }

        private int _fitW, _fitH;
        private Rect _fitSafe;
        private float _camX, _camY;

        /// <summary>The part of the screen not covered by notches, punch-hole cameras, rounded corners or
        /// system bars (pixels, origin bottom-left). The whole screen if the platform reports nothing usable.</summary>
        private static Rect SafeArea()
        {
            var full = new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height);
            var r = UnityEngine.Screen.safeArea;
            if (r.width < full.width * 0.5f || r.height < full.height * 0.5f) return full;
            return Rect.MinMaxRect(Mathf.Max(0, r.xMin), Mathf.Max(0, r.yMin), Mathf.Min(full.xMax, r.xMax), Mathf.Min(full.yMax, r.yMax));
        }

        /// <summary>GUI coordinates (inside the safe area, origin top-left) of a screen point.</summary>
        private Vector2 ToGui(Vector3 screenPoint) => new Vector2(screenPoint.x - _fitSafe.x, _fitSafe.yMax - screenPoint.y);

        private void FitCamera()
        {
            _fitW = UnityEngine.Screen.width; _fitH = UnityEngine.Screen.height;
            _fitSafe = SafeArea();
            float aspect = (float)_fitW / Mathf.Max(1, _fitH);
            const float needW = 9.6f, needTop = 9.6f, needBottom = -8.2f;
            // Fit the play area inside the safe area; the background still fills the whole screen.
            float unitsPerPixel = Mathf.Max((needTop - needBottom) / Mathf.Max(1f, _fitSafe.height), needW / Mathf.Max(1f, _fitSafe.width));
            float halfH = _fitH * unitsPerPixel / 2f;
            float slack = Mathf.Max(0f, _fitSafe.height * unitsPerPixel - (needTop - needBottom));
            _handDrop = Mathf.Min(slack * 0.4f, 2f);
            float camLift = Mathf.Min(_handDrop * 0.5f, 0.5f); // board creeps up a little, the HUD above it stays clear
            _cam.orthographicSize = halfH;
            _camX = (_fitW / 2f - _fitSafe.center.x) * unitsPerPixel;
            _camY = (needTop + needBottom) / 2f - camLift + (_fitH / 2f - _fitSafe.center.y) * unitsPerPixel;
            _cam.transform.position = new Vector3(_camX, _camY, -10);
            _background.transform.position = new Vector3(_camX, _camY, 10);
            var b = Visuals.Background.bounds.size;
            _background.transform.localScale = new Vector3(halfH * 2f * aspect / b.x * 1.05f, halfH * 2f / b.y * 1.05f, 1);
        }

        private void SaveMeta() { PlayerPrefs.SetString("meta", _meta.Serialize()); PlayerPrefs.Save(); }

        // Phones can kill a backgrounded app without warning, so persist whenever we lose focus.
        // Browsers don't send pause or quit when a tab is hidden or closed, but they do send focus loss.
        private void OnApplicationPause(bool paused)
        {
            if (!paused || _meta == null) return;
            if (_screen == Screen.Playing && !_offerRevive) SetPaused(true); // come back to a pause card, not a running game
            SaveRun();
            SaveMeta();
        }
        private void OnApplicationFocus(bool focused) { if (!focused && _meta != null) { SaveRun(); SaveMeta(); } }
        private void OnApplicationQuit() { if (_meta != null) { SaveRun(); SaveMeta(); } }

        // ---------- the run in progress survives the app being closed ----------

        private const string RunKey = "run";

        private bool Resumable => _mode != Mode.Versus && _mode != Mode.VsAi;

        /// <summary>Remembers the current run (board, hand, score) so it can continue after the app is killed.</summary>
        private void SaveRun()
        {
            if (_screen != Screen.Playing || !Resumable) return;
            bool level = _mode == Mode.Level;
            var save = SavedGame.Capture(_mode.ToString(), MainSeat, _mode == Mode.Classic, level ? _level.Definition.Number : 0, Today, _hintsLeft,
                level ? _level.ExtraMoves : 0, level ? _level.Continues : 0);
            PlayerPrefs.SetString(RunKey, save.Serialize());
        }

        private static void ClearRun() => PlayerPrefs.DeleteKey(RunKey);

        /// <summary>Continues a run the app was closed in the middle of, opening on the pause card.</summary>
        private bool ResumeRun(Mode want)
        {
            var save = SavedGame.Parse(PlayerPrefs.GetString(RunKey, ""));
            if (save == null || !Enum.TryParse(save.Mode, out Mode mode) || !Enum.IsDefined(typeof(Mode), mode) || mode == Mode.Versus || mode == Mode.VsAi) { ClearRun(); return false; }
            if (mode != want) return false; // another mode's run stays saved for its own button; PLAY always opens the level map
            ClearRun();
            if (mode == Mode.Daily && save.Seed != PieceGenerator.DailySeed(DateTime.UtcNow)) return false; // that day's challenge is over
            try
            {
                switch (mode)
                {
                    case Mode.Level:
                        _level = new LevelSession(save.Level, HelpingHand(save.Level));
                        save.ApplyTo(_level.Seat);
                        _level.RestoreExtra(save.Extra, save.Continues);
                        break;
                    case Mode.Challenge: _challengeSeed = save.Seed; _single = save.NewSeat(); break;
                    default: _single = save.NewSeat(); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Block Drop could not resume a saved game: " + e.Message);
                return false;
            }
            EnterPlay(mode);
            _hintsLeft = Mathf.Clamp(save.Hints, 0, Competitive ? 0 : 3);
            _bestCelebrated = _mode == Mode.Classic && _best > 0 && Score > _best;
            _shownScore = Score;
            if (IsFinished) AfterMove(); // closed on the last move: offer the second chance or finish properly
            else { SaveRun(); SetPaused(true); }
            return true;
        }

        private void ApplyTheme()
        {
            var t = PlayerMeta.Themes[_meta.ThemeIndex];
            for (int i = 0; i < 5; i++) Visuals.BlockColors[i] = Visuals.Hex(t.Blocks[i]);
            _background.color = Visuals.Hex(t.BackgroundTint);
            Visuals.BoardPanel = Visuals.Hex(t.Panel);
            Visuals.EmptySlot = Visuals.Hex(t.Slot);
            if (_panel != null) _panel.color = Visuals.BoardPanel;
            if (_miniPanel != null) _miniPanel.color = Visuals.BoardPanel;
            foreach (var s in _slots) s.color = Visuals.EmptySlot;
            for (int i = 0; i < _decos.Count; i++)
            {
                var sr = _decos[i].T.GetComponent<SpriteRenderer>();
                var c = Visuals.BlockColors[i % 5]; c.a = 0.22f; sr.color = c;
            }
        }

        private void Track(MissionKind kind, int amount)
        {
            var before = new bool[3];
            for (int i = 0; i < 3; i++) before[i] = _meta.IsComplete(i, Today);
            _meta.Track(kind, amount, Today);
            for (int i = 0; i < 3; i++)
                if (!before[i] && _meta.IsComplete(i, Today))
                {
                    AddFloater("MISSION COMPLETE!", BoardCenter + new Vector3(0, -3.2f, 0), Visuals.Hex("7CF8FF"), 1.1f);
                    _fx.Star();
                }
        }

        private void Toast(string text, Color color)
        {
            text = Loc.L(text);
            float start = _toasts.Count > 0 ? Mathf.Max(Time.unscaledTime, _toasts[_toasts.Count - 1].until - 0.6f) : Time.unscaledTime;
            _toasts.Add((text, color, start + 2.4f));
        }

        private void Celebrate(List<Achievement> unlocked)
        {
            foreach (var a in unlocked) { Toast($"★ {Loc.L(a.Name).ToUpperInvariant()}   +{a.Reward} ●", Visuals.Hex("7CF8FF")); _fx.Star(); }
            if (unlocked.Count > 0) SaveMeta();
        }

        private void GainXp(int amount)
        {
            if (!Career) return;
            if (_meta.AddXp(amount) > 0)
            {
                Toast(Loc.F("LEVEL {0}!   +{1} ●", _meta.Level, PlayerMeta.LevelReward(_meta.Level)), Visuals.Gold);
                SaveMeta();
                _fx.Win();
                Confetti(40);
            }
        }

        private void DrawToasts(float w, float h)
        {
            _toasts.RemoveAll(t => t.until < Time.unscaledTime);
            if (_toasts.Count == 0) return;
            var (text, color, until) = _toasts[0];
            float left = until - Time.unscaledTime, a = Mathf.Clamp01(Mathf.Min(left / 0.3f, (2.4f - left) / 0.2f));
            var r = new Rect(w * 0.06f, h * (0.905f + 0.02f * (1 - a)), w * 0.88f, h * 0.055f);
            var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
            Panel(r, _modalStyle);
            Outlined(r, text, new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false }, color, 1.5f);
            GUI.color = old;
        }

        // ================= modes =================

        private bool Competitive => _mode == Mode.Versus || _mode == Mode.VsAi || _mode == Mode.Challenge;

        private Seat MainSeat => _mode switch
        {
            Mode.Level => _level.Seat,
            Mode.Versus => _versus.CurrentSeat,
            Mode.VsAi => _versus.Seats[0],
            _ => _single,
        };

        private Seat MiniSeat => _mode switch
        {
            Mode.Versus => _versus.Seats[1 - _versus.Turn],
            Mode.VsAi => _versus.Seats[1],
            _ => null,
        };

        private bool InputAllowed => _screen == Screen.Playing && !_aiThinking && !_levelIntro && !_offerContinue && !(_mode == Mode.VsAi && _versus.Turn != 0) && Time.time > _turnBannerUntil;

        private void StartMode(Mode mode, int seed = 0, int level = 0)
        {
            _mode = mode;
            switch (mode)
            {
                case Mode.Level: _level = new LevelSession(level, HelpingHand(level)); break;
                case Mode.Versus:
                case Mode.VsAi: _versus = new VersusMatch(Environment.TickCount); break;
                case Mode.Daily: _single = new Seat(PieceGenerator.DailySeed(DateTime.UtcNow)); break;
                case Mode.Challenge: _challengeSeed = seed; _single = new Seat(seed); break;
                default: _single = new Seat(Environment.TickCount, fairHands: true); break; // Classic: every hand is playable
            }
            _hintsLeft = Competitive ? 0 : 3;
            MaybeInterstitial();
            EnterPlay(mode);
            _levelIntro = mode == Mode.Level; // the start card: goals, moves and anything new
            _introAt = Time.unscaledTime;
        }

        private static string FailKey(int level) => "lvfail" + level;

        /// <summary>After a few losses on the same level the next try starts with a few extra moves, and says so.
        /// Visible help instead of secretly easier pieces: the game stays honest (docs/FEEL.md).</summary>
        private static int HelpingHand(int level) => PlayerPrefs.GetInt(FailKey(level), 0) >= HelpingHandAfter ? HelpingHandMoves : 0;

        /// <summary>Shows the board for <paramref name="mode"/> once its seats exist (new or resumed run).</summary>
        private void EnterPlay(Mode mode)
        {
            _mode = mode;
            _runId++;
            _hint = null; _newBest = false; _aiThinking = false; _finishing = false; _bestCelebrated = false; _offerRevive = false;
            _offerContinue = false; _levelIntro = false; _boosterTray = false; _hammerMode = false; _bonusShown = 0; _coinsEarned = 0; _shuffles = mode == Mode.Level ? _level.Shuffles : 0;
            _shownScore = 0;
            _floaters.Clear();
            _screen = Screen.Playing;
            _boardRoot.SetActive(true);
            _miniRoot.SetActive(MiniSeat != null);
            _menuDeco.SetActive(false);
            Array.Clear(_pop, 0, _pop.Length);
            if (mode == Mode.Versus) _turnBannerUntil = Time.time + 1.1f;
            RefreshBoard(); RebuildHand();
            UnityEngine.Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        // Where the player came from, so BACK (and the Android back key) returns there instead of a fixed screen.
        private readonly Stack<Screen> _nav = new Stack<Screen>();
        private bool _goingBack;

        private void GoBack()
        {
            var to = _nav.Count > 0 ? _nav.Pop() : Screen.Menu;
            _goingBack = true;
            try { ShowMenu(to); } finally { _goingBack = false; }
        }

        private void ShowMenu(Screen screen = Screen.Menu)
        {
            if (screen == Screen.Menu) _nav.Clear();
            else if (!_goingBack && _screen != screen && _screen != Screen.Playing && _screen != Screen.Result) _nav.Push(_screen);
            if (_screen == Screen.Playing) ClearRun(); // finished or quit: nothing to resume
            SetPaused(false);
            _screen = screen;
            if (screen == Screen.Shop || screen == Screen.Themes || screen == Screen.Missions || screen == Screen.Ranks) _shopOpened = Time.unscaledTime;
            if (screen == Screen.LevelSelect) { _mapBottom = -1; _mapVel = 0; _mapDown = _mapTapped = false; _mapPick = 0; } // reopen the map on the player's current level
            _boardRoot.SetActive(false);
            _miniRoot.SetActive(false);
            _menuDeco.SetActive(true);
            for (int i = 0; i < Dealer.HandSize; i++) if (_handViews[i] != null) Destroy(_handViews[i]);
            UnityEngine.Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private int Score => MainSeat.Board.Score;

        private bool IsFinished => _mode switch
        {
            Mode.Level => _level.State != LevelState.Playing,
            Mode.Versus or Mode.VsAi => _versus.Over,
            _ => _single.Out,
        };

        private const float AutoNextDelay = 3.2f;

        private void Finish()
        {
            bool good = true;
            if (_mode == Mode.Level)
            {
                good = _level.State == LevelState.Won;
                _pieceMsg = "";
                int number = _level.Definition.Number;
                PlayerPrefs.SetInt(FailKey(number), good ? 0 : PlayerPrefs.GetInt(FailKey(number), 0) + 1);
                if (good)
                {
                    var (pic, tile) = Collection.ForLevel(number);
                    bool newPiece = !Collection.Revealed(_progress, pic, tile), wasComplete = Collection.Complete(_progress, pic);
                    _levelStarsBefore = _progress.StarsFor(number);
                    _coinsEarned = _progress.Record(number, _level.Stars) * CoinsPerStar; // new stars only: replays are never a grind
                    _meta.AddCoins(_coinsEarned);
                    _levelCoins = _coinsEarned;
                    int boss = _levelStarsBefore == 0 ? LevelLibrary.BossBonus(number) : 0; // only the first win of a boss pays
                    if (boss > 0)
                    {
                        _meta.AddCoins(boss); _meta.AddBoosters(Booster.Hammer, 1); _levelCoins += boss;
                        Toast(Loc.F("BOSS DOWN!  +{0} ●  +1 HAMMER", boss), Visuals.Hex("FF9BD2"));
                    }
                    PlayerPrefs.SetString("progress", _progress.Serialize());
                    if (newPiece) _pieceMsg = Loc.F("PICTURE PIECE!  {0} {1}/{2}", Loc.L(Collection.Pictures[pic].Name), Collection.RevealedCount(_progress, pic), Collection.TilesPerPicture);
                    if (!wasComplete && Collection.Complete(_progress, pic))
                    {
                        _meta.AddCoins(Collection.CompletionReward);
                        _meta.AddBoosters(Booster.Hammer, PlayerMeta.ChapterBoosters);
                        _meta.AddBoosters(Booster.Shuffle, PlayerMeta.ChapterBoosters);
                        Toast(Loc.F("PICTURE COMPLETE!  +{0} ●", Collection.CompletionReward), Visuals.Gold);
                        Toast(Loc.F("CHAPTER GIFT: +{0} HAMMERS, +{0} SHUFFLES", PlayerMeta.ChapterBoosters), Visuals.Hex("FF9BD2"));
                        Confetti(60);
                    }
                    // earned themes: finishing chapters and collecting stars unlock looks no one can buy
                    foreach (int theme in _meta.UnlockEarnedThemes(Collection.CompletedPictures(_progress), _progress.TotalStars))
                    {
                        Toast(Loc.F("EXCLUSIVE THEME UNLOCKED: {0}!", Loc.L(PlayerMeta.Themes[theme].Name).ToUpperInvariant()), Visuals.Hex("7CF8FF"));
                        _fx.Star();
                    }
                }
            }
            if (_mode == Mode.Classic || _mode == Mode.Daily)
            {
                if (Score > _best) { _best = Score; _newBest = true; PlayerPrefs.SetInt("best", _best); }
                OnlineService.SubmitScore(_mode == Mode.Classic ? OnlineService.ClassicBoard : OnlineService.DailyBoard, MainSeat.Board);
                good = _newBest;
            }
            if (_mode == Mode.VsAi) good = _versus.Winner == 0;
            Track(MissionKind.PlayGames, 1);
            if (_mode == Mode.Daily) Track(MissionKind.PlayDaily, 1);
            if (_mode == Mode.VsAi && _versus.Winner == 0) Track(MissionKind.BeatAi, 1);
            _adPolicy.OnGameEnded(Today, _newBest);
            PlayerPrefs.SetString("ads", _adPolicy.Serialize());
            if (_mode == Mode.Level && _level.State == LevelState.Won) Track(MissionKind.EarnStars, _level.Stars);
            ApplyRank();
            if (Career)
            {
                Celebrate(_meta.Record(Stat.GamesPlayed, 1));
                Celebrate(_meta.Record(Stat.BestGameScore, Score));
                if (_mode == Mode.Level) Celebrate(_meta.Record(Stat.LevelStars, _progress.TotalStars));
                if (_mode == Mode.VsAi && _aiLevel == BotLevel.Hard && _versus.Winner == 0) Celebrate(_meta.Record(Stat.HardAiWins, 1));
            }
            SaveMeta();
            PlayerPrefs.Save();
            if (good && _mode == Mode.Level) OnlineService.SubmitLevels(_progress.TotalStars);
            if (good) { _fx.Win(); if (_mode == Mode.Level) StartLevelCelebration(); else Confetti(60); } else _fx.GameOver();
            _resultTime = Time.time;
            ShowMenu(Screen.Result);
        }

        // ================= rendering =================

        private void RefreshBoard()
        {
            var b = MainSeat.Board;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    bool filled = b.IsFilled(x, y);
                    _tiles[x, y].enabled = filled;
                    if (filled) _tiles[x, y].color = Visuals.BlockColors[b[x, y]];
                    _gloss[x, y].enabled = filled && b[x, y] != Board.Stone;
                    // gems and ice always show their mark: their rule differs, so shape must say it, not only colour
                    _marks[x, y].sprite = filled && (NewLook.PatternsOn || b[x, y] > Board.Stone) ? NewLook.Pattern(b[x, y]) : null;
                }
            var mini = MiniSeat;
            if (mini != null)
                for (int x = 0; x < Size; x++)
                    for (int y = 0; y < Size; y++)
                        _miniTiles[x, y].color = mini.Board.IsFilled(x, y) ? Visuals.BlockColors[mini.Board[x, y]] : Visuals.EmptySlot;
        }

        private static Vector3 HandSlot(int i) => new Vector3((i - 1) * HandSpacing, HandY - _handDrop, 0);

        private void RebuildHand()
        {
            var hand = MainSeat.Dealer.Hand;
            for (int i = 0; i < Dealer.HandSize; i++)
            {
                if (_handViews[i] != null) Destroy(_handViews[i]);
                _handViews[i] = null;
                var p = hand[i];
                if (p == null) continue;
                var root = new GameObject("hand" + i);
                root.transform.SetParent(_world, false);
                bool fits = MainSeat.Board.CanPlaceAnywhere(p);
                var col = Visuals.BlockColors[p.ColorIndex];
                if (!fits) col = Color.Lerp(col, Visuals.EmptySlot, 0.65f);
                foreach (var (cx, cy) in p.Cells)
                {
                    var pos = new Vector3(cx - (p.Width - 1) / 2f, (p.Height - 1) / 2f - cy, 0);
                    var t = NewSprite("t", Visuals.Tile, root.transform, pos, 0.94f, 10, col);
                    NewSprite("g", Visuals.Gloss, t.transform, Vector3.zero, 1f, 11, new Color(1, 1, 1, fits ? 1 : 0.3f));
                    if (NewLook.PatternsOn) NewSprite("m", NewLook.Pattern(p.ColorIndex), t.transform, Vector3.zero, 1f, 12, new Color(1, 1, 1, fits ? 1 : 0.4f));
                }
                root.transform.position = HandSlot(i);
                root.transform.localScale = Vector3.one * HandScale;
                _handViews[i] = root;
            }
        }

        private void SetHandOrder(GameObject root, int baseOrder)
        {
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder = baseOrder + (sr.name == "m" ? 2 : sr.sprite == Visuals.Gloss ? 1 : 0); // tile, gloss, pattern
        }

        // ================= input & game loop =================

        private Vector3 PointerWorld()
        {
            var p = _cam.ScreenToWorldPoint(Input.mousePosition);
            p.z = 0;
            return p;
        }

        private (int ox, int oy) DropCell(Piece p, Vector3 center)
        {
            int ox = Mathf.RoundToInt(center.x - BoardCenter.x + 3.5f - (p.Width - 1) / 2f);
            int oy = Mathf.RoundToInt(3.5f - (center.y - BoardCenter.y) - (p.Height - 1) / 2f);
            return (ox, oy);
        }

        private void Update()
        {
            if (UnityEngine.Screen.width != _fitW || UnityEngine.Screen.height != _fitH || SafeArea() != _fitSafe) FitCamera();
            PollKeyboard();
            if (_screen == Screen.LevelSelect) UpdateMap();
            if (Input.GetKeyDown(KeyCode.Escape) && _screen != Screen.Menu)
            {
                if (_screen == Screen.Playing && _offerRevive) DeclineRevive();
                else if (_screen == Screen.Playing && _levelIntro) { _levelIntro = false; ShowMenu(Screen.LevelSelect); } // the start card: back to the map
                else if (_screen == Screen.Playing && (_hammerMode || _boosterTray)) { _hammerMode = false; _boosterTray = false; }
                else if (_screen == Screen.Playing) SetPaused(!_paused); // never throw a run away on an accidental back press
                else GoBack();
                return;
            }
            // Keep trying to come online (slow or late network at startup); then push a name chosen offline.
            if (!OnlineService.Ready && Time.unscaledTime >= _nextOnlineTry) { _nextOnlineTry = Time.unscaledTime + RetryBackoff.Delay(OnlineService.Failures, UnityEngine.Random.value); _ = OnlineService.EnsureReady(); }
            if (_screen != Screen.Playing && CloudSync.TakeRestored())
            {   // progress came back from the cloud save: reload what the game keeps in memory
                _progress = Progress.Parse(PlayerPrefs.GetString("progress", ""));
                _meta = PlayerMeta.Parse(PlayerPrefs.GetString("meta", ""));
                _best = PlayerPrefs.GetInt("best", 0);
            }
            if (OnlineService.Ready && !_nameSynced)
            {
                _nameSynced = true;
                if (_localName != "" && OnlineService.PlayerName.Split('#')[0] != _localName) _ = OnlineService.SetNameAsync(_localName);
            }

            AnimateDecor();
            AnimateParticles();
            AnimateShake();
            NewLook.Tick(_boardRoot.transform);
            if (_screen != Screen.Playing || _paused || _offerRevive) return;
            _playSeconds += Time.unscaledDeltaTime;
            if (!_breakShown && _playSeconds > 40 * 60) { _breakShown = true; _breakOpen = true; }
            TickGuide();
            if (_breakOpen) return;

            _shownScore = Mathf.MoveTowards(_shownScore, Score, Mathf.Max(1f, Mathf.Abs(Score - _shownScore)) * Time.deltaTime * 6f);
            AnimatePops();
            UpdateGhosts();
            if (!InputAllowed) return;

            var hand = MainSeat.Dealer.Hand;
            var wp = PointerWorld();
            if (_hammerMode)
            {
                if (Input.GetMouseButtonDown(0)) TryHammer(wp);
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                float bestD = 1.9f;
                for (int i = 0; i < hand.Length; i++)
                {
                    if (hand[i] == null || _handViews[i] == null) continue;
                    float d = Vector2.Distance(wp, HandSlot(i));
                    if (d < bestD) { bestD = d; _dragging = i; }
                }
                if (_dragging >= 0) { _fx.Pick(); SetHandOrder(_handViews[_dragging], 20); _hint = null; }
            }
            if (_dragging >= 0 && Input.GetMouseButton(0))
            {
                _dragPos = wp + new Vector3(0, Lift, 0);
                var v = _handViews[_dragging].transform;
                v.position = Vector3.Lerp(v.position, _dragPos, 0.55f);
                v.localScale = Vector3.Lerp(v.localScale, Vector3.one, 0.35f);
            }
            if (_dragging >= 0 && Input.GetMouseButtonUp(0))
            {
                int i = _dragging;
                _dragging = -1;
                var p = hand[i];
                var (ox, oy) = DropCell(p, _dragPos);
                if (MainSeat.Board.CanPlace(p, ox, oy)) PlaceWithEffects(i, ox, oy);
                else
                {
                    if (Vector2.Distance(_dragPos, BoardCenter) < 5f) _fx.Invalid();
                    RebuildHand();
                }
            }
        }

        private void PlaceWithEffects(int handIndex, int ox, int oy)
        {
            var seat = MainSeat;
            var board = seat.Board;
            var piece = seat.Dealer.Hand[handIndex];
            var (rows, cols) = board.PreviewClears(piece, ox, oy);
            // remember colours of cells about to clear (for the burst)
            var burst = new List<(Vector3 pos, Color col)>();
            var cleared = new HashSet<(int, int)>();
            foreach (int y in rows) for (int x = 0; x < Size; x++) cleared.Add((x, y));
            foreach (int x in cols) for (int y = 0; y < Size; y++) cleared.Add((x, y));
            foreach (var (x, y) in cleared)
            {
                int c = board.IsFilled(x, y) ? board[x, y] : piece.ColorIndex;
                burst.Add((CellPos(x, y), Visuals.BlockColors[c]));
            }

            int before = board.Score, gemsBefore = board.Count(Board.Gem);
            int goalsBefore = _mode == Mode.Level ? GoalsMet() : 0;
            bool ok = _mode switch
            {
                Mode.Level => _level.TryPlace(handIndex, ox, oy),
                Mode.Versus or Mode.VsAi => _versus.TryPlace(handIndex, ox, oy),
                _ => _single.TryPlace(handIndex, ox, oy),
            };
            if (!ok) { RebuildHand(); return; }

            if (_mode == Mode.Level)
            {
                // goal progress lands in the same frame as the move, with its own sound
                int gems = gemsBefore - board.Count(Board.Gem);
                if (gems > 0) { AddFloater(gems == 1 ? Loc.L("GEM!") : Loc.F("{0} GEMS!", gems), BoardCenter + new Vector3(0, 1.2f, 0), Visuals.BlockColors[Board.Gem], 1.3f); _fx.Star(); }
                if (GoalsMet() > goalsBefore && !_level.AllGoalsMet) { AddFloater("GOAL COMPLETE!", BoardCenter + new Vector3(0, 3.0f, 0), Visuals.Hex("2EE59D"), 1.3f); _fx.Star(); }
            }

            int gained = board.Score - before;
            int lines = rows.Count + cols.Count;
            foreach (var (cx, cy) in piece.Cells) _pop[ox + cx, oy + cy] = 1f;
            _fx.Place();
            PlaceSparkle(piece, ox, oy);
            Track(MissionKind.PlaceBlocks, 1);
            if (gained > 0) Track(MissionKind.ScorePoints, gained);
            GainXp(gained);
            PlayerPrefs.SetString("meta", _meta.Serialize());
            if (_mode == Mode.Classic && !_bestCelebrated && _best > 0 && board.Score > _best)
            {
                _bestCelebrated = true;
                AddFloater("NEW BEST!", BoardCenter + new Vector3(0, 2.8f, 0), Visuals.Hex("FFE27A"), 1.6f);
                _fx.Win();
                Confetti(50);
            }
            AdvanceGuide(Guide.AfterPlace(_tutorial));

            if (lines > 0)
            {
                _fx.Clear(lines, board.Combo);
                Track(MissionKind.ClearLines, lines);
                Track(MissionKind.ComboReach, board.Combo);
                if (Career) { Celebrate(_meta.Record(Stat.LinesCleared, lines)); Celebrate(_meta.Record(Stat.BestCombo, board.Combo)); }
                AdvanceGuide(Guide.AfterClear(_tutorial));
                if (NewLook.On) { NewLookClear(lines, board, gained, burst); AfterMove(); return; }
                if (lines >= 3) StartCoroutine(MegaClear());
                else StartCoroutine(HitStop(0.035f + 0.02f * lines));
                foreach (var (pos, col) in burst) Shatter(pos, col);
                _shake = 0.12f + 0.08f * lines;
                var mid = BoardCenter + new Vector3(0, 0.6f, 0);
                AddFloater("+" + gained, mid + new Vector3(0, -0.9f, 0), Visuals.Gold, 1.0f);
                string praise = lines >= 5 ? "UNBELIEVABLE!" : lines == 4 ? "AMAZING!" : lines == 3 ? "EXCELLENT!" : lines == 2 ? "GREAT!" : board.Combo >= 2 ? "NICE!" : null;
                if (praise != null) AddFloater(praise, mid + new Vector3(0, 0.5f, 0), Color.white, 1.6f);
                if (board.Combo >= 2) AddFloater(Loc.F("COMBO x{0}", board.Combo), mid + new Vector3(0, 1.8f, 0), Visuals.Hex("FF9BD2"), 1.1f);
                if (IsEmpty(board)) { AddFloater("PERFECT CLEAR!", mid + new Vector3(0, -2.2f, 0), Visuals.Hex("7CF8FF"), 1.4f); Confetti(40); }
            }
            else if (gained > 0) AddFloater("+" + gained, CellPos(ox, oy) + new Vector3(0.5f, 0.6f, 0), new Color(1, 1, 1, 0.9f), 0.7f);

            AfterMove();
        }

        /// <summary>The feedback ladder (Core/Feel.cs): celebration scales with the clear, a single line gets
        /// no praise word, only 3+ lines shake, and reduced motion keeps the words and drops the movement.</summary>
        private void NewLookClear(int lines, Board board, int gained, List<(Vector3 pos, Color col)> burst)
        {
            bool reduced = NewLook.ReducedMotion;
            var tier = Feel.TierFor(lines, board.Combo, Feel.IsEmpty(board));
            if (Feel.SlowMotion(tier, reduced)) StartCoroutine(SlowMotion());
            else if (Feel.HitStop(lines, reduced) > 0f) StartCoroutine(HitStop(Feel.HitStop(lines, reduced)));
            if (!reduced) foreach (var (pos, col) in burst) Shatter(pos, col);
            _shake = Feel.Shake(tier, lines, reduced);
            NewLook.Pulse(tier);
            var mid = BoardCenter + new Vector3(0, 0.6f, 0);
            AddFloater("+" + gained, mid + new Vector3(0, -0.9f, 0), Visuals.Gold, 1.0f);
            var praise = Praise.For(lines, board.Combo, tier == FeedbackTier.BoardClear);
            if (praise.Any) AddFloater(praise.Word, mid + new Vector3(0, 0.5f, 0), tier == FeedbackTier.BoardClear ? Visuals.Hex("7CF8FF") : Color.white, praise.Size);
            if (board.Combo >= 2) AddFloater(Loc.F("COMBO x{0}", board.Combo), mid + new Vector3(0, 1.8f, 0), Visuals.Hex("FF9BD2"), 1.1f);
            int confetti = Feel.Confetti(tier, reduced);
            if (confetti > 0) Confetti(confetti);
        }

        private IEnumerator SlowMotion()
        {
            _flashUntil = Time.unscaledTime + 0.14f;
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.22f);
            Time.timeScale = _paused ? 0f : 1f;
        }

        private IEnumerator MegaClear()
        {
            _flashUntil = Time.unscaledTime + 0.14f;
            Confetti(30);
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.22f);
            Time.timeScale = _paused ? 0f : 1f;
        }

        private static bool IsEmpty(Board b)
        {
            for (int x = 0; x < b.Size; x++) for (int y = 0; y < b.Size; y++) if (b.IsFilled(x, y)) return false;
            return true;
        }

        private void AfterMove()
        {
            SaveRun();
            if (IsFinished)
            {
                // Classic offers one second chance (a fresh hand that fits) before the game ends.
                if (_mode == Mode.Classic && _single.Revives == 0) { _offerRevive = true; RefreshBoard(); RebuildHand(); return; }
                // Levels: a loss may continue with more moves; a win first turns spare moves into points.
                if (_mode == Mode.Level && _level.CanContinue) { _offerContinue = true; RefreshBoard(); RebuildHand(); return; }
                if (_mode == Mode.Level && _level.State == LevelState.Won) { StartCoroutine(BonusBlast()); return; }
                StartCoroutine(FinishSoon());
                return;
            }
            if (_mode == Mode.Level && _level.Shuffles > _shuffles)
            {
                _shuffles = _level.Shuffles;
                AddFloater("NO ROOM! FRESH PIECES", BoardCenter + new Vector3(0, -3.2f, 0), Visuals.Hex("7CF8FF"), 1.0f);
            }
            if (_mode == Mode.Versus) { _turnBannerUntil = Time.time + 1.1f; Array.Clear(_pop, 0, _pop.Length); }
            RefreshBoard(); RebuildHand();
            if (_mode == Mode.VsAi && _versus.Turn == 1) StartCoroutine(AiTurns());
        }

        private int _runId;
        private bool _finishing;

        private int GoalsMet()
        {
            int n = 0;
            for (int i = 0; i < _level.Definition.Goals.Length; i++) if (_level.GoalMet(i)) n++;
            return n;
        }

        /// <summary>The level is won: every spare move bursts into points, one by one, before the result card.
        /// Anticipation is the reward (docs/FEEL.md), so the count is visible and rises with a sound per move.</summary>
        private IEnumerator BonusBlast()
        {
            int run = _runId;
            _finishing = true;
            _aiThinking = true; // blocks input
            RefreshBoard(); RebuildHand();
            AddFloater("LEVEL COMPLETE!", BoardCenter + new Vector3(0, 2.6f, 0), Visuals.Hex("FFE27A"), 1.7f);
            _fx.Win();
            yield return new WaitForSecondsRealtime(0.8f);
            int moves = _level.MovesLeft;
            if (moves > 0) AddFloater(Loc.F("BONUS BLAST! {0} MOVES", moves), BoardCenter + new Vector3(0, 1.2f, 0), Visuals.Hex("FF9BD2"), 1.3f);
            float step = Mathf.Clamp(1.6f / Mathf.Max(1, moves), 0.06f, 0.16f);
            for (int i = 0; i < moves; i++)
            {
                if (run != _runId || _screen != Screen.Playing) yield break;
                _bonusShown += LevelLibrary.MoveBonus;
                var cell = CellPos(_rng.Next(Size), _rng.Next(Size));
                AddFloater("+" + LevelLibrary.MoveBonus, cell, Visuals.Gold, 0.8f);
                if (!NewLook.ReducedMotion) Shatter(cell, Visuals.BlockColors[_rng.Next(5)]);
                _fx.Place();
                yield return new WaitForSecondsRealtime(step);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            if (run != _runId || _screen != Screen.Playing) yield break;
            _finishing = false;
            _aiThinking = false;
            Finish();
        }

        private void AcceptContinue()
        {
            if (!_meta.Spend(ContinueCost)) { _fx.Invalid(); return; }
            SaveMeta();
            _offerContinue = false;
            if (_level.Continue())
            {
                SaveRun();
                RefreshBoard(); RebuildHand();
                AddFloater(Loc.F("+{0} MOVES!", LevelSession.ContinueMoves), BoardCenter + new Vector3(0, 2.8f, 0), Visuals.Hex("7CF8FF"), 1.4f);
                _fx.Star();
            }
            else StartCoroutine(FinishSoon());
        }

        private void DeclineContinue()
        {
            _offerContinue = false;
            StartCoroutine(FinishSoon());
        }

        /// <summary>Out of moves in a level: an honest "how close" line and an optional paid continue. The way out
        /// is as big as the way in, and nothing counts down.</summary>
        private void DrawContinue(float w, float h)
        {
            if (!_offerContinue) return;
            var card = new Rect(w * 0.08f, h * 0.28f, w * 0.84f, h * 0.38f);
            Modal(w, h, card);
            Outlined(new Rect(card.x, card.y + h * 0.02f, card.width, h * 0.06f), "OUT OF MOVES", new GUIStyle(_h2) { fontSize = Mathf.RoundToInt(_h2.fontSize * 0.85f) }, Visuals.Hex("FF8FA3"), 3);
            string close = _level.NearMiss() ?? Loc.L("Keep going: 5 more moves could do it.");
            Outlined(new Rect(card.x + w * 0.05f, card.y + h * 0.085f, card.width - w * 0.1f, h * 0.07f), close, _body, Color.white, 1.4f);
            Outlined(new Rect(card.x, card.y + h * 0.155f, card.width, h * 0.035f), $"● {_meta.Coins}", _small, Visuals.Gold, 1.2f);
            float bw = card.width * 0.8f, bx = card.x + card.width * 0.1f;
            bool afford = _meta.Coins >= ContinueCost;
            if (Btn(new Rect(bx, card.y + h * 0.2f, bw, h * 0.065f), Loc.F("+{0} MOVES  {1} ●", LevelSession.ContinueMoves, ContinueCost), afford ? "4BE38A" : "4A4370", afford ? "1FA45B" : "2E2850", 0.85f)) AcceptContinue();
            if (Btn(new Rect(bx, card.y + h * 0.28f, bw, h * 0.06f), "NO THANKS", "5A4FA8", "3A2F80", 0.8f)) DeclineContinue();
        }

        /// <summary>The level's start card: what to do, how many moves, and the chapter's new idea when there is one.</summary>
        private void DrawLevelIntro(float w, float h)
        {
            if (!_levelIntro || _mode != Mode.Level) return;
            var d = _level.Definition;
            string intro = d.Intro != null ? Loc.L(d.Intro) : null;
            var card = new Rect(w * 0.08f, h * 0.26f, w * 0.84f, h * (intro != null ? 0.5f : 0.44f) + (d.IsHard ? h * 0.042f : 0f));
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color; GUI.color = new Color(0.02f, 0.01f, 0.1f, 0.88f); // hide the board behind the card
                GUI.DrawTexture(new Rect(-w, -h, w * 3, h * 3), Texture2D.whiteTexture); GUI.color = old;
            }
            // One card for every level: purple body, gold rim. Difficulty is a small badge, never a recolour.
            var oldMatrix = GUI.matrix;
            float open = NewLook.ReducedMotion ? 1f : Mathf.Clamp01((Time.unscaledTime - _introAt) / 0.35f);
            if (open < 1f)
            {
                float sc = 0.86f + 0.14f * EaseOutBack(open);
                GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), card.center);
            }
            Panel(new Rect(card.x - w * 0.008f, card.y - w * 0.008f, card.width + w * 0.016f, card.height + w * 0.016f), ShopStyle("FFE27A", "C9922B"));
            Panel(card, ShopStyle("6A33D1", "2A1770"));
            float y = card.y + h * 0.02f;
            Outlined(new Rect(card.x, y, card.width, h * 0.06f), Loc.F("LEVEL {0}", d.Number), _h2, Visuals.Hex("FFE27A"), 3);
            y += h * 0.06f;
            Outlined(new Rect(card.x, y, card.width, h * 0.035f), Loc.L(d.Chapter.Name).ToUpperInvariant(), _small, new Color(1, 1, 1, 0.8f), 1.2f);
            y += h * 0.04f;
            if (d.IsHard)
            {
                bool sup = d.Tier == LevelTier.SuperHard;
                Pill(new Rect(card.center.x - w * 0.15f, y, w * 0.3f, h * 0.032f), Loc.L(sup ? "SUPER HARD" : "HARD LEVEL"), sup ? "FF8FA3" : "FFE27A", sup ? "C23B63" : "C9922B", Color.white);
                y += h * 0.042f;
            }
            if (intro != null)
            {
                Outlined(new Rect(card.x, y, card.width, h * 0.03f), "NEW!", _small, Visuals.Hex("7CF8FF"), 1.2f);
                Outlined(new Rect(card.x + w * 0.05f, y + h * 0.028f, card.width - w * 0.1f, h * 0.05f), intro, _small, Color.white, 1.2f);
                y += h * 0.085f;
            }
            for (int i = 0; i < d.Goals.Length; i++, y += h * 0.042f)
                Outlined(new Rect(card.x, y, card.width, h * 0.04f), GoalText(d.Goals[i]), _body, Color.white, 1.4f);
            string moves = Loc.F("in {0} moves", _level.MaxMoves);
            if (_level.ExtraMoves > 0) moves += "   " + Loc.F("(+{0} helping hand)", _level.ExtraMoves);
            Outlined(new Rect(card.x, y, card.width, h * 0.035f), moves, _small, Visuals.Hex("FFE27A"), 1.2f);
            if (Btn(new Rect(card.x + card.width * 0.15f, card.yMax - h * 0.145f, card.width * 0.7f, h * 0.065f), "PLAY", "4BE38A", "1FA45B")) _levelIntro = false;
            if (Btn(new Rect(card.x + card.width * 0.3f, card.yMax - h * 0.07f, card.width * 0.4f, h * 0.052f), "BACK", "5A4FA8", "3A2F80", 0.7f)) { _levelIntro = false; ShowMenu(Screen.LevelSelect); }
            GUI.matrix = oldMatrix;
        }

        private static string GoalText(LevelGoal g) => g.Kind switch
        {
            GoalKind.Score => Loc.F("Score {0} points", g.Target),
            GoalKind.Lines => Loc.F("Clear {0} lines", g.Target),
            GoalKind.Stones => g.Target == 1 ? Loc.L("Clear the stone") : Loc.F("Clear all {0} stones", g.Target),
            _ => g.Target == 1 ? Loc.L("Collect the gem") : Loc.F("Collect all {0} gems", g.Target),
        };

        /// <summary>Short HUD form of a goal's progress.</summary>
        private string GoalChip(int i)
        {
            var g = _level.Definition.Goals[i];
            int p = Mathf.Min(_level.Progress(i), g.Target);
            return g.Kind switch
            {
                GoalKind.Score => Loc.F("SCORE {0}/{1}", p, g.Target),
                GoalKind.Lines => Loc.F("LINES {0}/{1}", p, g.Target),
                GoalKind.Stones => Loc.F("STONES {0}/{1}", p, g.Target),
                _ => Loc.F("GEMS {0}/{1}", p, g.Target),
            };
        }

        private IEnumerator FinishSoon()
        {
            int run = _runId;
            _finishing = true;
            RefreshBoard(); RebuildHand();
            _aiThinking = true;
            yield return new WaitForSecondsRealtime(0.9f);
            if (run != _runId || _screen != Screen.Playing) yield break; // a new run started meanwhile
            _finishing = false;
            _aiThinking = false;
            Finish();
        }

        private IEnumerator AiTurns()
        {
            _aiThinking = true;
            while (_screen == Screen.Playing && !_versus.Over && _versus.Turn == 1)
            {
                yield return new WaitForSeconds(_versus.Seats[0].Out ? 0.25f : 0.65f);
                var ai = _versus.Seats[1];
                if (!Bot.TryMove(ai.Board, ai.Dealer.Hand, _aiLevel, _rng, out int i, out int x, out int y)) break;
                int lines = ai.Board.PreviewClears(ai.Dealer.Hand[i], x, y).rows.Count + ai.Board.PreviewClears(ai.Dealer.Hand[i], x, y).cols.Count;
                _versus.TryPlace(i, x, y);
                if (lines > 0) AddFloater(lines > 1 ? Loc.F("AI x{0}!", lines) : "AI +line", MiniCenter + new Vector3(0, -1.6f, 0), Visuals.Hex("C9A7FF"), 0.8f);
                RefreshBoard();
            }
            _aiThinking = false;
            if (_screen != Screen.Playing) yield break;
            if (_versus.Over) { Finish(); yield break; }
            RefreshBoard(); RebuildHand();
        }

        // ---------- landing preview + line glow + hint ----------

        private void UpdateGhosts()
        {
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) _ghosts[x, y].color = Color.clear;
            var b = MainSeat.Board;
            Piece p = null; int ox = 0, oy = 0; bool hint = false;
            if (_dragging >= 0)
            {
                p = MainSeat.Dealer.Hand[_dragging];
                (ox, oy) = DropCell(p, _dragPos);
            }
            else if (_hint.HasValue && Time.time < _hintUntil)
            {
                var h = _hint.Value;
                p = MainSeat.Dealer.Hand[h.hand]; ox = h.x; oy = h.y; hint = true;
            }
            RefreshBoardColorsOnly();
            if (p == null || !b.CanPlace(p, ox, oy)) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            var col = Visuals.BlockColors[p.ColorIndex];
            foreach (var (cx, cy) in p.Cells)
                _ghosts[ox + cx, oy + cy].color = new Color(col.r, col.g, col.b, hint ? 0.25f + 0.35f * pulse : 0.45f);

            var (rows, cols) = b.PreviewClears(p, ox, oy);
            foreach (int y in rows) for (int x = 0; x < Size; x++) Glow(x, y, col, pulse);
            foreach (int x in cols) for (int y = 0; y < Size; y++) Glow(x, y, col, pulse);
        }

        private void Glow(int x, int y, Color pieceCol, float pulse)
        {
            if (_tiles[x, y].enabled) _tiles[x, y].color = Color.Lerp(pieceCol, Color.white, 0.25f + 0.25f * pulse);
            else _ghosts[x, y].color = new Color(pieceCol.r, pieceCol.g, pieceCol.b, 0.7f);
        }

        private void RefreshBoardColorsOnly()
        {
            var b = MainSeat.Board;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (b.IsFilled(x, y)) _tiles[x, y].color = Visuals.BlockColors[b[x, y]];
        }

        /// <summary>The tray over the goal chips: hammer, shuffle and hint, each showing what's left or its price.</summary>
        private void DrawBoosterTray(float w, float h)
        {
            var card = new Rect(w * 0.04f, h * 0.083f, w * 0.92f, h * 0.15f);
            Panel(card, _modalStyle);
            if (_hammerMode)
            {
                Outlined(new Rect(card.x, card.y + h * 0.012f, card.width, h * 0.05f), "Tap a block to smash it", _body, Visuals.Hex("FFE27A"), 1.5f);
                Outlined(new Rect(card.x, card.y + h * 0.055f, card.width, h * 0.03f), "Ice cracks to stone. Smashed gems count.", _small, new Color(1, 1, 1, 0.8f), 1f);
                if (Btn(new Rect(card.x + card.width * 0.3f, card.y + h * 0.09f, card.width * 0.4f, h * 0.05f), "CANCEL", "5A4FA8", "3A2F80", 0.75f)) _hammerMode = false;
                return;
            }
            float bw = (card.width - w * 0.08f) / 3f, by = card.y + h * 0.02f, bh = h * 0.11f;
            for (int k = 0; k < 3; k++)
            {
                var r = new Rect(card.x + w * 0.02f + k * (bw + w * 0.02f), by, bw, bh);
                if (k < 2)
                {
                    var b = (Booster)k;
                    int have = _meta.BoostersOf(b);
                    string name = Loc.L(b == Booster.Hammer ? "HAMMER" : "SHUFFLE");
                    string label = have > 0 ? $"{name}\n×{have}" : $"{name}\n{PlayerMeta.BoosterPrice(b)} ●";
                    bool can = have > 0 || _meta.Coins >= PlayerMeta.BoosterPrice(b);
                    if (Btn(r, label, can ? (k == 0 ? "FF8FAB" : "6E8BFF") : "4A4370", can ? (k == 0 ? "D9466F" : "3A4FE0") : "2E2850", 0.62f)) UseBooster(b);
                }
                else
                {
                    GUI.enabled = _hintsLeft > 0;
                    if (Btn(r, Loc.F("HINT\n×{0}", _hintsLeft), "FFC24B", "F08A1C", 0.62f)) { UseHint(); _boosterTray = false; }
                    GUI.enabled = true;
                }
            }
        }

        /// <summary>Uses a booster, buying one with coins first if none are left (the price is on the button).</summary>
        private void UseBooster(Booster b)
        {
            if (_meta.BoostersOf(b) == 0 && !_meta.BuyBooster(b)) { _fx.Invalid(); return; }
            if (b == Booster.Hammer) { _hammerMode = true; SaveMeta(); return; } // spent on the actual smash
            if (!_meta.UseBooster(b) || !_level.Shuffle()) { _fx.Invalid(); return; }
            SaveMeta(); SaveRun();
            _boosterTray = false;
            RebuildHand();
            AddFloater("SHUFFLE!", BoardCenter + new Vector3(0, -3.2f, 0), Visuals.Hex("7CF8FF"), 1.2f);
            _fx.Star();
        }

        private void TryHammer(Vector3 wp)
        {
            int x = Mathf.RoundToInt(wp.x - BoardCenter.x + 3.5f), y = Mathf.RoundToInt(3.5f - (wp.y - BoardCenter.y));
            var board = _level.Seat.Board;
            if (x < 0 || y < 0 || x >= Size || y >= Size) return; // a tap outside the board does nothing
            if (!board.IsFilled(x, y)) { _fx.Invalid(); return; }
            int cell = board[x, y], gemsBefore = board.Count(Board.Gem), goalsBefore = GoalsMet();
            if (!_meta.UseBooster(Booster.Hammer) || !_level.Smash(x, y)) { _fx.Invalid(); return; }
            _hammerMode = false;
            _boosterTray = false;
            SaveMeta();
            var pos = CellPos(x, y);
            if (!NewLook.ReducedMotion) { Shatter(pos, Visuals.BlockColors[cell]); _shake = 0.12f; }
            _pop[x, y] = 1f;
            _fx.Clear(1, 0);
            if (board.Count(Board.Gem) < gemsBefore) AddFloater("GEM!", pos + new Vector3(0, 0.8f, 0), Visuals.BlockColors[Board.Gem], 1.2f);
            else AddFloater("SMASH!", pos + new Vector3(0, 0.8f, 0), Color.white, 1.1f);
            if (GoalsMet() > goalsBefore && !_level.AllGoalsMet) AddFloater("GOAL COMPLETE!", BoardCenter + new Vector3(0, 3.0f, 0), Visuals.Hex("2EE59D"), 1.3f);
            AfterMove(); // the smash may finish the level
        }

        private void UseHint()
        {
            if (_hintsLeft <= 0) return;
            var seat = MainSeat;
            if (Bot.TryMove(seat.Board, seat.Dealer.Hand, BotLevel.Hard, _rng, out int i, out int x, out int y))
            {
                _hint = (i, x, y); _hintUntil = Time.time + 2.5f; _hintsLeft--; _fx.Pick();
            }
        }

        // ---------- effects ----------

        private void AnimatePops()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    float t = _pop[x, y];
                    if (t <= 0) { _tiles[x, y].transform.localScale = Vector3.one * 0.94f; continue; }
                    _pop[x, y] = Mathf.Max(0, t - Time.deltaTime * 5f);
                    float s = 0.94f * (1f + 0.22f * Mathf.Sin(t * Mathf.PI));
                    _tiles[x, y].transform.localScale = Vector3.one * s;
                }
        }

        private SpriteRenderer Rent(Sprite s, Color c, Vector3 pos, float scale, int order)
        {
            var sr = _pool.Count > 0 ? _pool.Pop() : NewSprite("fx", s, _world, pos, scale, order, c);
            sr.gameObject.SetActive(true);
            sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            sr.transform.position = pos; sr.transform.localScale = Vector3.one * scale; sr.transform.rotation = Quaternion.identity;
            return sr;
        }

        private void Shatter(Vector3 pos, Color col)
        {
            // the block itself flashes and spins away
            var shard = Rent(Visuals.Tile, Color.Lerp(col, Color.white, 0.5f), pos, 0.94f, 6);
            _particles.Add(new Particle { Sr = shard, V = new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 3f), 0), Life = 0.45f, Max = 0.45f, Spin = Random.Range(-360f, 360f), Grow = -1.6f });
            for (int k = 0; k < 6; k++)
            {
                var spark = Rent(Visuals.Circle, col, pos, Random.Range(0.18f, 0.34f), 7);
                var dir = Random.insideUnitCircle.normalized * Random.Range(2.5f, 6.5f);
                _particles.Add(new Particle { Sr = spark, V = new Vector3(dir.x, dir.y + 2f, 0), Life = 0.7f, Max = 0.7f, Spin = 0, Grow = -0.2f });
            }
        }

        private void Confetti(int n)
        {
            float top = _cam.transform.position.y + _cam.orthographicSize;
            for (int k = 0; k < n; k++)
            {
                var c = Visuals.BlockColors[Random.Range(0, 5)];
                var sr = Rent(Visuals.Tile, c, new Vector3(Random.Range(-5f, 5f), top + Random.Range(0f, 3f), 0), Random.Range(0.18f, 0.32f), 30);
                _particles.Add(new Particle { Sr = sr, V = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-6f, -2f), 0), Life = 3f, Max = 3f, Spin = Random.Range(-540f, 540f), Grow = 0 });
            }
        }

        private void AnimateParticles()
        {
            float dt = Time.deltaTime;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Life -= dt;
                if (p.Life <= 0) { p.Sr.gameObject.SetActive(false); _pool.Push(p.Sr); _particles.RemoveAt(i); continue; }
                p.V += new Vector3(0, p.Max > 2f ? -1.5f : -11f, 0) * dt;
                var t = p.Sr.transform;
                t.position += p.V * dt;
                t.Rotate(0, 0, p.Spin * dt);
                t.localScale = Vector3.one * Mathf.Max(0.01f, t.localScale.x + p.Grow * dt);
                var c = p.Sr.color; c.a = Mathf.Clamp01(p.Life / p.Max * 1.5f); p.Sr.color = c;
                _particles[i] = p;
            }
        }

        private void AnimateShake()
        {
            if (_shake <= 0) { _cam.transform.position = new Vector3(_camX, _camY, -10); return; }
            _shake = Mathf.Max(0, _shake - Time.deltaTime);
            var o = Random.insideUnitCircle * _shake * 0.9f;
            _cam.transform.position = new Vector3(_camX + o.x, _camY + o.y, -10);
        }

        private void AnimateDecor()
        {
            if (!_menuDeco.activeSelf) return;
            float top = _cam.transform.position.y + _cam.orthographicSize + 2f;
            foreach (var d in _decos)
            {
                d.T.position += Vector3.up * d.Speed * Time.deltaTime;
                d.T.Rotate(0, 0, d.Spin * Time.deltaTime);
                if (d.T.position.y > top) d.T.position = new Vector3(Random.Range(-6f, 6f), top - _cam.orthographicSize * 2f - 4f, 0);
            }
        }

        private void AddFloater(string text, Vector3 world, Color color, float size)
        {
            _floaters.Add(new Floater { Text = Loc.L(text), World = world, Born = Time.time, Life = 1.1f, Size = size, Color = color });
        }

        // ================= UI (IMGUI with generated skin) =================

        private GUIStyle _h1, _h2, _body, _small, _field;
        private readonly Dictionary<string, GUIStyle> _buttons = new Dictionary<string, GUIStyle>();
        private GUIStyle _cardStyle, _modalStyle, _barBackStyle, _barFillStyle;
        private int _styleFor;

        private void EnsureStyles()
        {
            int fs = Mathf.Max(16, UnityEngine.Screen.height / 40);
            if (_styleFor == fs) return;
            _styleFor = fs;
            _h1 = new GUIStyle(GUI.skin.label) { fontSize = fs * 3, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _h2 = new GUIStyle(_h1) { fontSize = Mathf.RoundToInt(fs * 1.7f) };
            _body = new GUIStyle(GUI.skin.label) { fontSize = fs, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _small = new GUIStyle(_body) { fontSize = Mathf.RoundToInt(fs * 0.8f) };
            if (NewLook.On && NewLook.Display != null) { _h1.font = _h2.font = NewLook.Display; _h1.fontStyle = _h2.fontStyle = FontStyle.Normal; }
            _field = new GUIStyle(GUI.skin.textField) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            // every state gets the same texture: the skin's own hover/active textures are 9-sliced for a different
            // border and render as garbled stripes on a touch screen when the field is pressed.
            var fieldBg = Visuals.RoundedRect(96, 96, 28, Visuals.Hex("F4F1FF"), Visuals.Hex("D9D2FF"), 0f);
            foreach (var state in new[] { _field.normal, _field.hover, _field.active, _field.focused, _field.onNormal, _field.onHover, _field.onActive, _field.onFocused })
            { state.background = fieldBg; state.textColor = Visuals.Hex("2A0E61"); }
            _field.border = new RectOffset(30, 30, 30, 30);
            _barBackStyle = Sliced(Visuals.RoundedRect(64, 32, 15, new Color(0, 0, 0, 0.35f), new Color(0, 0, 0, 0.35f), 0f), 15);
            _barFillStyle = Sliced(Visuals.RoundedRect(64, 32, 15, Visuals.Hex("FFE27A"), Visuals.Hex("FFB627"), 0f), 15);
            _cardStyle = Sliced(Visuals.RoundedRect(96, 96, 32, new Color(0.08f, 0.06f, 0.25f, 0.82f), new Color(0.05f, 0.04f, 0.18f, 0.82f), 0f), 34);
            _modalStyle = Sliced(Visuals.RoundedRect(96, 96, 32, Visuals.Hex("3B2A8C"), Visuals.Hex("1E1458"), 0.08f), 34);
            _buttons.Clear();
        }

        /// <summary>Dims everything behind and draws an opaque popup card.</summary>
        private void Modal(float w, float h, Rect card)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color; GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(new Rect(-w, -h, w * 3, h * 3), Texture2D.whiteTexture); GUI.color = old; // past the safe area too
            }
            Panel(card, _modalStyle);
        }

        private static GUIStyle Sliced(Texture2D tex, int border)
        {
            var st = new GUIStyle { border = new RectOffset(border, border, border, border) };
            st.normal.background = tex;
            return st;
        }

        private void Panel(Rect r, GUIStyle st) { if (Event.current.type == EventType.Repaint) st.Draw(r, false, false, false, false); }

        private GUIStyle ButtonStyle(string hexTop, string hexBottom, float scale = 1f)
        {
            string key = hexTop + hexBottom + scale;
            if (_buttons.TryGetValue(key, out var st)) return st;
            Color top = Visuals.Hex(hexTop), bottom = Visuals.Hex(hexBottom);
            st = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(_styleFor * 1.15f * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, border = new RectOffset(30, 30, 30, 34), padding = new RectOffset(10, 10, 4, 12),
            };
            st.normal.background = Visuals.RoundedRect(96, 104, 30, top, bottom, 0.12f);
            st.hover.background = st.normal.background;
            st.active.background = Visuals.RoundedRect(96, 104, 30, bottom, bottom * 0.85f, 0.04f);
            st.normal.textColor = st.hover.textColor = st.active.textColor = Color.white;
            _buttons[key] = st;
            return st;
        }

        private static readonly Dictionary<(string, int, int, int), int> WrapFit = new Dictionary<(string, int, int, int), int>();

        /// <summary>Wrapped text must fit its box, so a longer translation shrinks (down to about 55%) instead of
        /// spilling past the box or being cut off. The result is cached per text, size and box.</summary>
        private static GUIStyle FitWrapped(GUIStyle st, string text, Rect r)
        {
            var key = (text, st.fontSize, Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));
            if (!WrapFit.TryGetValue(key, out int size))
            {
                size = st.fontSize;
                int min = Mathf.Max(8, Mathf.RoundToInt(st.fontSize * 0.55f));
                var probe = new GUIStyle(st);
                var content = new GUIContent(text);
                while (size > min)
                {
                    probe.fontSize = size;
                    if (probe.CalcHeight(content, r.width) <= r.height * 1.05f) break;
                    size--;
                }
                if (WrapFit.Count > 600) WrapFit.Clear();
                WrapFit[key] = size;
            }
            return size == st.fontSize ? st : new GUIStyle(st) { fontSize = size };
        }

        private static void Outlined(Rect r, string text, GUIStyle st, Color color, float thickness = 2f)
        {
            text = Loc.L(text);
            if (!st.wordWrap && !string.IsNullOrEmpty(text) && st.fontSize > 0)
            {
                float need = st.CalcSize(new GUIContent(text)).x; // longer translations shrink instead of overflowing
                if (need > r.width * 0.96f) st = new GUIStyle(st) { fontSize = Mathf.Max(8, Mathf.FloorToInt(st.fontSize * r.width * 0.96f / need)) };
            }
            else if (st.wordWrap && !string.IsNullOrEmpty(text) && st.fontSize > 0)
                st = FitWrapped(st, text, r);
            var old = st.normal.textColor;
            st.normal.textColor = new Color(0.08f, 0.03f, 0.2f, color.a * 0.85f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * thickness, r.y + Mathf.Sin(a) * thickness + thickness, r.width, r.height), text, st);
            }
            st.normal.textColor = color;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }

        private bool Btn(Rect r, string text, string top, string bottom, float scale = 1f)
        {
            text = Loc.L(text);
            var st = ButtonStyle(top, bottom, scale);
            float need = st.CalcSize(new GUIContent(text)).x;
            if (need > r.width * 0.92f && st.fontSize > 0) st = new GUIStyle(st) { fontSize = Mathf.Max(8, Mathf.FloorToInt(st.fontSize * r.width * 0.92f / need)) };
            return GUI.Button(r, text, st);
        }

        /// <summary>Red notification bubble on a button's top-right corner.</summary>
        private void Badge(Rect button, int count)
        {
            float d = button.height * 0.62f, pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            var r = new Rect(button.xMax - d * 0.75f, button.y - d * 0.3f, d * pulse, d * pulse);
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color; GUI.color = Visuals.Hex("FF3D64");
                GUI.DrawTexture(r, Visuals.Circle.texture); GUI.color = old;
            }
            Outlined(r, count.ToString(), new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false }, Color.white, 1f);
        }

        private bool _lookFont;
        private Font _skinFont;

        private void OnGUI()
        {
            if (NewLook.On != _lookFont)
            {
                if (!_lookFont) _skinFont = GUI.skin.font;
                _lookFont = NewLook.On;
                GUI.skin.font = _lookFont && NewLook.Body != null ? NewLook.Body : _skinFont;
                _styleFor = 0;
            }
            EnsureStyles();
            // Lay the UI out inside the safe area so notches and rounded corners never cover buttons or the score.
            float w = _fitSafe.width, h = _fitSafe.height;
            GUI.matrix = Matrix4x4.Translate(new Vector3(_fitSafe.x, UnityEngine.Screen.height - _fitSafe.yMax, 0));
            switch (_screen)
            {
                case Screen.Menu: GUI.enabled = _welcomeReward == 0; DrawMenu(w, h); break;
                case Screen.LevelSelect: DrawLevelSelect(w, h); break;
                case Screen.AiSelect: DrawAiSelect(w, h); break;
                case Screen.Playing: GUI.enabled = !_breakOpen && !_paused && !_offerRevive && !_offerContinue && !_levelIntro; DrawHud(w, h); GUI.enabled = true; break;
                case Screen.Result: DrawResult(w, h); break;
                case Screen.Challenge: DrawChallenge(w, h); break;
                case Screen.Leaderboard: DrawLeaderboard(w, h); break;
                case Screen.Profile: DrawProfile(w, h); break;
                case Screen.Missions: DrawMissions(w, h); break;
                case Screen.Themes: DrawThemes(w, h); break;
                case Screen.Trophies: DrawTrophies(w, h); break;
                case Screen.Collection: DrawCollection(w, h); break;
                case Screen.Settings: DrawSettings(w, h); break;
                case Screen.Shop: DrawShop(w, h); break;
                case Screen.Ranks: DrawRanks(w, h); break;
            }
            GUI.enabled = true;
            DrawFloaters(w);
            DrawToasts(w, h);
            if (_screen == Screen.Playing) { DrawStreakMeter(w, h); DrawTutorial(w, h); DrawBreak(w, h); DrawPause(w, h); DrawSecondChance(w, h); DrawLevelIntro(w, h); DrawContinue(w, h); }
            if (Time.unscaledTime < _flashUntil)
            {
                var old = GUI.color; GUI.color = new Color(1, 1, 1, 0.55f * (_flashUntil - Time.unscaledTime) / 0.14f);
                GUI.DrawTexture(new Rect(-w, -h, w * 3, h * 3), Texture2D.whiteTexture); GUI.color = old;
            }
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawStreakMeter(float w, float h)
        {
            // levels use this strip for the star bar; their combos still get the COMBO floater
            if (_mode == Mode.Versus || _mode == Mode.VsAi || _mode == Mode.Level || (_tutorial < 3 && _mode == Mode.Classic)) return;
            int combo = MainSeat.Board.Combo;
            if (combo < 1) return;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 9f);
            string[] hex = { "FF9B5E", "FF6FB5", "B98CFF", "7CF8FF", "FFE27A" };
            var c = Visuals.Hex(hex[Mathf.Min(combo - 1, hex.Length - 1)]);
            var r = new Rect(w * 0.3f, h * 0.205f, w * 0.4f, h * 0.045f);
            Panel(r, _cardStyle);
            var st = new GUIStyle(_body) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(_body.fontSize * pulse) };
            Outlined(r, Loc.F("STREAK x{0}", combo + 1), st, c, 1.5f);
        }

        private void DrawTutorial(float w, float h) => DrawGuide(w, h);

        private IEnumerator HitStop(float seconds)
        {
            if (_paused) yield break;
            Time.timeScale = 0.05f; // a brief freeze-frame on impact: one of the strongest game-feel signals
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = _paused ? 0f : 1f;
        }

        private void MaybeInterstitial()
        {
            double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool removeAds = PlayerPrefs.GetInt("removeAds", 0) == 1 || _meta.NoAds;
            if (!_adPolicy.ShouldShowInterstitial(Today, now, removeAds, _firstSession)) return;
            _ads.Show(AdKind.Interstitial, shown =>
            {
                if (!shown) return;
                _adPolicy.OnInterstitialShown(now);
                PlayerPrefs.SetString("ads", _adPolicy.Serialize());
            });
        }

        private void AcceptRevive()
        {
            _ads.Show(AdKind.Rewarded, ok =>
            {
                _offerRevive = false;
                if (ok && _single.Revive())
                {
                    SaveRun();
                    RefreshBoard(); RebuildHand();
                    AddFloater("SECOND CHANCE!", BoardCenter + new Vector3(0, 2.8f, 0), Visuals.Hex("7CF8FF"), 1.4f);
                    _fx.Star();
                }
                else StartCoroutine(FinishSoon());
            });
        }

        private void DeclineRevive()
        {
            _offerRevive = false;
            StartCoroutine(FinishSoon());
        }

        private void DrawSecondChance(float w, float h)
        {
            if (!_offerRevive) return;
            var card = new Rect(w * 0.08f, h * 0.3f, w * 0.84f, h * 0.34f);
            Modal(w, h, card);
            Outlined(new Rect(card.x, card.y + h * 0.02f, card.width, h * 0.06f), "OUT OF MOVES", new GUIStyle(_h2) { fontSize = Mathf.RoundToInt(_h2.fontSize * 0.85f) }, Visuals.Hex("FF8FA3"), 3);
            Outlined(new Rect(card.x + w * 0.05f, card.y + h * 0.08f, card.width - w * 0.1f, h * 0.08f), Loc.F("Score {0}. Second chance: 3 new pieces that fit!", Score), _small, Color.white, 1.2f);
            float bw = card.width * 0.8f, bx = card.x + card.width * 0.1f;
            if (Btn(new Rect(bx, card.y + h * 0.165f, bw, h * 0.065f), _ads.RewardedIsFree ? "CONTINUE  (FREE)" : "WATCH VIDEO TO CONTINUE", "4BE38A", "1FA45B", 0.85f)) AcceptRevive();
            if (Btn(new Rect(bx, card.y + h * 0.245f, bw, h * 0.06f), "NO THANKS", "5A4FA8", "3A2F80", 0.8f)) DeclineRevive();
        }

        private void SetPaused(bool paused)
        {
            // A finished game can't be paused (or quit from the pause card) during the short wait before its
            // result screen, so the best score, leaderboard submit and missions always get recorded.
            if (paused && _finishing) return;
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f; // freezes animations and the AI's turn coroutine
        }

        private void DrawPause(float w, float h)
        {
            if (!_paused) return;
            var card = new Rect(w * 0.1f, h * 0.3f, w * 0.8f, h * 0.36f);
            Modal(w, h, card);
            Outlined(new Rect(card.x, card.y + h * 0.025f, card.width, h * 0.06f), "PAUSED", _h2, Color.white, 3);
            Outlined(new Rect(card.x, card.y + h * 0.085f, card.width, h * 0.04f), Loc.F("Score {0}", Score), _body, Visuals.Gold, 1.5f);
            float bw = card.width * 0.8f, bx = card.x + card.width * 0.1f;
            if (Btn(new Rect(bx, card.y + h * 0.14f, bw, h * 0.065f), "RESUME", "4BE38A", "1FA45B")) SetPaused(false);
            if (Btn(new Rect(bx, card.y + h * 0.22f, bw, h * 0.065f), "QUIT TO MENU", "FF7AB6", "E0347C", 0.85f)) ShowMenu();
            Outlined(new Rect(card.x, card.y + h * 0.29f, card.width, h * 0.04f), "Quitting ends this game.", _small, new Color(1, 1, 1, 0.88f), 1f);
        }

        private void DrawBreak(float w, float h)
        {
            if (!_breakOpen) return;
            var card = new Rect(w * 0.08f, h * 0.33f, w * 0.84f, h * 0.28f);
            Modal(w, h, card);
            Outlined(new Rect(card.x, card.y + h * 0.02f, card.width, h * 0.06f), "GREAT SESSION!", _h2, Visuals.Hex("2EE59D"), 3);
            Outlined(new Rect(card.x + w * 0.05f, card.y + h * 0.09f, card.width - w * 0.1f, h * 0.08f), "You've played for 40 minutes. A quick stretch keeps your mind sharp.", _small, Color.white, 1.2f);
            if (Btn(new Rect(card.x + card.width * 0.15f, card.y + h * 0.19f, card.width * 0.7f, h * 0.065f), "KEEP PLAYING", "4BE38A", "1FA45B")) _breakOpen = false;
        }

        private Rect Row(float w, float y, float hRow, float margin = 0.08f) => new Rect(w * margin, y, w * (1 - 2 * margin), hRow);

        private void DrawMenu(float w, float h)
        {
            float bob = Mathf.Sin(Time.time * 2f) * h * 0.006f;
            // coins + streak chip
            Panel(new Rect(w * 0.04f, h * 0.025f, w * 0.92f, h * 0.045f), _cardStyle);
            Outlined(new Rect(w * 0.07f, h * 0.022f, w * 0.5f, h * 0.045f), Loc.F("LV {0}   ·   STREAK {1}", _meta.Level, _meta.Streak), new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = false }, Visuals.Hex("FF9B5E"), 1.2f);
            if (Event.current.type == EventType.Repaint)
            {
                var bar = new Rect(w * 0.07f, h * 0.06f, w * 0.86f, Mathf.Max(3f, h * 0.004f));
                var oldC = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.35f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = Visuals.Gold; GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * _meta.Xp / PlayerMeta.XpToNext(_meta.Level), bar.height), Texture2D.whiteTexture);
                GUI.color = oldC;
            }
            Outlined(new Rect(w * 0.43f, h * 0.025f, w * 0.5f, h * 0.045f), $"● {_meta.Coins}", new GUIStyle(_body) { alignment = TextAnchor.MiddleRight }, Visuals.Gold, 1.5f);
            Outlined(new Rect(0, h * 0.075f + bob, w, h * 0.08f), "BLOCK", _h1, Visuals.Hex("FFE27A"), 3);
            Outlined(new Rect(0, h * 0.145f + bob, w, h * 0.08f), "DROP", _h1, Color.white, 3);
            if (NewLook.On) Outlined(new Rect(0, h * 0.216f + bob, w, h * 0.03f), "Fair hands. Every game.", _small, Visuals.Hex("B9B8E8"), 1f);
            // Five quick taps on the logo switch the new look on or off (for testing on a device before it ships on).
            if (GUI.Button(new Rect(w * 0.2f, h * 0.075f, w * 0.6f, h * 0.15f), GUIContent.none, GUIStyle.none)) LogoTapped();
            float y = DrawHomeButtons(w, h, h * 0.245f);
            Outlined(new Rect(0, y, w, h * 0.035f), Loc.F("BEST {0}   ·   {1}", _best, Loc.L(OnlineService.Ready ? "ONLINE" : "OFFLINE")), _small, new Color(1, 1, 1, 0.85f), 1.5f);
            if (Debug.isDebugBuild && GUI.Button(new Rect(0, y, w, h * 0.035f), GUIContent.none, GUIStyle.none)) // development builds and the editor only
            {
                _devTaps = Time.unscaledTime - _devTapAt < 0.6f ? _devTaps + 1 : 1; _devTapAt = Time.unscaledTime;
                if (_devTaps >= 5)
                {
                    _devTaps = 0; _devPreview = !_devPreview;
                    Toast(_devPreview ? "PREVIEW ON: ALL LEVELS OPEN" : "PREVIEW OFF", Visuals.Hex("7CF8FF"));
                    if (_devPreview) _welcomeReward = PlayerMeta.StreakReward(Mathf.Max(1, _meta.Streak));
                }
            }
            PlayerButton(Row(w, y + h * 0.045f, h * 0.06f, 0.18f));

            if (_welcomeReward > 0)
            {
                GUI.enabled = true;
                var card = new Rect(w * 0.05f, h * 0.28f, w * 0.9f, h * 0.38f);
                Modal(w, h, card);
                Outlined(new Rect(card.x, card.y + h * 0.015f, card.width, h * 0.055f), Loc.F("DAY {0} STREAK!", _meta.Streak), new GUIStyle(_h2) { fontSize = Mathf.RoundToInt(_h2.fontSize * 0.8f) }, Visuals.Hex("FF9B5E"), 3);
                Outlined(new Rect(card.x, card.y + h * 0.07f, card.width, h * 0.06f), $"+{_welcomeReward} ●", _h2, Visuals.Gold, 3);
                // the weekly ladder: days done, today, and what is still ahead (day 7 is the big one)
                int today = PlayerMeta.LadderDay(_meta.Streak);
                float cell = card.width / 7f, cs = cell * 0.78f, cy = card.y + h * 0.145f;
                for (int day = 1; day <= 7; day++)
                {
                    var c = new Rect(card.x + (day - 1) * cell + (cell - cs) / 2f, cy, cs, cs);
                    bool done = day < today, now = day == today, big = day == 7;
                    float pop = now && !NewLook.ReducedMotion ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
                    var cr = new Rect(c.center.x - c.width * pop / 2f, c.center.y - c.height * pop / 2f, c.width * pop, c.height * pop);
                    Panel(cr, ShopStyle(done ? "2EE59D" : now ? "FFE27A" : big ? "B07CFF" : "5A4FA8", done ? "13805A" : now ? "E08A00" : big ? "5B2DB0" : "2F2670"));
                    Outlined(cr, done ? "✓" : big ? "★" : day.ToString(), new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter, wordWrap = false, font = null }, Color.white, 1.5f);
                    int rw = PlayerMeta.StreakReward(Mathf.Max(1, _meta.Streak - today + day));
                    Outlined(new Rect(c.x - cell * 0.1f, c.yMax + h * 0.004f, c.width + cell * 0.2f, h * 0.026f), "+" + rw, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontSize = Mathf.Max(8, Mathf.RoundToInt(_small.fontSize * 0.8f)) }, day == today ? Visuals.Gold : new Color(1, 1, 1, 0.7f), 1f);
                }
                Outlined(new Rect(card.x + w * 0.04f, cy + cs + h * 0.04f, card.width - w * 0.08f, h * 0.04f), today == 7 ? Loc.L("Day 7 prize: a free hammer!") : Loc.F("Come back tomorrow for +{0}", PlayerMeta.StreakReward(_meta.Streak + 1)), _small, Color.white, 1.2f);
                if (Btn(new Rect(card.x + card.width * 0.2f, card.yMax - h * 0.085f, card.width * 0.6f, h * 0.065f), "COLLECT", "4BE38A", "1FA45B")) _welcomeReward = 0;
            }
        }

        private void DrawMissions(float w, float h)
        {
            Outlined(new Rect(0, h * 0.06f, w, h * 0.08f), "DAILY MISSIONS", _h2, Visuals.Hex("7CF8FF"), 3);
            Outlined(new Rect(0, h * 0.13f, w, h * 0.04f), Loc.F("● {0}   ·   STREAK {1}   ·   BEST STREAK {2}", _meta.Coins, _meta.Streak, _meta.BestStreak), _small, Visuals.Gold, 1.2f);
            var ms = PlayerMeta.MissionsFor(Today);
            for (int i = 0; i < 3; i++)
            {
                bool done = _meta.Claimed[i], ready = !done && _meta.IsComplete(i, Today);
                var card = BeginShopCard(new Rect(w * 0.05f, h * (0.19f + i * 0.17f), w * 0.9f, h * 0.15f), i,
                    done ? "2EE59D" : ready ? "FFD54A" : "6E8BFF", done ? "13805A" : ready ? "E08A00" : "3A4FE0", ready, done, "FFD959");
                var left = new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
                Outlined(new Rect(card.x + w * 0.05f, card.y + h * 0.012f, card.width * 0.9f, h * 0.045f), ms[i].Text, left, Color.white, 1.2f);
                int prog = Mathf.Min(_meta.Progress[i], ms[i].Target);
                var bar = new Rect(card.x + w * 0.05f, card.y + h * 0.075f, card.width * 0.52f, h * 0.024f);
                Panel(bar, _barBackStyle);
                float f = (float)prog / ms[i].Target;
                // a finished mission fills the whole bar in green, so it never reads as empty on the gold card
                if (f > 0.04f) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * f), bar.height), ready || done ? Swatch(Visuals.Hex("12B070")) : _barFillStyle);
                Outlined(new Rect(bar.x, bar.y + h * 0.03f, bar.width, h * 0.035f), ready ? Loc.L("READY TO CLAIM!") : $"{prog} / {ms[i].Target}", new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, fontStyle = ready ? FontStyle.Bold : FontStyle.Normal, wordWrap = false }, ready ? Color.white : new Color(1, 1, 1, 0.9f), 1);
                var btn = new Rect(card.xMax - w * 0.3f, card.y + h * 0.065f, w * 0.26f, h * 0.06f);
                var chip = new Rect(btn.x, btn.y + btn.height * 0.1f, btn.width, btn.height * 0.8f);
                if (done) Pill(chip, "DONE ✓", "FFFFFF", "C9F7E4", Visuals.Hex("0E6B48"));
                else if (ready)
                {
                    // a pulsing CLAIM button with the reward and the same coin icon as every other reward chip
                    float pulse = NewLook.ReducedMotion ? 1f : 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 5f);
                    var cb = new Rect(btn.center.x - btn.width * pulse / 2f, btn.center.y - btn.height * pulse / 2f, btn.width * pulse, btn.height * pulse);
                    if (Btn(cb, Loc.L("CLAIM"), "4BE38A", "1FA45B", 0.8f) && _meta.Claim(i, Today)) { _fx.Win(); Confetti(25); _shopBurstFrom = btn.center; _shopBurstAt = Time.unscaledTime; SaveMeta(); }
                    RewardChip(new Rect(cb.x, cb.y - h * 0.032f, cb.width, h * 0.03f), ms[i].Reward, Color.white);
                }
                else { Pill(chip, "", "FFFFFF", "DCD3FF", Visuals.Hex("3A1B8F")); RewardChip(chip, ms[i].Reward, Visuals.Hex("3A1B8F")); }
                EndShopCard();
            }
            DrawShopCoinBurst(w, h);
            DrawDailyChest(w, h);
            Outlined(new Rect(w * 0.08f, h * 0.755f, w * 0.84f, h * 0.045f), "New missions every day. Coins unlock themes; they never buy an advantage.", _small, new Color(1, 1, 1, 0.9f), 1.2f);
            int got = 0; for (int i = 0; i < PlayerMeta.Achievements.Length; i++) if (_meta.HasAchievement(i)) got++;
            float half = w * 0.84f / 2f - w * 0.01f;
            if (Btn(new Rect(w * 0.08f, h * 0.81f, half, h * 0.06f), Loc.F("TROPHIES  {0}/{1}", got, PlayerMeta.Achievements.Length), "7CF8FF", "1C8FE0", 0.85f)) ShowMenu(Screen.Trophies);
            if (Btn(new Rect(w * 0.92f - half, h * 0.81f, half, h * 0.06f), "RANKS", "FFD54A", "D99A00", 0.85f)) ShowMenu(Screen.Ranks);
            if (Btn(Row(w, h * 0.89f, h * 0.065f, 0.25f), "BACK", "5A4FA8", "3A2F80")) GoBack();
        }

        /// <summary>Under the three missions: a chest that fills as each one is claimed and opens for a fixed prize.</summary>
        private void DrawDailyChest(float w, float h)
        {
            int n = _meta.ClaimedCount(Today);
            var r = new Rect(w * 0.05f, h * 0.693f, w * 0.9f, h * 0.056f);
            bool ready = _meta.ChestReady(Today), opened = _meta.ChestClaimed;
            if (ready)
            {
                float pulse = NewLook.ReducedMotion ? 1f : 1f + 0.02f * Mathf.Sin(Time.unscaledTime * 5f);
                var rb = new Rect(r.center.x - r.width * pulse / 2f, r.center.y - r.height * pulse / 2f, r.width * pulse, r.height * pulse);
                if (Btn(rb, Loc.L("OPEN CHEST") + "  +" + PlayerMeta.ChestCoins, "4BE38A", "1FA45B", 0.8f) && _meta.ClaimChest(Today)) { _fx.Win(); Confetti(60); _shopBurstFrom = rb.center; _shopBurstAt = Time.unscaledTime; SaveMeta(); }
                return;
            }
            Panel(r, _barBackStyle);
            float pad = r.height * 0.18f, bw = r.width * 0.34f;
            Outlined(new Rect(r.x + pad * 1.5f, r.y, r.width * 0.5f, r.height), opened ? Loc.L("CHEST OPENED ✓") : Loc.L("DAILY CHEST"), new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleLeft }, opened ? Visuals.Hex("2EE59D") : Visuals.Hex("FFE27A"), 1.2f);
            for (int i = 0; i < 3; i++)
            {
                var seg = new Rect(r.xMax - pad * 1.5f - bw + i * (bw / 3f), r.center.y - r.height * 0.15f, bw / 3f - 4f, r.height * 0.3f);
                Panel(seg, i < n || opened ? Swatch(Visuals.Hex("12B070")) : _barBackStyle);
            }
        }

        /// <summary>"+N" and a gold coin, drawn as a picture so every reward chip shows the same icon whatever the font.</summary>
        private void RewardChip(Rect r, int amount, Color text)
        {
            float d = r.height * 0.62f;
            var st = new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleRight };
            var label = new Rect(r.x, r.y, r.width * 0.5f + d * 0.1f, r.height);
            Outlined(label, "+" + amount, st, text, 0f);
            if (Event.current.type != EventType.Repaint) return;
            var coin = new Rect(label.xMax + d * 0.35f, r.center.y - d / 2f, d, d);
            var old = GUI.color;
            GUI.color = Visuals.Hex("C98A00"); GUI.DrawTexture(new Rect(coin.x - 1, coin.y - 1, d + 2, d + 2), Visuals.Circle.texture);
            GUI.color = Visuals.Gold; GUI.DrawTexture(coin, Visuals.Circle.texture);
            GUI.color = old;
        }

        private void DrawTrophies(float w, float h) => DrawTrophiesPolish(w, h);

        private readonly Dictionary<Color, GUIStyle> _swatches = new Dictionary<Color, GUIStyle>();
        private GUIStyle Swatch(Color c)
        {
            if (!_swatches.TryGetValue(c, out var st)) _swatches[c] = st = Sliced(Visuals.RoundedRect(32, 32, 8, Color.Lerp(c, Color.white, 0.25f), c, 0.15f), 9);
            return st;
        }

        /// <summary>Themes in four tiers. Starter and premium are coin goals, legendary is the long-term prize,
        /// exclusive looks are earned and show how far along the player is.</summary>
        /// <summary>Distance to an earned theme, so the goal always shows how far away it is.</summary>
        private string ExclusiveProgress(Theme th) => th.ChapterReward > 0
            ? Loc.F("{0}/{1} CHAPTERS", Mathf.Min(Collection.CompletedPictures(_progress), th.ChapterReward), th.ChapterReward)
            : $"{Mathf.Min(_progress.TotalStars, th.StarsReward)}/{th.StarsReward} ★";

        /// <summary>Real-money shop. Each card says exactly what is inside; nothing is random and nothing expires.</summary>
        private void Buy(Product p)
        {
            _store.Buy(p.Id, ok =>
            {
                if (!ok || !_meta.Grant(p)) { _fx.Invalid(); return; }
                SaveMeta();
                Toast(Loc.F("THANK YOU! {0}", Store.Contents(p)), Visuals.Gold);
                _fx.Win();
                _shopBurstAt = Time.unscaledTime;
                Confetti(p.Coins >= 8000 || p.Themes.Length > 0 ? 90 : 50); // scaled to the purchase
            });
        }

        // ---- level map: a scrolling winding path, one node per level, a banner at each chapter start ----
        private float _mapBottom = -1, _mapVel, _mapTravel;
        private bool _mapDown, _mapTapped;
        private Vector2 _mapLast, _mapTap;
        private static readonly string[] MapColors = { "6E8BFF", "B56EFF", "FF8FAB", "3DC9E0" };

        private float MapTop => _fitSafe.height * 0.17f;
        private float MapBottomEdge => _fitSafe.height * 0.80f;
        private float MapStep => _fitSafe.height * 0.1f;
        private float MapSteps => (MapBottomEdge - MapTop) / MapStep;

        /// <summary>Drag or fling to scroll, tap a node to play it. Runs in Update so it works with touch and mouse alike.</summary>
        private void UpdateMap()
        {
            if (_mapBottom < 0) _mapBottom = LevelMap.StartBottom(_progress.Frontier);
            if (_mapPick > 0) { _mapDown = false; _mapVel = 0; return; }   // the start card is open
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            var m = ToGui(Input.mousePosition);
            if (Input.GetMouseButtonDown(0) && m.y > MapTop && m.y < MapBottomEdge) { _mapDown = true; _mapTravel = 0; _mapVel = 0; _mapLast = m; }
            else if (_mapDown && Input.GetMouseButton(0))
            {
                var d = m - _mapLast; _mapLast = m; _mapTravel += d.magnitude;
                if (_mapTravel > 14f) { _mapBottom += d.y / MapStep; _mapVel = d.y / MapStep / dt; }
            }
            else
            {
                if (_mapDown && _mapTravel <= 14f) { _mapTapped = true; _mapTap = m; }
                _mapDown = false;
                _mapBottom += _mapVel * dt;
                _mapVel *= Mathf.Exp(-4f * dt);
                if (Mathf.Abs(_mapVel) < 0.05f) _mapVel = 0;
            }
            _mapBottom += Input.mouseScrollDelta.y * 1.5f;
            _mapBottom = LevelMap.ClampBottom(_mapBottom, MapSteps);
        }

        private static readonly Dictionary<string, Texture2D> MapTextures = new Dictionary<string, Texture2D>();

        /// <summary>Original map art from Resources/Map (drawn by tools/art/make_map_art.py). Null if missing, so the map still works.</summary>
        private static Texture2D MapTex(string name)
        {
            if (!MapTextures.TryGetValue(name, out var t)) MapTextures[name] = t = Resources.Load<Texture2D>("Map/" + name);
            return t;
        }

        private static void Sprite(Rect r, string name, Color tint)
        {
            var t = MapTex(name);
            GUI.color = tint;
            GUI.DrawTexture(r, t != null ? t : Visuals.Circle.texture);
            GUI.color = Color.white;
        }

        private int _mapPick; // kept at 0: Levels 2.0 shows its own start card after a tap

        private void OpenLevels() => ShowMenu(Screen.LevelSelect);

        private void PickLevel(int n) => StartMode(Mode.Level, level: n);

        private void DrawLevelSelect(float w, float h)
        {
            Outlined(new Rect(0, h * 0.05f, w, h * 0.08f), "LEVELS", _h2, Visuals.Hex("FFE27A"), 3);
            Outlined(new Rect(0, h * 0.115f, w, h * 0.04f), $"{_progress.TotalStars} ★", _body, Color.white, 1.5f);
            int frontier = _progress.Frontier;
            var (first, last) = LevelMap.Visible(_mapBottom, MapSteps);
            if (_mapPick == 0 && (frontier < first || frontier > last) && Btn(new Rect(w * 0.7f, h * 0.115f, w * 0.26f, h * 0.045f), Loc.F("GO TO {0}", frontier), "FFC24B", "F08A1C", 0.7f))
                _mapBottom = LevelMap.ClampBottom(LevelMap.StartBottom(frontier), MapSteps);

            float step = MapStep, mapH = MapBottomEdge - MapTop, baseY = mapH - step * 0.5f, node = step * 0.78f;
            float X(int n) => w * LevelMap.NodeX(n);
            float Y(int n) => baseY - (n - _mapBottom) * step;
            int tapped = -1;
            var tap = _mapTap - new Vector2(0, MapTop);
            GUI.BeginClip(new Rect(0, MapTop, w, mapH));
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint)
            {
                var (ta, tb, tt) = LevelMap.Blend(_mapBottom + MapSteps / 2);   // backdrop: sky and hills fade from chapter to chapter
                var full = new Rect(0, 0, w, mapH);
                Sprite(full, "bg_" + LevelMap.ThemeKeys[ta], Color.white);
                if (tt > 0.01f) Sprite(full, "bg_" + LevelMap.ThemeKeys[tb], new Color(1, 1, 1, tt));
                for (int n = first; n <= last; n++) DrawMapProp(n, Y(n), w, step);
                for (int n = first; n < last; n++)                            // the road between neighbouring nodes
                {
                    var a = new Vector2(X(n), Y(n)); var b = new Vector2(X(n + 1), Y(n + 1));
                    bool done = _progress.StarsFor(n) > 0;
                    for (int pass = 0; pass < 2; pass++)
                        for (int k = 1; k < 14; k++)
                        {
                            var q = Vector2.Lerp(a, b, k / 14f); float ds = step * (pass == 0 ? 0.17f : 0.11f);
                            Sprite(new Rect(q.x - ds / 2, q.y - ds / 2, ds, ds), "", pass == 0 ? new Color(0, 0, 0, 0.18f) : done ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(1, 1, 1, 0.7f));
                        }
                }
            }
            for (int n = first; n <= last; n++)
            {
                if (!LevelMap.StartsChapter(n)) continue;
                // a compact ribbon on the side away from the node, so it never covers the node or its stars
                float nodeX = X(n), nodeHalf = node * 0.75f, margin = w * 0.03f;
                bool ribbonLeft = nodeX >= w * 0.5f;
                float room = ribbonLeft ? nodeX - nodeHalf - margin * 2 : w - nodeX - nodeHalf - margin * 2;
                float bw = Mathf.Min(w * 0.5f, Mathf.Max(w * 0.34f, room)), bh = bw * 96f / 512f;
                var banner = new Rect(ribbonLeft ? margin : w - margin - bw, Y(n) - bh / 2, bw, bh);
                if (repaint) Sprite(banner, "ribbon", Color.white);
                var label = new Rect(banner.x + bw * 0.12f, banner.y, bw * 0.76f, bh * 0.62f);
                var text = Loc.F("CHAPTER {0}", LevelMap.ChapterOf(n)) + "  ·  " + Loc.L(LevelLibrary.Chapters[Mathf.Min(LevelMap.ChapterOf(n) - 1, LevelLibrary.Chapters.Length - 1)].Name);
                Outlined(label, text, new GUIStyle(_small) { wordWrap = false, fontStyle = FontStyle.Bold }, Color.white, 1.2f);
            }
            for (int n = first; n <= last; n++)
            {
                float cx = X(n), cy = Y(n);
                var r = new Rect(cx - node / 2, cy - node / 2, node, node);
                bool open = (Debug.isDebugBuild && _devPreview) || _progress.IsUnlocked(n);
                int stars = _progress.StarsFor(n);
                var tier = LevelLibrary.TierFor(n);
                if (repaint)
                {
                    Color c = !open ? Visuals.Hex("6C6590") : stars == 3 ? Visuals.Hex("FFD54A") : stars > 0 ? Visuals.Hex("4BE38A")
                        : tier == LevelTier.SuperHard ? Visuals.Hex("C04BFF") : tier == LevelTier.Hard ? Visuals.Hex("FF6B6B") : Visuals.Hex(MapColors[LevelMap.ChapterOf(n) % MapColors.Length]);
                    if (n == frontier) { float pulse = 1.22f + 0.06f * Mathf.Sin(Time.time * 5f); Sprite(new Rect(cx - node * pulse / 2, cy - node * pulse / 2, node * pulse, node * pulse), "", new Color(1, 1, 1, 0.5f)); }
                    Sprite(r, "node", c);
                    if (n == 1) Sprite(new Rect(r.x - node * 0.1f, r.y - node * 0.42f, node * 0.55f, node * 0.55f), "crown", Color.white);
                    if (!open) Sprite(new Rect(r.xMax - node * 0.38f, r.yMax - node * 0.4f, node * 0.34f, node * 0.34f), "lock", Color.white);
                    if (open) for (int i = 0; i < 3; i++) Sprite(new Rect(cx + (i - 1.5f) * node * 0.3f, cy + node * 0.46f, node * 0.3f, node * 0.3f), i < stars ? "star_on" : "star_off", Color.white);
                }
                Outlined(r, n.ToString(), new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false }, open ? Color.white : new Color(1, 1, 1, 0.85f), 1.5f);
                if ((tier == LevelTier.Hard || tier == LevelTier.SuperHard) && open) Outlined(new Rect(r.xMax - node * 0.28f, r.y - node * 0.08f, node * 0.36f, node * 0.36f), "!", new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false }, tier == LevelTier.SuperHard ? Visuals.Hex("E7A6FF") : Visuals.Hex("FFB0B0"), 1.2f);
                if (n == frontier) Outlined(new Rect(cx - node / 2, cy - node * 1.0f + Mathf.Sin(Time.time * 4f) * node * 0.06f, node, node * 0.4f), "▼", new GUIStyle(_body) { wordWrap = false }, Color.white, 1.2f);
                if (_mapTapped && open && (new Vector2(cx, cy) - tap).sqrMagnitude < node * node * 0.3f) tapped = n;
            }
            GUI.EndClip();
            _mapTapped = false;
            if (tapped > 0) PickLevel(tapped);

                        var left = new Rect(w * 0.06f, h * 0.835f, w * 0.52f, h * 0.075f);
            if (Btn(left, Loc.F("COLLECTION  {0}/{1}", Collection.CompletedPictures(_progress), Collection.Pictures.Length), "FF8FAB", "D9466F", 0.75f)) ShowMenu(Screen.Collection);
            if (Btn(new Rect(w * 0.62f, h * 0.835f, w * 0.32f, h * 0.075f), "BACK", "5A4FA8", "3A2F80", 0.85f)) GoBack();
        }

        /// <summary>Scenery beside the path (sprites tinted by the chapter's colour): a tree, a house or a lamp post.</summary>
        private void DrawMapProp(int n, float nodeY, float w, float step)
        {
            var (kind, px) = LevelMap.PropFor(n);
            if (kind < 0) return;
            var c = Color.Lerp(Visuals.Hex(LevelMap.ThemeColors[LevelMap.ThemeOf(LevelMap.ChapterOf(n))]), Color.white, 0.2f) * 0.85f; c.a = 1;
            float u = step * 0.62f;
            Sprite(new Rect(w * px - u / 2, nodeY - u * 0.65f, u, u), kind == 0 ? "tree" : kind == 1 ? "house" : "lamp", kind == 2 ? Color.white : c);
        }

        /// <summary>A chapter's picture centred at <paramref name="cx"/>, with unrevealed tiles shown as a checker.</summary>
        private void DrawPicture(int i, float cx, float top, float pw)
        {
            var pic = Collection.Pictures[i];
            float ph = pw * Collection.Height / Collection.Width, cellPx = pw / Collection.Width, px = cx - pw / 2f;
            Panel(new Rect(px - 6, top - 6, pw + 12, ph + 12), _cardStyle);
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            for (int y = 0; y < Collection.Height; y++)
                for (int x = 0; x < Collection.Width; x++)
                {
                    bool shown = Collection.Revealed(_progress, i, Collection.TileAt(x, y));
                    GUI.color = shown ? Visuals.Hex(pic.Colors[pic.Rows[y][x]]) : (((x / Collection.TileW + y / Collection.TileH) % 2 == 0) ? new Color(1, 1, 1, 0.10f) : new Color(1, 1, 1, 0.05f));
                    GUI.DrawTexture(new Rect(px + x * cellPx, top + y * cellPx, cellPx + 0.5f, cellPx + 0.5f), Texture2D.whiteTexture);
                }
            GUI.color = old;
        }

        private void DrawCollection(float w, float h)
        {
            Outlined(new Rect(0, h * 0.045f, w, h * 0.07f), "COLLECTION", _h2, Visuals.Hex("FF8FAB"), 3);
            Outlined(new Rect(0, h * 0.11f, w, h * 0.035f), "Beat levels to reveal each picture.", _small, Color.white, 1.2f);
            // two pictures per row, five rows: the whole journey at a glance
            float pw = Mathf.Min(w * 0.4f, (h * 0.72f / 5f - h * 0.045f) * Collection.Width / Collection.Height);
            float ph = pw * Collection.Height / Collection.Width, rowH = ph + h * 0.045f;
            for (int i = 0; i < Collection.Pictures.Length; i++)
            {
                float cx = w * (i % 2 == 0 ? 0.27f : 0.73f), top = h * 0.16f + (i / 2) * rowH;
                int got = Collection.RevealedCount(_progress, i);
                Outlined(new Rect(cx - pw / 2, top, pw, h * 0.03f), Loc.F("{0}  {1}/{2}", Loc.L(Collection.Pictures[i].Name).ToUpperInvariant(), got, Collection.TilesPerPicture),
                    new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false }, got == Collection.TilesPerPicture ? Visuals.Gold : Color.white, 1.2f);
                DrawPicture(i, cx, top + h * 0.033f, pw);
            }
            if (Btn(Row(w, h * 0.9f, h * 0.065f, 0.25f), "BACK", "5A4FA8", "3A2F80")) GoBack();
        }

        private void DrawAiSelect(float w, float h)
        {
            Outlined(new Rect(0, h * 0.12f, w, h * 0.08f), "VS AI", _h1, Visuals.Hex("C9A7FF"), 3);
            Outlined(new Rect(w * 0.05f, h * 0.21f, w * 0.9f, h * 0.08f), "Same pieces for you and the AI. Highest score wins.", _small, Color.white, 1.5f);
            Outlined(new Rect(w * 0.05f, h * 0.265f, w * 0.9f, h * 0.04f), RankLine(RankTrack.VsAi), new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false }, Visuals.Hex(Ranks.TierColor(_meta.RankStep(RankTrack.VsAi))), 1.2f);
            float y = h * 0.32f, bh = h * 0.085f, gap = h * 0.02f;
            if (Btn(Row(w, y, bh), "EASY", "4BE38A", "1FA45B")) { _aiLevel = BotLevel.Easy; StartMode(Mode.VsAi); }
            if (Btn(Row(w, y + bh + gap, bh), "MEDIUM", "FFC24B", "F08A1C")) { _aiLevel = BotLevel.Medium; StartMode(Mode.VsAi); }
            if (Btn(Row(w, y + 2 * (bh + gap), bh), "HARD (MASTER AI)", "FF6B8B", "D61F4E")) { _aiLevel = BotLevel.Hard; StartMode(Mode.VsAi); }
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) GoBack();
        }

        private void DrawHud(float w, float h)
        {
            float top = h * 0.03f, bs = h * 0.06f;
            if (Btn(new Rect(w * 0.04f, top, bs * 1.6f, bs), "MENU", "5A4FA8", "3A2F80", 0.7f)) { SetPaused(true); return; }
            if (_mode == Mode.Level)
            {
                // boosters and the hint share one tray, so the play area stays clear
                if (Btn(new Rect(w * 0.96f - bs * 2.1f, top, bs * 2.1f, bs), _boosterTray ? "CLOSE" : "BOOST", "FFC24B", "F08A1C", 0.7f)) { _boosterTray = !_boosterTray; _hammerMode = false; }
            }
            else if (_hintsLeft > 0 && Btn(new Rect(w * 0.96f - bs * 2.1f, top, bs * 2.1f, bs), Loc.F("HINT {0}", _hintsLeft), "FFC24B", "F08A1C", 0.7f)) UseHint();

            DrawHudPolish(w, h);
            switch (_mode)
            {
                case Mode.Level:
                {
                    var d = _level.Definition;
                    // top bar: MENU left, BOOST right, the title and its badge centred between them
                    float tx = w * 0.04f + bs * 1.6f + w * 0.02f, tw = (w * 0.96f - bs * 2.1f - w * 0.02f) - tx;
                    Outlined(new Rect(tx, top, tw, bs * 0.58f), Loc.F("LEVEL {0}", d.Number), _body, Visuals.Hex("FFE27A"), 1.5f);
                    var badge = new Rect(tx + tw * 0.1f, top + bs * 0.64f, tw * 0.8f, bs * 0.34f);
                    if (d.IsHard) Pill(badge, Loc.L(d.Tier == LevelTier.SuperHard ? "SUPER HARD" : "HARD"), d.Tier == LevelTier.SuperHard ? "FF8FA3" : "FFE27A", d.Tier == LevelTier.SuperHard ? "C23B63" : "C9922B", Color.white);
                    else Outlined(badge, Loc.L(d.Chapter.Name).ToUpperInvariant(), _small, new Color(1, 1, 1, 0.7f), 1f);
                    // moves left: big and always visible, warm when few are left
                    float rowY = h * 0.1f, rowH = h * 0.085f;
                    int left = _level.MovesLeft;
                    Panel(new Rect(w * 0.04f, rowY, w * 0.24f, rowH), _cardStyle);
                    Outlined(new Rect(w * 0.04f, rowY, w * 0.24f, rowH * 0.62f), left.ToString(), _h2, left <= 3 ? Visuals.Hex("FF8FA3") : Color.white, 2.5f);
                    Outlined(new Rect(w * 0.04f, rowY + rowH * 0.6f, w * 0.24f, rowH * 0.34f), "MOVES", _small, new Color(1, 1, 1, 0.75f), 1f);
                    // goals: a label over a slim progress bar that turns green when done
                    float gx = w * 0.31f, gw = w * 0.65f, gh = rowH / d.Goals.Length;
                    for (int i = 0; i < d.Goals.Length; i++)
                    {
                        var chip = new Rect(gx, rowY + i * gh, gw, gh - h * 0.006f);
                        Panel(chip, _barBackStyle);
                        float f = Mathf.Clamp01((float)_level.Progress(i) / Mathf.Max(1, d.Goals[i].Target));
                        bool met = _level.GoalMet(i);
                        float pad = chip.height * 0.14f, pbH = Mathf.Max(h * 0.008f, chip.height * 0.18f);
                        var pb = new Rect(chip.x + pad * 1.5f, chip.yMax - pad - pbH, chip.width - pad * 3f, pbH);
                        Panel(pb, _barBackStyle);
                        if (f > 0.02f) Panel(new Rect(pb.x, pb.y, Mathf.Max(pb.height, pb.width * f), pb.height), _barFillStyle);
                        Outlined(new Rect(chip.x + pad, chip.y, chip.width - 2 * pad, chip.height - pbH - pad), (met ? "✓ " : "") + GoalChip(i), new GUIStyle(_small) { fontStyle = FontStyle.Bold, wordWrap = false, fontSize = Mathf.Max(8, Mathf.RoundToInt(_small.fontSize * 0.85f)) }, met ? Visuals.Hex("2EE59D") : Color.white, 1.2f);
                    }
                    // score bar: the three stars sit right on it, the score and the next star hang under it
                    float shown = _shownScore + _bonusShown;
                    var bar = new Rect(w * 0.08f, h * 0.245f, w * 0.84f, h * 0.018f);
                    Panel(bar, _barBackStyle);
                    float sf = Mathf.Clamp01(shown / Mathf.Max(1, d.ThreeStarScore));
                    if (sf > 0.04f) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * sf), bar.height), _barFillStyle);
                    // three stars: the first is for winning, the second and third sit at their score marks (kept apart so they never crowd)
                    float starW = h * 0.06f, minGap = h * 0.065f;
                    float x3 = bar.xMax, x2 = Mathf.Min(bar.x + bar.width * Mathf.Clamp01((float)d.TwoStarScore / Mathf.Max(1, d.ThreeStarScore)), x3 - minGap);
                    float x1 = Mathf.Min(bar.x + starW * 0.3f, x2 - minGap);
                    var starStyle = new GUIStyle(_h2) { fontSize = Mathf.RoundToInt(_h2.fontSize * 0.7f), font = null, fontStyle = FontStyle.Bold };
                    for (int si = 0; si < 3; si++)
                    {
                        float sx = si == 0 ? x1 : si == 1 ? x2 : x3;
                        bool got = si == 0 ? _level.AllGoalsMet : shown >= (si == 1 ? d.TwoStarScore : d.ThreeStarScore);
                        Outlined(new Rect(sx - starW / 2f, bar.y - h * 0.04f, starW, h * 0.04f), got ? "★" : "☆", starStyle, got ? Visuals.Gold : new Color(1, 1, 1, 0.9f), 2f);
                    }
                    Outlined(new Rect(bar.x, bar.yMax + h * 0.004f, bar.width * 0.3f, h * 0.03f), Mathf.RoundToInt(shown).ToString(), new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontStyle = FontStyle.Bold }, Color.white, 1.2f);
                    if (_boosterTray) DrawBoosterTray(w, h);
                    break;
                }
                case Mode.Versus:
                case Mode.VsAi:
                {
                    bool ai = _mode == Mode.VsAi;
                    int me = ai ? 0 : _versus.Turn;
                    string meName = ai ? Loc.L("YOU") : Loc.F("PLAYER {0}", me + 1), them = ai ? Loc.F("AI ({0})", Loc.L(_aiLevel.ToString())) : Loc.F("PLAYER {0}", 2 - me);
                    Outlined(new Rect(w * 0.06f, h * 0.10f, w * 0.5f, h * 0.05f), meName, _body, Visuals.Hex("FFE27A"), 1.5f);
                    Outlined(new Rect(w * 0.06f, h * 0.14f, w * 0.5f, h * 0.07f), _versus.Seats[me].Board.Score.ToString(), _h2, Color.white, 2.5f);
                    Outlined(new Rect(w * 0.55f, h * 0.025f + bs, w * 0.4f, h * 0.035f), $"{them}: {_versus.Seats[1 - me].Board.Score}{(_versus.Seats[1 - me].Out ? " " + Loc.L("(out)") : "")}", _small, Visuals.Hex("E6D9FF"), 1.2f);
                    if (_aiThinking && ai) Outlined(new Rect(0, h * 0.205f, w, h * 0.035f), Loc.L("AI is thinking") + new string('.', 1 + (int)(Time.time * 3) % 3), _small, Visuals.Hex("C9A7FF"), 1.2f);
                    if (!ai && Time.time < _turnBannerUntil)
                    {
                        Panel(new Rect(w * 0.1f, h * 0.38f, w * 0.8f, h * 0.14f), _cardStyle);
                        Outlined(new Rect(0, h * 0.38f, w, h * 0.14f), Loc.F("PLAYER {0}\nYOUR TURN", me + 1), _h2, Color.white, 2.5f);
                    }
                    break;
                }
                default:
                {
                    string label = _mode == Mode.Daily ? "DAILY" : _mode == Mode.Challenge ? ChallengeCode.Encode(_challengeSeed) : Loc.F("BEST {0}", Mathf.Max(_best, Score));
                    Outlined(new Rect(w * 0.27f, top, w * 0.46f, bs), label, _body, Visuals.Hex("FFE27A"), 1.5f);
                    if (_mode == Mode.Classic) Outlined(new Rect(w * 0.27f, top + bs * 0.8f, w * 0.46f, bs * 0.5f), "CLASSIC", _small, Visuals.Hex("7CF8FF"), 1.2f);
                    float punch = 1f + Mathf.Clamp01((Score - _shownScore) / 60f) * 0.25f;
                    var st = new GUIStyle(_h1) { fontSize = Mathf.RoundToInt(_h1.fontSize * punch) };
                    Outlined(new Rect(0, h * 0.095f, w, h * 0.1f), Mathf.RoundToInt(_shownScore).ToString(), st, Color.white, 3);
                    break;
                }
            }
        }

        private void DrawResult(float w, float h)
        {
            float t = Time.time - _resultTime;
            float rankExtra = _mode == Mode.Level && _rankResult != null ? h * 0.105f : 0f; // room for the rank bar on a level win
            Panel(new Rect(w * 0.06f, h * 0.14f, w * 0.88f, h * 0.62f + rankExtra), _cardStyle);
            float y = h * 0.17f;
            switch (_mode)
            {
                case Mode.Level:
                {
                    bool won = _level.State == LevelState.Won;
                    Outlined(new Rect(0, y, w, h * 0.08f), won ? "LEVEL COMPLETE!" : "OUT OF MOVES", _h2, won ? Visuals.Hex("FFE27A") : Visuals.Hex("FF8FA3"), 3);
                    if (!won)
                    {
                        string close = _level.NearMiss();
                        if (close != null) Outlined(new Rect(w * 0.1f, y + h * 0.09f, w * 0.8f, h * 0.06f), close, _body, Color.white, 1.4f);
                        int fails = PlayerPrefs.GetInt(FailKey(_level.Definition.Number), 0);
                        if (fails >= HelpingHandAfter) Outlined(new Rect(w * 0.1f, y + h * 0.15f, w * 0.8f, h * 0.05f), Loc.F("Helping hand: +{0} moves on your next try!", HelpingHandMoves), _small, Visuals.Hex("7CF8FF"), 1.2f);
                    }
                    if (won)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float appear = Mathf.Clamp01((t - 0.4f - i * 0.35f) * 4f);
                            bool earned = i < _level.Stars;
                            if (appear > 0 && earned && appear < 0.3f && !_starPlayed[i]) { _fx.Star(); _starPlayed[i] = true; }
                            var st = new GUIStyle(_h1) { fontSize = Mathf.RoundToInt(_h1.fontSize * (0.6f + 0.6f * EaseOutBack(appear))) };
                            Outlined(new Rect(w * (0.2f + i * 0.2f) - w * 0.1f, y + h * 0.08f, w * 0.4f, h * 0.12f), earned ? "★" : "☆", st, earned ? Visuals.Gold : new Color(1, 1, 1, 0.35f), 3);
                        }
                    }
                    if (won) DrawLevelCelebration(w, h, y, t);
                    // the final score counts up, it never jumps
                    int final = won ? _level.FinalScore : _level.Seat.Board.Score;
                    int counted = Mathf.RoundToInt(final * Mathf.Clamp01((t - 0.2f) / 0.9f));
                    string scoreLine = won && _level.Bonus > 0 ? Loc.F("SCORE {0}  (BONUS +{1})", counted, _level.Bonus) : Loc.F("SCORE {0}", counted);
                    if (won && _coinsEarned > 0) scoreLine += $"   +{_coinsEarned} ●";
                    Outlined(new Rect(0, y + h * 0.21f, w, h * 0.06f), scoreLine, _body, Color.white, 1.5f);
                    if (_pieceMsg != "") Outlined(new Rect(0, y + h * 0.262f, w, h * 0.036f), _pieceMsg, new GUIStyle(_small) { wordWrap = false, fontStyle = FontStyle.Bold }, Visuals.Hex("7CF8FF"), 1.2f);
                    if (rankExtra > 0f) DrawRankBlock(w, h, t, y + h * 0.3f);
                    float by = y + h * 0.3f + rankExtra, bh = h * 0.07f, gap = h * 0.015f;
                    if (won && _level.Definition.Number < LevelLibrary.Count)
                    {
                        // Winning rolls straight on to the next level after a short celebration; any button below takes over.
                        float left = AutoNextDelay - t;
                        var nb = Row(w, by, bh, 0.14f);
                        if (Btn(nb, "NEXT LEVEL", "4BE38A", "1FA45B") || left <= 0f) { ResetStars(); StartMode(Mode.Level, level: _level.Definition.Number + 1); return; }
                        GUI.DrawTexture(new Rect(nb.x, nb.yMax - bh * 0.07f, nb.width * Mathf.Clamp01(left / (AutoNextDelay - 1f)), bh * 0.07f), Texture2D.whiteTexture);
                    }
                    if (Btn(Row(w, by + bh + gap, bh, 0.14f), "RETRY", "FFC24B", "F08A1C")) { ResetStars(); StartMode(Mode.Level, level: _level.Definition.Number); }
                    LevelResultNav(w, by + 2 * (bh + gap), bh);
                    return;
                }
                case Mode.Versus:
                case Mode.VsAi:
                {
                    int win = _versus.Winner;
                    bool ai = _mode == Mode.VsAi;
                    string title = win < 0 ? "IT'S A TIE!" : ai ? (win == 0 ? "YOU WIN!" : "AI WINS") : Loc.F("PLAYER {0} WINS!", win + 1);
                    Outlined(new Rect(0, y, w, h * 0.08f), title, _h2, win == 0 || !ai ? Visuals.Hex("FFE27A") : Visuals.Hex("FF8FA3"), 3);
                    Outlined(new Rect(0, y + h * 0.1f, w, h * 0.1f), $"{(ai ? Loc.L("YOU") : "P1")}  {_versus.Seats[0].Board.Score}\n{(ai ? "AI" : "P2")}  {_versus.Seats[1].Board.Score}", _h2, Color.white, 2);
                    if (Btn(Row(w, y + h * 0.3f, h * 0.07f, 0.14f), "REMATCH", "4BE38A", "1FA45B")) StartMode(_mode);
                    break;
                }
                case Mode.Challenge:
                {
                    string code = ChallengeCode.Encode(_challengeSeed);
                    Outlined(new Rect(0, y, w, h * 0.08f), Loc.F("SCORE {0}", _single.Board.Score), _h2, Color.white, 3);
                    Outlined(new Rect(0, y + h * 0.08f, w, h * 0.08f), code, _h1, Visuals.Hex("7CF8FF"), 3);
                    Outlined(new Rect(w * 0.1f, y + h * 0.16f, w * 0.8f, h * 0.1f), "Send this code to a friend. They get exactly your pieces. Highest score wins!", _small, Color.white, 1.2f);
                    if (Btn(Row(w, y + h * 0.27f, h * 0.07f, 0.14f), "COPY CODE", "4FD8FF", "1C8FE0")) GUIUtility.systemCopyBuffer = Loc.F("Beat my {0} in Block Drop! Code: {1}", _single.Board.Score, code);
                    if (Btn(Row(w, y + h * 0.355f, h * 0.07f, 0.14f), "PLAY AGAIN", "4BE38A", "1FA45B")) StartMode(Mode.Challenge, _challengeSeed);
                    break;
                }
                default:
                {
                    Outlined(new Rect(0, y, w, h * 0.08f), _newBest ? "NEW BEST!" : "GAME OVER", _h2, _newBest ? Visuals.Hex("FFE27A") : Visuals.Hex("FF8FA3"), 3);
                    var st = new GUIStyle(_h1) { fontSize = Mathf.RoundToInt(_h1.fontSize * (1f + 0.3f * EaseOutBack(Mathf.Clamp01(t * 2f)) - 0.3f)) };
                    Outlined(new Rect(0, y + h * 0.1f, w, h * 0.12f), Score.ToString(), st, Color.white, 3);
                    Outlined(new Rect(0, y + h * 0.21f, w, h * 0.05f), Loc.F("BEST {0}", _best), _body, Visuals.Gold, 1.5f);
                    if (!_newBest && _best > 0 && Score >= _best * 0.8f)
                        Outlined(new Rect(w * 0.05f, y + h * 0.25f, w * 0.9f, h * 0.06f), Loc.F("So close! Only {0} points from your best.", _best - Score), _small, Visuals.Hex("7CF8FF"), 1.2f);
                    else if (NewLook.On && !_newBest && NearMiss.Find(MainSeat.Board).Message() is string miss)
                        Outlined(new Rect(0, y + h * 0.255f, w, h * 0.04f), miss, _small, Visuals.Hex("7CF8FF"), 1.2f);
                    if (Btn(Row(w, y + h * 0.3f, h * 0.07f, 0.14f), "PLAY AGAIN", "4BE38A", "1FA45B")) StartMode(_mode);
                    break;
                }
            }
            DrawRankBlock(w, h, t);
            if (HomeBtn(Row(w, h * 0.66f, h * 0.07f, 0.14f))) ShowMenu();
        }

        private readonly bool[] _starPlayed = new bool[3];
        private void ResetStars() { for (int i = 0; i < 3; i++) _starPlayed[i] = false; }

        private int _logoTaps;
        private float _logoTapAt;

        private void LogoTapped()
        {
            _logoTaps = Time.unscaledTime - _logoTapAt < 0.6f ? _logoTaps + 1 : 1;
            _logoTapAt = Time.unscaledTime;
            if (_logoTaps < 5) return;
            _logoTaps = 0;
            NewLook.Toggle();
            NewLook.Apply(_world, _cam);
            Toast(NewLook.On ? "NEW LOOK ON" : "NEW LOOK OFF", Visuals.Hex("7CF8FF"));
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
        }

        private void PlayCode()
        {
            if (ChallengeCode.TryDecode(_codeInput, out int seed)) StartMode(Mode.Challenge, seed);
            else _codeError = Loc.F("Codes have {0} letters/digits, e.g. K7Q2MX", ChallengeCode.Length);
        }

        /// <summary>Text entry that works with the phone keyboard: taps on the game are blocked while the
        /// keyboard is up, so the keyboard's Done key submits. Falls back to a normal field in the editor.</summary>
        private string KeyboardField(Rect r, string value, string target, int limit, string placeholder)
        {
            if (!TouchScreenKeyboard.isSupported) return GUI.TextField(r, value, limit, _field);
            var st = new GUIStyle(_field);
            if (value == "") st.normal.textColor = st.hover.textColor = st.active.textColor = new Color(0.16f, 0.05f, 0.38f, 0.35f);
            if (GUI.Button(r, value == "" ? Loc.L(placeholder) : value, st))
            {
                _kb = TouchScreenKeyboard.Open(value, TouchScreenKeyboardType.ASCIICapable, false, false, false, false, placeholder, limit);
                _kbTarget = target;
            }
            return value;
        }

        private void PollKeyboard()
        {
            if (_kb == null) return;
            string t = _kb.text ?? "";
            if (_kbTarget == "code") _codeInput = t.ToUpperInvariant(); else _nameInput = t;
            var status = _kb.status;
            if (status == TouchScreenKeyboard.Status.Visible) return;
            string target = _kbTarget;
            _kb = null;
            if (status != TouchScreenKeyboard.Status.Done) return;
            if (target == "code" && _screen == Screen.Challenge) PlayCode();
            else if (target == "name" && _screen == Screen.Profile) SaveName();
        }

        private void DrawChallenge(float w, float h)
        {
            Outlined(new Rect(0, h * 0.08f, w, h * 0.08f), "CHALLENGE", _h2, Visuals.Hex("7CF8FF"), 3);
            Outlined(new Rect(w * 0.08f, h * 0.165f, w * 0.84f, h * 0.125f), "Start a challenge and share its code, or enter a friend's code to play their exact pieces.", _small, Color.white, 1.5f);
            if (Btn(Row(w, h * 0.30f, h * 0.08f), "NEW CHALLENGE", "4BE38A", "1FA45B")) StartMode(Mode.Challenge, ChallengeCode.NewSeed(_rng));
            Outlined(new Rect(0, h * 0.415f, w, h * 0.04f), "FRIEND'S CODE", _body, Visuals.Hex("FFE27A"), 1.5f);
            _codeInput = KeyboardField(Row(w, h * 0.465f, h * 0.085f, 0.16f), _codeInput, "code", ChallengeCode.Length, "K7Q2MX").ToUpperInvariant();
            if (Btn(Row(w, h * 0.57f, h * 0.075f), "PLAY CODE", "6E8BFF", "3A4FE0")) PlayCode();
            if (_codeError != "") Outlined(new Rect(w * 0.05f, h * 0.655f, w * 0.9f, h * 0.08f), _codeError, _small, Visuals.Hex("FF8FA3"), 1.2f);
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) GoBack();
        }

        private void DrawProfile(float w, float h)
        {
            Outlined(new Rect(0, h * 0.1f, w, h * 0.08f), "YOUR NAME", _h2, Visuals.Gold, 3);
            Outlined(new Rect(w * 0.08f, h * 0.185f, w * 0.84f, h * 0.12f), "Shown on leaderboards. Letters, numbers and _ only (3–20).", _small, Color.white, 1.5f);
            _nameInput = KeyboardField(Row(w, h * 0.32f, h * 0.085f, 0.1f), _nameInput, "name", 20, "Your name");
            if (Btn(Row(w, h * 0.43f, h * 0.075f), "SAVE", "4BE38A", "1FA45B")) SaveName();
            if (_nameMsg != "") Outlined(new Rect(w * 0.05f, h * 0.52f, w * 0.9f, h * 0.1f), _nameMsg, _small, Visuals.Hex("FFE27A"), 1.2f);
            if (Btn(Row(w, h * 0.66f, h * 0.07f), Loc.F("LANGUAGE: {0}", Loc.Names[(int)Loc.Current]), "6E8BFF", "3A4FE0", 0.8f))
            {
                Loc.Current = (Lang)(((int)Loc.Current + 1) % Loc.Names.Length);
                PlayerPrefs.SetString("lang", Loc.Code(Loc.Current));
                PlayerPrefs.Save();
            }
            // "Continue with Google" only exists in builds that have Google Play Games set up (AccountLink.Available), so everywhere else this row is simply absent
            if (AccountLink.Available)
            {
                var gr = Row(w, h * 0.755f, h * 0.07f);
                if (AccountLink.Linked) Pill(gr, Loc.L("GOOGLE CONNECTED ✓"), "FFFFFF", "C9F7E4", Visuals.Hex("0E6B48"));
                else if (Btn(gr, _googleBusy ? Loc.L("CONNECTING…") : Loc.L("CONTINUE WITH GOOGLE"), "4C8DF6", "2B5FD0", 0.7f) && !_googleBusy) ContinueWithGoogle();
                if (_googleMsg != "") Outlined(new Rect(w * 0.05f, h * 0.83f, w * 0.9f, h * 0.03f), _googleMsg, new GUIStyle(_small) { wordWrap = false }, Visuals.Hex("FFE27A"), 1.2f);
            }
            if (Btn(Row(w, h * 0.89f, h * 0.065f, 0.25f), "BACK", "5A4FA8", "3A2F80")) GoBack();
        }

        private bool _googleBusy;
        private string _googleMsg = "";

        private async void ContinueWithGoogle()
        {
            _googleBusy = true; _googleMsg = "";
            string err = await AccountLink.ContinueAsync();
            _googleBusy = false;
            _googleMsg = err ?? Loc.L("Connected! Your progress is saved to your Google account.");
        }

        private async void SaveName()
        {
            string n = _nameInput.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(n, "^[A-Za-z0-9_]{3,20}$")) { _nameMsg = "Use 3–20 letters, numbers or _ (no spaces)."; return; }
            if (!NameFilter.IsAllowed(n)) { _nameMsg = Loc.L("Please choose a different name."); return; }
            _localName = n; PlayerPrefs.SetString("name", n); PlayerPrefs.Save();
            _nameMsg = "Saving…";
            string err = await OnlineService.SetNameAsync(n);
            _nameMsg = err ?? Loc.F("Saved! You are {0}", OnlineService.PlayerName);
        }

        private readonly Dictionary<string, GUIStyle> _rankStyles = new Dictionary<string, GUIStyle>();
        private static readonly string[] EmblemColors = { "FF5D8F", "FFB23E", "3DDC97", "4CC9F0", "9B7BFF", "FF7A59" };

        private GUIStyle RankStyle(string top, string bottom)
        {
            string key = top + bottom;
            if (!_rankStyles.TryGetValue(key, out var st))
                _rankStyles[key] = st = Sliced(Visuals.RoundedRect(64, 64, 20, Visuals.Hex(top), Visuals.Hex(bottom), 0.1f), 22);
            return st;
        }

        private void DrawRankRow(Rect row, OnlineService.Row r)
        {
            int medal = Ranking.Medal(r.Rank);
            string top = medal == 1 ? "FFD54A" : medal == 2 ? "D5DCF2" : medal == 3 ? "E09B63" : "3B2F8F";
            string bottom = medal == 1 ? "D99A00" : medal == 2 ? "9AA6CF" : medal == 3 ? "B06A35" : "2A2070";
            Panel(row, RankStyle(top, bottom));
            if (r.IsMe) Panel(new Rect(row.x - 3, row.y - 3, row.width + 6, row.height + 6), RankStyle("7CF8FF", "4CC9F0"));
            if (r.IsMe) Panel(row, RankStyle("2F4FB0", "233C8A"));

            Color ink = medal > 0 && !r.IsMe ? Visuals.Hex("3A1B00") : Color.white;
            float pad = row.height * 0.12f, d = row.height - pad * 2;
            var rankStyle = new GUIStyle(_h2) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            Outlined(new Rect(row.x + pad, row.y, row.height * 0.8f, row.height), r.Rank.ToString(), rankStyle, ink, 1.2f);

            var emb = new Rect(row.x + pad + row.height * 0.85f, row.y + pad, d, d);
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color; GUI.color = Visuals.Hex(EmblemColors[Ranking.Emblem(r.Name)]);
                GUI.DrawTexture(emb, Visuals.Circle.texture); GUI.color = old;
            }
            var initial = new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false };
            Outlined(emb, Ranking.Initial(r.Name), initial, Color.white, 1.2f);

            var pill = new Rect(row.xMax - row.width * 0.3f - pad, row.y + row.height * 0.17f, row.width * 0.3f, row.height * 0.66f);
            Panel(pill, RankStyle("FFFFFF", "E8E2FF"));
            var score = new GUIStyle(_body) { fontStyle = FontStyle.Bold, wordWrap = false };
            Outlined(pill, Ranking.FormatScore(r.Score), score, Visuals.Hex("3A1B7A"), 0.6f);

            float nx = emb.xMax + pad * 1.2f, nw = pill.x - nx - pad;
            Ranking.SplitName(r.Name, out var name, out var tag);
            string sub = r.IsMe ? (tag.Length > 0 ? tag + " · " : "") + Loc.L("(you)") : tag;
            var left = new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = false };
            var small = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            Outlined(new Rect(nx, row.y + (sub.Length > 0 ? 0 : row.height * 0.1f), nw, row.height * (sub.Length > 0 ? 0.62f : 0.8f)), name, left, ink, 1f);
            if (sub.Length > 0) Outlined(new Rect(nx, row.y + row.height * 0.52f, nw, row.height * 0.4f), sub, small, ink, 0.8f);
        }

        private void DrawLeaderboard(float w, float h)
        {
            Outlined(new Rect(0, h * 0.05f, w, h * 0.08f), "RANKINGS", _h2, Visuals.Gold, 3);
            float tabW = (w * 0.84f - 2 * w * 0.015f) / 3f;
            var tabs = new[] { ("LEVELS", OnlineService.LevelsBoard), ("CLASSIC", OnlineService.ClassicBoard), ("DAILY", OnlineService.DailyBoard) };
            for (int k = 0; k < tabs.Length; k++)
            {
                bool on = _boardTab == tabs[k].Item2;
                if (Btn(new Rect(w * 0.08f + k * (tabW + w * 0.015f), h * 0.14f, tabW, h * 0.06f), tabs[k].Item1, on ? "FFD54A" : "5A4FA8", on ? "D99A00" : "3A2F80", 0.7f)) { _boardTab = tabs[k].Item2; OnlineService.Refresh(_boardTab); if (_boardTab == OnlineService.LevelsBoard) OnlineService.SubmitLevels(_progress.TotalStars); }
            }
            var card = new Rect(w * 0.04f, h * 0.22f, w * 0.92f, h * 0.62f);
            Panel(card, _cardStyle);
            float y = card.y + h * 0.015f, rh = h * 0.058f;
            if (OnlineService.Errors.TryGetValue(_boardTab, out var err)) Outlined(new Rect(card.x, y, card.width, rh * 3), err, _body, Color.white, 1.2f);
            else if (!OnlineService.Ready) Outlined(new Rect(card.x, y, card.width, rh * 2), OnlineService.Status, _body, Color.white, 1.2f);
            else if (!OnlineService.Top.TryGetValue(_boardTab, out var rows)) Outlined(new Rect(card.x, y, card.width, rh), "Loading…", _body, Color.white, 1.2f);
            else if (rows.Count == 0) Outlined(new Rect(card.x, y, card.width, rh * 2), "No scores yet.\nBe the first!", _body, Color.white, 1.2f);
            else
            {
                rh = Mathf.Min(rh, (card.height - h * 0.03f) / rows.Count);
                float gap = rh * 0.08f;
                foreach (var r in rows)
                {
                    DrawRankRow(new Rect(card.x + card.width * 0.03f, y, card.width * 0.94f, rh - gap), r);
                    y += rh;
                }
            }
            if (Btn(Row(w, h * 0.87f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) GoBack();
        }

        private void DrawFloaters(float w)
        {
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                float age = Time.time - f.Born;
                if (age > f.Life) { _floaters.RemoveAt(i); continue; }
                var sp = ToGui(_cam.WorldToScreenPoint(f.World + new Vector3(0, age * 1.2f, 0)));
                float k = EaseOutBack(Mathf.Clamp01(age * 5f));
                var st = new GUIStyle(_h2) { fontSize = Mathf.Max(8, Mathf.RoundToInt(_h2.fontSize * f.Size * k)) };
                var c = f.Color; c.a = Mathf.Clamp01((f.Life - age) * 3f);
                Outlined(new Rect(0, sp.y - st.fontSize, w, st.fontSize * 2), f.Text, st, c, 3);
            }
        }
    }
}
