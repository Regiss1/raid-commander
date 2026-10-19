# Project Summary: Solo MMO Party Commander

Unity · C# · Single-player · Prototype Planning

Consolidated overview of our discussions, including the game concept, MVP, development roadmap, architecture, future modding support, and long-term direction.

## 1. Game concept and vision

The idea is to create a single-player game that recreates the experience of managing an entire MMO dungeon or raid party, using a control system inspired by real-time strategy (RTS) games.

Instead of controlling a single character as in a traditional MMORPG, the player commands multiple characters simultaneously.

The game combines three core concepts:

- MMO party mechanics: Tanks, healers, DPS, abilities, cooldowns, boss encounters, positioning, and combat roles.
- RTS-style controls: Selecting characters, issuing movement and attack orders, switching between units, and activating abilities.
- Autonomous party AI: Characters continue fighting and performing their roles when the player isn't directly commanding them.

The central gameplay challenge is deciding when to let the AI handle combat and when to intervene with direct commands.

Conceptual gameplay example

The AI maintains combat while the player changes positioning and activates key abilities to respond to boss mechanics.

### Desired player experience

During a boss encounter, the tank manages basic threat and incoming damage, the healer automatically supports injured allies, and the DPS attacks the enemy.

The player can intervene at any moment to reposition a character, activate a defensive ability, interrupt an attack, or cast a critical heal.

The objective isn't to manually perform every action for every unit. It's to coordinate a capable party under pressure.

## 2. The first prototype (MVP)

The biggest risk identified during our discussion was attempting too much before verifying that the fundamental gameplay works.

We agreed on a deliberately small starting scope.

Minimum playable encounter

MVP target

# 3

Party members

# 1

Boss

# 1

Arena

# 1

Boss mechanic

The MVP includes a tank, a healer, a DPS, and a single boss in an arena. Characters are represented by simple capsules or other placeholder models.

The controls should initially support left-click selection, right-click movement, number keys to switch party members, and a single manual ability activated with Q.

The encounter should include automatic attacks, healing, basic health and damage systems, simple autonomous AI, a telegraphed boss AoE, and victory/defeat conditions.

Target outcome: A replayable two-to-three-minute encounter where positioning, timing, AI decisions, and player commands meaningfully affect the result.

The MVP explicitly excludes networking, inventory, leveling, equipment, talent trees, multiple dungeons, elaborate graphics, and modding tools.

## 3. Prototype development roadmap

These are the seven milestones we established, in the order I'd recommend implementing them.

| Milestone           | Development goal                                                                  | Completion checkpoint                                              |
| ------------------- | --------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| 1. Foundation       | Create the Unity project, arena, camera, character selection, and basic movement. | One character can be selected and moved.                           |
| 2. Party controls   | Add the three roles, selection switching, indicators, and health bars.            | All party members can receive movement orders.                     |
| 3. Basic combat     | Implement health, damage, auto-attacks, healing, and enemy death.                 | The party can defeat a stationary enemy.                           |
| 4. Autonomous AI    | Create role-specific combat behavior using finite state machines.                 | Characters continue fighting without constant player intervention. |
| 5. Manual abilities | Add one manually triggered ability per role, with cooldowns.                      | Switching and activating abilities influences combat.              |
| 6. Boss encounter   | Implement boss attacks, a telegraphed AoE, and encounter restart.                 | The player can win or lose based on decisions and positioning.     |
| 7. Playtesting      | Evaluate controls, AI, encounter pacing, and necessary micromanagement.           | The game feels manageable, responsive, and genuinely fun.          |

### The most important checkpoint

Milestone 7 is the actual validation of the game concept.

Questions to answer include:

- Is managing three characters engaging or exhausting?
- Can players understand what each character's AI is doing?
- Does selecting and moving characters feel responsive?
- Do boss mechanics encourage meaningful intervention?
- Is the player making tactical decisions, or constantly correcting unreliable AI?

If the prototype isn't enjoyable, revise the control and automation mechanics before adding content.

## 4. Technical architecture

The chosen engine is Unity, using C#.

We discussed a lightweight, component-based architecture, organized around gameplay features rather than creating a complicated framework prematurely.

### Proposed project structure

```
PartyCommander/
├── Assets/
│   ├── _Game/
│   │   ├── Scenes/
│   │   ├── Scripts/
│   │   │   ├── Core/
│   │   │   ├── Input/
│   │   │   ├── Units/
│   │   │   ├── AI/
│   │   │   ├── Combat/
│   │   │   ├── Abilities/
│   │   │   ├── Encounters/
│   │   │   ├── UI/
│   │   │   ├── Content/
│   │   │   │   ├── Definitions/
│   │   │   │   ├── Registries/
│   │   │   │   └── Loaders/
│   │   │   └── Modding/
│   │   │       ├── Contracts/
│   │   │       ├── Loading/
│   │   │       ├── Validation/
│   │   │       └── Scripting/
│   │   ├── Data/
│   │   │   └── BaseGame/
│   │   │       ├── Units/
│   │   │       ├── Classes/
│   │   │       ├── Abilities/
│   │   │       └── Encounters/
│   │   ├── Prefabs/
│   │   ├── Input/
│   │   ├── Art/
│   │   ├── Editor/
│   │   └── Tests/
│   └── ThirdParty/
├── ModSamples/
├── Packages/
├── ProjectSettings/
└── README.md
```

