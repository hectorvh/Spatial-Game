# Feature 12: Reflective Blue Rail Separation Lines (Lane Dividers)

## 1. Objective & Cognitive Function

In a 3-lane endless runner, participants must quickly estimate whether Astro Bunny is centered within a lane or in the middle of a lane transition. Adding continuous, high-contrast separation lines bisecting the three rails provides clear lane delineation:

* **Research Accessibility:** Eliminates lane ambiguity for participants across different age cohorts, providing immediate peripheral cues for lane centering without cluttering the upper play area.
* **Visual Aesthetic:** Reinforces the high-tech sci-fi corridor palette with reflective, neon-tinted boundary markers that move in lockstep with the scrolling ground tiles.

---

## 2. Spatial Math & Transform Placement

The runner operates with three parallel rails anchored at:

* Lane 0 (Left): X = -5.10
* Lane 1 (Center): X = -2.10
* Lane 2 (Right): X = 0.90

The separation lines must sit exactly midway between the lanes to clearly divide the corridor:

Divider_Left-Center = (-5.10 + (-2.10)) / 2 = **-3.60**

Divider_Center-Right = (-2.10 + 0.90) / 2 = **-0.60**

```
   Lane 0 (Left)          Lane 1 (Center)         Lane 2 (Right)
     X = -5.10               X = -2.10               X = 0.90
        │                       │                       │
        │   ◄── 3.0m ──►        │   ◄── 3.0m ──►        │
        │                       │                       │
        │       [LINE 1]        │       [LINE 2]        │
        │      X = -3.60        │      X = -0.60        │
        │     (Reflective       │     (Reflective       │
        │        Blue)          │        Blue)          │
        ▼           ▼           ▼           ▼           ▼
════════╪═══════════│═══════════╪═══════════│═══════════╪════════
     Rail 0                  Rail 1                  Rail 2
```

* **Y-Axis Offset:** Y = 0.015 m (slightly elevated above the ground mesh to prevent Z-fighting with track geometry).
* **Z-Axis Dimension:** Exactly matches the length of one ground tile segment (e.g., 10.0 m or 12.0 m) so lines seamlessly tile and loop without visible seams.
* **Width:** 0.08 m to 0.12 m (thin enough to not compete with obstacles, but thick enough to be readable at speed).

---

## 3. Material & Shader Specifications

The material must produce a polished, metallic sci-fi finish that catches scene highlights and ambient rim reflections.

| Property | Value / Setting | Technical Purpose |
| --- | --- | --- |
| Shader | Universal Render Pipeline/Lit | Standard PBR rendering compatible with WebGL |
| Base Color (`_BaseColor`) | `#0088FF` or `#00B4D8` (Cyan-Blue) | High-saturation reflective blue matching HUD border accents |
| Metallic (`_Metallic`) | 0.85 – 0.90 | Simulates polished metallic ground inlay |
| Smoothness (`_Smoothness`) | 0.88 – 0.95 | Sharp specular highlights reflecting overhead corridor lighting |
| Emission (`_EmissionColor`) | Active: `#004488` (Intensity: 0.4–0.6) | Subtle electric glow visible against dark ground tiles in deep space |
| Bending Compatibility | CurvedWorld vertex displacement | Must share the curved vertex displacement shader used by the track |

---

## 4. Hierarchy & Prefab Integration

To avoid adding CPU tracking overhead or new manager scripts, the rail divider lines are parented directly to the looping track prefab:

```
Ground.prefab (or GroundMoving children Ground1...Ground10)
  ├── FloorMesh (Existing corridor ground tile)
  ├── BoundaryRail_Left (Modular side wall / conduits)
  ├── BoundaryRail_Right (Modular side wall / conduits)
  ├── RailDivider_Left
  │     ├── Transform: Pos(-3.60, 0.015, 0), Scale(0.1, 1.0, TileLength)
  │     └── MeshRenderer: Mat_ReflectiveBlue_Divider
  └── RailDivider_Right
        ├── Transform: Pos(-0.60, 0.015, 0), Scale(0.1, 1.0, TileLength)
        └── MeshRenderer: Mat_ReflectiveBlue_Divider
```

* **Recycling Behavior:** Because the divider strips are children of `Ground.prefab`, `TrackManager.cs` automatically scrolls and recycles them along the Z axis without requiring code modifications.
* **Physics & Colliders:** No colliders attached. Layer set to `Default` or `VisualDressing` to guarantee crystal projectiles and player hitboxes pass unobstructed.

---

## 5. Updated Iteration Roadmap Placement

Incorporate this change into Iteration 4 alongside ambient dressings and visual upgrades:

### Iteration 4: Visual Polish, Debris, Rail Dividers & Audio

1. Create `Mat_ReflectiveBlue_Divider` (URP Lit, Metallic: 0.85, Smoothness: 0.90, Base Color: `#0088FF`, subtle emission).
2. Open `Ground.prefab` and instantiate two quad/cube strips at local X = -3.60 and X = -0.60, scaled to span the tile length.
3. Ensure the divider material shares the vertex curve displacement shader if Feature 3 (curved tracks) is active.
4. Implement `AmbientDebrisManager.cs` for drifting meteoroids and background hull plating.
5. Replace skybox with the gas giant texture and wire audio events in `AudioManager.cs`.
