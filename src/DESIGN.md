# WorldEdit: design

Status: first version. The plugin is built and its core tests pass; the engine side (bulk edit jobs) is written and awaits the first engine build.
Scope: the first phase of WorldEdit's public feature list, with the engine's edit jobs as the bulk path (clean-room).

## Where the work runs

- The worker (this plugin) parses commands, compiles patterns and masks into binary programs, resolves selections, and keeps sessions: the selection,
  the clipboard handle, the paste transform. It never reads or writes blocks in bulk.
- The engine runs every bulk change as a job: one request per command, validated in full, applied in tick slices with a budget,
  recorded for undo, and polled by the plugin. A 1,000,000-block `//set` is one host call plus polls, not a million calls.
- Walking commands (`/up`, `/ascend`, `/descend`) read a handful of blocks in one column and teleport once.

## Layout

- `src/Core/`: pure logic. `Geometry` (points, boxes, directions), `Selection` (corners and the face moves), `CommandArgs` (flags and quotes),
  `Compilers` (patterns and masks to WEPT and WEMK), `Wire` (the WEJB request writer), `Session` (per-player state, the sweep), `Result`.
- `src/Mc/`: `WorldEditPlugin` (enable, config, sessions, permissions), `CommandSet` (command trees), `Edits` (operations), `Jobs` (`JobWatch`, the
  poll), `Wand` (interaction event), `Moves` (navigation), `Support` (permission nodes, block resolver, conversions, refusal messages).
- `tests/WorldEdit.Tests/`: compiles `src/Core` directly; a small runner, no packages.

## Decisions

- **Requests are validated twice.** The worker's compilers catch the user's mistakes with messages; the engine's decoder assumes nothing and refuses
  anything malformed (sizes, ids, counts, coordinates beyond the world border). The worker is untrusted, so the engine's check is the real one.
- **Block-type nodes carry a state id.** A pattern's `*stone` and a mask's block list name the block by any of its states. The worker resolves names
  with the engine's own parser (`BlockState.TryParse`), so no block-id table crosses the boundary.
- **Clipboards are owned by the engine.** The worker keeps a handle, the copy origin and the world. Block data never reaches the worker. A handle is
  owned by one player: other players get "unknown".
- **Undo records are per section.** A job records the cells it changed (a 4096-bit mask per section and the before and after states). Undo writes the
  before states, redo the after states, and each undo records its own reverse. A record over the owner's budget is not kept: the stacks are cleared
  rather than leaving a gap that a later undo would restore on top of.
- **Paste transforms are index maps.** Flips and quarter turns are applied per cell by the engine, from the clipboard's size and origin. The worker
  only sends the transform.
- **Sessions outlive the connection by ten minutes.** The selection survives a quick reconnect; the engine keeps history and clipboards for the same
  time after the owner leaves.
- **Permissions default to operators.** The node names match WorldEdit's documentation; the defaults are an admin choice that the config can change
  later.

## Deviations from the original plan (phase 1)

- Clipboards are dense (one state per cell) rather than palette sections. The per-player budget bounds them either way.
- No section fast path yet: a fill writes cell by cell inside the tick budget. A million-block fill takes many ticks, as designed, but is slower than
  the target.
- Client updates use the same flags as `/fill` (per-block updates); section batching is later work.
- The engine's edit budgets are the documented defaults in code; their server config keys are not read yet.
- `//pos1 x y z` takes space-separated coordinates (the vanilla block position argument), not `x,y,z`.
- `/up` and the other navigation commands are not wand-based yet (the navigation wand needs the raycast, which is not in the engine).
- Expressions, brushes, schematics, shapes beyond cuboids, `#clipboard` and `hand` patterns, and positional masks are not in this version.

## Open items

- The engine raycast (`player_raycast`) for `//hpos` and the far and navigation wands.
- Schematic `.schem` read and write (a streamed codec in the worker, with the engine clipboard transfer calls).
- A real-client check of the wand and the messages.
