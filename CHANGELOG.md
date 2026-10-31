# Changelog

## 1.0.0

First release as **Summon Commander**, the successor of Skeleton Commander 1.9.1.

### Added
- **Spirit Caller support** – spirit wolves, boars, moose and bears get everything skeletons have: follow /
  stay / dismiss hotkeys and commands, minimap pins (Spirit Caller icon), HUD panels, health bars, glow,
  death notifications and the summon limit fixes (each creature type keeps its own limit, as in the base game).
- **Walking through summons** – the collision between you and Spirit Caller spirits is removed, so a group of
  spirits can no longer box you in (`Movement.PassThroughSpirits`, on); the same for skeletons with
  `Movement.PassThroughSkeletons` (off).
- **Following at a distance** – following summons stop `Movement.FollowDistance` (4 m) from you, measured
  from their edge, instead of pushing to within 3 m of your position, and step away when closer than
  `Movement.PersonalSpace` (1.5 m). `Movement.ImprovedFollow = false` restores the base game's following.
- **Spirit species in the HUD** – `Spirits: wolf 2/3 · boar 3/3 · moose 1/3 · bear 0/3`: your spirits of each
  species against the Spirit Caller's limit (N of each at upgrade level N), full species highlighted
  (`HUD.ShowSpiritSpecies`).
- **HUD grouped by species** – a panel per species (skeletons, wolves, boars, moose, bears) in its own color,
  with its count against the summon limit, and a compact one-line row per summon (number, name or weapon, HP bar,
  distance, staying). The list shrinks when it would not fit on the screen. Lists and `/sc list` are grouped the
  same way, with names in the species' colors.
- The chat command is now `/sc [list|follow|stay|dismiss] [all|skeletons|spirits]`.
- **Choosing the spirit species** – `Summon.NextSpirit` makes the Spirit Caller always summon a wolf, boar, moose
  or bear instead of a random species (`Random`, the default, keeps the base game's behavior); at that
  species' limit the new spirit replaces one of the same species. `Hotkeys.NextSpirit` (Left Alt + K) switches
  it in game, and the HUD's spirit line shows the choice (`next: wolf`).
- **Polish translation** – the mod's HUD, messages, hover hints, minimap pins and chat output follow the game's
  language: Polish when the game is in Polish, English otherwise. Species panels are headed in the plural
  (Skeletons, Wolves, Boars, Moose, Bears / Szkielety, Wilki, Dziki, Łosie, Niedźwiedzie); other creature
  names still come from the game's own translations. Chat commands and config settings stay in English.
- `HUD.PanelWidth` sets the width of all species panels (`0`, the default, fits the longest name). In a set
  width the HP bars keep room for their numbers and the names get the rest; text that does not fit shrinks a
  little before it is cut with an ellipsis.

### Changed
- `HUD.ShowStateSummary` is off by default: the counter no longer adds "(N following, M staying)".
- `HUD.ShowState` is off by default: the HUD panels' HP bars take the space of the "staying" column, which is
  only reserved while the option is on.
- The name column of each HUD panel fits its longest label (e.g. "#4 Kjettil · sword & shield"), and all panels
  are as wide as that needs; spirit panels, whose labels are only numbers, give the space to longer HP bars.
- Settings that only concern skeletons are named so: `Summon.TeamEnabled` and `Summon.TeamSlot1`–`4` are now
  `SkeletonTeam.Enabled` and `SkeletonTeam.Slot1`–`4`, `HUD.ShowTeam` is `HUD.ShowSkeletonTeam`. Values under the
  old names (e.g. from a copied Skeleton Commander config) are moved to the new ones.
- Plugin GUID `xmordax.valheim.summoncommander`; Skeleton Commander's config file is copied on first launch.
  If Skeleton Commander is also installed, Summon Commander takes over: Skeleton Commander 1.10.0+ does not
  load, and older versions are turned off at startup.

### Fixed
- HUD panels no longer cut species names with Polish letters (and other letters the HUD font lacks) down to
  their first letters: "Niedźwiedź", "Łoś" and the skeletons' header showed as "Nie…", "Ł…" and "K…".
