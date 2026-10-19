# Spatial Paws — Version 9 Technical Specification & Roadmap

This document outlines the software requirements, mathematical transforms, architectural changes, visual improvements, and narrative framework for **Version 9** of the Spatial Paws Runner (active scene: `Assets/Scenes/Jerboa Runner.unity`). Development is broken into phased iterations to ensure the playable loop remains testable at every stage.

---

## 1. System Architecture & Core Loop Changes

Version 9 introduces a split between a guided **Trial / Tutorial Mode** and an unguided **Standard Assessment Level**, upgrades the spatial stimulus to span the full track width with support for dynamic motion, and adds curved track projection.

```
[Level Selection Carousel] 
         │
         ▼
[Level Intro Cinematic Video]
         │
         ▼
┌────────────────────────────────────────────────────────┐
│                   GAMEPLAY MODE GATE                   │
└────────────────────────────────────────────────────────┘
         │                                      │
         ▼ (Trial Mode)                         ▼ (Real Level Mode)
[Phase 1: Full-Span Stimulus]           [Phase 2: Direct Quiz Wave]
 - Large Scale (2.5) at X = -2.10         - No Stimulus Box spawned
 - Guaranteed Intercept across 3 rails    - No Top Question Banner
 - Question & Guidance displayed          - No Instruction Prompts
         │                                - Player answers purely via
         ▼                                  spatial memory / stimulus cue
[Phase 2: Quiz & Answer Phase]                   │
 - 3 Answer Boxes (Left/Center/Right)            │
 - Shoot Destructible Panel / Dodge              │
         │                                       │
         └───────────────────┬───────────────────┘
                             │
                             ▼
              [Run Continues / Curves Turn]
              [Ambient Debris & Starfields]
                             │
                             ▼ (Corridor Finish)
             [Hangar Arrival & Takeoff Video]
```

---

## 2. Iterative Technical Specifications

### Feature 1: Full-Span Centered QuestionStimulus

#### Objective

Eliminate stimulus misses and lane-dependent token hunting. The 3D spatial relationship token must intercept Astro Bunny regardless of the lane the player occupies, while removing obsolete prompt text.

#### Spatial Coordinates & Transform Settings

* **Root Anchor Transform (`QuestionStimulus`):**
  * Position: X = -2.10, Y = 3.37, Z = 50.0 (Spawns at standard spawn depth).
  * Fixed Horizontal Lock: X is clamped to exactly `-2.10` (the physical track center). It never spawns on rails `-5.10` or `0.90`.
  * Local Scale: `Vector3(2.5f, 2.5f, 2.5f)`.

* **Span Coverage:**
  * At a uniform scale of `2.5`, a hollow box of base width 2.0 m expands to an effective visual span of 5.0 m.
  * The outer bounding box trigger covers from X = -4.60 to X = 0.40, fully enveloping all 3 rail centers (X = -5.10, -2.10, 0.90) with a generous collision margin.

* **Interaction:**
  * Box collider trigger covers the entire 3-rail width (X size ≥ 6.5 m, Y size ≥ 3.0 m, Z size ≥ 2.0 m).
  * Every run-through triggers `OnTriggerEnter` in `PlayerControllerX` regardless of player lane.

* **UI Banner Cleanup:**
  * Remove the text `"Catch the signal"` entirely from `GameHUD.cs`.
  * During stimulus approach, the top HUD banner remains completely blank or collapsed.

---

### Feature 2: Dynamic Spatial Stimuli (Motion Relational System)

#### Objective

Expand spatial cognition testing from purely static arrangements (inside, support, sagittal axis) to dynamic path-motion relations (orbiting, traversing, entering/exiting).

```
     Dynamic Path A: "Around"                Dynamic Path B: "Through"
        ┌───────────────┐                       ┌───────────────┐
   (●)──│               │                  ───► │  ===(●)===    │ ───►
    ▲   │   Gray Box    │   │                   │   Hollow Box  │
    └───│               │◄──┘                   └───────────────┘
        └───────────────┘
```

