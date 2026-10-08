using System;
using System.Collections.Generic;
using BlockDrop.Core;
using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>Builds the whole game at runtime (no prefabs needed): board, 3-piece hand, drag & drop, HUD.</summary>
    public sealed class GameController : MonoBehaviour
    {
        private const int Size = 8;
        private const float Cell = 1f;
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.35f, 0.35f), new Color(0.35f, 0.7f, 0.95f), new Color(0.45f, 0.85f, 0.45f),
            new Color(0.98f, 0.78f, 0.3f), new Color(0.7f, 0.5f, 0.95f),
        };
        private static readonly Color EmptyColor = new Color(0.18f, 0.2f, 0.26f);

        private Board _board;
        private PieceGenerator _gen;
        private readonly Piece[] _hand = new Piece[3];
        private SpriteRenderer[,] _grid;
        private readonly List<GameObject>[] _handViews = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        private Sprite _square;
        private int _dragging = -1;
        private bool _daily;
        private bool _gameOver;
        private int _best;

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
            NewGame(false);
        }

        private void NewGame(bool daily)
        {
            _daily = daily; _gameOver = false;
            _board = new Board(Size);
            _gen = new PieceGenerator(daily ? PieceGenerator.DailySeed(DateTime.UtcNow) : Environment.TickCount);
            if (_grid == null) BuildGrid();
            for (int i = 0; i < 3; i++) _hand[i] = _gen.Next();
            RefreshAll();
        }

        private Vector3 CellPos(int x, int y) => new Vector3((x - (Size - 1) / 2f) * Cell, ((Size - 1) / 2f - y) * Cell, 0);

        private void BuildGrid()
        {
            _grid = new SpriteRenderer[Size, Size];
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    var go = new GameObject($"cell {x},{y}");
                    go.transform.SetParent(transform);
                    go.transform.position = CellPos(x, y);
                    go.transform.localScale = Vector3.one * (Cell * 0.92f);
                    _grid[x, y] = go.AddComponent<SpriteRenderer>();
                    _grid[x, y].sprite = _square;
                }
        }

        private Vector3 HandOrigin(int slot) => new Vector3(-5.5f + slot * 5.5f, -7.5f, 0);

        private void RefreshAll()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    _grid[x, y].color = _board.IsFilled(x, y) ? Palette[_board[x, y]] : EmptyColor;
            for (int i = 0; i < 3; i++) DrawHand(i, HandOrigin(i), 0.5f);
        }

        private void DrawHand(int slot, Vector3 origin, float scale)
        {
            foreach (var go in _handViews[slot]) Destroy(go);
            _handViews[slot].Clear();
            var p = _hand[slot];
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

        private void Update()
        {
            if (_gameOver) return;
            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
                for (int i = 0; i < 3; i++)
                    if (_hand[i] != null && Vector2.Distance(world, HandOrigin(i)) < 2.2f) _dragging = i;
            if (_dragging >= 0 && Input.GetMouseButton(0))
                DrawHand(_dragging, world + new Vector3(0, 1.5f, 0), Cell);
            if (_dragging >= 0 && Input.GetMouseButtonUp(0))
            {
                var p = _hand[_dragging];
                Vector3 drop = world + new Vector3(0, 1.5f, 0);
                int ox = Mathf.RoundToInt(drop.x / Cell + (Size - 1) / 2f - (p.Width - 1) / 2f);
                int oy = Mathf.RoundToInt((Size - 1) / 2f - drop.y / Cell - (p.Height - 1) / 2f);
                if (_board.CanPlace(p, ox, oy))
                {
                    _board.Place(p, ox, oy);
                    _hand[_dragging] = null;
                    if (_hand[0] == null && _hand[1] == null && _hand[2] == null)
                        for (int i = 0; i < 3; i++) _hand[i] = _gen.Next();
                    if (_board.Score > _best) { _best = _board.Score; PlayerPrefs.SetInt("best", _best); }
                    _gameOver = _board.IsGameOver(_hand);
                }
                _dragging = -1;
                RefreshAll();
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 30) };
            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height / 4f));
            GUILayout.Label($"{(_daily ? "Daily challenge" : "Classic")}   Score {_board.Score}   Best {_best}   Combo x{_board.Combo}", style);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New game", GUILayout.Height(style.fontSize * 2))) NewGame(false);
            if (GUILayout.Button("Daily", GUILayout.Height(style.fontSize * 2))) NewGame(true);
            GUILayout.EndHorizontal();
            if (_gameOver) GUILayout.Label("Game over — no moves left!", style);
            GUILayout.EndArea();
        }
    }
}
