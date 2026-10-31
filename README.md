# Summon Commander

A Valheim mod (BepInEx 5) for tracking and commanding your summons: skeletons from the **Dead Raiser**
and spirit animals (wolf, boar, moose, bear) from the **Spirit Caller**. It is the successor of
Skeleton Commander: everything that mod does for skeletons now works for spirits too, and summons no
longer box you in.

**Client-side:** only the players who want to use the mod need to install it. It is not required on
dedicated servers or by other players.

> Summon Commander replaces Skeleton Commander, which you can uninstall
> (`BepInEx/plugins/SkeletonCommander/`). If both are installed, Summon Commander takes over: Skeleton
> Commander 1.10.0 and later does not load, and older versions are turned off at startup. Your Skeleton
> Commander settings are copied on the first launch.

## Features

- **No more getting stuck in your summons** – you walk through Spirit Caller spirits instead of being
  blocked by them (`Movement.PassThroughSpirits`, on by default; also for skeletons with
  `Movement.PassThroughSkeletons`). Only the collision between you and the summons is removed; they still
  fight, get hit and collide with each other and the world.
- **Following at a distance** – following summons stop `Movement.FollowDistance` (4 m) away from you,
  measured from their edge, so a big moose or bear keeps more room than a skeleton, and summons arriving
  from different sides spread around you instead of crowding onto your position. A summon closer than
  `Movement.PersonalSpace` (1.5 m), e.g. after you walked into the group, steps away.
- **Commands and hotkeys** – follow, stay and dismiss for all summons at once, only skeletons or only
  spirits (chat command), or only the one you are aiming at (the hover text shows the hotkeys).