#### JSON Schema Extension (`questionsv2.json`)

Introduce a `motionType` and animation parameters within `spatialConfig`:

```json
{
  "id": 7,
  "question": "Which path did the red sphere describe relative to the container?",
  "answers": ["Around the box", "Through the box", "Over the box"],
  "correctIndex": 0,
  "category": "Path Motion",
  "difficulty": 2,
  "spatialConfig": {
    "isDynamic": true,
    "motionType": "OrbitHorizontal",
    "speed": 2.5,
    "amplitude": {"x": 2.2, "y": 0.0, "z": 1.4},
    "sphereScale": {"x": 0.6, "y": 0.6, "z": 0.6},
    "boxDimensions": {"width": 2.0, "height": 2.2, "depth": 1.5}
  }
}
```

#### Supported Motion Primitives

1. `Static`: Local position fixed per legacy coordinates.
2. `OrbitHorizontal`: Parametric ellipse in the XZ plane around the box:

   x(t) = A_x · cos(ωt),  z(t) = A_z · sin(ωt)

3. `TraverseThrough`: Linear translation along the Z or X axis passing directly through the open cage:

   z(t) = PingPong(t · speed, depth) − depth / 2

4. `OscillateVertical`: Motion over and under the box along the Y axis.

#### Implementation

* Update `StimulusSceneBuilder.cs`: If `isDynamic == true`, attach a runtime driver component (`DynamicStimulusAnimator.cs`) to the instantiated sphere.
* **HUD Box-View Handling:** The preview camera in `UIStimulusView` renders the animated sphere in real time on the render texture without freezing, preserving the motion representation on the player's HUD.

---

### Feature 3: Ground Curvature & Track Turning (Curved World Mechanics)

#### Objective

Give the sensation that the space corridor and terrain are banking, bending, and turning, while keeping Astro Bunny firmly anchored at world Z = 0 to preserve collision stability and camera framing.

#### Technical Approaches

```
                      [Curved Track Projection]
                           
                                  ___.....---=== (Spawn Z = 50, Offset X)
                       ..---===""
                 ..-═""
    [Camera] ──► [Bunny at Z=0]
```

1. **Vertex Shader Horizon Bending (Recommended for WebGL):**
   * Implement a curved-world Bending Shader on the ground tiles, boundary rails, and moving obstacles.
   * The vertex shader applies a non-linear displacement based on distance from the player camera:

     V_offset.x = V_world.x + k_x · (V_world.z − Z_player)²
     V_offset.y = V_world.y − k_y · (V_world.z − Z_player)²

   * **Advantage:** Rigidbody physics and colliders remain standard straight-axis primitives in local space, eliminating desync bugs while delivering dramatic visual turns.

2. **Procedural Segment Rotation (Transform Pivot):**
   * If physical turns are required, `TrackManager.cs` pivots oncoming ground tiles and obstacles around a dynamic curve radius R.
   * All elements tagged `Ground`, `Obstacle`, and `QuestionStimulus` inherit tangential velocity along the spline. Astro Bunny stays at (X_lane, 0, 0) with a dynamic character roll/tilt angle matching track curvature.

---

### Feature 4: Ambient Space Elements & Flying Debris

#### Objective

Increase immersion and depth perception with non-colliding visual elements (asteroids, star clusters, drifting hull panels, broken communication antennas) flying past the corridor.

#### System Design (`AmbientDebrisManager.cs`)

* **Object Pooling:** Maintain an active pool of 15–20 low-poly meshes (`MeshFilter` + `MeshRenderer`, no colliders).
* **Spawn Volume:** Spawn randomly at Z ∈ [60, 80], X ∈ [-15, 15], Y ∈ [1, 8].
* **Velocity Vectors:** Move towards Z < -20 with randomized lateral and vertical drifts:

  V = (Random(-2, 2), Random(-0.5, 0.5), -Random(12, 25))

