# Spatial Paws — Game Logic, Objects, and Characters

This document describes the **current playable version** of Spatial Paws (scene: `Assets/Scenes/Jerboa Runner.unity`). It is a 3-lane endless runner with a two-phase quiz: the player first intercepts a 3D spatial scene (hollow gray box + red sphere), then dodges solid answer panels and shoots the one that matches the revealed question.

---

## 1. Game overview

The player stays in place on the Z axis. The world (track tiles, quiz waves, and any spawned obstacles) moves toward the camera, which creates the feeling of running forward.

There are **three lanes**. The player can only stand in one lane at a time. Colliding with an object tagged `Obstacle` (an answer panel) knocks that box away and subtracts **50** from the score. The run does not end on a collision.

A second layer sits on top of the runner: every few seconds a **question wave** appears far ahead. The question is shown in a fixed HUD banner at the top of the screen while those panels are on the track. Shooting the panel with the correct answer destroys that wave. Shooting a wrong panel does nothing except log a miss. Running into a panel costs 50 points and knocks that panel away.

Questions are loaded from a local JSON file. Player actions are written to a local session log JSON, ready to upload to a database later.

---

## 2. Core loop

```
Start
  → load questionsv2.json
  → start session log
  → track tiles begin scrolling
  → after a delay, start trial 1
Play (per question)
  Phase 1 — Stimulus
  → HUD banner stays blank (no prompt, question text hidden)
  → one gray frame + red sphere scrolls down the track center from Z = 50,
    with a trigger wide enough to cover all three rails
  → catch it from any lane (trigger, no score change) → question accepted
  → miss (Z < -10): despawn, wait 1s, respawn at center; repeat until caught
  Phase 2 — Quiz
  → HUD shows the question
  → after 1.5s, three answer panels spawn at Z = 50
  → correct shot: +100, wave disappears
  → wrong shot: panel stays
  → physical hit with Obstacle: score -50, box flies, run continues
  → last question wave is gone: FINISHED
End
  → show final score
  → write session JSON to disk
```

### Win / lose

There is no finish line and no lives. The quiz is **one round** of `questionsv2.json` (currently 7 questions: 5 static spatial relations and 2 path-motion items). A question is not shown until its stimulus is collected. When every question has been accepted and that last answer wave is shot or has passed the bunny, the run ends: movement and shooting stop, and a **FINISHED** panel shows the final score.

### Score

Shooting the correct answer panel awards **100 points**. Colliding with a panel subtracts **50**. The score can go below zero. It is shown bottom-center as a zero-padded number (`000`, or `-050` when negative).

### HUD

`GameHUD` builds a screen-space overlay at runtime (no scene setup needed):

- **Bottom center:** rounded panel with a cyan `SCORE` caption and the current value. Same height as before, centered horizontally.
- **Bottom right:** a compact pause/play **icon** button (two bars / triangle). Tapping pause freezes the run and opens a menu that covers **80%** of the screen with **Continue**, **Settings**, and **Exit** (Settings and Exit are placeholders). Continue or the play icon resumes. Hidden when the quiz finishes.
- **Top:** a stretch banner. It stays blank during Phase 1 and shows the question from `questionsv2.json` after the stimulus is collected. It hides when the run ends, and stays hidden for the whole run in Standard mode.
- **Upper left, under the banner:** a square **Box-View** card (cyan border, pale-blue fill) previewing the accepted gray box and red sphere. Hidden until the stimulus is collected. Hidden again after a correct shot or a panel collision. Lane changes do not move it. A path-motion sphere keeps animating inside the card.
- **Center (end of quiz):** a **FINISHED** panel with the final score.

Panels use a procedurally generated 9-sliced rounded-rectangle sprite (dark fill, cyan border). The question banner uses wrapping and auto-sized text so it stays readable on PC and phone. The `CanvasScaler` uses `Expand` with a reference resolution that flips between `1920×1080` and `1080×1920`. In portrait the banner is taller with smaller side margins; in landscape it stays compact with wider side margins so it does not collide with the play view.

### Game modes

`GameModeSettings.CurrentMode` is set from the `gameMode` field on the scene's `BoxSpawner` at Start.