- **Minimap pins** – each summon gets a pin (skeleton trophy for skeletons, Spirit Caller icon for
  spirits, in the species' HUD color); the label on the large map shows the species and whether it stays.
- **HUD** – below the minimap: how many skeletons and spirits you have (optionally how many are following or
  staying), how many spirits of each species you have against the Spirit Caller's limit
  (`Spirits: wolf 2/3 · boar 3/3 · moose 1/3 · bear 0/3`, full species highlighted), and a panel per
  species – skeletons, wolves, boars, moose, bears – each in its own color, with its count against the limit
  and a compact row per summon: number, name (if you renamed it) or weapon (skeletons), an HP bar colored by
  health, the Staff of Protection barrier, distance and (optionally) whether it stays. Spirits are called by
  their species (e.g. Wolf). The panels are as wide as the longest name needs, or as wide as you set
  (`HUD.PanelWidth`), with the content fitted to that width. With a full pack the list shrinks to fit on the
  screen. Every part can be hidden.
- **Choosing the spirit species** – make the Spirit Caller always summon a wolf, boar, moose or bear instead of
  a random species (`Summon.NextSpirit`, or Left Alt + K in game); the HUD shows your choice.
- **English and Polish** – the mod's texts follow the game's language (Polish in a Polish game, English
  otherwise); creature names come from the game's own translations.
- **Health bars, glow and death notifications** for your summons.
- **Skeleton team loadout** – choose the weapons of your skeleton team in `SkeletonTeam.Slot1`–`Slot4`;
  each new skeleton gets the weapon the team is missing.
- **Summon limit fix** – summons told to stay count toward the staff's summon limit, so you cannot exceed
  it by leaving summons behind. As in the base game, each creature type has its own limit: the Spirit
  Caller picks a random species on every cast and allows N of each species at upgrade level N (e.g. 2
  wolves, 2 boars, 2 moose and 2 bears at level 2), the Dead Raiser N skeletons. Over the limit
  the summon with the least HP is dismissed (`Summon.DismissWeakest`). Summons far away or outside the area
  the game simulates are dismissed, also when you use a portal or die, unless another player is near them
  (`Summon.KeepNearOtherPlayers`).

## Requirements

- Valheim (PC)
- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (BepInEx 5)

## Installation

### With a mod manager (recommended)

Install **SummonCommander** from [Thunderstore](https://thunderstore.io/c/valheim/p/xMordax/SummonCommander/)
with r2modman or the Thunderstore Mod Manager; BepInExPack Valheim is installed with it.

### Manually

1. Install BepInExPack Valheim following its instructions and start the game once so BepInEx
   creates its folders.
2. Copy `SummonCommander.dll` to:
   ```
   <Valheim folder>/BepInEx/plugins/SummonCommander/
   ```
3. Start the game. On first launch the config file
   `BepInEx/config/xmordax.valheim.summoncommander.cfg` is created (with your Skeleton Commander settings,
   if you had that mod).

If you used Skeleton Commander, you can remove it (`BepInEx/plugins/SkeletonCommander/`). To uninstall
Summon Commander, delete the `BepInEx/plugins/SummonCommander/` folder (and optionally the `.cfg` file).

## Controls

### Hotkeys

| Hotkey (default) | Action |
|---|---|
| Left Alt + F | summons follow you |
| Left Alt + G | summons stay in place |
| Left Alt + H | dismiss summons |
| Left Alt + J | list summons in chat |
| Left Alt + K | switch the species of the next Spirit Caller spirit (random, wolf, boar, moose, bear) |

When you aim at one of your summons, Follow / Stay / Dismiss affect only that one. Hotkeys are ignored
while typing in chat and in menus, the inventory and the large map. While a hotkey's modifier is held,
the game ignores that hotkey's key (Left Alt + F / G do not trigger the Forsaken power or emote wheel).

### Chat commands

Type in chat (Enter), or in the F5 console without the slash. The last word is optional (`all` by default).

| Command | Action |
|---|---|
| `/sc` or `/sc list [all\|skeletons\|spirits]` | numbered list of summons (newest first) |
| `/sc follow [all\|skeletons\|spirits]` | summons follow you |
| `/sc stay [all\|skeletons\|spirits]` | summons stay in place |
| `/sc dismiss [all\|skeletons\|spirits]` | dismiss summons |

The commands do not require `devcommands`. They affect summons in the area loaded by the game.

## Configuration

File: `BepInEx/config/xmordax.valheim.summoncommander.cfg` (or in-game with ConfigurationManager, F1).

| Section | Option | Default | Description |
|---|---|---|---|
| `Hotkeys` | `Follow` / `Stay` / `Dismiss` / `List` / `NextSpirit` | `F` / `G` / `H` / `J` / `K` + `LeftAlt` | hotkeys (`None` disables one) |
| `Movement` | `PassThroughSpirits` | `true` | walk through Spirit Caller spirits |
| `Movement` | `PassThroughSkeletons` | `false` | walk through Dead Raiser skeletons |
| `Movement` | `ImprovedFollow` | `true` | following summons keep `FollowDistance` / `PersonalSpace`; off = base game following |
| `Movement` | `FollowDistance` | `4` | where following summons stop, from their edge (2–15 m) |
| `Movement` | `PersonalSpace` | `1.5` | closer summons step away; kept 1 m below `FollowDistance` (0 = off, 0–10 m) |
| `Summon` | `DismissWeakest` | `true` | over the limit, dismiss the summon with the least HP; off = the oldest |
| `Summon` | `NextSpirit` | `Random` | species the Spirit Caller summons: `Random`, `Wolf`, `Boar`, `Moose`, `Bear` |
| `Summon` | `DismissBeyondDistance` | `100` | farther summons are dismissed (20–120 m) |
| `Summon` | `KeepNearOtherPlayers` | `true` | far summons near another player are kept |
| `SkeletonTeam` | `Enabled` | `true` | skeletons only: use the team slots; off = random weapons |
| `SkeletonTeam` | `Slot1`–`Slot4` | `SwordAndShield`, `Bow`, `SwordAndShield`, `Bow` | skeletons only: your team; a limit of N uses slots 1 to N |
| `Notifications` | `Death` | `true` | message when a summon dies |
| `HUD` | `ShowCounter`, `ShowDetails`, `ShowSkeletonTeam`, `ShowSpiritSpecies`, `ShowName`, `ShowWeapon`, `ShowHealth`, `ShowDistance`, `ShowShield` | `true` | parts of the HUD |
| `HUD` | `ShowState` | `false` | "staying" next to the summons that stay; off = longer HP bars |
| `HUD` | `ShowStateSummary` | `false` | "(N following, M staying)" next to the counter |
| `HUD` | `CounterPositionY` | `0.3` | vertical counter position (0 = top, 1 = bottom) |
| `HUD` | `Style` | `Bars` | panels with HP bars (`Bars`) or one text line per summon (`Text`) |
| `HUD` | `FontSize` | `18` | HUD font size (10–40) |
| `HUD` | `PanelWidth` | `0` | width of the species panels in HUD pixels (0–1000); `0` = fit the longest name; content fits the set width |
| `HealthBars` | `AlwaysShow` / `MaxDistance` / `ColorByHealth` | `true` / `40` / `true` | health bars above summons |
| `Glow` | `Enabled` / `Color` / `Intensity` / `Range` | `true` / `66D9FFFF` / `1.2` / `3` | glow around summons |

## License

MIT – see https://github.com/michalw387/SummonCommander/blob/main/LICENSE