* **Tumble & Rotation:** Constant rotational torque assigned at spawn (`transform.Rotate(spinAxis * speed * Time.deltaTime)`).
* **Research Tagging:** Tag objects as `Untagged` or `VisualDressing` so neither the player nor crystal shots register hits or miss penalties. Include an optional metadata property `elementId` in case future protocols require participants to recall passing background objects.

---

### Feature 5: Two Game Modes — Trial Mode vs. Standard Assessment

#### Objective

Allow participants to learn the spatial paradigm during a guided trial session, then transition to a clean, distraction-free assessment level without scaffolding.

| Feature / System | Mode A: Trial / Practice Mode | Mode B: Standard / Real Level Mode |
| --- | --- | --- |
| **Top Question Banner** | Visible (Shows text after accept) | **Hidden** (Banner disabled) |
| **QuestionStimulus (3D Box)** | Spawns at full span (X=-2.10) | **Disabled** (No Phase 1 token) |
| **HUD Box-View Preview** | Shows accepted configuration | **Disabled** |
| **Answer Obstacle Waves** | Spawns standard 3 panels | Spawns 3 panels directly on schedule |
| **On-Screen Instructions** | "Use A/D to steer, Space to shoot" | None |
| **Data Logging** | Logged as `session_type = "trial"` | Logged as `session_type = "assessment"` |

#### Configuration Hook

Expose an enum in the level bootstrapper:

```csharp
public enum GameMode { Trial, StandardLevel }
public static GameMode CurrentMode = GameMode.Trial;
```

---

### Feature 6: Visual Upgrade — Gas Giant Skybox & Distant Planet

* Replace the legacy HDR asset (`skybox_volcanic.hdr`) with a stylized, high-contrast Jupiter-like celestial skybox.
* Add a large, high-resolution gas giant mesh with animated cloud banding, subtle atmospheric limb glow, and a distant ring system positioned at high pitch (Y > 15, Z > 120) so it sits prominently above the track vanishing point.
* Adjust global directional lighting to cast warm amber/ochre rim lighting along Astro Bunny's silhouette and track edges.

---

### Feature 7: Natural Character Movement & Banking

#### Objective

Eliminate rigid translation across lanes. Give Astro Bunny weight, banking tilt, and directional anticipation when shifting between X = -5.10, -2.10, and 0.90.

#### Motion Dynamics (`PlayerControllerX.cs`)

* **Banking (Roll Angle):** When shifting lanes, apply roll rotation around the Z axis proportional to horizontal velocity:

  θ_z = −(dX/dt) · bankFactor  (clamped to ±18°)

* **Yaw Anticipation:** Add a brief yaw turn toward the target lane, easing back to zero upon arrival:

  θ_y = (dX/dt) · turnFactor  (clamped to ±12°)

* **BlendTree Integration:** Wire an `HorizontalSpeed` float parameter into `BunnyController` to blend the run cycle seamlessly into left-lean and right-lean animations.

---

### Feature 8: Paginated Level Selection UI (Swipeable Carousel)

#### Architecture

Replace flat menu buttons with a horizontal paging carousel:

```
   ◄ [Prev]       [ Level Card: Station Escape ]       [Next] ►
                  ┌────────────────────────────┐
                  │      [Planet Preview]      │
                  │  World 1: Station Corridor │
                  │  Status: UNLOCKED          │
                  │        [ START RUN ]       │
                  └────────────────────────────┘
                  Page Indicator: (●) (○) (○) (○)
```

* **Components:** Unity uGUI `ScrollRect` configured with horizontal snap (`HorizontalLayoutGroup` + custom snap-to-page script).
* **Card Display:** One full card per level showing environment thumbnail, character icon, target spatial typology, and high score.
* **Input:** Supports mouse drag, mobile swipe, keyboard navigation (Arrow keys / A-D), and left/right navigation arrows.

---

### Feature 9: Level Intro Cutscene Integration

* Attach a fullscreen `VideoPlayer` component attached to a Dedicated UI Canvas.
* When a level card is confirmed, the game loads the level video asset (`.mp4` / WebM for WebGL compatibility):
  * **Level 1:** Meteor shower hitting the space station, red alert sirens, Astro Bunny sprinting into the evacuation corridor.
