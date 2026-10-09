using UnityEngine;

namespace BlockDrop.Game
{
    /// <summary>Procedurally synthesised sound effects + short haptic pulses. No audio files needed.
    /// Clear chimes climb a pentatonic scale with the combo count for that "one more go" feeling.</summary>
    public sealed class Feedback : MonoBehaviour
    {
        private const int Rate = 44100;
        private static readonly float[] Pentatonic = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };

        private AudioSource _src;
        private AudioClip _pick, _place, _invalid, _gameOver, _win, _star;
        private AudioClip[] _clear;

        public bool SoundOn { get; set; } = true;
        public bool HapticsOn { get; set; } = true;

        private static bool _neverTrue; // keeps Handheld.Vibrate referenced so Unity adds the VIBRATE permission
        private AndroidJavaObject _vibrator;

        private void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _pick = Tone(new[] { 1400f }, 0.035f, 0.25f, Wave.Sine);
            _place = Thock();
            _invalid = Tone(new[] { 160f, 150f }, 0.12f, 0.25f, Wave.Square);
            _star = Tone(new[] { 1318.5f, 1975.5f }, 0.22f, 0.3f, Wave.Bell);
            _gameOver = Arpeggio(new[] { 392f, 349.2f, 311.1f, 261.6f }, 0.16f, 0.3f);
            _win = Arpeggio(new[] { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, 0.1f, 0.35f);
            _clear = new AudioClip[Pentatonic.Length];
            for (int i = 0; i < _clear.Length; i++)
            {
                float root = 523.25f * Mathf.Pow(2f, Pentatonic[i] / 12f);
                _clear[i] = Arpeggio(new[] { root, root * 1.25f, root * 1.5f, root * 2f }, 0.06f, 0.32f);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch { _vibrator = null; }
#endif
            if (_neverTrue) Handheld.Vibrate();
        }

        public void Pick() { Play(_pick, 1f); Haptic(8, 60); }
        public void Place() { Play(_place, Random.Range(0.95f, 1.05f)); Haptic(15, 90); }
        public void Invalid() { Play(_invalid, 1f); Haptic(30, 40); }
        public void Star() { Play(_star, 1f); Haptic(20, 120); }
        public void GameOver() { Play(_gameOver, 1f); Haptic(60, 120); }
        public void Win() { Play(_win, 1f); Haptic(40, 200); }

        public void Clear(int lines, int combo)
        {
            int step = Mathf.Clamp(combo + lines - 1, 0, _clear.Length - 1);
            Play(_clear[step], 1f);
            Haptic(25 + lines * 15, Mathf.Clamp(120 + lines * 40, 0, 255));
        }

        private void Play(AudioClip clip, float pitch)
        {
            if (!SoundOn || clip == null) return;
            _src.pitch = pitch;
            _src.PlayOneShot(clip);
        }

        private void Haptic(long ms, int amplitude)
        {
            if (!HapticsOn || _vibrator == null) return;
            try
            {
                using var effect = new AndroidJavaClass("android.os.VibrationEffect");
                using var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, Mathf.Clamp(amplitude, 1, 255));
                _vibrator.Call("vibrate", oneShot);
            }
            catch { /* device without vibrator or API mismatch: ignore */ }
        }

        // ---------- synthesis ----------

        private enum Wave { Sine, Square, Bell }

        private static float Osc(Wave w, float phase)
        {
            switch (w)
            {
                case Wave.Square: return Mathf.Sign(Mathf.Sin(phase)) * 0.5f;
                case Wave.Bell: return Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2.76f) + 0.2f * Mathf.Sin(phase * 5.4f);
                default: return Mathf.Sin(phase);
            }
        }

        private static AudioClip Tone(float[] freqs, float seconds, float volume, Wave wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 6f / seconds);
                float s = 0;
                foreach (var f in freqs) s += Osc(wave, 2 * Mathf.PI * f * t);
                data[i] = s / freqs.Length * env * volume;
            }
            var clip = AudioClip.Create("tone", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Thock()
        {
            int n = Mathf.CeilToInt(0.11f * Rate);
            var data = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(260f, 120f, t / 0.11f);
                float body = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 38f);
                float click = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 400f) * 0.4f;
                data[i] = (body + click) * 0.45f;
            }
            var clip = AudioClip.Create("thock", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(float[] notes, float noteLen, float volume)
        {
            float tail = 0.35f;
            int n = Mathf.CeilToInt((notes.Length * noteLen + tail) * Rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * noteLen * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-t * 7f);
                    data[i] += Osc(Wave.Bell, 2 * Mathf.PI * notes[k] * t) * env * volume * 0.5f;
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("arp", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
