using System;
using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>Builds the whole game at runtime (no prefabs needed): menu, level select, board,
    /// 3-piece hand with drag & drop, HUD and result screens. Works fully offline.</summary>
    public sealed class GameController : MonoBehaviour
    {
        private enum Screen { Menu, LevelSelect, Playing, Result, Challenge, Leaderboard }
        private enum Mode { Classic, Daily, Level, Versus, Challenge }

        private const int Size = 8;
        private const float Cell = 1f;
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.35f, 0.35f), new Color(0.35f, 0.7f, 0.95f), new Color(0.45f, 0.85f, 0.45f),
            new Color(0.98f, 0.78f, 0.3f), new Color(0.7f, 0.5f, 0.95f), new Color(0.45f, 0.47f, 0.55f),
        };
        private static readonly Color EmptyColor = new Color(0.18f, 0.2f, 0.26f);

        private Screen _screen = Screen.Menu;
        private Mode _mode;
        private Seat _single;
        private LevelSession _level;
        private VersusMatch _versus;
        private Progress _progress;
        private int _best;
        private int _challengeSeed;
        private string _codeInput = "", _codeError = "";
        private string _boardTab = OnlineService.ClassicBoard;

        private GameObject _boardRoot;
        private SpriteRenderer[,] _grid;
        private readonly System.Collections.Generic.List<GameObject>[] _handViews =
            { new System.Collections.Generic.List<GameObject>(), new System.Collections.Generic.List<GameObject>(), new System.Collections.Generic.List<GameObject>() };
        private Sprite _square;
        private int _dragging = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<GameController>() == null) new GameObject("BlockDrop").AddComponent<GameController>();
        }

        private void Start()
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            var cam = Camera.main != null ? Camera.main : new GameObject("Main Camera").AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 9f; cam.transform.position = new Vector3(0, -2f, -10);
            cam.backgroundColor = new Color(0.1f, 0.11f, 0.15f); cam.clearFlags = CameraClearFlags.SolidColor;
            _best = PlayerPrefs.GetInt("best", 0);
            _progress = Progress.Parse(PlayerPrefs.GetString("progress", ""));
            BuildGrid();
            ShowBoard(false);
            _ = OnlineService.EnsureReady(); // best effort; game is fully playable offline
        }

        // ---------- state ----------

        private Seat CurrentSeat => _mode switch
        {
            Mode.Level => _level.Seat,
            Mode.Versus => _versus.CurrentSeat,
            _ => _single,
        };

        private void StartClassic(bool daily)
        {
            _mode = daily ? Mode.Daily : Mode.Classic;
            _single = new Seat(daily ? PieceGenerator.DailySeed(DateTime.UtcNow) : Environment.TickCount);
            BeginPlay();
        }

        private void StartLevel(int number)
        {
            _mode = Mode.Level;
            _level = new LevelSession(number);
            BeginPlay();
        }

        private void StartChallenge(int seed)
        {
            _mode = Mode.Challenge;
            _challengeSeed = seed;
            _single = new Seat(seed);
            BeginPlay();
        }

        private void StartVersus()
        {
            _mode = Mode.Versus;
            _versus = new VersusMatch(Environment.TickCount);
            BeginPlay();
        }

        private void BeginPlay()
        {
            _screen = Screen.Playing;
            _dragging = -1;
            ShowBoard(true);
            RefreshAll();
        }

        private bool TryPlace(int hand, int x, int y) => _mode switch
        {
            Mode.Level => _level.TryPlace(hand, x, y),
            Mode.Versus => _versus.TryPlace(hand, x, y),
            _ => _single.TryPlace(hand, x, y),
        };

        private bool IsFinished => _mode switch
        {
            Mode.Level => _level.State != LevelState.Playing,
            Mode.Versus => _versus.Over,
            _ => _single.Out,
        };

        private void Finish()
        {
            if (_mode == Mode.Level && _level.State == LevelState.Won)
            {
                _progress.Record(_level.Definition.Number, _level.Stars);
                PlayerPrefs.SetString("progress", _progress.Serialize());
            }
            if ((_mode == Mode.Classic || _mode == Mode.Daily) && _single.Board.Score > _best)
            {
                _best = _single.Board.Score;
                PlayerPrefs.SetInt("best", _best);
            }
            if (_mode == Mode.Classic) OnlineService.SubmitScore(OnlineService.ClassicBoard, _single.Board.Score);
            if (_mode == Mode.Daily) OnlineService.SubmitScore(OnlineService.DailyBoard, _single.Board.Score);
            PlayerPrefs.Save();
            _screen = Screen.Result;
            ShowBoard(false);
        }

        // ---------- board rendering ----------

        private Vector3 CellPos(int x, int y) => new Vector3((x - (Size - 1) / 2f) * Cell, ((Size - 1) / 2f - y) * Cell, 0);
        private Vector3 HandOrigin(int slot) => new Vector3(-5.5f + slot * 5.5f, -7.5f, 0);

        private void BuildGrid()
        {
            _boardRoot = new GameObject("Board");
            _boardRoot.transform.SetParent(transform);
            _grid = new SpriteRenderer[Size, Size];
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    var go = new GameObject($"cell {x},{y}");
                    go.transform.SetParent(_boardRoot.transform);
                    go.transform.position = CellPos(x, y);
                    go.transform.localScale = Vector3.one * (Cell * 0.92f);
                    _grid[x, y] = go.AddComponent<SpriteRenderer>();
                    _grid[x, y].sprite = _square;
                }
        }

        private void ShowBoard(bool show)
        {
            _boardRoot.SetActive(show);
            if (!show) for (int i = 0; i < Dealer.HandSize; i++) ClearHand(i);
        }

        private void RefreshAll()
        {
            var seat = CurrentSeat;
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    _grid[x, y].color = seat.Board.IsFilled(x, y) ? Palette[seat.Board[x, y]] : EmptyColor;
            for (int i = 0; i < Dealer.HandSize; i++) DrawHand(i, HandOrigin(i), 0.5f);
        }

        private void ClearHand(int slot)
        {
            foreach (var go in _handViews[slot]) Destroy(go);
            _handViews[slot].Clear();
        }

        private void DrawHand(int slot, Vector3 origin, float scale)
        {
            ClearHand(slot);
            var p = CurrentSeat.Dealer.Hand[slot];
            if (p == null) return;
            foreach (var (cx, cy) in p.Cells)
            {
                var go = new GameObject("piece");
                go.transform.position = origin + new Vector3((cx - (p.Width - 1) / 2f) * scale, ((p.Height - 1) / 2f - cy) * scale, -1);
                go.transform.localScale = Vector3.one * scale * 0.9f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _square; sr.color = Palette[p.ColorIndex]; sr.sortingOrder = 2;
                _handViews[slot].Add(go);
            }
        }

        // ---------- input ----------

        private void Update()
        {
            if (_screen != Screen.Playing) return;
            var hand = CurrentSeat.Dealer.Hand;
            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
                for (int i = 0; i < hand.Length; i++)
                    if (hand[i] != null && Vector2.Distance(world, HandOrigin(i)) < 2.2f) _dragging = i;
            if (_dragging >= 0 && Input.GetMouseButton(0))
                DrawHand(_dragging, world + new Vector3(0, 1.5f, 0), Cell);
            if (_dragging >= 0 && Input.GetMouseButtonUp(0))
            {
                var p = hand[_dragging];
                Vector3 drop = world + new Vector3(0, 1.5f, 0);
                int ox = Mathf.RoundToInt(drop.x / Cell + (Size - 1) / 2f - (p.Width - 1) / 2f);
                int oy = Mathf.RoundToInt((Size - 1) / 2f - drop.y / Cell - (p.Height - 1) / 2f);
                TryPlace(_dragging, ox, oy);
                _dragging = -1;
                if (IsFinished) Finish();
                else RefreshAll();
            }
        }

        // ---------- UI ----------

        private GUIStyle _label, _title, _button;

        private void EnsureStyles()
        {
            int fs = Mathf.Max(18, UnityEngine.Screen.height / 32);
            if (_label != null && _label.fontSize == fs) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
            _title = new GUIStyle(_label) { fontSize = fs * 2, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = fs };
        }

        private bool Button(string text) => GUILayout.Button(text, _button, GUILayout.Height(_button.fontSize * 2.6f));

        private void OnGUI()
        {
            EnsureStyles();
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height, pad = w * 0.06f;
            switch (_screen)
            {
                case Screen.Menu: DrawMenu(new Rect(pad, h * 0.12f, w - 2 * pad, h * 0.8f)); break;
                case Screen.LevelSelect: DrawLevelSelect(new Rect(pad, h * 0.06f, w - 2 * pad, h * 0.9f)); break;
                case Screen.Playing: DrawHud(new Rect(20, 20, w - 40, h * 0.22f)); break;
                case Screen.Result: DrawResult(new Rect(pad, h * 0.2f, w - 2 * pad, h * 0.6f)); break;
                case Screen.Challenge: DrawChallenge(new Rect(pad, h * 0.12f, w - 2 * pad, h * 0.8f)); break;
                case Screen.Leaderboard: DrawLeaderboard(new Rect(pad, h * 0.06f, w - 2 * pad, h * 0.9f)); break;
            }
        }

        private void DrawMenu(Rect r)
        {
            GUILayout.BeginArea(r);
            GUILayout.Label("Block Drop", _title);
            GUILayout.Space(_label.fontSize);
            if (Button($"Levels  ({_progress.TotalStars}/{LevelLibrary.Count * 3} ★)")) _screen = Screen.LevelSelect;
            if (Button("Classic")) StartClassic(false);
            if (Button("Daily challenge")) StartClassic(true);
            if (Button("2 players (pass & play)")) StartVersus();
            if (Button("Challenge a friend")) { _codeError = ""; _screen = Screen.Challenge; }
            if (Button("Leaderboards")) { OnlineService.Refresh(_boardTab); _screen = Screen.Leaderboard; }
            GUILayout.Space(_label.fontSize);
            GUILayout.Label($"Best classic score: {_best}\n{OnlineService.Status}{(OnlineService.PlayerName != "" ? " as " + OnlineService.PlayerName : "")}", _label);
            GUILayout.EndArea();
        }

        private void DrawLevelSelect(Rect r)
        {
            GUILayout.BeginArea(r);
            GUILayout.Label("Levels", _title);
            const int perRow = 5;
            for (int row = 0; row < LevelLibrary.Count / perRow; row++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < perRow; c++)
                {
                    int n = row * perRow + c + 1;
                    bool open = _progress.IsUnlocked(n);
                    GUI.enabled = open;
                    string stars = new string('★', _progress.StarsFor(n)) + new string('☆', 3 - _progress.StarsFor(n));
                    if (GUILayout.Button(open ? $"{n}\n{stars}" : $"{n}\nlocked", _button, GUILayout.Height(_button.fontSize * 3.2f)))
                        StartLevel(n);
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(_label.fontSize);
            if (Button("Back")) _screen = Screen.Menu;
            GUILayout.EndArea();
        }

        private void DrawChallenge(Rect r)
        {
            GUILayout.BeginArea(r);
            GUILayout.Label("Challenge a friend", _title);
            GUILayout.Label("Start a new challenge and share its code, or enter a friend's code to play their exact pieces. Works offline.", _label);
            if (Button("New challenge")) StartChallenge(ChallengeCode.NewSeed(new System.Random()));
            GUILayout.Space(_label.fontSize);
            GUILayout.Label("Friend's code:", _label);
            _codeInput = GUILayout.TextField(_codeInput, ChallengeCode.Length, new GUIStyle(GUI.skin.textField) { fontSize = _label.fontSize * 3 / 2 }, GUILayout.Height(_button.fontSize * 2.6f));
            if (Button("Play friend's code"))
            {
                if (ChallengeCode.TryDecode(_codeInput, out int seed)) StartChallenge(seed);
                else _codeError = $"That code isn't valid: it has {ChallengeCode.Length} letters/digits, e.g. K7Q2MX.";
            }
            if (_codeError != "") GUILayout.Label(_codeError, _label);
            if (Button("Back")) _screen = Screen.Menu;
            GUILayout.EndArea();
        }

        private void DrawLeaderboard(Rect r)
        {
            GUILayout.BeginArea(r);
            GUILayout.Label("Leaderboards", _title);
            GUILayout.BeginHorizontal();
            if (Button("Classic")) { _boardTab = OnlineService.ClassicBoard; OnlineService.Refresh(_boardTab); }
            if (Button("Daily")) { _boardTab = OnlineService.DailyBoard; OnlineService.Refresh(_boardTab); }
            GUILayout.EndHorizontal();
            if (!OnlineService.Ready) GUILayout.Label(OnlineService.Status, _label);
            else if (!OnlineService.Top.TryGetValue(_boardTab, out var rows)) GUILayout.Label("Loading…", _label);
            else if (rows.Count == 0) GUILayout.Label("No scores yet. Be the first!", _label);
            else foreach (var row in rows)
                GUILayout.Label($"{row.Rank,2}.  {row.Name}{(row.IsMe ? "  (you)" : "")}   {row.Score}", _label);
            GUILayout.Space(_label.fontSize);
            if (Button("Back")) _screen = Screen.Menu;
            GUILayout.EndArea();
        }

        private void DrawHud(Rect r)
        {
            GUILayout.BeginArea(r);
            switch (_mode)
            {
                case Mode.Level:
                    var d = _level.Definition;
                    GUILayout.Label($"Level {d.Number}   Score {_level.Seat.Board.Score} / {d.TargetScore}   Moves left {_level.MovesLeft}\n★ {d.TargetScore}   ★★ {d.TwoStarScore}   ★★★ {d.ThreeStarScore}", _label);
                    break;
                case Mode.Versus:
                    GUILayout.Label($"P1 {_versus.Seats[0].Board.Score}{(_versus.Seats[0].Out ? " (out)" : "")}   vs   P2 {_versus.Seats[1].Board.Score}{(_versus.Seats[1].Out ? " (out)" : "")}\nPlayer {_versus.Turn + 1}'s turn — pass the phone", _label);
                    break;
                case Mode.Challenge:
                    GUILayout.Label($"Challenge {ChallengeCode.Encode(_challengeSeed)}   Score {_single.Board.Score}   Combo x{_single.Board.Combo}", _label);
                    break;
                default:
                    GUILayout.Label($"{(_mode == Mode.Daily ? "Daily challenge" : "Classic")}   Score {_single.Board.Score}   Best {_best}   Combo x{_single.Board.Combo}", _label);
                    break;
            }
            if (GUILayout.Button("Menu", _button, GUILayout.Width(_button.fontSize * 6), GUILayout.Height(_button.fontSize * 2)))
            {
                _screen = Screen.Menu;
                ShowBoard(false);
            }
            GUILayout.EndArea();
        }

        private void DrawResult(Rect r)
        {
            GUILayout.BeginArea(r);
            switch (_mode)
            {
                case Mode.Level:
                    bool won = _level.State == LevelState.Won;
                    GUILayout.Label(won ? $"Level {_level.Definition.Number} complete!" : "Out of moves", _title);
                    GUILayout.Label(won
                        ? $"{new string('★', _level.Stars)}{new string('☆', 3 - _level.Stars)}   Score {_level.Seat.Board.Score}"
                        : $"Score {_level.Seat.Board.Score} — needed {_level.Definition.TargetScore}", _label);
                    if (won && _level.Definition.Number < LevelLibrary.Count && Button("Next level")) StartLevel(_level.Definition.Number + 1);
                    if (Button("Retry")) StartLevel(_level.Definition.Number);
                    if (Button("Levels")) _screen = Screen.LevelSelect;
                    break;
                case Mode.Versus:
                    int win = _versus.Winner;
                    GUILayout.Label(win < 0 ? "It's a tie!" : $"Player {win + 1} wins!", _title);
                    GUILayout.Label($"P1 {_versus.Seats[0].Board.Score}   P2 {_versus.Seats[1].Board.Score}", _label);
                    if (Button("Rematch")) StartVersus();
                    break;
                case Mode.Challenge:
                    string code = ChallengeCode.Encode(_challengeSeed);
                    GUILayout.Label($"Score {_single.Board.Score}", _title);
                    GUILayout.Label($"Challenge code: {code}\nSend it to a friend: they enter it under \"Challenge a friend\" and get exactly the same pieces. Highest score wins!", _label);
                    if (Button("Copy code")) GUIUtility.systemCopyBuffer = $"Beat my {_single.Board.Score} in Block Drop! Code: {code}";
                    if (Button("Play this code again")) StartChallenge(_challengeSeed);
                    break;
                default:
                    GUILayout.Label("Game over", _title);
                    GUILayout.Label($"Score {_single.Board.Score}   Best {_best}", _label);
                    if (Button("Play again")) StartClassic(_mode == Mode.Daily);
                    break;
            }
            if (Button("Menu")) _screen = Screen.Menu;
            GUILayout.EndArea();
        }
    }
}