* The cutscene can be skipped via a prompt (`Press Space to Skip`) or seamlessly transitions to live runner gameplay when the video reaches its final frame.

---

### Feature 10: Corridor Climax — Hangar Arrival & Takeoff Cutscene

#### Level End Sequence

1. **Trigger Condition:** Once all planned assessment waves for Level 1 are cleared, `TrackManager.cs` stops recycling standard corridor tiles.
2. **Hangar Tile Instantiation:** Spawn a dedicated end-cap prefab (`HangarBayEndTile.prefab`) featuring a wide open docking bay, warning lights, and Bunny's personal shuttle craft docked at the center.
3. **Player Stop Tween:** As Astro Bunny crosses into the hangar threshold (Z ≈ 20), player control locks. Astro Bunny runs toward the shuttle ramp and vanishes inside.
4. **Takeoff Sequence:** Trigger the external takeoff video or an in-engine cinematic: the shuttle engines ignite, blast doors open, and the ship escapes into space toward the gas giant.
5. **Level Clear Screen:** Display final accuracy, response times, and an advance button to the Level Map.

---

### Feature 11: Audio System Architecture

Integrate sound effects from `Assets/SoundEffects/CasualGameSounds/` and dynamic music tracks:

| Category | Audio Event | Source Asset / Trigger | Sound Characteristics |
| --- | --- | --- | --- |
| **SFX** | Crystal Blaster Fire | Space bar / `ShootBlaster()` | High-tech crystal laser ping |
| **SFX** | Correct Answer Hit | Projectile hits valid panel | Harmonious positive chime / spark burst |
| **SFX** | Wrong Panel / Wall Hit | Collision or wrong shot | Low-frequency thud / shield spark |
| **SFX** | Stimulus Intercept | Collect 3D spatial token | Sci-fi telemetry ping / data unlock |
| **SFX** | Lane Shift | Left / Right key press | Subtle air swoosh / servo dash |
| **Music** | Level 1 BGM | Scene load / runner start | Driving, rhythmic sci-fi synth-wave |
| **Music** | Menu BGM | Level select screen | Calm, atmospheric ambient space theme |

---

## 3. Narrative & Multi-Character Lore Proposal

### 3.1 Background & Inciting Incident

A specialized scientific research contingent operates aboard **Station Orestes**, orbiting the fringe gas giant **Zephyrus** (a massive storm-ringed world similar to Jupiter). The team is composed of four senior researchers:

1. **Dr. Jerry "Astro" Bunny** — Specialist in Spatial Cartography & Navigation (Jerboa/Rabbit).
2. **Prof. Kalu Monkey** — Biomechanics & Canopy Robotics (Primate).
3. **Cmdr. Barnaby Dog** — Structural Geology & Surface Operations (Canine; built on the legacy Laika lineage).
4. **Dr. Shelby Turtle** — Oceanographic & Subsurface Dynamics (Testudine).

Without warning, an uncharted, dense meteoroid swarm ruptures Station Orestes' outer shields and compromises the primary reactor. Evacuation protocols are triggered: with main docking bays sealed, each scientist must sprint to their individual single-occupancy escape craft located in separate emergency hangars and launch toward Zephyrus' habitable surface moons.

```
               [STATION ORESTES (METEOR IMPACT)]
                               │
            ┌──────────────────┼──────────────────┬──────────────────┐
            ▼                  ▼                  ▼                  ▼
     [Astro Bunny]       [Prof. Monkey]     [Cmdr. Dog]       [Dr. Turtle]
      Corridor Run        Canopy Drift       Dune Trek         Abyss Trench
     (Evacuation)         (Jungle Moon)     (Desert Moon)      (Ocean Moon)
            │                  │                  │                  │
            └──────────────────┴──────────────────┴──────────────────┘
                               │
                               ▼
        [SURFACE RENDEZVOUS BEACON & TEAM ESCAPE SHUTTLE]
```

### 3.2 Level Environments & Character Progression

#### Level 1: Station Evacuation (Astro Bunny)

