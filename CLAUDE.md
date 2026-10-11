# Block Drop Puzzle
8x8 block puzzle with fair piece generation, daily challenge, levels, vs AI and online leaderboards.

## Stack
Unity 6000.6.2f1 · C# · NUnit EditMode tests · IL2CPP ARM64 · Unity Gaming Services (auth, leaderboards).

## Commands
Set `UNITY=/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity`, then:
- Test: `"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode`
- Build: `"$UNITY" -batchmode -quit -projectPath . -executeMethod BlockDrop.EditorTools.BuildScript.BuildAndroid`

## Architecture
- `Assets/Scripts/Core/` pure C# rules with no engine references: board, pieces, modes, meta, fair play,
  localisation, name filter. All unit tested.
- `Assets/Scripts/Game/` MonoBehaviours: GameController, Visuals, Feedback, Ads, OnlineService.

## Game rules
- Piece generation stays provably fair; FairPlayTests must keep passing.
- Public nicknames go through `NameFilter`; offensive names from other players render as `Player#1234`.
- Ads never interrupt play. Rewarded ads are opt in.

## Testing rules
- Every behaviour change needs an EditMode test. Unity cannot build in GitHub Actions, so run tests locally
  before pushing; CI cannot catch a compile error for you.

## Security and git
- Never commit secrets, keystores, .env files or email addresses.
- Keep the Content-Security-Policy strict; add a host to connect-src only when a feature needs it.
- Leaderboard scores are client submitted (audit finding V1): validate server side before public launch.
- `main` is protected: branch, open a PR, merge when CI is green. One issue per PR, under ~300 lines.
- Branch protection expects a `test-and-build` check that no longer runs; merge with `--admin` after local tests pass.

## Done when
Tests pass locally (60/60), short summary, PR opened.