| | Trial (default) | StandardLevel |
|---|---|---|
| Question banner | Visible after the stimulus is accepted | Hidden all run |
| HUD Box-View | Visible after the stimulus is accepted | Hidden all run |
| Phase 1 stimulus | Spawns | Still spawns |
| Answer waves | 3 panels | 3 panels |
| `sessionType` in the log | `trial` | `assessment` |

Standard mode currently removes the on-screen scaffolding only. The spec's "no Phase 1 token at all" variant is a separate switch that is deliberately not wired yet, because dropping the stimulus changes what the answer panels are measuring.

### Difficulty (obstacle spawner)

`SpawnManager` (if present in a scene) increases world obstacle speed from `10` toward `50`, and spawns more often as speed rises (`cooldown = 20 / currentSpeed`). The current Jerboa Runner scene is driven mainly by the quiz spawner; the asteroid spawner script still exists for scenes that use it.

---

## 3. Controls

| Action | Keys |
|---|---|
| Move left one lane | `A` or Left Arrow |
| Move right one lane | `D` or Right Arrow |
| Shoot | `Space` |
| Pause / resume | Pause button (bottom right) |

Lane 0 is the leftmost rail, lane 2 the rightmost. The character cannot move past those bounds.

Default rail X positions: center `-2.10`, **same 3.0 spacing** left and right (`-5.10`, `-2.10`, `0.90`). Quiz panels use the same three X values.

---

## 4. Characters

### Astro Bunny (playable — current scene)

- **Scene object:** `BunnyRunning`
- **Controller:** `PlayerControllerX`
- **Assets:** `Assets/Characters Game/Characters_Raw/AstroBunny/`
- **Model / clips:** `BunnyCharacter`, `BunnyRun`, `BunnyRunning`, `Bunnydying`, `BunnyBackflip`, `Bunnybiped`
- **Animator:** `BunnyController`

Astro Bunny is a jerboa-like astronaut on the three rails. The character does not run in world space; the ground moves instead. A collision with an answer panel does not knock the bunny off the rails: the box flies away, the score drops by 50, and the body blinks briefly.

**Banking.** While sliding between lanes the bunny leans into the move (roll up to ±18°) and turns slightly toward the target lane (yaw up to ±12°), both proportional to sideways speed and smoothed back to upright on arrival. This is a transform lean applied after the Animator. There are no lean animation clips.

A cannon child (`Cannon`) is the muzzle for the crystal shot.

### Astro Laika (character asset, not the current scene player)

- **Prefab:** `Assets/Characters Game/Characters_Raw/Laika/AstroLaika.prefab`
- **Mesh:** `Captain_Laika`

Laika is an earlier playable astronaut dog. `SpawnManager` still looks up a GameObject named `AstroLaika`. The Jerboa Runner scene uses `BunnyRunning` instead. Laika remains in the project for reuse.

---

## 5. World and objects

### Track / ground

- **Prefab:** `Assets/Ground/Ground.prefab`
- **Scene parent:** `GroundMoving` with children `Ground1` … `Ground10`
- **Script:** `TrackManager`

Tiles keep the position, rotation, and scale from the Hierarchy. `TrackManager` only scrolls them backward and recycles the rearmost tile to the front, using the gap already present in the scene. Spawn offsets and `tileScale` are not applied at Play.

### Camera and lighting

### Curved world (visual only)

Ground, rails, answer waves, the stimulus, crystal shots, and debris are drawn with `SpatialPaws/CurvedWorldLit` (`Assets/Resources/`). The shader bends vertices by distance ahead of the bunny (`d = z − bunnyZ`): down by `horizonDrop · d²` and sideways by a slowly swaying `turn · d²`. Physics does not bend: transforms, colliders, lanes, and triggers stay on the straight track, and nothing at the bunny's Z moves. `CurvedWorldController` sets the values each frame. `CurvedWorldFollower` keeps TMP answer labels on their bent boxes, and hit or pickup effects spawn at `CurvedWorld.Bend(position)`. The HUD Box-View is not bent.

### Ambient debris

`AmbientDebrisManager` flies a pool of 18 asteroids, hull panels, antennas, and star clusters past both sides of the corridor (X = −2.10 ± 5.5–15 m, Y 1–8, Z 80 → −20), tumbling and drifting outward. No colliders, `Untagged`, no score effect. Each item has an `elementId`.