* **Environment:** Metal corridors, flashing emergency lights, exposed conduit rails, escaping steam, falling bulkheads.
* **Gameplay Focus:** Containment, support, and initial dynamic path-motion relations (re-aligning emergency bypass circuits to unlock blast doors).
* **Climax:** Reaching Hangar Bay 4, boarding "The Acorn" escape craft, and launching away from the exploding station.

#### Level 2: The Bioluminescent Canopy (Prof. Kalu Monkey)

* **Environment:** Overgrown canopy moon, floating moss platforms, giant twisting roots, alien vines.
* **Gameplay Focus:** Relative heights, vertical support, and topological proximity (navigating branches and canopy gaps).

#### Level 3: The Sunken Dunes (Cmdr. Barnaby Dog)

* **Environment:** Vast desert expanse, sandstorms, ancient sandstone arches, ruined navigation pylons.
* **Gameplay Focus:** Projective axes (lateral left/right, sagittal front/back landmarks across open space).

#### Level 4: The Abyssal Trench (Dr. Shelby Turtle)

* **Environment:** Deep oceanic moon trenches, bioluminescent vents, thermal pillars, crystalline reefs.
* **Gameplay Focus:** Distance relations (near/far demonstratives, multi-depth perspective recognition).

#### Grand Finale: The Rendezvous

Having survived their individual planetary descents, the four researchers reach a planetary communications beacon, solve a collaborative spatial alignment matrix, coordinate their shuttle crafts, and chart a safe return route together.

---

## 4. Phased Iteration Roadmap

To implement these changes reliably without breaking existing game systems, execute the work in the following sequential order:

### Phase 1: Full-Span Stimulus & UI Cleanup (Features 1 & 5)

1. In `BoxSpawner.cs`, update `SpawnStimulusWave()`:
   * Fix spawn position to X = -2.10, Y = 3.37.
   * Set root local scale to `Vector3(2.5f, 2.5f, 2.5f)`.
   * Update the trigger collider width to 6.5 m to guarantee full 3-rail intercept.
2. In `GameHUD.cs`, remove `"Catch the signal"` string rendering.
3. Add `GameMode` toggle (`Trial` vs `StandardLevel`). When `StandardLevel` is active, bypass stimulus wave dispatch and hide question banners.

### Phase 2: Dynamic Stimulus System (Feature 2)

1. Add `isDynamic`, `motionType`, and motion amplitudes to `questionsv2.json`.
2. Create `DynamicStimulusAnimator.cs` with parametric `Orbit` and `Traverse` routines.
3. Update `StimulusSceneBuilder.cs` to attach the dynamic component when flagged.
4. Verify that `UIStimulusView` continues rendering smooth live animations in the HUD render texture.

### Phase 3: Character Motion Polish & Track Curvature (Features 3 & 7)

1. Update `PlayerControllerX.cs` to apply roll banking (±18°) and yaw damping during lane transitions.
2. Create and assign the `CurvedWorld` vertex displacement shader to track tiles and moving wave obstacles.
3. Verify that projectile hitboxes, raycasts, and panel colliders remain aligned during visual track bends.

### Phase 4: Ambient Atmosphere & Audio Polish (Features 4, 6, & 11)

1. Implement `AmbientDebrisManager.cs` to spawn drifting star clusters, space rocks, and metallic debris across the upper track volume.
2. Replace skybox with the high-resolution gas giant asset and configure warm directional rim lighting.
3. Wire audio events in `AudioManager.cs`: laser blasts, panel destruction, stimulus collection chime, and background synth tracks.

### Phase 5: UI Carousel, Intro Video & Hangar Climax (Features 8, 9, & 10)

1. Build the paginated `LevelSelectionCarousel` UI with horizontal drag/swipe and page indicators.
2. Wire fullscreen `VideoPlayer` intro playback prior to level startup.
3. Model/assemble `HangarBayEndTile.prefab` and integrate the stop-and-board sequence in `TrackManager.cs` to trigger the escape takeoff cutscene upon completing all assessment waves.
