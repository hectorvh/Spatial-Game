# Spatial Paws — Version 9 status

Compared against `spatial-paws-v9-spec.md` and the current Jerboa Runner scripts. This file records what is already in code, what was done differently on purpose, and what is still missing.

The playable loop is still the same: catch the Phase 1 stimulus, then shoot the answer wave. Nothing below removes that.

---

## Done

### Feature 1 — Full-span centered stimulus

The token no longer hunts a rail. Any lane can intercept it.

| Spec | In code |
|---|---|
| Spawn at X = `-2.10`, Y = `3.37`, Z = `50` | `BoxSpawner.stimulusCenterX`, `stimulusSpawnY`, existing `spawnZ` |
| Stop picking a random rail | `PickNewStimulusLane()` removed. `CenterLaneIndex()` only writes a lane number into the session log |
| Scale `2.5` | Applied on a `StimulusModel` child, not the root |
| Trigger covers all three rails | Root trigger `7.0 × 8.0 × 2.4`, center `(0, −2.4, 0)`. World X span is about `−5.60 … 1.40` |
| Pickup from any lane | `IsOnStimulusRail` removed from `SpatialStimulusToken` |
| Remove `"Catch the Signal"` | String and `ShowCatchPrompt()` removed. Banner stays blank until the stimulus is accepted |

