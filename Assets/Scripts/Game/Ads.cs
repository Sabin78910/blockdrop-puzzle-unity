using System;

namespace BlockDrop.Game
{
    public enum AdKind { Interstitial, Rewarded }

    /// <summary>The game talks to ads only through this interface, so an ad network (for example Unity LevelPlay)
    /// can be plugged in later without touching gameplay code.</summary>
    public interface IAdProvider
    {
        /// <summary>True while rewards are given without a video (beta).</summary>
        bool RewardedIsFree { get; }
        void Show(AdKind kind, Action<bool> onDone);
    }

    /// <summary>Until an ad account exists: rewarded offers are free and interstitials are never shown.</summary>
    public sealed class BetaAdProvider : IAdProvider
    {
        public bool RewardedIsFree => true;
        public void Show(AdKind kind, Action<bool> onDone) => onDone?.Invoke(kind == AdKind.Rewarded);
    }
}
