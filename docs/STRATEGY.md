# Block Drop: strategy to beat the category leaders (10 Oct 2026)

## The market
- Block Blast: 70M daily and 300M monthly players, 368M downloads in 2025, up to ~$584k/day almost entirely from ads.
  It runs 10,000+ A/B tests a year and uses AI bots to test versions before release.
- Puzzle retention benchmarks (GameAnalytics 2026): day 1 31.9%, day 7 12.2%, day 30 5.4%. Our targets: 40% / 15% / 7%.

## Where the leaders are weak (from player reviews)
1. Too many ads: "an ad after every level", 30-second unskippable videos (Block Blast, Woodoku).
2. Rigged pieces: players say Adventure mode deals pieces that cannot fit, to force losses and ads.

## Our positioning: "the fair block puzzle"
| Built | What it does |
|---|---|
| Every hand is playable (Classic) | A new hand always contains a piece that fits; tested with an almost-full board |
| Second Chance | Out of moves in Classic: one fresh hand that fits. Free during beta; a rewarded video later |
| Fair ad pacing (`AdPolicy`) | No ads in the first session or first 2 games of a day, never right after a new best, 3+ games and 2+ minutes between ads, max 6 a day, none with Remove Ads |
| Hit stop | A brief freeze-frame on every clear (research: hit stop is among the strongest game-feel features) |
| Already shipped | Combo streaks, mega-clear slow-mo, daily streak, missions, trophies, levels, Master AI, themes, fair levels calibrated by our bot |

## Next (needs your accounts)
- Unity LevelPlay (or Unity Ads) account, then swap `BetaAdProvider` for a real provider: rewarded Second Chance + paced interstitials.
- Play Console in-app product `remove_ads`.
- Unity Analytics + Remote Config to run A/B tests (privacy policy and Data safety updated first).

## Next (no accounts needed)
- Picture collection "Journey" mode (like Block Blast Adventure, but with fair pieces).
- Weekly events and seasonal themes.
- Languages for the top growth markets: Spanish, Portuguese (Brazil, Mexico are Block Blast's growth engines), Hindi, Nepali.

## Sources
pocketgamer.biz (70m DAU) · businesswire.com 2026-01-08 (10,000 experiments) · eu.36kr.com (ad revenue) ·
kimola.com and justuseapp.com review analyses · gameindustrylibrary.com GameAnalytics retention benchmarks 2026 ·
unity.com interstitial best practices · pangleglobal.com rewarded placement · AIIDE "impact feel" study; GDC "Juice it or lose it"
