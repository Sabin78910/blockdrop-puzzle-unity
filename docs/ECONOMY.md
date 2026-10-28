# Late-game coin economy and the theme ladder

## Research in one paragraph
Practitioner write-ups on free-to-play economies agree on four things: every coin source needs a matching sink; prices that
must stay reachable are scaled in steps rather than linearly; surplus is allowed to be short, not permanent; and the late game
is a separate "prestige" layer, tuned to arrive at a steady pace, not an afterthought on the early prices. Sources:
[Game economy design in F2P (Machinations)](https://machinations.io/articles/game-economy-design-free-to-play-games),
[Creating Value (Kolibri Games)](https://kolibrigames.com/blog/economy-in-free-to-play-mobile-games-part-2/),
[Live Service Layered Cake II (ironSource)](https://medium.com/ironsource-levelup/live-service-layered-cake-part-ii-3d9b28a0928d).
These are practitioner sources, not data: the pacing numbers below are our own tuning and come from the simulation noted at the end.

## Is a billion-coin price sensible?
Not on today's earn rate. An active player earns about 700 coins a day, so one billion would take about 3,900 years.
It only works if income grows with the price, so the ladder ships with **Fortune**:

> every coin reward is multiplied by (price of the best bought theme you own) / 9,000, never below x1.

Fortune is the prestige layer. It needs no second currency, only cosmetics sit behind it, and it is deterministic, so the server can
compute it. Coin packs bought with money are not multiplied, so money cannot skip the ladder.

## The curve
| Tier | Themes (price) | Opens when |
|---|---|---|
| Starter | 0 to 500 | always |
| Premium | 1.2K to 2.5K | always |
| Legendary | 5K, 6.5K, 8K, 9K | always |
| Mythic | Ember 15K, Tidal 40K, Orchid 100K, Obsidian 250K | own a Legendary theme |
| Cosmic | Nebula 500K, Supernova 1M, Quasar 5M, Pulsar 25M | own a Mythic theme |
| Eternal | Aether 100M, Genesis 250M, Singularity 1B | own a Cosmic theme; Singularity also needs Genesis |
| Exclusive | earned in play | chapters and level stars (unchanged) |

Pacing for a daily player who already owns a Legendary theme (700 coins a day at x1, buying the cheapest next theme each time, simulated):
Ember after 3 weeks, Orchid (100K) after 3 months, Supernova (1M) after 6 months, Aether (100M) after 12 months, Singularity (1B) after about 14.5 months.
Between steps the player sees "about N days" on the button, so a price is a date, not a wall.

## Server authority
`CloudCode/Wallet.js` keeps the balance and the owned list of every theme costing 5,000 or more in protected Cloud Save
(players cannot write it). The phone reports lifetime earned and spent counters; the server credits them through a token bucket
(3 days of 4,000 coins times Fortune, refilled continuously), so editing a save changes nothing the server believes.
Purchases are checked exactly on the server: price, ladder rule, ownership, balance. On every sync the phone's server-sold theme bits
are replaced by the server's list, so an edited `o=` mask is undone at the next sync.

Limits, stated plainly: rewards are not replayed, so a cheater is capped at the earn rate, not stopped. Boosters and continues are
spent on the phone and reported, so under-reporting spend keeps a few coins. Theme bundles from the store are honoured from the save
(no receipt check exists yet). High themes cannot be bought offline.
