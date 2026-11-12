# CyberBiker

Unity motorcycle action game. Repository: https://github.com/salohins/CyberBiker

## Gameplay architecture

See [Documentation/Architecture.md](Documentation/Architecture.md) for system
responsibilities, the assignment's OOP examples, class relationships, run states,
and the city tile connection contract.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-Architecture.ps1`
to check domain behavior, scene bindings, and gameplay in a separate validation
copy. Add `-Build` to also build a Windows development player. This leaves the
working project's open editor session alone. The validation copy, logs and player
build are under `Logs/RefactorValidation`.

In addition to steering and aiming, **Escape** pauses/resumes on desktop. Collectibles retain their original pickup effects and do not heal the player. The refactor preserves the existing serialized controller
and animation callback entry points; gameplay rules now live in smaller composed
objects and use interfaces and events for communication.

## Game audio

The sound mix and clip references are in `Assets/Resources/CyberBikerAudio.prefab`.
Tune its GameAudio sliders to save a mix, or use the runtime audio rig for temporary
Play mode adjustments. The engine uses a recorded V12 launch and a speed-responsive cruise layer; wind responds to speed and steering; nearby cars trigger directional
whooshes; weapons, impacts, explosions, collectibles and pause controls have effects.
[Audio credits and licenses](Assets/Audio/CyberBiker/CREDITS.md) cover the downloaded
recordings and Kenney effects. No generated sound clips are included.

## Local development on Windows

Use **Unity 6000.6.0f1**, revision `f7f8ed4d1e24`, as recorded in
`ProjectSettings/ProjectVersion.txt`. This local branch upgrades the original
Unity 2023.1.11f1 project to Unity 6.6. The original version remains in Git history.
Unity Hub and an active Unity license are required for the normal editor workflow.
The base editor is sufficient to work in Play mode; mobile builds need their
platform modules separately.

1. Add this repository folder to Unity Hub using **Add project from disk**.
2. Double-click **Open-CyberBiker.cmd**, or open the project with Unity 6000.6.0f1
   from Hub. Unity restores the last open scene.
3. Let Unity Package Manager restore the versions in `Packages/manifest.json`
   and `Packages/packages-lock.json` and finish importing assets.
4. Open `Assets/Level/Scenes/Menu.unity` if needed, press **Play**, then start the
   game from its menu. Click inside the Game view to give it keyboard focus.

Editor controls: **A/D** or **Left/Right arrows** steer. Hold the **right mouse
button** and move the mouse to aim; firing is automatic while aiming once the
weapon is ready. Touch controls are also implemented for mobile.

The launcher checks the editor version to avoid an accidental project migration.
For a custom editor installation, set `UNITY_EDITOR_PATH` to the matching
`Editor/Unity.exe`, or pass `-EditorPath` to `tools/Unity.ps1`.

## Verify the local setup

Close this project's Unity Editor first, then run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Unity.ps1 -Check
```

This restores packages, imports assets, and compiles scripts in batch mode for
the Windows editor workflow. It exits with an error if Unity fails. The log is
`Logs/local-check.log`. This is an import/compilation check, not a gameplay test
or a player build. Interactive editor logs are written to `Logs/editor.log`.

## Project map

- `Assets/Level/Scenes/Menu.unity`: entry scene, build index 0.
- `Assets/Level/Scenes/Level.unity`: gameplay scene, build index 1.
- `Assets/Scripts/`: player, input, camera, gameplay, AI, and world generation.
- `Assets/StoreAssets/`: bundled third-party assets and editor extensions.
- `Packages/`: Unity dependency manifest and lockfile.
- `ProjectSettings/`: shared Unity configuration, including build scene order.

Keep asset `.meta` files with their assets. `Library`, `Temp`, `Logs`, build output,
and generated IDE files are already ignored by Git. Work on a feature branch and
review `git diff` before committing; Unity can serialize asset or settings changes
when importing and editing. Package or editor upgrades should be deliberate.