### Audio

`AudioManager` (auto-created, persists across scenes) plays 2D SFX from `Assets/Resources/AudioLibrary.asset`:

| Event | Clip |
|---|---|
| Shoot | `DM-CGS-20` |
| Correct hit | `DM-CGS-33` |
| Wrong shot | `DM-CGS-16` |
| Crash | `DM-CGS-22` |
| Stimulus collect | `DM-CGS-15` |
| Lane shift | `DM-CGS-19` |

Level music is a generated synth loop (`ProceduralSynthLoop`) unless a clip is assigned to `levelMusic`. It ducks while paused and fades down on FINISHED.

### Camera and lighting

The scene `Camera` keeps its edit-mode position and rotation when Play starts. `CameraLaneFollow` only slides it on X with the bunny (relative to that scene center). Y and Z stay as placed. A directional light lights the scene. The active skybox is `skybox_volcanic`, using `Assets/Skyboxes/SourceFiles/volcanic_planet.hdr`. `skybox_star` remains in the project.

### Answer panels (active in play)

Spawned at runtime by `BoxSpawner` as simple cubes (`AnswerBox`):

- Solid `BoxCollider` (not a trigger)
- Kinematic rigidbody (mass 10)
- Tag: `Obstacle` (contact with the bunny = −50 score)
- World-space TextMesh Pro label on the front face
- Only the panel whose text matches `questionsv2.json` → `correctIndex` can be destroyed by a shot

Scale defaults: `2.4 × 1.4 × 1.2`.

**Color and texture.** The three panels of a wave get **green, blue and purple** in random order, so each wave shows all three colors but never in the same lanes. The palette (`answerBoxColors` on `BoxSpawner`) is sampled from the game's own look: the green comes from the neon blaster materials (`Assets/Weapons/NeonRay.mat`, `Green1.mat`) and the blue from the HUD border cyan-blue.

The texture is generated once at runtime (`GetPanelTexture`) as a grayscale sci-fi panel — dark inner field, bright border band, corner bolts and faint plating lines — matching the HUD's dark-panel-with-bright-border style. Being grayscale, the palette tint (`_BaseColor`) controls the hue, and the dark inner field keeps the white answer label readable. One shared `Universal Render Pipeline/Lit` material per palette color is cached and reused, so spawning waves does not leak materials. Assign `answerBoxTexture` in the Inspector to override with an imported texture.

The legacy `YesBOX` / `NoBOX` / `SkipBOX` textures are deliberately **not** reused here: they are green UV atlases with the words "YES"/"NO"/"SKIP" baked in and authored for the original crate mesh UVs, so they would print stray lettering on a primitive cube and fight the blue and purple tints.

A whole answer wave is grouped under a `QuestionWave` parent that moves toward the player (`moveSpeed` 10). Hitting the correct panel destroys the entire wave. Waves that pass Z = `-10` are destroyed. Either event ends Phase 2 for that question so the next stimulus can spawn.

**Scene object:** `Answers` holds `BoxSpawner`. After a 2-second start delay, trials run **one at a time** (stimulus, then answers). Panels and the stimulus spawn at Z = `50`.

### Spatial stimulus (Phase 1)

Spawned at runtime by `BoxSpawner` as `QuestionStimulus`:

- Hollow gray frame (12-edge box with dark borders) and a red sphere
- Sphere local position/scale come from `spatialConfig` in `questionsv2.json` (inside, on top, in front, left of, near, …)
- Trigger `BoxCollider` (`isTrigger = true`), kinematic rigidbody
- Tag: `QuestionStimulus`
- Speed `10`, despawn at Z < `-10` if not collected
- Pickup: scale-down tween + spark burst; **no** score change, knockback, or crash
- Miss: wait 1 second, respawn the same question at the same center position

**Full-span intercept.** The token is locked to the track center (`stimulusCenterX` = `-2.10`, `stimulusSpawnY` = `3.37`) instead of picking a rail, so it can never be missed by standing in the wrong lane. The visuals live on a `StimulusModel` child scaled `2.5×`, which keeps the trigger in world units on the root: `stimulusTriggerSize` `7.0 × 8.0 × 2.4` with center offset `(0, -2.4, 0)`. That spans X `-5.60 … 1.40`, covering all three rails (`-5.10`, `-2.10`, `0.90`), and reaches down to track level. Pickup is no longer gated on the bunny's lane.

