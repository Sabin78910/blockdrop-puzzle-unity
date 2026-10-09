# Block Drop Puzzle

8×8 block puzzle: drag pieces onto the board, clear rows and columns, chain combos. Also has a seeded **daily challenge** where everyone gets the same pieces.


Built with Unity 6 (6000.6.2f1). The game rules (`Assets/Scripts/Core`) are pure C# with no engine dependency, and they're covered by EditMode tests.

## Open
Unity Hub → Add → select this folder → open with 6000.6.2f1 → open `Assets/Scenes/Main.unity` → Play.
Code editing: VS Code with the Unity extension, or Rider.

## Command line (Mac Terminal)
```bash
UNITY="/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/results.xml
"$UNITY" -batchmode -nographics -quit -projectPath . -executeMethod BlockDrop.EditorTools.BuildScript.BuildAndroid
```

## Cloud builds (no laptop needed)
Builds run on **Unity Build Automation** (cloud.unity.com → Block Drop Puzzle → DevOps → Build Automation):
target **Default Android**, Windows Micro builder (free tier), triggered from the `main` branch.
Unity reads this repo through a read-only deploy key.
