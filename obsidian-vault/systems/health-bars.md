# Reactive health bars v1.3

**Status:** implemented

## Overview

Party bars and the boss HUD share one event-driven view of `CombatUnit` health. Combat units own health; presentation reads it when notified and does not depend on combat logging.

## Behavior

- Party members have independent world-space bars above their models. A configurable world offset follows movement, the bars face the camera, and projection compensation keeps them readable during zoom.
- Party bars have a thin dark outline in their role color: blue for Tank, green for Healer, and orange for DPS. The outline surrounds the complete bar, including its missing-health region.
- One boss HUD stays at the top center of the screen. The percentage appears on the left, current / maximum health on the right, and `Boss` below the bar.
- Percentages use whole numbers. Health values use whole numbers with comma thousands separators, such as `7,500 / 10,000`.
- Damage, healing, death, maximum health changes, and resets update the fill and text together. Re-enabling a bar immediately refreshes it.
- Boss text uses a shared TextMeshPro font and black outline style, adjusted for approximately two screen pixels at the current Canvas scale. Extra letter spacing reserves room for the outlines of adjacent characters, preventing them from covering neighboring glyphs. Font size stays at least 14 screen pixels on small displays.

## Implementation

- `CombatUnit.HealthChanged` remains the shared notification. `CombatUnit` no longer holds UI references. ScriptableObject stats provide initial maximum health; `SetMaximumHealth` owns runtime cap changes, preserves current health and clamps it to the new cap, and publishes after applying the change. Changing the cap does not heal or revive a defeated unit. A zero cap is supported safely.
- `HealthBarBinding` subscribes once to its unit, initializes from current state, updates the fill and optional TMP labels, and releases its subscription on disable/destruction or rebinding. It does not poll health in `Update` or `LateUpdate`.
- `WorldHealthBar` handles world offset, camera facing and projected size only. `ScreenBossHealthBar` handles HUD layout and name only. Neither presentation calculates combat outcomes.
- Party backgrounds use a uGUI `Outline` configured from the existing `SelectableUnit.Role` by the scene setup. The 1.4 Canvas-unit outline projects to approximately one screen pixel with the existing zoom compensation; the fill remains green.
- Filled uGUI Images use a shared solid rectangular sprite with no transparent edges, and fills reach the bar edges without an inset. Full health has no black side strips; the dark background becomes visible as health is lost. A sprite is required so `fillAmount` changes rendered geometry; a sprite-less Filled Image would otherwise draw a full quad.
- `HealthBarSceneAssets` creates the shared font/material and converts existing boss text in place. TMP Essential Resources stay in `Assets/TextMesh Pro/`; first-party assets stay under `Assets/_Game/Art/UI/HealthBars/`.
- `HealthBarTextOutline` derives material width from the font atlas and projected text scale. Matching face dilation moves the outline outward and preserves the white glyph face. It adds at least four screen pixels of character spacing for the two-pixel outlines, converting to TMP's em-based spacing as the display scale changes. It releases its temporary material and restores the configured spacing when disabled.

## Source files

- `raid-commander-game/Assets/_Game/Scripts/Combat/CombatUnit.cs`
- `raid-commander-game/Assets/_Game/Scripts/Combat/WorldHealthBar.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/HealthBarBinding.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/ScreenBossHealthBar.cs`
- `raid-commander-game/Assets/_Game/Scripts/UI/HealthBarTextOutline.cs`
- `raid-commander-game/Assets/_Game/Editor/CombatSceneSetup.cs`
- `raid-commander-game/Assets/_Game/Editor/HealthBarSceneAssets.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/ReactiveHealthBarVerification.cs`
- `raid-commander-game/Assets/_Game/Tests/Editor/CombatVerification.cs`
- `raid-commander-game/Assets/_Game/Art/UI/HealthBars/`
- `raid-commander-game/Assets/_Game/Scenes/RaidArena.unity`

## Current scope

Supports three party bars and one active boss HUD. Party bars have no text. Floating numbers, resource bars, shields, animations and multiple boss HUDs remain outside this task.

The updated `RaidArena.unity` scene and shared TMP assets are configured and saved. Use **Raid Commander > Configure Combat and Logging** to reapply setup, and **Raid Commander > Verify Reactive Health Bars in Play Mode** to run combat and health bar integration checks.

Verification passed for effective damage/healing, full health, death, resets, zero maximum health, cap changes, independent units, rebinding, disable/re-enable and destruction, logging-independent updates, actual rendered fill geometry, NavMesh movement and zoom limits. Boss layout checks passed at 1280x720, 1920x1080, 1024x768, 720x1280 and 640x480. Game view sizes and camera/runtime settings are restored after verification. Unity's Console reported zero current errors.

The v1.1 letter-spacing correction was visually checked in Play Mode at 1280x720 and 640x480 with `75%`, `7,500 / 10,000` and `Boss`; adjacent characters remain distinct at both sizes.

The v1.2 rectangular fills were visually checked in Play Mode at full and partial health. Full health covers the background to both edges; partial health exposes only the missing-health region.

The v1.3 role outlines were visually checked in Play Mode on all three party bars, with partial Tank health and full Healer/DPS health. Outlines surround the complete bar independently of the fill amount.

## Revisions

| Version | Date (YYYY-MM-DD) | Summary |
|---|---|---|
| v1.3 | 2026-10-20 | Added thin dark blue, green and orange party bar outlines using the existing party roles. |
| v1.2 | 2026-10-20 | Replaced rounded button fills with a solid rectangular sprite and removed insets that exposed dark bar borders. |
| v1.1 | 2026-10-20 | Added scale-aware boss letter spacing so adjacent outlines do not cover characters. |
| v1 | 2026-10-20 | Added shared reactive health bindings, runtime maximum-health notifications, boss values and outlined TMP text, zoom-aware party presentation and integration checks. |