**Why the scale is not on the root.** A `2.5` scale on the trigger object would also scale the collider. The mesh is scaled on the child so the trigger stays in world meters and actually covers rails `-5.10`, `-2.10`, and `0.90` (and the scene's `1.1` lane value).

Misses still respawn after `missRespawnDelay` (1s), but always at the same center, not on a different rail.

### Feature 2 — Dynamic spatial stimuli

Static questions still work. Path-motion questions animate the red sphere.

- `SpatialConfig` now has `isDynamic`, `motionType`, `speed`, and `amplitude`.
- `DynamicStimulusAnimator.cs` drives the sphere:
  - `Static` — fixed at `sphereLocalPosition`
  - `OrbitHorizontal` — ellipse in XZ: `x = A_x·cos(ωt)`, `z = A_z·sin(ωt)`
  - `TraverseThrough` — ping-pong through the cage on the larger of `amplitude.x` / `amplitude.z`
  - `OscillateVertical` — `y = A_y·sin(ωt)` (implemented; no JSON item uses it yet)
- `StimulusSceneBuilder` attaches the animator when `isDynamic` is true, on both the on-track token and the HUD Box-View copy. The preview is rebuilt from JSON, so it has its own driver and does not freeze.
- Unknown `motionType` logs a warning and falls back to `Static`.
- `questionsv2.json` now has 7 questions. Items 1–5 are unchanged static relations. New items:
  - **id 6** — `OrbitHorizontal` (“Around the box”)
  - **id 7** — `TraverseThrough` (“Through the box”)

### Feature 5 — Game modes (HUD and logging only)

`BoxSpawner.gameMode` sets `GameModeSettings.CurrentMode` at Start. Default is `Trial`.

| | Trial | StandardLevel |
|---|---|---|
| Question banner after accept | Shown | Hidden |
| HUD Box-View | Shown | Hidden |
| Phase 1 stimulus | Spawns | **Still spawns** |
| Answer waves | 3 panels | 3 panels |
| Session JSON `sessionType` | `trial` | `assessment` |

There were no on-screen “Use A/D…” instruction strings in the current HUD, so that row of the spec is already empty in both modes.

### Feature 7 — Bunny banking (transform only)

`PlayerControllerX` now leans and turns while changing lanes.

- `lateralSpeed` = dX/dt from the lane slide in `Update`.
- Roll `θz = −(dX/dt) · bankFactor`, clamped to `±maxBankAngle` (18°). In Unity, negative Z roll tilts the top toward +X, so the bunny leans into the move.
- Yaw `θy = (dX/dt) · turnFactor`, clamped to `±maxYawAngle` (12°), toward the target lane.
- Both use `SmoothDamp` (`bankSmoothTime` 0.08 s) and ease back to 0 when the slide ends.
- Applied in `LateUpdate` as `Euler(0, yaw, roll) * startRotation`, after the Animator, so the run clip cannot overwrite it.
- Frozen while paused (`deltaTime` 0) and on game over. Reset by `HoldPositionSteady()` after a crash.

Defaults: `bankFactor` 1.2, `turnFactor` 0.8. At the default slide speed both hit their clamp during a lane change.

### Feature 4 — Ambient debris

`AmbientDebrisManager` is created by `PlayerControllerX.Start` (`Ensure()`), like the HUD and logger.

The live pool is the Meshy asteroid in `Assets/Resources/AmbientDebris/Asteroid/` (`Meshy_AI_Ironbound_Asteroid_1019212454_texture.fbx`). While that model loads, every slot is an asteroid. Hull panels, antennas, and star clusters are not spawned. Their folders exist and are empty. The old runtime meshes (icosahedron rocks, cubes, glowing tips) stay in the script and are used only if the asteroid model is missing.

- Pool of `poolSize` **6**. No colliders, `Untagged`, shadows off. Each item has `AmbientDebrisItem.elementId` (for example `Asteroid_42`) for later recall protocols.
- Fitted to **3.6 m** on the largest axis (three times the first 1.2 m import size). The size comes from the mesh bounds, not the renderer bounds, so a fresh FBX is not left at a near-zero scale.
- **Material.** The imported URP material is kept. It is forced opaque and double-sided, smoothness is set to **0**, and metallic is capped at **0.35**. The curved shader is not applied: that shader has no skybox reflections, so the metal Meshy material drew black and the rocks were invisible.
- Spawn: Z ∈ **[250, 300]**, Y ∈ [1, 8], X = corridor center (−2.10) ± [5.5, 15]. The 5.5 m keep-out means nothing flies through the lanes or the 7 m stimulus trigger.
- At the start of a run they are placed **one at a time**, the first immediately and then one every `initialPlaceInterval` (**3 s**). They are not spread along the track.
- **Speed.** Each asteroid draws two speeds from **18–70 m/s**. The higher is its speed at 300 m; the lower is its speed at the bunny (Z = 0). Between those Z values the approach speed falls in a straight line. Pairs are kept apart so no two asteroids share the same far speed or the same near speed. A new pair is drawn on each recycle. Lateral drift stays `(side · Random(0, 2), Random(−0.5, 0.5))` and always points away from the corridor.
- **Entrance.** On every appearance, including a recycle, the asteroid fades from transparent to opaque over `fadeInSeconds` (**2 s**) with a smooth step, then the material is set opaque again.
- Constant tumble around a random axis (20–90 °/s). Recycles at Z < −20.
- Freezes on pause and game over.

### Feature 11 — Audio

`AudioManager` is created by `PlayerControllerX.Start`. It persists across scene loads, uses only 2D sources, and has six SFX voices so pitch jitter on one sound does not bend another.

Clips are assigned in `Assets/Resources/AudioLibrary.asset` (`AudioLibrary` ScriptableObject). The CasualGameSounds WAVs are unlabeled, so they were matched by duration, brightness, and pitch direction:

| Event | Clip | Why | Call site |
|---|---|---|---|
| Shoot | `DM-CGS-20` | 0.10 s, bright, falling zap | `PlayerControllerX.ShootBlaster` |
| Correct hit | `DM-CGS-33` | 1.26 s, rising bright jingle | `TargetBox` correct branch |
| Wrong shot | `DM-CGS-16` | 0.18 s, low falling blip | `TargetBox` incorrect branch |
| Crash | `DM-CGS-22` | 0.22 s, low thump | `PlayerControllerX.OnCollisionEnter` |
| Stimulus collect | `DM-CGS-15` | 0.25 s, rising blip | `SpatialStimulusToken.TryCollect` |
| Lane shift | `DM-CGS-19` | 0.33 s, noisy swell | `PlayerControllerX.Update` on each lane change |

To change a sound, select `AudioLibrary.asset` and drop another clip into its slot. `sfxVolume`, `musicVolume`, and the pause and finish music levels are on the same asset.

**BGM.** The pack contains no music. `levelMusic` is empty, so `ProceduralSynthLoop` generates a seamless loop at start: 112 BPM, Am–F–C–G, saw bass, detuned pad, 16th-note arpeggio, kick, snare, and hats. Assigning a real clip to `levelMusic` replaces it. Music ducks to 35% while paused and fades to 40% on the FINISHED screen. The fades use unscaled time, so they still work while the pause menu has time stopped.

### Feature 3 — Curved-world shader (visual only)

The world looks like it drops over a horizon and sways left and right. Transforms and colliders do not move. Lanes, shots, triggers, and `TrackManager` recycling are unchanged.

- `Assets/Resources/CurvedWorldLit.shader` (`SpatialPaws/CurvedWorldLit`): opaque URP lit shader with main-light shadows, SH ambient, Blinn specular, emission, and fog. ShadowCaster, DepthOnly, and DepthNormals passes apply the same bend, so shadows and the depth prepass line up with the color pass.
- Bend, with `d = max(0, z − originZ)`: `x += kx · d²`, `y −= ky · d²`. Nothing behind the bunny bends, and nothing at the bunny's Z (d = 0) moves, which is where pickups and crashes happen.
- `CurvedWorldController` sets the globals every frame:
  - `originZ` = bunny Z.
  - `ky` = `horizonDrop` **0.0009** (half of the first 0.0018). About 0.66 m drop at the answer-wave Z of 27, and about 5.8 m at Z 80.
  - `kx` = `maxTurnStrength` **0.00125** (half of the first 0.0025) × sin(2πt / `turnPeriod` **80 s**). The 80 s period is 30% of the original 24 s rate.
  - The sway stops while paused and on game over.
- `CurvedWorld.Apply(root)` swaps opaque materials to cached curved copies. It copies the base map (`baseColorTexture`, `_BaseMap`, or `_MainTex`) and its scale/offset, color, emission, smoothness (or 1 − roughness), metallic, and `_Cull`.
- **Rail color.** The rail glTF is double-sided and its metalness is in the blue channel of `metallicRoughnessTexture`, with `metallicFactor` 1. The first conversion ignored that map, so the rails were drawn as bare metal and the baked color disappeared. The shader now samples that map (roughness in G, metalness in B). A missing map stays white, so materials that only have a metallic factor are unchanged.
- Applied to:
  - Ground tiles and rails (`TrackManager.Start`)
  - Answer waves (`BoxSpawner.SpawnAnswerWave`)
  - The stimulus (`SpawnStimulusWave`)
  - Crystal shots, including their trail (`ShootBlaster`)
- Asteroids are left on their imported material and are not bent. See Feature 4.
- TMP answer labels cannot use the shader, so `CurvedWorldFollower` moves each label to `CurvedWorld.Offset()` of its straight position every frame.
- Effects spawned at a box or token (score sparks, explosion, pickup burst) use `CurvedWorld.Bend(position)` so they appear where the object is drawn.
- The HUD Box-View preview is not bent.

---

## Done differently on purpose

These are not bugs. They are the safe first version of the spec.

1. **Standard mode does not skip Phase 1.** The spec says Standard disables the 3D stimulus and goes straight to answer panels. That changes what the quiz measures, so it was left as a later switch. Today Standard only hides the banner and Box-View.
2. **Scale is on a child, trigger is wider than 6.5 m.** Spec scale `2.5` on a 2 m box is only about 5 m of mesh. The collider is what covers the rails. Default width is `7.0` m, not `6.5`.
3. **OscillateVertical exists in code** but has no question in `questionsv2.json`.
4. **No lean BlendTree.** `BunnyController` has one run clip, so banking is a transform lean, not an animation blend. `HorizontalSpeed` is not sent to the Animator.
5. **Curve is a shader bend, not a spline.** The bunny stays at Z ≈ 0 and the track stays straight in physics. The "turns" are a sideways bend that changes sign over time.
6. **Debris does not cross the lanes.** The spec's X range is [−15, 15] around the origin. Here it is ±15 around the corridor center with a 5.5 m keep-out, so nothing passes through the bunny or the shots and nothing blocks the stimulus. The spec's four debris kinds are also not all in use: the pool is six copies of the one Meshy asteroid.
7. **Music is generated.** There is no music in the project. `ProceduralSynthLoop` stands in until a track is assigned to `AudioLibrary.levelMusic`. Menu music is not wired because there is no menu scene yet.

`descriptions/game-logic.md` was updated to match this behavior.

---

## Not play-verified

There is no Unity compile step in this workspace. First Play should confirm:

- The trigger actually overlaps the bunny on the left and right rails (height depends on the character collider vs `stimulusSpawnY` and `stimulusTriggerCenter`).
- Questions 6 and 7 read as “around” and “through” at the chosen `speed` / `amplitude`.
- The Box-View sphere keeps moving after the token is collected.
- Switching `BoxSpawner.gameMode` to `StandardLevel` hides the banner and Box-View and writes `sessionType: "assessment"`.
- **Compile.** The new scripts, the hand-written `AudioLibrary.cs.meta`, and `AudioLibrary.asset` have not been imported by Unity yet. On first open, check that `AudioLibrary.asset` shows all six clips (if a slot is empty, drag the WAV back in).
- **Shader.** `CurvedWorldLit` compiles on URP 17 and nothing turns magenta. If the console prints "CurvedWorld: shader … unavailable", everything stays flat and the game still works.
- **Banking direction.** The bunny leans toward the lane it is moving to. If the model's rig is authored mirrored, flip the sign of `bankFactor`.
- **Bend strength.** After the 50% cut, the answer wave at Z 27 is drawn about 0.66 m low and, at peak sway, about 0.9 m sideways. Shots bend by the same amount, so they still meet the boxes on screen. One full left-right-left sway takes 80 seconds.
- **Rails vs ground.** Ground tiles are 8-vertex cubes, so they bend in straight 10 m chords. Rails bend smoothly and can sit up to about 5 cm above the ground mid-tile. If that shows, replace the ground mesh with a subdivided one.
- **Materials left flat.** Transparent materials and particles are left unbent on purpose. Nothing on the track uses them today.
- **Lighting.** The curved shader only uses the main light, with no additional point or spot lights, and has no normal maps. The ground and rail materials have none, but a later bent asset that depends on them would look flatter.
- **Asteroids.** Six rocks appear one at a time between 250 m and 300 m, fade in over 2 s, and slow from their own far speed (up to 70 m/s) to their own near speed (at least 18 m/s) by the time they reach the bunny. They stay beside the lanes. Hull panels, antennas, and star clusters are not in this pass.

---

## Missing

Assets called out below are not in the project.

### Phase 3 — Character motion and curved track (Features 3 and 7)

- `HorizontalSpeed` blend into lean clips on `BunnyController`. It needs new left-lean and right-lean clips.
- Physical spline turns in `TrackManager` were deliberately not done. The shader bend replaces them.
- Randomized curve timing (`CurveChangeScheduler`: Fixed, Uniform, Gaussian, Exponential, Escalating) was requested and is **not started**. The sway is still one sine of period 80 s. It is not seeded, it is not paused for a stimulus window, and there is no `postMessage` bridge to the Next.js shell. Blocked on three decisions: which trial window blocks a change, whether the horizon drop randomizes as well as the left/right turn, and the message shape the shell already expects.

### Phase 4 — Atmosphere and audio (Features 4, 6, and 11)

- Gas-giant skybox, distant planet mesh, rings, and warm rim light. Current skyboxes are `skybox_volcanic` and `skybox_star` only.
- Hull-panel, antenna, and star-cluster models. `Assets/Resources/AmbientDebris/HullPanel`, `Antenna`, and `StarCluster` are empty. Dropping a model in `Asteroid` already replaces the procedural rocks; the other three kinds are not selected while that asteroid model is present.
- A real level track for `AudioLibrary.levelMusic`, and menu music (no menu scene yet).
- Audio settings (volume sliders) in the pause menu's Settings button, which is still a placeholder.

### Phase 5 — Menus, video, and ending (Features 8, 9, and 10)

- Level-select carousel (swipe, page dots, card per level, high score).
- Fullscreen intro `VideoPlayer` with skip. No `.mp4` / WebM in the project.
- Hangar end tile, player lock, board animation, takeoff cutscene, and a level-clear screen with accuracy and reaction times.
- `TrackManager` still only recycles the corridor. It does not stop recycling or spawn an end cap.
- The bunny stays at world Z ≈ 0 while the world scrolls, so a hangar cannot be “run toward.” It has to scroll to the player.

### Narrative and later levels (section 3 of the spec)

Design only. Not implemented and not blocked by the code above.

- Station Orestes / Zephyrus story, four researchers, Levels 2–4, rendezvous finale.
- Playable characters beyond Astro Bunny. Laika exists as an asset and is not the current player. Monkey and Turtle models are not in the project.

### Spec table rows not wired

| Spec item | Status |
|---|---|
| Standard mode skips the stimulus entirely | Not done. HUD hide only. |
| `OscillateVertical` question | Animator exists, no JSON item |
| On-screen steering hint in Trial | Never present in this HUD |
| `elementId` on debris for later recall tasks | Stored on `AmbientDebrisItem`, not logged yet |
| Lean BlendTree (`HorizontalSpeed`) | Transform lean only; no clips |
| Menu music | No menu scene |
| Randomized curve intervals (seeded, deferred during trials, logged to the shell) | Not started. Sway is a fixed 80 s sine at half the first bend strength |

---

## Suggested order for what is left

```
1. Play-verify the checklist above (compile, shader, bank sign, bend strength)
2. Optional: Standard mode skips Phase 1  ← confirm with the study design first
3. Real BGM track in AudioLibrary.levelMusic; volume sliders in Settings
4. Skybox / planet / rim light when art exists
5. Lean clips + HorizontalSpeed BlendTree (needs animation work)
6. Carousel → intro video → hangar end sequence
```

Step 2 changes the assessment. Steps 4–6 need art, animation, or a second scene.
