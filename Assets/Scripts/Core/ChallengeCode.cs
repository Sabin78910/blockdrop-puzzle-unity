namespace BlockDrop.Core
{
    /// <summary>Turns a game seed into a short code a friend can type (e.g. "K7Q2MX") so both
    /// players get the exact same pieces. No server needed: works offline.</summary>
    public static class ChallengeCode
    {
        // 32 symbols without look-alikes (no 0/O, 1/I)
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int Length = 6;
        public const int MaxSeed = 1 << 30; // 6 symbols × 5 bits

        public static string Encode(int seed)
        {
            uint v = (uint)seed % MaxSeed;
            var chars = new char[Length];
            for (int i = Length - 1; i >= 0; i--) { chars[i] = Alphabet[(int)(v & 31)]; v >>= 5; }
            return new string(chars);
        }

        public static bool TryDecode(string code, out int seed)
        {
            seed = 0;
            if (code == null) return false;
            code = code.Trim().ToUpperInvariant();
            if (code.Length != Length) return false;
            int v = 0;
            foreach (char c in code)
            {
                int d = Alphabet.IndexOf(c);
                if (d < 0) return false;
                v = (v << 5) | d;
            }
            seed = v;
            return true;
        }

        public static int NewSeed(System.Random rng) => rng.Next(MaxSeed);
    }
}
