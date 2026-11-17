# Run progression

## Two play modes

Press **Play** in the main menu, then choose:

- **Progression:** the existing five-minute sequence below, with riding breaks and escalating rival encounters.
- **Constant Combat:** the original Difficulty 4 traffic/riding setup, continuously filling and maintaining a five-rider pack. Opening riders and replacements use the original smooth arrivals: some overtake from behind, others approach from farther ahead, with the existing fade and engine/whoosh audio. Defeated riders are replaced as soon as another safe arrival can spawn; it does not wait for an entire wave, stage boundary or wave cooldown. The existing fighter/queue roles and navigation remain in use.

The choice survives Restart and returning to the menu within the current app/play session. A fresh session defaults to Progression. In the game, **TUNE → PLAY MODE** also offers both choices. Selecting another mode and applying restarts the run; Cancel keeps the current mode. The mode is separate from saved movement/combat tuning, so old device settings cannot override a menu choice.

For direct Level play in the Editor, use **GameplayManager → Difficulty Manager → Start Mode**. The **Constant Combat Difficulty** reference points to the preserved Difficulty 4 prefab. The shared **Assets/Resources/RunModes.asset** also references that same preset, so an already-open scene missing the new field can resolve the actual combat setup instead of silently switching to Progression. Its **Max Enemies On Road** is five. Initial Delay defaults to **2 gameplay seconds**, followed by **0.5 seconds** between arrivals, using the existing front/rear introduction distances and fade. The original traffic settings remain **Car Offset Modifier 25**, with **1/2/3-car row weights 2/4/1**.

## Progression timeline

The Level scene now uses an eight-stage, five-minute opening instead of a permanent Difficulty 4.

| Run time | Stage | Enemies | Traffic row weights (1 / 2 / 3 cars) | Added row spacing |
| --- | --- | --- | --- | --- |
| 0:00–0:40 | Warmup Ride | None | 8 / 2 / 0 | 100 |
| 0:40–1:10 | First Rival | One wave of 1 | 7 / 3 / 0 | 90 |
| 1:10–1:40 | Open Road | None | 6 / 3 / 1 | 80 |
| 1:40–2:20 | Rival Pair | One wave of 2 | 5 / 4 / 1 | 70 |
| 2:20–2:50 | Breathing Room | None | 5 / 4 / 1 | 65 |
| 2:50–3:35 | Traffic Chase | One wave of 2 | 4 / 4 / 2 | 55 |
| 3:35–4:00 | Riding Break | None | 4 / 4 / 2 | 50 |
| 4:00–5:00 | Rival Pack | One wave of 3, at most 2 visible | 3 / 5 / 2 | 45 |

After five minutes the last two stages repeat: 25 seconds riding, 60 seconds for a three-rider wave. Each combat stage starts at most one wave, so quickly defeating it earns extra riding time. One enemy fights at a time. Arrivals and waiting riders use the existing encounter system.

These are gameplay seconds: pause freezes the timer, and combat slow motion slows it. Existing enemies finish naturally during riding breaks; new and pending spawns stop, and enemies are never deleted in front of the player. A slow fight may extend into the next riding section. Existing traffic rows remain in place and new rows gradually adopt the next stage's density.

## Tuning in Unity

Select **GameplayManager → Difficulty Manager** in the Level scene. The Inspector shows the ordered sections, a readable timeline and the live current stage/time remaining during Play. **Loop From Section = 6** is the zero-based index of Riding Break; -1 holds the last difficulty instead.

Open a referenced prefab in **Assets/Level/Prefabs/Difficulties/Run Progression** to tune:

- **Duration Seconds:** time before the next stage. Zero uses Game Duration divided evenly between sections, preserving older setups.
- **Enemies Per Wave:** zero makes a riding stage.
- **Max Waves Per Section:** 1 gives one burst, 0 allows repeating waves.
- **Max Enemies On Road:** fighter plus waiting/arriving riders.
- **Enemy Initial Delay / Spawn Interval / Wave Cooldown / Handoff Delay:** arrival and encounter pacing.
- **Car Offset Modifier:** additional longitudinal spacing; the original turn-angle route spacing remains in effect.
- **One / Two / Three Car Row:** weighted traffic density.

The old Difficulty 0–4 prefabs are preserved. Car sizes, lane positions, steering, damage and speed tuning are unchanged by this progression setup. The authored test enemy is inactive so the opening really starts without combat.

## Gap assist

Gap assist is enabled only while the real **Launch** state is active. Regular riding does not snap or lock steering. Cancelling or leaving Launch cancels the assist, and sword finishers/collisions retain priority. The assist adds no extra forward boost; Launch keeps its existing speed behavior. All cars remain solid.

The **Gap Dash Lab** now has a **Launch** preview toggle. Its preview uses the real Launch state while holding the selected preview speed, so alignment can be tuned independently of Launch's speed multiplier.
