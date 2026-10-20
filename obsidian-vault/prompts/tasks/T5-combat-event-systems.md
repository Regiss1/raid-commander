## Objective

Implement the foundational combat systems for our Unity 6, C#, 3D top-down MMO raid simulator, including an event-driven combat logging system inspired by World of Warcraft.

The architecture must support future autonomous AI, abilities, boss mechanics, and combat statistics without introducing those systems prematurely.

Before implementing, inspect the existing project and follow its established architecture and conventions.

## 1. Combat fundamentals

Implement the following:

- Health and maximum health for every combat unit.
- Reusable damage and healing operations.
- Friendly and hostile faction identification.
- Target references for 3D units.
- Melee and ranged attack range calculations primarily on the XZ plane.
- Character rotation to face combat targets.
- Death handling that prevents defeated units from acting.
- World-space health bars above party units, readable from the top-down camera.
- Show the boss health bar in a fixed screen-space HUD at the top of the screen, with the name “Boss” below it. The HUD must not be attached to the boss character.

Use lightweight ScriptableObject definitions for configurable combat stats where appropriate.

Separate combat calculations from visual effects and presentation.

## 2. Combat event architecture

Implement a lightweight, event-driven combat architecture.

The intended flow is:

1. A combat action requests damage or healing.
2. The combat system validates and calculates the result.
3. The combat system applies the result to the affected unit.
4. The combat system publishes a resolved combat event.
5. Independent subscribers receive the event, including the combat log service.

Gameplay behavior must not depend on combat logging being enabled.

Use C# events or another simple type-safe notification mechanism already consistent with the project architecture.

Avoid introducing an elaborate global event bus or messaging framework.

## 3. Combat log data model

Implement an immutable `CombatLogEntry` containing:

FieldTypeDescriptionSequencelongEvent order within the encounterTimestampdoubleSeconds since the combat session startedTypeCombatEventTypeType of combat eventSourceIdint?Unit responsibleTargetIdint?Affected unitSpellIdint?Ability identifier, if applicableDamageint?Effective damageHealingint?Effective healingFlagsCombatLogFlagsCritical, blocked, absorbed, etc.

Use stable, encounter-scoped unit identifiers instead of storing GameObject references in log entries.

Use an implementation compatible with the project's Unity/C# version.

Define `CombatEventType` with the following values:

- SwingDamage, SwingMissed
- SpellCastStart, SpellCastSuccess, SpellCastFailed
- SpellDamage, SpellMissed, SpellHeal
- SpellPeriodicDamage, SpellPeriodicHeal
- AuraApplied, AuraRemoved
- UnitDied

Define `CombatLogFlags` as a flags enum containing None, Critical, Blocked, and Absorbed.

Only publish event types corresponding to combat behavior actually implemented. The others prepare the schema for subsequent tasks.

## 4. CombatLogService

Implement an encounter-scoped `CombatLogService`.

Responsibilities:

- Receive resolved combat events.
- Assign monotonically increasing sequence numbers.
- Assign timestamps relative to the current combat session.
- Store an ordered history of immutable log entries.
- Notify subscribers when new entries are recorded.
- Support resetting the log for a new combat session.

Maintain a bounded, configurable history, initially limited to 1,000 entries.

Expose read-only access to the recorded history.

Important constraints:

- The service must not calculate or apply damage or healing.
- Logging must never trigger gameplay actions.
- UI components must not be directly coupled to the combat implementation.
- Keep allocation and processing overhead low.
- Avoid logging every combat event through Unity Debug.Log.

## 5. Event recording rules

The log must represent what actually happened, not merely what was attempted.

Examples:

- Damage records the effective health reduction after applicable calculations.
- Healing records the effective health restored, capped at maximum health.
- A unit death generates one UnitDied event.
- A successful melee hit generates SwingDamage.
- A basic spell-based healing action generates SpellHeal.

Ensure damage events are recorded before their resulting death events.

The eventual AoE implementation must record an individual damage event for every affected target.

Do not implement armor, critical-hit calculations, shields, periodic effects, spellcasting, or AoE mechanics solely to populate log fields.

## 6. Combat log visualization

Implement a minimal combat log debug panel.

Requirements:

- Display recent events in chronological order.
- Show the event timestamp, source, target, event type, and effective amount.
- Resolve unit IDs to readable names for display.
- Use a screen-space UI appropriate for a 3D top-down game.
- Keep presentation separate from the logging service.
- Allow the debug panel to be hidden.

A polished UI and advanced filtering are not required.

## 7. Testing and debugging

Since autonomous AI and abilities are not yet implemented, provide simple debug actions that exercise the combat and logging systems.

Support:

- Applying a basic melee hit from one unit to another.
- Applying a basic heal from one unit to another.
- Killing a unit through damage.
- Resetting unit health and the combat session.

These actions must use the normal combat event pipeline rather than directly creating log entries.

## Acceptance criteria

1. Damage and healing update health correctly.
2. Healing cannot exceed maximum health.
3. Dead units cannot continue acting.
4. Appropriate resolved combat events are published.
5. The combat log records events in the correct order.
6. Each event has a valid sequence number and timestamp.
7. Log entries contain stable source and target identifiers.
8. Damage and healing values represent effective results.
9. UnitDied is recorded exactly once per death.
10. Bounded history correctly discards the oldest entries.
11. The debug combat log displays meaningful human-readable messages.
12. Combat functions correctly even without a combat log subscriber.
13. The boss health bar stays at the top of the screen independently of the boss transform, with “Boss” displayed below it.
14. Appropriate automated tests cover core combat logic and logging behavior.
15. The Unity Console contains no errors.

## Out of scope

Do not implement:

- Autonomous AI or behavior trees.
- Full ability and cooldown systems.
- Boss attack patterns.
- Threat and aggro management.
- Damage or healing meters.
- Combat history file export.
- Encounter replays.
- Advanced log filtering.
- Buff and debuff systems.

## Deliverables

Provide the C# implementation, necessary Unity prefabs and scene changes, tests, and instructions for validating the system in the Unity Editor.

After implementation, summarize the files changed, architectural decisions made, and any remaining manual setup steps.