This is the planned long-term structure. A lightweight initial scaffold already exists under `raid-commander-game/Assets/_Game/`; the presence of a directory does not mean its feature is implemented. See `../structure/folder.structure.md` for the current scaffold. In particular, mod loading, editor tooling, and mod samples remain future work.

### Architectural principles

- Component-based design: Separate character movement, combat, health, ability execution, and AI decisions.
- Shared command execution: Player orders and AI decisions use the same underlying character systems. Explicit commands should override autonomous actions predictably.
- Data-driven content: Separate gameplay configurations from the code that executes them.
- ScriptableObjects: Use for authoring game-owned abilities, classes, and encounter configurations.
- Finite state machines: Begin with simple, understandable AI states rather than advanced behavior frameworks.
- Version control: Use Git, preserve Unity `.meta` files, and follow consistent C# namespaces.
- Incremental development: Introduce interfaces, assemblies, or additional abstractions when they serve a concrete purpose.

The especially important architectural decision is that being selected by the player doesn't automatically disable a character's AI. The AI continues operating unless the player gives an explicit command requiring an override.

## 5. Long-term ambition: community modding

One of the most interesting ideas introduced during our discussion was eventually allowing players to build and share their own encounters and classes.

The long-term vision includes community-designed bosses, dungeon or raid encounters, custom mechanics, and potentially completely new character classes.

We identified two levels of modding.

Data-driven modding — First priority

Players create bosses, classes, abilities, and encounters using external definitions such as JSON files, combining built-in game mechanics.

Scriptable modding — Long-term goal

Advanced modders use a controlled scripting API, potentially based on Lua, to implement more original mechanics and behaviors.

### Important distinction

The core game should understand encounters independently of where their definitions originate.

For example, a built-in boss configured using a ScriptableObject and a modded boss configured using JSON should both be converted into compatible runtime definitions and executed through the same combat systems.

The proposed architecture separates:

- `Content`: Definitions, registries, validation-ready data models, and loading abstractions.
- `Modding`: External content discovery, compatibility, mod validation, and eventual scripting.
- `Combat` and `Encounters`: The systems that execute the loaded content.

The ideal result is that the game doesn't need a new C# controller for every boss created by the community.

### First modding-related checkpoint

After the original MVP works, attempt to configure its boss through an external JSON file.

The experiment should demonstrate that you can change the boss's health, ability cooldowns, and AoE radius without rebuilding the game.

This isn't a complete modding API. It's an early proof that the architecture supports externally defined content.

## 6. Further development beyond the prototype

Once the MVP is fun and technically stable, the project can evolve through broader stages.

Phase A — Polished gameplay foundation

Improve controls, character AI, pathfinding, combat feedback, skill targeting, and encounter readability. Introduce more varied boss mechanics and consider expanding the party size only after three-character gameplay works.

Phase B — Expand gameplay systems

Introduce additional classes and abilities, more sophisticated AI, threat/aggro management, multiple encounter phases, and eventually multi-boss dungeons.

Phase C — Modding foundation

Build external content loaders, a stable content schema, mod compatibility checks, a basic mod manager, documentation, and sample mods.

Phase D — Community creation tools

Develop visual encounter editors, additional modding capabilities, encounter sharing, and potentially a safe scripting API for advanced content creators.

These are directional goals, not committed features. Their priority should depend on how well the early gameplay works.

## 7. Open design questions

There are a few important decisions we haven't settled yet, and I would deliberately leave them open until the first prototype provides useful feedback.

| Question                                                                     | Why it matters                                                         |
| ---------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| How many characters should a full party contain?                             | More characters could create too much micromanagement.                 |
| Should the player control one character at a time or issue orders to groups? | Changes the entire control scheme.                                     |
| How much autonomy should the AI have?                                        | Determines whether the game feels more like an RTS or party-based MMO. |
| Should tactical pause or slowed time be supported?                           | Could make complex encounters more approachable.                       |
| Should positioning, interrupts, or healing be the primary challenge?         | Influences boss design and character abilities.                        |
| How customizable should party AI be?                                         | Could eventually become a major feature in its own right.              |

One additional idea worth exploring later is player-configurable AI priorities. For example, the player could instruct a healer to prioritize the tank, conserve an emergency ability, or avoid dangerous ground effects.

This could become an interesting strategic layer, but it shouldn't be part of the first MVP.

## 8. Immediate next steps

Foundation setup is in place: the Unity 6 project, Git ignore rules, initial `_Game` scaffold, architecture guidance, and Input System configuration. AI Navigation still needs to be installed in the editor. The first playable milestone remains: create the 3D arena and camera, add a controllable placeholder unit, implement selection and NavMesh movement, and save a playable scene. Track implementation status in `../checklists/development.checklist.md`.

&#x20;Copy checklist

## 9. Overall development philosophy

The underlying principle of this project should be:

Build a small game that proves the concept before building a large game that fulfills the vision.

The long-term dream is an extensible, community-driven MMO encounter simulator, with customizable classes, sophisticated AI, and player-created raids.

But the very first goal is considerably simpler:

A tank, a healer, and a DPS fighting one boss in one room, with a control scheme that makes managing them enjoyable.

The project should grow through completed, playable milestones. Favor simple systems that can be extended, avoid premature complexity, and let playtesting determine which ideas are worth developing.

The modding ambition is worth keeping in the architecture from the beginning, particularly through reusable gameplay systems and data-driven content. The actual modding functionality can wait.

The defining question for the entire project remains: Can managing an AI-assisted MMO party through RTS controls become a satisfying game in its own right?

If the prototype answers that question positively, you'll have a strong foundation to develop the rest of the vision.
