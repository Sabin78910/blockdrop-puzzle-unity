# Block Drop Puzzle
Purpose: 8x8 block puzzle game (Unity 6, C#).

## Commands
- Test: `"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode`
- Lint: `(compile in Unity editor; no warnings)`
- Build: `"$UNITY" -batchmode -quit -projectPath . -executeMethod BlockDrop.EditorTools.BuildScript.BuildAndroid`

## Architecture
Assets/Scripts/Core — pure C# rules (noEngineReferences), Assets/Scripts/Game — MonoBehaviours, Assets/Tests/EditMode — NUnit tests

## Rules
- Read only the files you need; do not scan the whole repo.
- Every behavior change needs a test. Run tests and lint before finishing.
- No new dependencies, permissions, or signing/secrets changes without asking.
- Never commit secrets, keystores, .env files.
- Keep PRs under ~300 changed lines; one issue per PR.
- Be concise: diffs plus a 3-line summary.
- If tests still fail after 3 attempts, stop and report the blocker.

## Definition of done
Lint clean, tests pass, CI green, short summary, PR opened as draft.
