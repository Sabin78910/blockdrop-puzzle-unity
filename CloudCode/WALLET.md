# Wallet: server-owned coins and expensive themes

`Wallet.js` decides the coin balance and who owns Legendary, Mythic, Cosmic and Eternal themes (price 5,000 and up). Design,
curve and limits: `docs/ECONOMY.md`.

## Calling it
`CallEndpoint("Wallet", { op: "sync" | "buy", earned, spent, theme?, legacyOwned? })` with the phone's lifetime counters
(`PlayerMeta.EarnedTotal`, `SpentOther`). Reply `{ ok, reason, balance, owned:[names], fortune }`. `WalletSync.cs` does this and
hands the answer to `PlayerMeta.ApplyServer`.

## Deploy (leo or the service account)
1. Cloud Code: new script `Wallet`, paste `CloudCode/Wallet.js`, Publish (or `ugs deploy CloudCode/Wallet.js`).
2. Ship the build that calls it. Until the script is published, buying a Legendary or higher theme shows "Purchase failed, try again".
3. Test: `node --test CloudCode/Wallet.test.js` (also checks the catalog against `Meta.cs`).

## Not verified
The rules are tested locally. The Cloud Save calls (`getProtectedItems`, `setProtectedItem` with `writeLock`) and the live
script have not run against Unity Cloud, so make one real sync and one real buy after publishing.