**Path motion (dynamic stimuli).** When `spatialConfig.isDynamic` is true, `StimulusSceneBuilder` attaches `DynamicStimulusAnimator` to the sphere on both the on-track token and the HUD Box-View copy:

| `motionType` | Path |
|---|---|
| `Static` | Fixed at `sphereLocalPosition` (default) |
| `OrbitHorizontal` | Ellipse in XZ: `x = A_x·cos(ωt)`, `z = A_z·sin(ωt)` |
| `TraverseThrough` | Ping-pong through the open cage along the larger of `amplitude.x` / `amplitude.z` |
| `OscillateVertical` | `y = A_y·sin(ωt)` over and under the box |

`speed` is ω, `amplitude` the per-axis extent. Both are offsets from `sphereLocalPosition`. An unknown `motionType` logs a warning and falls back to `Static`.

### Legacy Yes / No / Skip boxes (not spawned)

These prefabs are **kept in Assets** and still assigned on `BoxSpawner`, but they are not instantiated:

| Prefab | Path |
|---|---|
| Yes box | `Assets/Obstacles/BOX_yes.prefab` |
| No box | `Assets/Obstacles/BOX_no.prefab` |
| Skip box | `Assets/Obstacles/SkipBOX.prefab` |

Raw meshes: `Assets/Obstacles/Obstacles_Raw/` (`YesBOX`, `SkipBOX`, `BOXMain`, unlabeled `Box`).

### Crystal projectile (shot)

- **Prefab:** `Assets/Weapons/Cristal1 1.prefab`
- **Script:** `CrystalProjectile`
- **Tag:** `Bullet`
- **Collider:** trigger
- Speed `40`, lifetime `2` seconds
- Moves along local left (down the track from the cannon)

If it hits a `TargetBox`, the box decides whether it was a correct or incorrect answer. If it hits any other `Obstacle`, that obstacle is destroyed (asteroid-style shoot-to-clear).

Cannon visual: `Assets/Weapons/CannonPos/Cannon.prefab`, blaster mesh `Assets/Weapons/RawFiles/Blaster_Green.obj`.

### Other obstacles (scripts / older content)

`SpawnManager` can spawn a list of obstacle prefabs on a random lane at Z = `28`. Those objects should use tag `Obstacle` so they deduct score on contact. The current Jerboa Runner scene does not list asteroid prefabs in the snippet above; the quiz panels themselves are the solid obstacles the player should dodge or shoot.

---

## 6. Quiz logic

### Data file

`Assets/StreamingAssets/questionsv2.json` (legacy Solar System file `questions.json` is unused)

Each row:

| Field | Meaning |
|---|---|
| `id` | Question id (also written to the session log) |
| `question` | Text shown in the HUD **after** the stimulus is collected |
| `answers` | Exactly three strings, one per panel |
| `correctIndex` | `0`, `1`, or `2` — which answer is destructible |
| `category` | Spatial-cognition grouping (containment, support, axes, distance) |
| `difficulty` | Optional integer |
| `spatialConfig` | Local sphere position/scale and box width/height/depth for the Phase 1 token |
| `spatialConfig.isDynamic` | `true` to animate the sphere along a path |
| `spatialConfig.motionType` | `Static`, `OrbitHorizontal`, `TraverseThrough`, `OscillateVertical` |
| `spatialConfig.speed` | Angular / traverse speed (default `2.5`) |
| `spatialConfig.amplitude` | Per-axis motion extent |

`QuestionDatabase` loads this file at Start (file path or `UnityWebRequest` on WebGL), shuffles the list once, and hands each question to a trial. The list is not repeated. After the last answer wave is gone, the run ends. If `spatialConfig` is missing, the sphere pose is inferred from the correct answer text (`Inside`, `OnTop`, `InFront`, `LeftOf`, …).

### How a correct panel is chosen

1. Take `answers[correctIndex]` from JSON. That string is the only correct answer.
2. Shuffle which panel prefab/slot and which **lane** get which answer.
3. The panel that displays the correct string has `isDestructible = true`.
4. A `Bullet` trigger hitting that panel logs `shot_correct` and destroys the wave.
5. A bullet hitting any other panel logs `shot_incorrect` and leaves the wave in place.

