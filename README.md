# WorldEdit for Ramjet

> **Requires `Ramjet.Api` 1.0.0-preview.3 or newer, and a server that runs it.** This plugin uses the server-side region edit API (`Edit`), which is new in that version; it does not compile against or run on older versions.

Region editing for Ramjet, a Minecraft Java server: selections with a wand or coordinates, fill and replace with patterns and masks, walls and faces,
copy, cut and paste with rotation and flips, undo and redo with memory budgets, and a few movement commands. Command names, aliases and permission
nodes follow WorldEdit's public documentation, so admins and permission files port. The implementation is new: written from public documentation
only, no WorldEdit code was read or copied.

- `src/`: the plugin, a Ramjet source plugin. Deploy it as `plugins/worldedit/` (the server compiles it). `src/worldedit.csproj` exists only for an
  out-of-server compile check. Design notes: `src/DESIGN.md`.
- `src/Core/`: the pure logic (selections, parsers, byte formats, sessions). It has no dependency on the plugin API.
- `src/Mc/`: the plugin glue: commands, the wand, navigation, job watching, configuration.
- `tests/WorldEdit.Tests/`: console tests of the core (selection math, direction maths, argument splitting, pattern and mask compilers, job bytes,
  sessions).

## Commands

| Command | What it does |
|---|---|
| `//pos1 [x y z]`, `//pos2 [x y z]` | set a corner to your block or to coordinates (the wand does the same) |
| `//desel`, `//size` | clear the selection; show its size and volume |
| `//expand <n> [dir]`, `//contract <n> [dir]` | grow or shrink a side (your facing direction by default) |
| `//set <pattern>` | fill the selection |
| `//replace [mask] <pattern>` | replace blocks (existing blocks by default) |
| `//walls <pattern>`, `//faces <pattern>` (alias `//outline`) | the four sides, or all six faces, of the selection |
| `//copy`, `//cut [pattern]` | copy to the clipboard; cut also fills the selection (air by default) |
| `//paste [-a] [-o]` | paste with the copy origin on your block; `-a` skips air, `-o` pastes at the copy origin |
| `//rotate <degrees>`, `//flip [dir]` | turn or mirror the next paste (multiples of 90 degrees) |
| `/clearclipboard`, `//clearhistory` | clear the clipboard; clear your undo history |
| `//undo [n]`, `//redo [n]` | undo and redo your edits (the engine keeps the records) |
| `/up <n>`, `/ascend [n]`, `/descend [n]` | move through blocks; `/up` leaves glass under you |
| `/worldedit` (alias `/we`) | help and version |

The selection wand is `minecraft:wooden_axe` by default: left click sets the first corner, right click the second. The click is cancelled, so it
does not break or use the block.

Patterns: single blocks with states (`oak_stairs[facing=north]`), weighted lists (`50%stone,50%dirt`), and random states of a block (`*stone`).
Masks: block names, exact states, `#existing`, and `!` for negation; comma lists are unions.

Not in this version: shapes beyond the cuboid, expressions, brushes, schematics (`.schem`), `#clipboard` and `hand` patterns, `#solid`-style and
positional masks, `//hpos` (needs a raycast), `/mask` and `//gmask`, and the super pickaxe.

## Permissions

Every command has a `worldedit.*` node (listed in `src/Mc/Support.cs`). They default to operators. Grant them per group with the permissions
plugin.

## Configuration

`plugins/worldedit/data/config.json` (written with defaults on first run):

- `MaxRegionVolume`: the largest selection a command may edit, in blocks (1048576).
- `WandItem`: the selection wand item (`minecraft:wooden_axe`).

## Build and test

The engine half of the work (the bulk edit jobs and the `edit_*` host calls) is part of the Ramjet engine. The plugin needs its managed API.

```bash
dotnet run --project tests/WorldEdit.Tests -c Release
build/bin> dotnet build src/worldedit.csproj -c Release
```

The plugin builds against the public [`Ramjet.Api`](https://www.nuget.org/packages/Ramjet.Api) package (`1.0.0-preview.4` or newer) from nuget.org; no engine build is needed.

## License

MIT, see `LICENSE`.
