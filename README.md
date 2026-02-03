# FNA 2.5D Isometric Factory

This is a small FNA prototype of a 2.5D isometric automation factory game. It includes miners, conveyors, smelters, storage, power, research, and a guided tutorial overlay.

## Gameplay
- Miners periodically produce Ore
- Conveyors move items along their direction
- Smelters consume Ore and output Plates
- Storage absorbs items and counts them

## Controls
- Arrow keys / WASD: pan camera
- Mouse wheel / +/-: zoom
- Left click: place current tool
- Right click: rotate tile direction
- 1: conveyor
- 2: miner
- 3: smelter
- 4: storage
- 5: splitter
- 6: merger
- 7: assembler
- 8: lab
- 9: generator
- 0: erase
- R: rotate current placement direction
- Space: pause / resume
- Esc / F1: open settings
- F2: toggle tutorial overlay

You can also use the top toolbar to pick tools and rotate the placement direction.

## Gameplay / Goals
- Each placed building costs credits. Start with 50 credits.
- Ore delivered to storage gives +1 credit, Plate gives +5 credits.
- Goals are based on total Plates stored (20/50/100/200). Completing a goal grants +25 credits.
- The game auto-pauses when the window loses focus and resumes when it regains focus.

## UI / Audio
- Bottom taskbar shows credits, plates, science, power %, goal target, research target, and event timer.
- Simple synthesized sound effects for place, rotate, error, and goal completion.
- Isometric tiles now draw elevation/shadows for a more 2.5D look.
- Hover over toolbar/taskbar icons for tooltips explaining each button and rule.

## Settings / Tutorial
- Settings: toggle grid, ore highlight, tooltips, auto-pause, SFX volume, language, tutorial
- Tutorial: step-by-step onboarding overlay with automatic progress

## Expanded Factory Systems
- Multi-stage production: Ore -> Plate (Smelter) -> Gear (Assembler) -> Science (Lab).
- Splitter/Merger for routing and combining belts (unlock via research).
- Power system: Generators produce power; machines consume power. Low power slows production.
- Research points earned by delivering Science. Research unlocks Splitter, Merger, Assembler, Generator, Fast Conveyor.
- Random events: Demand Surge increases plate value; Power Outage reduces power output.
- Resource deposits: Miners only work on ore nodes (highlighted on empty tiles).

The window title shows total stored items, current tool, direction, and pause status.

## Run
1. Install .NET 8 SDK
2. Build and run:
   - `dotnet build`
   - `dotnet run`

FNA requires native dependencies (SDL3 / FNA3D, etc.). If you hit native library errors, place the required libraries next to the output or follow the FNA documentation for setup.

## Files
- `Program.cs`: entry point
- `Game1.cs`: main loop, render, input, simulation
- `GameTypes.cs`: enums and data structures
- `InputState.cs`: input edge detection
- `Camera2D.cs`: simple camera data
