# CustomerServiceSimulator

A reengineering exercise: rebuilding the student project *Customer Service Simulator* on Godot 4.6 (C# / .NET 8).

## Status

Early development. Four minigames are implemented — `throw_paper_ball`, `fly_swatter`,
 `solitaire`, `platformer` — each with extensive gdUnit4 test coverage. There is no main scene, no
 autoloads, and no upstream game-manager yet; each minigame's `.tscn` is launched directly for
 manual testing.

## Build & run

- **Build**: `dotnet build`
- **Run**: open the project in Godot 4.6 (mono build) and run a minigame `.tscn` directly — there
  is no main scene, so launching drops you in the editor. VS Code's "Launch" config builds first.
- **Test**: `dotnet test`, or `tools/run-tests.ps1` (`tools/run-tests.sh` on POSIX), which adds an
  up-front input-sensitivity warning. Some suites simulate keyboard/mouse through the OS-global
  input state — don't touch the keyboard or mouse while a run is in progress, or tests fail
  spuriously. Set `GDUNIT_NO_INPUT_POPUP=1` for headless runs.

Requires the .NET SDK version pinned in `global.json` (8.0.411, `rollForward: feature`).  
Additional tools or extensions are packaged with the project.

## Layout

Game content lives under `assets/`, organised as `<kind>/<system>/<feature>/` (`scripts/`,
 `scenes/`, `textures/`, and `animations/` / `resources/` where needed), mirrored by `test/`.
Additional directories house dev tools, godot extensions, and other project development
 or traceability resources.

## Attributions

This project features extensive use of third party assets in the graphics department.
Any asset not developed inhouse is listed in the `docs/asset_traceability.md` file,
 along with their original author, licensing terms and any modifications made for this
 project.  
Any licensed asset is still subject to their original licensing terms regardless of this
 project's own license and terms.

## AI Disclosure

LLM or "generative AI" assistance is used for coding and debugging.  
LLMs or "AI" are never used in sourcing, producing or modifying graphics or audio assets.