Sample items ask where the red sphere is relative to the box (inside, on top, in front, to the left, near).

---

## 7. Session event log

`PlaySessionLogger` starts with the player. Events are kept in memory and written to:

`Application.persistentDataPath/session_logs/<sessionId>.json`

(On Linux Editor this is typically under `~/.config/unity3d/DefaultCompany/Spatial Paws Game/session_logs/`.)

The file is written when the quiz finishes, on collision (first save), and on quit (once).

| Event | Source |
|---|---|
| `lane_change` | Left/right in `PlayerControllerX` |
| `shot_fired` | Space / `ShootBlaster()` |
| `shot_correct` | Bullet + destructible `TargetBox` |
| `shot_incorrect` | Bullet + wrong `TargetBox` |
| `stimulus_spawned` | Each Phase 1 token dispatch (`questionId`, lane, attempt) |
| `stimulus_missed` | Token passed Z = -10 (`bunnyLane` at pass) |
| `stimulus_accepted` | Bunny collected the token (`attemptsTotal`, `reactionTimeMs`) |
| `question_shown` | After accept, when the HUD reveals the question |
| `crash` | Bunny collides with an `Obstacle` (score −50) |

Each session JSON has `sessionId`, `sessionType` (`trial` or `assessment`), `startedAt`, `endedAt`, `platform`, and an `events` array. Unused numeric fields are `-1`. This shape is meant to map later to a `sessions` + `events` database without changing the game’s event names.

---

## 8. Scripts (quick map)

| Script | Role |
|---|---|
| `PlayerControllerX` | Lanes, banking lean, shooting, collision penalty, session start |
| `AudioManager` / `AudioLibrary` / `ProceduralSynthLoop` | SFX voices, clip assignments, generated BGM |
| `AmbientDebrisManager` / `AmbientDebrisItem` | Collider-free background debris pool |
| `CurvedWorld` / `CurvedWorldController` / `CurvedWorldFollower` | Visual horizon bend: material swap, per-frame globals, label follow |
| `CameraLaneFollow` | Camera X follows left / center / right lane |
| `BoxSpawner` | Two-phase trials: `SpawnStimulusWave()` then `SpawnAnswerWave()` |
| `SpatialStimulusToken` | Stimulus movement, pickup trigger, miss at Z < -10 |
| `StimulusSceneBuilder` | Hollow gray frame + red sphere from `spatialConfig`; HUD preview meshes |
| `DynamicStimulusAnimator` | Parametric sphere path for `isDynamic` questions (track + HUD) |
| `GameMode` / `GameModeSettings` | Trial vs StandardLevel switch for HUD scaffolding and log tagging |
| `UIStimulusView` | Isolated orthographic render-texture Box-View on the HUD |
| `TargetBox` | Label, correct/incorrect hit |
| `QuestionWave` | Move a wave; destroy on correct hit or off-screen |
| `QuestionDatabase` / `QuestionData` | JSON quiz table and spatial config |
| `QuizText` / `FaceCamera` | World-space labels |
| `CrystalProjectile` | Shot movement and obstacle vs box handling |
| `TrackManager` | Infinite ground |
| `SpawnManager` | Timed obstacle spawn + speed ramp (optional scene) |
| `PlaySessionLogger` / `PlaySessionData` | Local event JSON |
| `GameHUD` | Score, question banner, pause, Box-View card |

---

## 9. Tags

| Tag | Used for |
|---|---|
| `Obstacle` | Crash the player; crystal can destroy non-quiz obstacles |
| `QuestionStimulus` | Phase 1 pickup token (no crash, no score change) |
| `Pickup` | Reserved alias for collectibles |
| `Bullet` | Crystal shot; `TargetBox` listens for this |
| `Ground` | (Available; jumping/grounded was used in earlier player versions) |

Layer `UI_3D_Preview` is used only by the off-screen Box-View studio. The gameplay camera does not render it.

---

## 10. Build notes

Product name: **Spatial Paws Game**.  
Play scene: **Jerboa Runner**.  
WebGL build profiles exist under `Assets/Settings/Build Profiles/`.  
Quiz JSON must stay in `StreamingAssets` so it is copied into builds and can be edited without recompiling scripts.
