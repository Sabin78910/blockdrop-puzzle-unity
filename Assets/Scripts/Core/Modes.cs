using System;
using System.Collections.Generic;
using System.Text;

namespace BlockDrop.Core
{
    /// <summary>Deals a hand of three pieces; a new hand arrives once all three are used.</summary>
    public sealed class Dealer
    {
        public const int HandSize = 3;
        private const int FairRedraws = 12;
        private PieceGenerator _gen;
        private readonly Func<Piece, bool> _fits; // null = classic random dealing
        private readonly bool _wholeHand;
        public readonly Piece[] Hand = new Piece[HandSize];

        /// <param name="fits">When given, every new hand contains at least one piece that fits ("every hand is playable").</param>
        /// <param name="wholeHand">Stricter, for levels: every piece in a new hand fits the board when it is dealt, so a
        /// piece that can never be placed cannot block the hand (stones split the board into small pockets).</param>
        public Dealer(int seed, Func<Piece, bool> fits = null, bool wholeHand = false)
        {
            _gen = new PieceGenerator(seed);
            _fits = fits;
            _wholeHand = wholeHand && fits != null;
            Deal();
        }

        private void Deal()
        {
            for (int i = 0; i < HandSize; i++) Hand[i] = _gen.Next();
            if (_fits != null) EnsurePlayable(_fits);
        }

        /// <summary>Redraws until a piece fits; as a last resort the first piece becomes a single block, which
        /// always fits because a completely full board would already have cleared its lines.</summary>
        private void EnsurePlayable(Func<Piece, bool> fits)
        {
            if (_wholeHand)
            {
                for (int i = 0; i < HandSize; i++)
                {
                    for (int t = 0; t < FairRedraws && !fits(Hand[i]); t++) Hand[i] = _gen.Next();
                    if (!fits(Hand[i])) Hand[i] = new Piece(Piece.Shapes[0], Hand[i].ColorIndex);
                }
                return;
            }
            for (int t = 0; t < FairRedraws && !Array.Exists(Hand, p => p != null && fits(p)); t++)
                for (int i = 0; i < HandSize; i++) Hand[i] = _gen.Next();
            if (!Array.Exists(Hand, p => p != null && fits(p))) Hand[0] = new Piece(Piece.Shapes[0], Hand[0]?.ColorIndex ?? 0);
        }

        /// <summary>A fresh hand guaranteed to be playable (used by "Second chance").</summary>
        public void Redeal(Func<Piece, bool> fits)
        {
            for (int i = 0; i < HandSize; i++) Hand[i] = _gen.Next();
            EnsurePlayable(fits);
        }

        public int Seed => _gen.Seed;
        public int Draws => _gen.Draws;

        /// <summary>Resumes a saved game: the same piece sequence continues after <paramref name="draws"/> pieces.</summary>
        public void Restore(int draws, Piece[] hand)
        {
            if (hand == null || hand.Length != HandSize) throw new ArgumentException("Hand needs " + HandSize + " slots");
            _gen = PieceGenerator.Resume(_gen.Seed, draws);
            Array.Copy(hand, Hand, HandSize);
        }

        public void Use(int index)
        {
            Hand[index] = null;
            foreach (var p in Hand) if (p != null) return;
            Deal();
        }
    }

    /// <summary>One player's board, hand and move count.</summary>
    public sealed class Seat
    {
        public readonly Board Board;
        public readonly Dealer Dealer;
        public int Moves { get; private set; }
        public bool Out { get; private set; }
        public int Revives { get; private set; }

        public Seat(int seed, int size = 8, bool fairHands = false, bool wholeHands = false)
        {
            Board = new Board(size);
            Dealer = new Dealer(seed, fairHands || wholeHands ? p => Board.CanPlaceAnywhere(p) : (Func<Piece, bool>)null, wholeHands);
        }

        /// <summary>Second chance after running out of moves: a fresh hand that fits the current board.</summary>
        public bool Revive()
        {
            if (!Out) return false;
            Dealer.Redeal(p => Board.CanPlaceAnywhere(p));
            Out = Board.IsGameOver(Dealer.Hand);
            Revives++;
            return !Out;
        }

        /// <summary>Resumes a saved game's counters; board and hand are restored separately.</summary>
        public void RestoreCounters(int moves, int revives)
        {
            Moves = moves;
            Revives = revives;
            Out = Board.IsGameOver(Dealer.Hand);
        }

        public bool TryPlace(int handIndex, int x, int y)
        {
            var piece = Dealer.Hand[handIndex];
            if (Out || piece == null || !Board.CanPlace(piece, x, y)) return false;
            Board.Place(piece, x, y);
            Dealer.Use(handIndex);
            Moves++;
            Out = Board.IsGameOver(Dealer.Hand);
            return true;
        }
    }

    public enum BotLevel { Easy, Medium, Hard }

    /// <summary>Greedy player: used to calibrate level targets (every level is beatable) and for hints.</summary>
    public static class Bot
    {
        /// <summary>Easy: random legal move. Medium: greedy. Hard: two-move lookahead within the hand.</summary>
        public static bool TryMove(Board board, Piece[] hand, BotLevel level, Random rng, out int handIndex, out int x, out int y)
        {
            switch (level)
            {
                case BotLevel.Easy: return TryRandomMove(board, hand, rng, out handIndex, out x, out y);
                case BotLevel.Hard: return TryLookaheadMove(board, hand, out handIndex, out x, out y);
                default: return TryBestMove(board, hand, out handIndex, out x, out y);
            }
        }

