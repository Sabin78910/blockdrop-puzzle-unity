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
    public sealed class GameController : MonoBehaviour
    {
        private enum Screen { Menu, LevelSelect, AiSelect, Playing, Result, Challenge, Leaderboard, Profile, Missions, Themes, Trophies }
        private enum Mode { Classic, Daily, Level, Versus, VsAi, Challenge }

        private const int Size = 8;
        private static readonly Vector3 BoardCenter = new Vector3(0, 0.6f, 0);
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
        private float _flashUntil, _playSeconds;
        private bool _breakShown, _breakOpen;
        private int _tutorial; // 0 = drag, 1 = clear a line, 2 = combos, 3 = done
        private float _tutorialStepAt;
        private static string Today => PlayerMeta.Day(DateTime.Now);
        private bool _bestCelebrated, _nameSynced, _paused;
        private TouchScreenKeyboard _kb;
        private string _kbTarget; // "code" or "name"
        private float _nextOnlineTry;
        private readonly List<(string text, Color color, float until)> _toasts = new List<(string, Color, float)>();
        private bool Career => _mode != Mode.Versus; // pass-and-play moves don't count toward one player's profile
        private int _best, _challengeSeed, _hintsLeft;
        private bool _aiThinking, _newBest;
        private float _shownScore, _resultTime, _turnBannerUntil;
        private string _codeInput = "", _codeError = "", _boardTab = OnlineService.ClassicBoard;
        private string _nameInput = "", _nameMsg = "", _localName = "";
        private readonly System.Random _rng = new System.Random();

        // ---------- scene objects ----------
        private Camera _cam;
        private Feedback _fx;
        private Transform _world;
        private SpriteRenderer _background;
        private GameObject _boardRoot, _miniRoot, _menuDeco;
        private SpriteRenderer[,] _tiles, _gloss, _ghosts, _miniTiles;
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
            _meta = PlayerMeta.Parse(PlayerPrefs.GetString("meta", ""));
            _welcomeReward = _meta.CheckIn(Today);
            Celebrate(_meta.Record(Stat.BestStreak, _meta.Streak));
            _tutorial = PlayerPrefs.GetInt("tutorial", 0);
            SaveMeta();
            _world = new GameObject("World").transform;
            BuildScene();
            FitCamera();
            ApplyTheme();
            ShowMenu();
            if (_welcomeReward > 0) { _fx.Win(); Confetti(40); }
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
            var panel = NewSprite("Panel", Visuals.Panel, _boardRoot.transform, BoardCenter, 1, -20, Visuals.BoardPanel);
            panel.drawMode = SpriteDrawMode.Sliced; panel.size = new Vector2(8.7f, 8.7f);
            var rim = NewSprite("Rim", Visuals.Panel, _boardRoot.transform, BoardCenter + new Vector3(0, -0.12f, 0), 1, -21, new Color(0, 0, 0, 0.45f));
            rim.drawMode = SpriteDrawMode.Sliced; rim.size = new Vector2(8.9f, 8.9f);

            _tiles = new SpriteRenderer[Size, Size];
            _gloss = new SpriteRenderer[Size, Size];
            _ghosts = new SpriteRenderer[Size, Size];
            _pop = new float[Size, Size];
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    var p = CellPos(x, y);
                    NewSprite("slot", Visuals.Slot, _boardRoot.transform, p, 0.94f, -5, Visuals.EmptySlot);
                    _tiles[x, y] = NewSprite("tile", Visuals.Tile, _boardRoot.transform, p, 0.94f, 0, Color.white);
                    _gloss[x, y] = NewSprite("gloss", Visuals.Gloss, _tiles[x, y].transform, Vector3.zero, 1f, 1, Color.white);
                    _ghosts[x, y] = NewSprite("ghost", Visuals.Tile, _boardRoot.transform, p, 0.94f, 2, Color.clear);
                }

            _miniRoot = new GameObject("MiniBoard");
            _miniRoot.transform.SetParent(_world, false);
            var mp = NewSprite("MiniPanel", Visuals.Panel, _miniRoot.transform, MiniCenter, 1, -20, Visuals.BoardPanel);
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
        private float _camY;

        private void FitCamera()
        {
            _fitW = UnityEngine.Screen.width; _fitH = UnityEngine.Screen.height;
            float aspect = (float)_fitW / Mathf.Max(1, _fitH);
            const float needW = 9.6f, needTop = 9.6f, needBottom = -8.2f;
            float halfH = Mathf.Max((needTop - needBottom) / 2f, needW / 2f / aspect);
            _cam.orthographicSize = halfH;
            _camY = (needTop + needBottom) / 2f;
            _cam.transform.position = new Vector3(0, _camY, -10);
            _background.transform.position = new Vector3(0, _cam.transform.position.y, 10);
            var b = Visuals.Background.bounds.size;
            _background.transform.localScale = new Vector3(halfH * 2f * aspect / b.x * 1.05f, halfH * 2f / b.y * 1.05f, 1);
        }

        private void SaveMeta() { PlayerPrefs.SetString("meta", _meta.Serialize()); PlayerPrefs.Save(); }

        // Phones can kill a backgrounded app without warning, so persist whenever we lose focus.
        private void OnApplicationPause(bool paused) { if (paused && _meta != null) SaveMeta(); }
        private void OnApplicationQuit() { if (_meta != null) SaveMeta(); }

        private void ApplyTheme()
        {
            var t = PlayerMeta.Themes[_meta.ThemeIndex];
            for (int i = 0; i < 5; i++) Visuals.BlockColors[i] = Visuals.Hex(t.Blocks[i]);
            _background.color = Visuals.Hex(t.BackgroundTint);
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
            float start = _toasts.Count > 0 ? Mathf.Max(Time.unscaledTime, _toasts[_toasts.Count - 1].until - 0.6f) : Time.unscaledTime;
            _toasts.Add((text, color, start + 2.4f));
        }

        private void Celebrate(List<Achievement> unlocked)
        {
            foreach (var a in unlocked) { Toast($"★ {a.Name.ToUpperInvariant()}   +{a.Reward} ●", Visuals.Hex("7CF8FF")); _fx.Star(); }
            if (unlocked.Count > 0) SaveMeta();
        }

        private void GainXp(int amount)
        {
            if (!Career) return;
            if (_meta.AddXp(amount) > 0)
            {
                Toast($"LEVEL {_meta.Level}!   +{PlayerMeta.LevelReward(_meta.Level)} ●", Visuals.Gold);
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

        private bool InputAllowed => _screen == Screen.Playing && !_aiThinking && !(_mode == Mode.VsAi && _versus.Turn != 0) && Time.time > _turnBannerUntil;

        private void StartMode(Mode mode, int seed = 0, int level = 0)
        {
            _mode = mode;
            switch (mode)
            {
                case Mode.Level: _level = new LevelSession(level); break;
                case Mode.Versus:
                case Mode.VsAi: _versus = new VersusMatch(Environment.TickCount); break;
                case Mode.Daily: _single = new Seat(PieceGenerator.DailySeed(DateTime.UtcNow)); break;
                case Mode.Challenge: _challengeSeed = seed; _single = new Seat(seed); break;
                default: _single = new Seat(Environment.TickCount); break;
            }
            _hintsLeft = Competitive ? 0 : 3;
            _hint = null; _newBest = false; _aiThinking = false; _bestCelebrated = false;
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

        private void ShowMenu(Screen screen = Screen.Menu)
        {
            SetPaused(false);
            _screen = screen;
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

        private void Finish()
        {
            bool good = true;
            if (_mode == Mode.Level)
            {
                good = _level.State == LevelState.Won;
                if (good) { _progress.Record(_level.Definition.Number, _level.Stars); PlayerPrefs.SetString("progress", _progress.Serialize()); }
            }
            if (_mode == Mode.Classic || _mode == Mode.Daily)
            {
                if (Score > _best) { _best = Score; _newBest = true; PlayerPrefs.SetInt("best", _best); }
                OnlineService.SubmitScore(_mode == Mode.Classic ? OnlineService.ClassicBoard : OnlineService.DailyBoard, Score);
                good = _newBest;
            }
            if (_mode == Mode.VsAi) good = _versus.Winner == 0;
            Track(MissionKind.PlayGames, 1);
            if (_mode == Mode.Level && _level.State == LevelState.Won) Track(MissionKind.EarnStars, _level.Stars);
            if (Career)
            {
                Celebrate(_meta.Record(Stat.GamesPlayed, 1));
                Celebrate(_meta.Record(Stat.BestGameScore, Score));
                if (_mode == Mode.Level) Celebrate(_meta.Record(Stat.LevelStars, _progress.TotalStars));
                if (_mode == Mode.VsAi && _aiLevel == BotLevel.Hard && _versus.Winner == 0) Celebrate(_meta.Record(Stat.HardAiWins, 1));
            }
            SaveMeta();
            PlayerPrefs.Save();
            if (good) { _fx.Win(); Confetti(60); } else _fx.GameOver();
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
                    _gloss[x, y].enabled = filled && b[x, y] != LevelLibrary.ObstacleColor;
                }
            var mini = MiniSeat;
            if (mini != null)
                for (int x = 0; x < Size; x++)
                    for (int y = 0; y < Size; y++)
                        _miniTiles[x, y].color = mini.Board.IsFilled(x, y) ? Visuals.BlockColors[mini.Board[x, y]] : Visuals.EmptySlot;
        }

        private static Vector3 HandSlot(int i) => new Vector3((i - 1) * HandSpacing, HandY, 0);

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
                }
                root.transform.position = HandSlot(i);
                root.transform.localScale = Vector3.one * HandScale;
                _handViews[i] = root;
            }
        }

        private void SetHandOrder(GameObject root, int baseOrder)
        {
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder = sr.sprite == Visuals.Gloss ? baseOrder + 1 : baseOrder;
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
            if (UnityEngine.Screen.width != _fitW || UnityEngine.Screen.height != _fitH) FitCamera();
            PollKeyboard();
            if (Input.GetKeyDown(KeyCode.Escape) && _screen != Screen.Menu)
            {
                if (_screen == Screen.Playing) SetPaused(!_paused); // never throw a run away on an accidental back press
                else ShowMenu();
                return;
            }
            // Keep trying to come online (slow or late network at startup); then push a name chosen offline.
            if (!OnlineService.Ready && Time.unscaledTime >= _nextOnlineTry) { _nextOnlineTry = Time.unscaledTime + 5f; _ = OnlineService.EnsureReady(); }
            else if (OnlineService.Ready && !_nameSynced)
            {
                _nameSynced = true;
                if (_localName != "" && OnlineService.PlayerName.Split('#')[0] != _localName) _ = OnlineService.SetNameAsync(_localName);
            }

            AnimateDecor();
            AnimateParticles();
            AnimateShake();
            if (_screen != Screen.Playing || _paused) return;
            _playSeconds += Time.unscaledDeltaTime;
            if (!_breakShown && _playSeconds > 40 * 60) { _breakShown = true; _breakOpen = true; }
            if (_tutorial == 2 && Time.time - _tutorialStepAt > 3.5f) { _tutorial = 3; PlayerPrefs.SetInt("tutorial", 3); }
            if (_breakOpen) return;

            _shownScore = Mathf.MoveTowards(_shownScore, Score, Mathf.Max(1f, Mathf.Abs(Score - _shownScore)) * Time.deltaTime * 6f);
            AnimatePops();
            UpdateGhosts();
            if (!InputAllowed) return;

            var hand = MainSeat.Dealer.Hand;
            var wp = PointerWorld();
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

            int before = board.Score;
            bool ok = _mode switch
            {
                Mode.Level => _level.TryPlace(handIndex, ox, oy),
                Mode.Versus or Mode.VsAi => _versus.TryPlace(handIndex, ox, oy),
                _ => _single.TryPlace(handIndex, ox, oy),
            };
            if (!ok) { RebuildHand(); return; }

            int gained = board.Score - before;
            int lines = rows.Count + cols.Count;
            foreach (var (cx, cy) in piece.Cells) _pop[ox + cx, oy + cy] = 1f;
            _fx.Place();
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
            if (_tutorial == 0) { _tutorial = 1; _tutorialStepAt = Time.time; }

            if (lines > 0)
            {
                _fx.Clear(lines, board.Combo);
                Track(MissionKind.ClearLines, lines);
                Track(MissionKind.ComboReach, board.Combo);
                if (Career) { Celebrate(_meta.Record(Stat.LinesCleared, lines)); Celebrate(_meta.Record(Stat.BestCombo, board.Combo)); }
                if (_tutorial == 1) { _tutorial = 2; _tutorialStepAt = Time.time; }
                if (lines >= 3) StartCoroutine(MegaClear());
                foreach (var (pos, col) in burst) Shatter(pos, col);
                _shake = 0.12f + 0.08f * lines;
                var mid = BoardCenter + new Vector3(0, 0.6f, 0);
                AddFloater("+" + gained, mid + new Vector3(0, -0.9f, 0), Visuals.Gold, 1.0f);
                string praise = lines >= 5 ? "UNBELIEVABLE!" : lines == 4 ? "AMAZING!" : lines == 3 ? "EXCELLENT!" : lines == 2 ? "GREAT!" : board.Combo >= 2 ? "NICE!" : null;
                if (praise != null) AddFloater(praise, mid + new Vector3(0, 0.5f, 0), Color.white, 1.6f);
                if (board.Combo >= 2) AddFloater($"COMBO x{board.Combo}", mid + new Vector3(0, 1.8f, 0), Visuals.Hex("FF9BD2"), 1.1f);
                if (IsEmpty(board)) { AddFloater("PERFECT CLEAR!", mid + new Vector3(0, -2.2f, 0), Visuals.Hex("7CF8FF"), 1.4f); Confetti(40); }
            }
            else if (gained > 0) AddFloater("+" + gained, CellPos(ox, oy) + new Vector3(0.5f, 0.6f, 0), new Color(1, 1, 1, 0.9f), 0.7f);

            AfterMove();
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
            if (IsFinished) { StartCoroutine(FinishSoon()); return; }
            if (_mode == Mode.Versus) { _turnBannerUntil = Time.time + 1.1f; Array.Clear(_pop, 0, _pop.Length); }
            RefreshBoard(); RebuildHand();
            if (_mode == Mode.VsAi && _versus.Turn == 1) StartCoroutine(AiTurns());
        }

        private IEnumerator FinishSoon()
        {
            RefreshBoard(); RebuildHand();
            _aiThinking = true;
            yield return new WaitForSeconds(0.9f);
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
                if (lines > 0) AddFloater(lines > 1 ? $"AI x{lines}!" : "AI +line", MiniCenter + new Vector3(0, -1.6f, 0), Visuals.Hex("C9A7FF"), 0.8f);
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
            if (_shake <= 0) { _cam.transform.position = new Vector3(0, _camY, -10); return; }
            _shake = Mathf.Max(0, _shake - Time.deltaTime);
            var o = Random.insideUnitCircle * _shake * 0.9f;
            _cam.transform.position = new Vector3(o.x, _camY + o.y, -10);
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
            _floaters.Add(new Floater { Text = text, World = world, Born = Time.time, Life = 1.1f, Size = size, Color = color });
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
            _field = new GUIStyle(GUI.skin.textField) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _field.normal.background = _field.focused.background = Visuals.RoundedRect(96, 96, 28, Visuals.Hex("F4F1FF"), Visuals.Hex("D9D2FF"), 0f);
            _field.normal.textColor = _field.focused.textColor = Visuals.Hex("2A0E61");
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
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture); GUI.color = old;
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

        private static void Outlined(Rect r, string text, GUIStyle st, Color color, float thickness = 2f)
        {
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

        private bool Btn(Rect r, string text, string top, string bottom, float scale = 1f) => GUI.Button(r, text, ButtonStyle(top, bottom, scale));

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

        private void OnGUI()
        {
            EnsureStyles();
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            switch (_screen)
            {
                case Screen.Menu: GUI.enabled = _welcomeReward == 0; DrawMenu(w, h); break;
                case Screen.LevelSelect: DrawLevelSelect(w, h); break;
                case Screen.AiSelect: DrawAiSelect(w, h); break;
                case Screen.Playing: GUI.enabled = !_breakOpen && !_paused; DrawHud(w, h); GUI.enabled = true; break;
                case Screen.Result: DrawResult(w, h); break;
                case Screen.Challenge: DrawChallenge(w, h); break;
                case Screen.Leaderboard: DrawLeaderboard(w, h); break;
                case Screen.Profile: DrawProfile(w, h); break;
                case Screen.Missions: DrawMissions(w, h); break;
                case Screen.Themes: DrawThemes(w, h); break;
                case Screen.Trophies: DrawTrophies(w, h); break;
            }
            GUI.enabled = true;
            DrawFloaters(h);
            DrawToasts(w, h);
            if (_screen == Screen.Playing) { DrawStreakMeter(w, h); DrawTutorial(w, h); DrawBreak(w, h); DrawPause(w, h); }
            if (Time.unscaledTime < _flashUntil)
            {
                var old = GUI.color; GUI.color = new Color(1, 1, 1, 0.55f * (_flashUntil - Time.unscaledTime) / 0.14f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture); GUI.color = old;
            }
        }

        private void DrawStreakMeter(float w, float h)
        {
            if (_mode == Mode.Versus || _mode == Mode.VsAi || (_tutorial < 3 && _mode == Mode.Classic)) return;
            int combo = MainSeat.Board.Combo;
            if (combo < 1) return;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 9f);
            string[] hex = { "FF9B5E", "FF6FB5", "B98CFF", "7CF8FF", "FFE27A" };
            var c = Visuals.Hex(hex[Mathf.Min(combo - 1, hex.Length - 1)]);
            var r = new Rect(w * 0.3f, h * 0.205f, w * 0.4f, h * 0.045f);
            Panel(r, _cardStyle);
            var st = new GUIStyle(_body) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(_body.fontSize * pulse) };
            Outlined(r, $"STREAK x{combo + 1}", st, c, 1.5f);
        }

        private void DrawTutorial(float w, float h)
        {
            if (_tutorial >= 3 || _mode != Mode.Classic) return;
            string[] text = { "Drag a piece onto the board", "Fill a row or column to clear it!", "Clear again and again for COMBOS!" };
            var card = new Rect(w * 0.08f, h * 0.195f, w * 0.84f, h * 0.055f);
            Panel(card, _cardStyle);
            Outlined(card, text[_tutorial], _small, Visuals.Hex("FFE27A"), 1.5f);
            if (_tutorial == 0 && _dragging < 0)
            {
                float t = (Time.time * 0.7f) % 1f;
                var from = _cam.WorldToScreenPoint(HandSlot(1));
                var to = _cam.WorldToScreenPoint(BoardCenter);
                var p = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
                float size = h * 0.05f;
                var old = GUI.color; GUI.color = new Color(1, 1, 1, 0.85f * Mathf.Sin(t * Mathf.PI));
                GUI.DrawTexture(new Rect(p.x - size / 2, h - p.y - size / 2, size, size), Visuals.Circle.texture);
                GUI.color = old;
            }
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f; // freezes animations and the AI's turn coroutine
        }

        private void DrawPause(float w, float h)
        {
            if (!_paused) return;
            var card = new Rect(w * 0.1f, h * 0.3f, w * 0.8f, h * 0.36f);
            Modal(w, h, card);
            Outlined(new Rect(card.x, card.y + h * 0.025f, card.width, h * 0.06f), "PAUSED", _h2, Color.white, 3);
            Outlined(new Rect(card.x, card.y + h * 0.085f, card.width, h * 0.04f), $"Score {Score}", _body, Visuals.Gold, 1.5f);
            float bw = card.width * 0.8f, bx = card.x + card.width * 0.1f;
            if (Btn(new Rect(bx, card.y + h * 0.14f, bw, h * 0.065f), "RESUME", "4BE38A", "1FA45B")) SetPaused(false);
            if (Btn(new Rect(bx, card.y + h * 0.22f, bw, h * 0.065f), "QUIT TO MENU", "FF7AB6", "E0347C", 0.85f)) ShowMenu();
            Outlined(new Rect(card.x, card.y + h * 0.29f, card.width, h * 0.04f), "Quitting ends this game.", _small, new Color(1, 1, 1, 0.7f), 1f);
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
            Outlined(new Rect(w * 0.07f, h * 0.022f, w * 0.5f, h * 0.045f), $"LV {_meta.Level}   ·   STREAK {_meta.Streak}", new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold }, Visuals.Hex("FF9B5E"), 1.2f);
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
            float y = h * 0.245f, bh = h * 0.064f, gap = h * 0.011f;
            float half = (w * 0.84f - gap) / 2f, x2 = w * 0.08f + half + gap;
            if (Btn(Row(w, y, bh * 1.2f), "PLAY", "4BE38A", "1FA45B", 1.4f)) StartMode(Mode.Classic);
            y += bh * 1.2f + gap;
            if (Btn(new Rect(w * 0.08f, y, half, bh), $"LEVELS {_progress.TotalStars}★", "FFC24B", "F08A1C", 0.85f)) ShowMenu(Screen.LevelSelect);
            if (Btn(new Rect(x2, y, half, bh), "DAILY", "4FD8FF", "1C8FE0", 0.85f)) StartMode(Mode.Daily);
            y += bh + gap;
            if (Btn(new Rect(w * 0.08f, y, half, bh), "VS AI", "B98CFF", "7A3FE0", 0.85f)) ShowMenu(Screen.AiSelect);
            if (Btn(new Rect(x2, y, half, bh), "2 PLAYERS", "FF7AB6", "E0347C", 0.85f)) StartMode(Mode.Versus);
            y += bh + gap;
            if (Btn(new Rect(w * 0.08f, y, half, bh), "CHALLENGE", "6E8BFF", "3A4FE0", 0.85f)) { _codeError = ""; ShowMenu(Screen.Challenge); }
            if (Btn(new Rect(x2, y, half, bh), "RANKINGS", "FFD54A", "D99A00", 0.85f)) { OnlineService.Refresh(_boardTab); ShowMenu(Screen.Leaderboard); }
            y += bh + gap;
            int ready = 0; for (int i = 0; i < 3; i++) if (_meta.IsComplete(i, Today) && !_meta.Claimed[i]) ready++;
            var missionsRect = new Rect(w * 0.08f, y, half, bh);
            if (Btn(missionsRect, "MISSIONS", "2EE59D", "13A86E", 0.85f)) ShowMenu(Screen.Missions);
            if (ready > 0) Badge(missionsRect, ready);
            if (Btn(new Rect(x2, y, half, bh), "THEMES", "FF8FAB", "D9466F", 0.85f)) ShowMenu(Screen.Themes);
            y += bh + gap * 2;
            if (Btn(new Rect(w * 0.08f, y, half, bh * 0.7f), _fx.SoundOn ? "SOUND: ON" : "SOUND: OFF", "5A4FA8", "3A2F80", 0.66f))
            { _fx.SoundOn = !_fx.SoundOn; PlayerPrefs.SetInt("sound", _fx.SoundOn ? 1 : 0); }
            if (Btn(new Rect(x2, y, half, bh * 0.7f), _fx.HapticsOn ? "VIBRATE: ON" : "VIBRATE: OFF", "5A4FA8", "3A2F80", 0.66f))
            { _fx.HapticsOn = !_fx.HapticsOn; PlayerPrefs.SetInt("haptics", _fx.HapticsOn ? 1 : 0); }
            y += bh * 0.7f + gap;
            string shown = _localName != "" ? _localName : OnlineService.Ready ? OnlineService.PlayerName.Split('#')[0] : "Set your name";
            if (shown.Length > 16) shown = shown.Substring(0, 15) + "…";
            if (Btn(Row(w, y, bh * 0.7f), $"PLAYER: {shown}", "2B2470", "1C1650", 0.66f)) { _nameInput = _localName; _nameMsg = ""; ShowMenu(Screen.Profile); }
            y += bh * 0.7f + gap;
            Outlined(new Rect(0, y, w, h * 0.035f), $"BEST {_best}   ·   {(OnlineService.Ready ? "ONLINE" : "OFFLINE")}", _small, new Color(1, 1, 1, 0.85f), 1.5f);

            if (_welcomeReward > 0)
            {
                GUI.enabled = true;
                var card = new Rect(w * 0.08f, h * 0.32f, w * 0.84f, h * 0.3f);
                Modal(w, h, card);
                Outlined(new Rect(card.x, card.y + h * 0.02f, card.width, h * 0.06f), $"DAY {_meta.Streak} STREAK!", new GUIStyle(_h2) { fontSize = Mathf.RoundToInt(_h2.fontSize * 0.8f) }, Visuals.Hex("FF9B5E"), 3);
                Outlined(new Rect(card.x, card.y + h * 0.09f, card.width, h * 0.06f), $"+{_welcomeReward} ●", _h2, Visuals.Gold, 3);
                Outlined(new Rect(card.x + w * 0.04f, card.y + h * 0.15f, card.width - w * 0.08f, h * 0.05f), $"Come back tomorrow for +{PlayerMeta.StreakReward(_meta.Streak + 1)}", _small, Color.white, 1.2f);
                if (Btn(new Rect(card.x + card.width * 0.2f, card.y + h * 0.21f, card.width * 0.6f, h * 0.065f), "COLLECT", "4BE38A", "1FA45B")) _welcomeReward = 0;
            }
        }

        private void DrawMissions(float w, float h)
        {
            Outlined(new Rect(0, h * 0.06f, w, h * 0.08f), "DAILY MISSIONS", _h2, Visuals.Hex("7CF8FF"), 3);
            Outlined(new Rect(0, h * 0.13f, w, h * 0.04f), $"● {_meta.Coins}   ·   STREAK {_meta.Streak}   ·   BEST STREAK {_meta.BestStreak}", _small, Visuals.Gold, 1.2f);
            var ms = PlayerMeta.MissionsFor(Today);
            for (int i = 0; i < 3; i++)
            {
                var card = new Rect(w * 0.06f, h * (0.19f + i * 0.17f), w * 0.88f, h * 0.15f);
                Panel(card, _cardStyle);
                var left = new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft };
                Outlined(new Rect(card.x + w * 0.05f, card.y + h * 0.012f, card.width * 0.9f, h * 0.045f), ms[i].Text, left, Color.white, 1.2f);
                int prog = Mathf.Min(_meta.Progress[i], ms[i].Target);
                var bar = new Rect(card.x + w * 0.05f, card.y + h * 0.065f, card.width * 0.55f, h * 0.022f);
                Panel(bar, _barBackStyle);
                float f = (float)prog / ms[i].Target;
                if (f > 0.04f) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * f), bar.height), _barFillStyle);
                Outlined(new Rect(bar.x, bar.y + h * 0.026f, bar.width, h * 0.035f), $"{prog} / {ms[i].Target}", new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft }, new Color(1, 1, 1, 0.8f), 1);
                var btn = new Rect(card.xMax - w * 0.3f, card.y + h * 0.05f, w * 0.26f, h * 0.06f);
                if (_meta.Claimed[i]) Outlined(btn, "DONE ✓", _body, Visuals.Hex("2EE59D"), 1.2f);
                else if (_meta.IsComplete(i, Today)) { if (Btn(btn, $"+{ms[i].Reward} ●", "FFD54A", "D99A00", 0.75f) && _meta.Claim(i, Today)) { _fx.Win(); Confetti(25); SaveMeta(); } }
                else Outlined(btn, $"+{ms[i].Reward} ●", _body, new Color(1, 1, 1, 0.5f), 1.2f);
            }
            Outlined(new Rect(w * 0.08f, h * 0.69f, w * 0.84f, h * 0.06f), "New missions every day. Coins unlock themes; they never buy an advantage.", _small, new Color(1, 1, 1, 0.75f), 1.2f);
            int got = 0; for (int i = 0; i < PlayerMeta.Achievements.Length; i++) if (_meta.HasAchievement(i)) got++;
            if (Btn(Row(w, h * 0.765f, h * 0.065f), $"TROPHIES  {got}/{PlayerMeta.Achievements.Length}", "7CF8FF", "1C8FE0", 0.85f)) ShowMenu(Screen.Trophies);
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private void DrawTrophies(float w, float h)
        {
            Outlined(new Rect(0, h * 0.045f, w, h * 0.07f), "TROPHIES", _h2, Visuals.Hex("7CF8FF"), 3);
            Outlined(new Rect(0, h * 0.11f, w, h * 0.035f), $"LEVEL {_meta.Level}   ·   {_meta.Xp}/{PlayerMeta.XpToNext(_meta.Level)} XP", _small, Visuals.Gold, 1.2f);
            var card = new Rect(w * 0.04f, h * 0.155f, w * 0.92f, h * 0.675f);
            Panel(card, _cardStyle);
            float rh = card.height / PlayerMeta.Achievements.Length;
            var left = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            var right = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            for (int i = 0; i < PlayerMeta.Achievements.Length; i++)
            {
                var a = PlayerMeta.Achievements[i];
                bool done = _meta.HasAchievement(i);
                int have = Mathf.Min(_meta.Stats[(int)a.Stat], a.Target);
                var row = new Rect(card.x + w * 0.05f, card.y + i * rh, card.width - w * 0.1f, rh);
                Outlined(new Rect(row.x, row.y + rh * 0.05f, row.width * 0.7f, rh * 0.5f), (done ? "★ " : "☆ ") + a.Name, new GUIStyle(left) { fontStyle = FontStyle.Bold }, done ? Visuals.Gold : Color.white, 1.2f);
                Outlined(new Rect(row.x, row.y + rh * 0.48f, row.width * 0.75f, rh * 0.45f), a.Text, left, new Color(1, 1, 1, 0.7f), 1f);
                Outlined(new Rect(row.x, row.y + rh * 0.05f, row.width, rh * 0.5f), done ? "DONE" : $"+{a.Reward} ●", right, done ? Visuals.Hex("2EE59D") : Visuals.Gold, 1.2f);
                if (!done) Outlined(new Rect(row.x, row.y + rh * 0.48f, row.width, rh * 0.45f), $"{have}/{a.Target}", new GUIStyle(right) { fontStyle = FontStyle.Normal }, new Color(1, 1, 1, 0.7f), 1f);
            }
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu(Screen.Missions);
        }

        private readonly Dictionary<Color, GUIStyle> _swatches = new Dictionary<Color, GUIStyle>();
        private GUIStyle Swatch(Color c)
        {
            if (!_swatches.TryGetValue(c, out var st)) _swatches[c] = st = Sliced(Visuals.RoundedRect(32, 32, 8, Color.Lerp(c, Color.white, 0.25f), c, 0.15f), 9);
            return st;
        }

        private void DrawThemes(float w, float h)
        {
            Outlined(new Rect(0, h * 0.05f, w, h * 0.08f), "THEMES", _h2, Visuals.Hex("FF8FAB"), 3);
            Outlined(new Rect(0, h * 0.12f, w, h * 0.04f), $"● {_meta.Coins}", _body, Visuals.Gold, 1.5f);
            for (int t = 0; t < PlayerMeta.Themes.Length; t++)
            {
                var th = PlayerMeta.Themes[t];
                var card = new Rect(w * 0.06f, h * (0.17f + t * 0.125f), w * 0.88f, h * 0.11f);
                Panel(card, _cardStyle);
                Outlined(new Rect(card.x + w * 0.05f, card.y + h * 0.008f, w * 0.4f, h * 0.045f), th.Name.ToUpperInvariant(), new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold }, Color.white, 1.2f);
                float sw = h * 0.035f;
                for (int k = 0; k < 5; k++) Panel(new Rect(card.x + w * 0.05f + k * (sw + 6), card.y + h * 0.058f, sw, sw), Swatch(Visuals.Hex(th.Blocks[k])));
                var btn = new Rect(card.xMax - w * 0.32f, card.y + h * 0.025f, w * 0.28f, h * 0.06f);
                bool owned = _meta.Owns(t), selected = _meta.ThemeIndex == t;
                if (selected) Outlined(btn, "IN USE", _body, Visuals.Hex("2EE59D"), 1.2f);
                else if (Btn(btn, owned ? "USE" : $"{th.Price} ●", owned ? "6E8BFF" : _meta.Coins >= th.Price ? "FFD54A" : "4A4370", owned ? "3A4FE0" : _meta.Coins >= th.Price ? "D99A00" : "2E2850", 0.75f))
                {
                    if (_meta.BuyOrSelect(t)) { ApplyTheme(); SaveMeta(); _fx.Star(); if (!owned) Confetti(25); }
                    else _fx.Invalid();
                }
            }
            if (Btn(Row(w, h * 0.82f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private void DrawLevelSelect(float w, float h)
        {
            Outlined(new Rect(0, h * 0.05f, w, h * 0.08f), "LEVELS", _h2, Visuals.Hex("FFE27A"), 3);
            Outlined(new Rect(0, h * 0.12f, w, h * 0.04f), $"{_progress.TotalStars} / {LevelLibrary.Count * 3} ★", _body, Color.white, 1.5f);
            const int perRow = 5;
            float gap = w * 0.025f, cell = (w * 0.86f - gap * (perRow - 1)) / perRow, y0 = h * 0.18f;
            for (int n = 1; n <= LevelLibrary.Count; n++)
            {
                int row = (n - 1) / perRow, col = (n - 1) % perRow;
                var r = new Rect(w * 0.07f + col * (cell + gap), y0 + row * (cell * 1.08f + gap), cell, cell * 1.08f);
                bool open = _progress.IsUnlocked(n);
                int stars = _progress.StarsFor(n);
                string label = open ? $"{n}\n{new string('★', stars)}{new string('☆', 3 - stars)}" : $"{n}";
                GUI.enabled = open;
                var (top, bottom) = !open ? ("4A4370", "2E2850") : stars == 3 ? ("FFD54A", "D99A00") : stars > 0 ? ("4BE38A", "1FA45B") : ("6E8BFF", "3A4FE0");
                if (Btn(r, label, top, bottom, 0.8f)) StartMode(Mode.Level, level: n);
                GUI.enabled = true;
            }
            if (Btn(Row(w, h * 0.88f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private void DrawAiSelect(float w, float h)
        {
            Outlined(new Rect(0, h * 0.12f, w, h * 0.08f), "VS AI", _h1, Visuals.Hex("C9A7FF"), 3);
            Outlined(new Rect(0, h * 0.22f, w, h * 0.06f), "Same pieces for you and the AI. Highest score wins.", _body, Color.white, 1.5f);
            float y = h * 0.32f, bh = h * 0.085f, gap = h * 0.02f;
            if (Btn(Row(w, y, bh), "EASY", "4BE38A", "1FA45B")) { _aiLevel = BotLevel.Easy; StartMode(Mode.VsAi); }
            if (Btn(Row(w, y + bh + gap, bh), "MEDIUM", "FFC24B", "F08A1C")) { _aiLevel = BotLevel.Medium; StartMode(Mode.VsAi); }
            if (Btn(Row(w, y + 2 * (bh + gap), bh), "HARD (MASTER AI)", "FF6B8B", "D61F4E")) { _aiLevel = BotLevel.Hard; StartMode(Mode.VsAi); }
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private void DrawHud(float w, float h)
        {
            float top = h * 0.03f, bs = h * 0.06f;
            if (Btn(new Rect(w * 0.04f, top, bs * 1.6f, bs), "MENU", "5A4FA8", "3A2F80", 0.7f)) { SetPaused(true); return; }
            if (_hintsLeft > 0 && Btn(new Rect(w * 0.96f - bs * 2.1f, top, bs * 2.1f, bs), $"HINT {_hintsLeft}", "FFC24B", "F08A1C", 0.7f)) UseHint();

            switch (_mode)
            {
                case Mode.Level:
                {
                    var d = _level.Definition;
                    Outlined(new Rect(w * 0.27f, top, w * 0.46f, bs), $"LEVEL {d.Number}", _body, Visuals.Hex("FFE27A"), 1.5f);
                    Outlined(new Rect(0, h * 0.085f, w, h * 0.06f), Mathf.RoundToInt(_shownScore).ToString(), _h2, Color.white, 3);
                    var bar = new Rect(w * 0.12f, h * 0.178f, w * 0.76f, h * 0.022f);
                    Panel(bar, _barBackStyle);
                    float f = Mathf.Clamp01(_shownScore / Mathf.Max(1, d.ThreeStarScore));
                    if (f > 0.04f) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * f), bar.height), _barFillStyle);
                    foreach (var s in new[] { d.TargetScore, d.TwoStarScore, d.ThreeStarScore })
                    {
                        float sx = bar.x + bar.width * Mathf.Clamp01((float)s / Mathf.Max(1, d.ThreeStarScore));
                        Outlined(new Rect(sx - 20, bar.y - h * 0.03f, 40, h * 0.03f), "★", _small, Score >= s ? Visuals.Gold : new Color(1, 1, 1, 0.5f), 1);
                    }
                    Outlined(new Rect(0, h * 0.205f, w, h * 0.04f), $"MOVES LEFT  {_level.MovesLeft}", _small, _level.MovesLeft <= 3 ? Visuals.Hex("FF8FA3") : Color.white, 1.5f);
                    break;
                }
                case Mode.Versus:
                case Mode.VsAi:
                {
                    bool ai = _mode == Mode.VsAi;
                    int me = ai ? 0 : _versus.Turn;
                    string meName = ai ? "YOU" : $"PLAYER {me + 1}", them = ai ? $"AI ({_aiLevel})" : $"PLAYER {2 - me}";
                    Outlined(new Rect(w * 0.06f, h * 0.10f, w * 0.5f, h * 0.05f), meName, _body, Visuals.Hex("FFE27A"), 1.5f);
                    Outlined(new Rect(w * 0.06f, h * 0.14f, w * 0.5f, h * 0.07f), _versus.Seats[me].Board.Score.ToString(), _h2, Color.white, 2.5f);
                    Outlined(new Rect(w * 0.55f, h * 0.025f + bs, w * 0.4f, h * 0.035f), $"{them}: {_versus.Seats[1 - me].Board.Score}{(_versus.Seats[1 - me].Out ? " (out)" : "")}", _small, Visuals.Hex("E6D9FF"), 1.2f);
                    if (_aiThinking && ai) Outlined(new Rect(0, h * 0.205f, w, h * 0.035f), "AI is thinking" + new string('.', 1 + (int)(Time.time * 3) % 3), _small, Visuals.Hex("C9A7FF"), 1.2f);
                    if (!ai && Time.time < _turnBannerUntil)
                    {
                        Panel(new Rect(w * 0.1f, h * 0.38f, w * 0.8f, h * 0.14f), _cardStyle);
                        Outlined(new Rect(0, h * 0.38f, w, h * 0.14f), $"PLAYER {me + 1}\nYOUR TURN", _h2, Color.white, 2.5f);
                    }
                    break;
                }
                default:
                {
                    string label = _mode == Mode.Daily ? "DAILY" : _mode == Mode.Challenge ? ChallengeCode.Encode(_challengeSeed) : $"BEST {Mathf.Max(_best, Score)}";
                    Outlined(new Rect(w * 0.27f, top, w * 0.46f, bs), label, _body, Visuals.Hex("FFE27A"), 1.5f);
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
            Panel(new Rect(w * 0.06f, h * 0.14f, w * 0.88f, h * 0.62f), _cardStyle);
            float y = h * 0.17f;
            switch (_mode)
            {
                case Mode.Level:
                {
                    bool won = _level.State == LevelState.Won;
                    Outlined(new Rect(0, y, w, h * 0.08f), won ? "LEVEL COMPLETE!" : "OUT OF MOVES", _h2, won ? Visuals.Hex("FFE27A") : Visuals.Hex("FF8FA3"), 3);
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
                    Outlined(new Rect(0, y + h * 0.21f, w, h * 0.06f), won ? $"SCORE {_level.Seat.Board.Score}" : $"SCORE {_level.Seat.Board.Score} / {_level.Definition.TargetScore}", _body, Color.white, 1.5f);
                    float by = y + h * 0.3f, bh = h * 0.07f, gap = h * 0.015f;
                    if (won && _level.Definition.Number < LevelLibrary.Count && Btn(Row(w, by, bh, 0.14f), "NEXT LEVEL", "4BE38A", "1FA45B")) { ResetStars(); StartMode(Mode.Level, level: _level.Definition.Number + 1); }
                    if (Btn(Row(w, by + bh + gap, bh, 0.14f), "RETRY", "FFC24B", "F08A1C")) { ResetStars(); StartMode(Mode.Level, level: _level.Definition.Number); }
                    if (Btn(Row(w, by + 2 * (bh + gap), bh, 0.14f), "LEVELS", "5A4FA8", "3A2F80")) { ResetStars(); ShowMenu(Screen.LevelSelect); }
                    return;
                }
                case Mode.Versus:
                case Mode.VsAi:
                {
                    int win = _versus.Winner;
                    bool ai = _mode == Mode.VsAi;
                    string title = win < 0 ? "IT'S A TIE!" : ai ? (win == 0 ? "YOU WIN!" : "AI WINS") : $"PLAYER {win + 1} WINS!";
                    Outlined(new Rect(0, y, w, h * 0.08f), title, _h2, win == 0 || !ai ? Visuals.Hex("FFE27A") : Visuals.Hex("FF8FA3"), 3);
                    Outlined(new Rect(0, y + h * 0.1f, w, h * 0.1f), $"{(ai ? "YOU" : "P1")}  {_versus.Seats[0].Board.Score}\n{(ai ? "AI" : "P2")}  {_versus.Seats[1].Board.Score}", _h2, Color.white, 2);
                    if (Btn(Row(w, y + h * 0.3f, h * 0.07f, 0.14f), "REMATCH", "4BE38A", "1FA45B")) StartMode(_mode);
                    break;
                }
                case Mode.Challenge:
                {
                    string code = ChallengeCode.Encode(_challengeSeed);
                    Outlined(new Rect(0, y, w, h * 0.08f), $"SCORE {_single.Board.Score}", _h2, Color.white, 3);
                    Outlined(new Rect(0, y + h * 0.08f, w, h * 0.08f), code, _h1, Visuals.Hex("7CF8FF"), 3);
                    Outlined(new Rect(w * 0.1f, y + h * 0.16f, w * 0.8f, h * 0.1f), "Send this code to a friend. They get exactly your pieces. Highest score wins!", _small, Color.white, 1.2f);
                    if (Btn(Row(w, y + h * 0.27f, h * 0.07f, 0.14f), "COPY CODE", "4FD8FF", "1C8FE0")) GUIUtility.systemCopyBuffer = $"Beat my {_single.Board.Score} in Block Drop! Code: {code}";
                    if (Btn(Row(w, y + h * 0.355f, h * 0.07f, 0.14f), "PLAY AGAIN", "4BE38A", "1FA45B")) StartMode(Mode.Challenge, _challengeSeed);
                    break;
                }
                default:
                {
                    Outlined(new Rect(0, y, w, h * 0.08f), _newBest ? "NEW BEST!" : "GAME OVER", _h2, _newBest ? Visuals.Hex("FFE27A") : Visuals.Hex("FF8FA3"), 3);
                    var st = new GUIStyle(_h1) { fontSize = Mathf.RoundToInt(_h1.fontSize * (1f + 0.3f * EaseOutBack(Mathf.Clamp01(t * 2f)) - 0.3f)) };
                    Outlined(new Rect(0, y + h * 0.1f, w, h * 0.12f), Score.ToString(), st, Color.white, 3);
                    Outlined(new Rect(0, y + h * 0.21f, w, h * 0.05f), $"BEST {_best}", _body, Visuals.Gold, 1.5f);
                    if (!_newBest && _best > 0 && Score >= _best * 0.8f)
                        Outlined(new Rect(0, y + h * 0.255f, w, h * 0.04f), $"So close! Only {_best - Score} points from your best.", _small, Visuals.Hex("7CF8FF"), 1.2f);
                    if (Btn(Row(w, y + h * 0.3f, h * 0.07f, 0.14f), "PLAY AGAIN", "4BE38A", "1FA45B")) StartMode(_mode);
                    break;
                }
            }
            if (Btn(Row(w, h * 0.66f, h * 0.07f, 0.14f), "MENU", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private readonly bool[] _starPlayed = new bool[3];
        private void ResetStars() { for (int i = 0; i < 3; i++) _starPlayed[i] = false; }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
        }

        private void PlayCode()
        {
            if (ChallengeCode.TryDecode(_codeInput, out int seed)) StartMode(Mode.Challenge, seed);
            else _codeError = $"Codes have {ChallengeCode.Length} letters/digits, e.g. K7Q2MX";
        }

        /// <summary>Text entry that works with the phone keyboard: taps on the game are blocked while the
        /// keyboard is up, so the keyboard's Done key submits. Falls back to a normal field in the editor.</summary>
        private string KeyboardField(Rect r, string value, string target, int limit, string placeholder)
        {
            if (!TouchScreenKeyboard.isSupported) return GUI.TextField(r, value, limit, _field);
            var st = new GUIStyle(_field);
            if (value == "") st.normal.textColor = st.hover.textColor = st.active.textColor = new Color(0.16f, 0.05f, 0.38f, 0.35f);
            if (GUI.Button(r, value == "" ? placeholder : value, st))
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
            Outlined(new Rect(w * 0.08f, h * 0.165f, w * 0.84f, h * 0.11f), "Start a challenge and share its code, or enter a friend's code to play their exact pieces.", _body, Color.white, 1.5f);
            if (Btn(Row(w, h * 0.30f, h * 0.08f), "NEW CHALLENGE", "4BE38A", "1FA45B")) StartMode(Mode.Challenge, ChallengeCode.NewSeed(_rng));
            Outlined(new Rect(0, h * 0.415f, w, h * 0.04f), "FRIEND'S CODE", _body, Visuals.Hex("FFE27A"), 1.5f);
            _codeInput = KeyboardField(Row(w, h * 0.465f, h * 0.085f, 0.16f), _codeInput, "code", ChallengeCode.Length, "K7Q2MX").ToUpperInvariant();
            if (Btn(Row(w, h * 0.57f, h * 0.075f), "PLAY CODE", "6E8BFF", "3A4FE0")) PlayCode();
            if (_codeError != "") Outlined(new Rect(0, h * 0.655f, w, h * 0.05f), _codeError, _small, Visuals.Hex("FF8FA3"), 1.2f);
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private void DrawProfile(float w, float h)
        {
            Outlined(new Rect(0, h * 0.1f, w, h * 0.08f), "YOUR NAME", _h2, Visuals.Gold, 3);
            Outlined(new Rect(w * 0.08f, h * 0.19f, w * 0.84f, h * 0.08f), "Shown on leaderboards. Letters, numbers and _ only (3–20).", _body, Color.white, 1.5f);
            _nameInput = KeyboardField(Row(w, h * 0.3f, h * 0.085f, 0.1f), _nameInput, "name", 20, "Your name");
            if (Btn(Row(w, h * 0.41f, h * 0.075f), "SAVE", "4BE38A", "1FA45B")) SaveName();
            if (_nameMsg != "") Outlined(new Rect(w * 0.05f, h * 0.5f, w * 0.9f, h * 0.08f), _nameMsg, _small, Visuals.Hex("FFE27A"), 1.2f);
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private async void SaveName()
        {
            string n = _nameInput.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(n, "^[A-Za-z0-9_]{3,20}$")) { _nameMsg = "Use 3–20 letters, numbers or _ (no spaces)."; return; }
            _localName = n; PlayerPrefs.SetString("name", n); PlayerPrefs.Save();
            _nameMsg = "Saving…";
            string err = await OnlineService.SetNameAsync(n);
            _nameMsg = err ?? $"Saved! You are {OnlineService.PlayerName}";
        }

        private void DrawLeaderboard(float w, float h)
        {
            Outlined(new Rect(0, h * 0.05f, w, h * 0.08f), "LEADERBOARD", _h2, Visuals.Gold, 3);
            float half = w * 0.4f;
            bool classic = _boardTab == OnlineService.ClassicBoard;
            if (Btn(new Rect(w * 0.08f, h * 0.14f, half, h * 0.06f), "CLASSIC", classic ? "FFD54A" : "5A4FA8", classic ? "D99A00" : "3A2F80", 0.8f)) { _boardTab = OnlineService.ClassicBoard; OnlineService.Refresh(_boardTab); }
            if (Btn(new Rect(w * 0.92f - half, h * 0.14f, half, h * 0.06f), "DAILY", !classic ? "FFD54A" : "5A4FA8", !classic ? "D99A00" : "3A2F80", 0.8f)) { _boardTab = OnlineService.DailyBoard; OnlineService.Refresh(_boardTab); }
            var card = new Rect(w * 0.06f, h * 0.22f, w * 0.88f, h * 0.6f);
            Panel(card, _cardStyle);
            float y = card.y + h * 0.02f, rh = h * 0.052f;
            if (OnlineService.Errors.TryGetValue(_boardTab, out var err)) Outlined(new Rect(card.x, y, card.width, rh * 3), err, _body, Color.white, 1.2f);
            else if (!OnlineService.Ready) Outlined(new Rect(card.x, y, card.width, rh * 2), OnlineService.Status, _body, Color.white, 1.2f);
            else if (!OnlineService.Top.TryGetValue(_boardTab, out var rows)) Outlined(new Rect(card.x, y, card.width, rh), "Loading…", _body, Color.white, 1.2f);
            else if (rows.Count == 0) Outlined(new Rect(card.x, y, card.width, rh * 2), "No scores yet.\nBe the first!", _body, Color.white, 1.2f);
            else
                foreach (var r in rows)
                {
                    var c = r.IsMe ? Visuals.Hex("7CF8FF") : r.Rank == 1 ? Visuals.Gold : Color.white;
                    var left = new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft };
                    var right = new GUIStyle(_body) { alignment = TextAnchor.MiddleRight };
                    Outlined(new Rect(card.x + w * 0.05f, y, card.width * 0.65f, rh), $"{r.Rank}.  {r.Name}{(r.IsMe ? " (you)" : "")}", left, c, 1.2f);
                    Outlined(new Rect(card.x, y, card.width - w * 0.05f, rh), r.Score.ToString(), right, c, 1.2f);
                    y += rh;
                }
            if (Btn(Row(w, h * 0.86f, h * 0.07f, 0.25f), "BACK", "5A4FA8", "3A2F80")) ShowMenu();
        }

        private void DrawFloaters(float h)
        {
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                float age = Time.time - f.Born;
                if (age > f.Life) { _floaters.RemoveAt(i); continue; }
                var sp = _cam.WorldToScreenPoint(f.World + new Vector3(0, age * 1.2f, 0));
                float k = EaseOutBack(Mathf.Clamp01(age * 5f));
                var st = new GUIStyle(_h2) { fontSize = Mathf.Max(8, Mathf.RoundToInt(_h2.fontSize * f.Size * k)) };
                var c = f.Color; c.a = Mathf.Clamp01((f.Life - age) * 3f);
                float bw = UnityEngine.Screen.width;
                Outlined(new Rect(0, h - sp.y - st.fontSize, bw, st.fontSize * 2), f.Text, st, c, 3);
            }
        }
    }
}