        private static bool TryRandomMove(Board board, Piece[] hand, Random rng, out int handIndex, out int x, out int y)
        {
            var moves = new List<(int, int, int)>();
            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i] == null) continue;
                for (int px = 0; px < board.Size; px++)
                    for (int py = 0; py < board.Size; py++)
                        if (board.CanPlace(hand[i], px, py)) moves.Add((i, px, py));
            }
            handIndex = x = y = -1;
            if (moves.Count == 0) return false;
            (handIndex, x, y) = moves[rng.Next(moves.Count)];
            return true;
        }

        private static bool TryLookaheadMove(Board board, Piece[] hand, out int handIndex, out int x, out int y)
        {
            handIndex = x = y = -1;
            int best = int.MinValue;
            for (int i = 0; i < hand.Length; i++)
            {
                var p = hand[i];
                if (p == null) continue;
                for (int px = 0; px < board.Size; px++)
                    for (int py = 0; py < board.Size; py++)
                    {
                        if (!board.CanPlace(p, px, py)) continue;
                        var sim = board.Clone();
                        int score = sim.Place(p, px, py).Points * 100 - TrappedCells(sim) * 8 + Contacts(board, p, px, py);
                        // best greedy follow-up with the rest of the hand
                        var rest = (Piece[])hand.Clone();
                        rest[i] = null;
                        bool any = false;
                        foreach (var q in rest) if (q != null) { any = true; break; }
                        if (any)
                        {
                            if (TryBestMove(sim, rest, out int j, out int qx, out int qy))
                            {
                                var sim2 = sim.Clone();
                                score += sim2.Place(rest[j], qx, qy).Points * 100 - TrappedCells(sim2) * 8;
                            }
                            else score -= 10000; // this move strands the rest of the hand
                        }
                        if (score > best) { best = score; handIndex = i; x = px; y = py; }
                    }
            }
            return handIndex >= 0;
        }

        public static bool TryBestMove(Board board, Piece[] hand, out int handIndex, out int x, out int y)
        {
            handIndex = x = y = -1;
            int best = int.MinValue;
            for (int i = 0; i < hand.Length; i++)
            {
                var p = hand[i];
                if (p == null) continue;
                for (int px = 0; px < board.Size; px++)
                    for (int py = 0; py < board.Size; py++)
                    {
                        if (!board.CanPlace(p, px, py)) continue;
                        var sim = board.Clone();
                        int score = sim.Place(p, px, py).Points * 100 - TrappedCells(sim) * 8 + Contacts(board, p, px, py);
                        if (score > best) { best = score; handIndex = i; x = px; y = py; }
                    }
            }
            return handIndex >= 0;
        }

        private static int TrappedCells(Board b)
        {
            int n = 0;
            for (int x = 0; x < b.Size; x++)
                for (int y = 0; y < b.Size; y++)
                    if (!b.IsFilled(x, y) && Blocked(b, x - 1, y) && Blocked(b, x + 1, y) && Blocked(b, x, y - 1) && Blocked(b, x, y + 1)) n++;
            return n;
        }

        private static bool Blocked(Board b, int x, int y) => x < 0 || y < 0 || x >= b.Size || y >= b.Size || b.IsFilled(x, y);

        private static int Contacts(Board b, Piece p, int ox, int oy)
        {
            int n = 0;
            foreach (var (cx, cy) in p.Cells)
            {
                int x = ox + cx, y = oy + cy;
                if (Blocked(b, x - 1, y)) n++;
                if (Blocked(b, x + 1, y)) n++;
                if (Blocked(b, x, y - 1)) n++;
                if (Blocked(b, x, y + 1)) n++;
            }
            return n;
        }

        /// <summary>Plays a seat greedily until moves run out or it is stuck; returns final score.</summary>
        public static int Play(Seat seat, int maxMoves)
        {
            while (!seat.Out && seat.Moves < maxMoves && TryBestMove(seat.Board, seat.Dealer.Hand, out int i, out int x, out int y))
                seat.TryPlace(i, x, y);
            return seat.Board.Score;
        }
    }

    /// <summary>Local pass-and-play for two players. Both get the identical piece sequence (same seed),
    /// take turns placing one piece, and drop out when stuck. Highest score wins.</summary>
    public sealed class VersusMatch
    {
        public readonly Seat[] Seats;
        public int Turn { get; private set; }

        public VersusMatch(int seed)
        {
            Seats = new[] { new Seat(seed), new Seat(seed) };
        }

        public Seat CurrentSeat => Seats[Turn];
        public bool Over => Seats[0].Out && Seats[1].Out;

        /// <summary>-1 while playing or on a tie, otherwise 0 or 1.</summary>
        public int Winner
        {
            get
            {
                if (!Over || Seats[0].Board.Score == Seats[1].Board.Score) return -1;
                return Seats[0].Board.Score > Seats[1].Board.Score ? 0 : 1;
            }
        }

        public bool TryPlace(int handIndex, int x, int y)
        {
            if (Over || !CurrentSeat.TryPlace(handIndex, x, y)) return false;
            int other = 1 - Turn;
            if (!Seats[other].Out) Turn = other;
            return true;
        }
    }
}
