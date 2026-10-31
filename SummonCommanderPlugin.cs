using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SummonCommander
{
    internal enum HudStyle
    {
        Bars,
        Text
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Summon Commander replaces Skeleton Commander; both at once would patch and command the skeletons twice.
    // Skeleton Commander 1.10.0+ does not load next to this mod; older versions load first (soft dependency)
    // and are turned off in Awake.
    [BepInDependency(SkeletonCommanderGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class SummonCommanderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "xmordax.valheim.summoncommander";
        public const string PluginName = "Summon Commander";
        public const string PluginVersion = "1.0.1";
        private const string SkeletonCommanderGuid = "xmordax.valheim.skeletoncommander";
        // Skeleton Commander's settings are copied on first launch (its file is left in place).
        private const string SkeletonCommanderConfigFileName = SkeletonCommanderGuid + ".cfg";

        private const float ScanInterval = 0.5f;
        private const string GlowObjectName = "SummonCommander_Glow";

        private static readonly AccessTools.FieldRef<Minimap, bool> PinUpdateRequired =
            AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");
        private static readonly AccessTools.FieldRef<Chat, float> ChatHideTimer =
            AccessTools.FieldRefAccess<Chat, float>("m_hideTimer");
        // False while typing in chat/console, in menus, the inventory or the large map.
        private static readonly Func<Player, bool> PlayerTakesInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        internal static ConfigEntry<KeyboardShortcut> FollowKey;
        internal static ConfigEntry<KeyboardShortcut> StayKey;
        internal static ConfigEntry<KeyboardShortcut> DismissKey;
        internal static ConfigEntry<KeyboardShortcut> ListKey;
        internal static ConfigEntry<KeyboardShortcut> NextSpiritKey;
        internal static ConfigEntry<SpiritSpecies> NextSpirit;
        internal static ConfigEntry<bool> ShowCounter;
        internal static ConfigEntry<bool> ShowDetails;
        internal static ConfigEntry<bool> ShowStateSummary;
        internal static ConfigEntry<bool> ShowSkeletonTeam;
        internal static ConfigEntry<bool> ShowSpiritSpecies;
        internal static ConfigEntry<bool> ShowName;
        internal static ConfigEntry<bool> ShowWeapon;
        internal static ConfigEntry<bool> ShowHealth;
        internal static ConfigEntry<bool> ShowDistance;
        internal static ConfigEntry<bool> ShowState;
        internal static ConfigEntry<bool> ShowShield;
        internal static ConfigEntry<float> CounterPositionY;
        internal static ConfigEntry<float> HudFontSize;
        internal static ConfigEntry<float> PanelWidth;
        internal static ConfigEntry<HudStyle> Style;
        internal static ConfigEntry<bool> HealthBars;
        internal static ConfigEntry<float> HealthBarDistance;
        internal static ConfigEntry<bool> HealthBarColors;
        internal static ConfigEntry<bool> GlowEnabled;
        internal static ConfigEntry<Color> GlowColor;
        internal static ConfigEntry<float> GlowIntensity;
        internal static ConfigEntry<float> GlowRange;
        internal static ConfigEntry<bool> DeathNotifications;
        internal static ConfigEntry<bool> SkeletonTeamEnabled;
        internal static ConfigEntry<TeamSlot>[] SkeletonTeamSlots;
        internal static ConfigEntry<bool> DismissWeakest;
        internal static ConfigEntry<float> DismissBeyondDistance;
        internal static ConfigEntry<bool> KeepNearOtherPlayers;
        internal static ConfigEntry<bool> PassThroughSpirits;
        internal static ConfigEntry<bool> PassThroughSkeletons;
        internal static ConfigEntry<bool> ImprovedFollow;
        internal static ConfigEntry<float> FollowDistance;
        internal static ConfigEntry<float> PersonalSpace;

        // The local player's summons as of the last scan; read by the Harmony patches.
        private static readonly HashSet<Character> Owned = new HashSet<Character>();

        private readonly List<Character> _summons = new List<Character>();
        private readonly Dictionary<Character, Minimap.PinData> _pins = new Dictionary<Character, Minimap.PinData>();
        // Spirit pins in their species' HUD color (boxed Color), applied by SummonPatches.ColorSummonPins; entries go
        // away with their pins.
        internal static readonly ConditionalWeakTable<Minimap.PinData, object> PinColors =
            new ConditionalWeakTable<Minimap.PinData, object>();
        private readonly List<Character> _stale = new List<Character>();
        private readonly HashSet<Character> _deathHooked = new HashSet<Character>();
        private readonly Dictionary<SummonKind, Sprite> _pinIcons = new Dictionary<SummonKind, Sprite>();
        private readonly SummonHud _hud = new SummonHud();
        private Harmony _harmony;
        private Minimap _minimap;
        private float _timer;

        internal static bool IsOwned(Character character)
        {
            return character != null && Owned.Contains(character);
        }

        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            DisableSkeletonCommander();
            CopySkeletonCommanderConfig();

            // Written to BepInEx/config/<PluginGuid>.cfg on first launch. ConfigurationManager lists
            // sections in the order they are first bound here.
            const string hotkeyNote = " When aiming at one of your summons, affects only that one.";
            FollowKey = Config.Bind("Hotkeys", "Follow", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                "Summons follow you (like /sc follow)." + hotkeyNote);
            StayKey = Config.Bind("Hotkeys", "Stay", new KeyboardShortcut(KeyCode.G, KeyCode.LeftAlt),
                "Summons stay in place (like /sc stay)." + hotkeyNote);
            DismissKey = Config.Bind("Hotkeys", "Dismiss", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt),
                "Dismisses summons (like /sc dismiss)." + hotkeyNote);
            ListKey = Config.Bind("Hotkeys", "List", new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt),
                "Lists your summons in chat (like /sc list).");
            NextSpiritKey = Config.Bind("Hotkeys", "NextSpirit", new KeyboardShortcut(KeyCode.K, KeyCode.LeftAlt),
                "Switches the species of the next Spirit Caller spirit (Summon.NextSpirit): random, wolf, boar, moose, bear.");

            PassThroughSpirits = Config.Bind("Movement", "PassThroughSpirits", true,
                "You walk through Spirit Caller spirits (yours and other players') instead of being blocked by them. " +
                "Only the collision between you and the spirits is removed: they still fight and can be hit.");
            PassThroughSkeletons = Config.Bind("Movement", "PassThroughSkeletons", false,
                "You walk through Dead Raiser skeletons (yours and other players') instead of being blocked by them.");
            ImprovedFollow = Config.Bind("Movement", "ImprovedFollow", true,
                "Following summons stop FollowDistance away from you (measured from their edge, so big spirits keep " +
                "more room) and step back when closer than PersonalSpace, instead of pushing to within 3 m of you as " +
                "in the base game. Off: the base game's following.");
            FollowDistance = Config.Bind("Movement", "FollowDistance", 4f,
                new ConfigDescription("How far (m) from you following summons stop, measured from the summon's edge.",
                    new AcceptableValueRange<float>(2f, 15f)));
            PersonalSpace = Config.Bind("Movement", "PersonalSpace", 1.5f,
                new ConfigDescription("Summons closer than this (m, from their edge) step away from you, e.g. after you " +
                    "walk into the group. Kept at least 1 m below FollowDistance. 0 = off.",
                    new AcceptableValueRange<float>(0f, 10f)));

            DismissWeakest = Config.Bind("Summon", "DismissWeakest", true,
                "Over the summon limit, the summon of that type with the least HP is dismissed (the oldest on a tie), " +
                "so a new skeleton replaces it with the weapon its team slot needs. Off: the oldest is dismissed as in " +
                "the base game.");
            NextSpirit = Config.Bind("Summon", "NextSpirit", SpiritSpecies.Random,
                "The species the Spirit Caller summons: Random (as in the base game) or always Wolf, Boar, Moose or Bear. " +
                "At the species' limit the new spirit replaces one of that species. Hotkeys.NextSpirit switches it in game.");
            DismissBeyondDistance = Config.Bind("Summon", "DismissBeyondDistance", 100f,
                new ConfigDescription("Summons farther away than this (in meters) from you, and from other players with KeepNearOtherPlayers, are " +
                    "dismissed, also when you teleport " +
                    "or die far from them. Summons left in an unloaded area would otherwise not count toward the summon limit. " +
                    "Summons leaving the area the game simulates around you (which depends on the Simulation distance " +
                    "graphics setting) are dismissed even when closer.",
                    new AcceptableValueRange<float>(20f, 120f)));
            KeepNearOtherPlayers = Config.Bind("Summon", "KeepNearOtherPlayers", true,
                "Far summons are kept while another player is near them, so they keep fighting alongside that player " +
                "(e.g. after you die). They don't count toward your summon limit while you are away; when you come back " +
                "to more summons than your limit, the extra ones are dismissed. Off: far summons are dismissed even " +
                "when other players are near them.");

            const string skeletonsOnly = "Dead Raiser skeletons only (spirits have no weapons). ";
            SkeletonTeamEnabled = Config.Bind("SkeletonTeam", "Enabled", true,
                skeletonsOnly + "Gives new skeletons the weapons set by Slot1-4. Off: skeletons get random weapons as " +
                "in the base game.");
            // Each new skeleton gets the weapon its team is missing. The summon limit is 1-4 depending on the
            // Dead Raiser's upgrade level; a limit of N uses slots 1..N.
            var slotDefaults = new[] { TeamSlot.SwordAndShield, TeamSlot.Bow, TeamSlot.SwordAndShield, TeamSlot.Bow };
            SkeletonTeamSlots = slotDefaults.Select((value, i) => Config.Bind("SkeletonTeam", $"Slot{i + 1}", value,
                skeletonsOnly + $"Weapons of skeleton {i + 1} of your team, used from a summon limit of {i + 1} (Dead " +
                "Raiser upgrade level). New skeletons get the weapon the team is missing; Random accepts any weapon. " +
                "Already summoned skeletons keep their weapons.")).ToArray();

            DeathNotifications = Config.Bind("Notifications", "Death", true,
                "On-screen message when one of your summons dies.");

            ShowCounter = Config.Bind("HUD", "ShowCounter", true,
                "Shows the summon counter below the minimap.");
            ShowDetails = Config.Bind("HUD", "ShowDetails", true,
                "Lists your summons below the counter, grouped by species (skeletons, wolves, boars, moose, bears), each " +
                "species in its own color: name, weapon, health, distance and whether it follows you.");
            ShowStateSummary = Config.Bind("HUD", "ShowStateSummary", false,
                "Shows how many summons are following you and how many are staying next to the counter, and how many of a " +
                "species stay in its panel header.");
            ShowSkeletonTeam = Config.Bind("HUD", "ShowSkeletonTeam", true,
                "Shows how many Dead Raiser skeletons of each weapon your team (SkeletonTeam.Slot1-4) has and wants.");
            ShowSpiritSpecies = Config.Bind("HUD", "ShowSpiritSpecies", true,
                "Shows how many Spirit Caller spirits of each species you have and the limit per species, " +
                "e.g. wolf 2/3 · boar 3/3; species at the limit are highlighted.");
            ShowName = Config.Bind("HUD", "ShowName", true, "Shows each summon's name in the detailed list.");
            ShowWeapon = Config.Bind("HUD", "ShowWeapon", true,
                "Shows the weapon of each skeleton, and the species of each spirit you renamed, in the detailed list.");
            ShowHealth = Config.Bind("HUD", "ShowHealth", true, "Shows each summon's health in the detailed list.");
            ShowDistance = Config.Bind("HUD", "ShowDistance", true, "Shows each summon's distance from you in the detailed list.");
            ShowState = Config.Bind("HUD", "ShowState", false,
                "Shows whether each summon is following you or staying in the detailed list (the panels mark only the ones " +
                "that stay, in a column the HP bars give up). Off: the HP bars are longer.");
            ShowShield = Config.Bind("HUD", "ShowShield", true,
                "Shows the barrier from the Staff of Protection (how much is left and for how long) in the detailed list.");
            CounterPositionY = Config.Bind("HUD", "CounterPositionY", 0.3f,
                new ConfigDescription("Vertical counter position as a fraction of screen height (0 = top, 1 = bottom).",
                    new AcceptableValueRange<float>(0f, 1f)));
            HudFontSize = Config.Bind("HUD", "FontSize", 18f,
                new ConfigDescription("Font size of the summon counter and list; the whole box scales with it.",
                    new AcceptableValueRange<float>(10f, 40f)));
            PanelWidth = Config.Bind("HUD", "PanelWidth", 0f,
                new ConfigDescription("Width of the species panels (Style = Bars) in pixels of the HUD. 0: as wide as the " +
                    "longest name needs (it scales with FontSize). In a set width the HP bars and names share the space; " +
                    "text that does not fit shrinks a little, then is cut with an ellipsis.",
                    new AcceptableValueRange<float>(0f, 1000f)));
            Style = Config.Bind("HUD", "Style", HudStyle.Bars,
                "How the detailed list shows your summons: Bars (a panel per species with a row and an HP bar per summon; " +
                "it shrinks when it would not fit on the screen) or Text (one line each).");

            HealthBars = Config.Bind("HealthBars", "AlwaysShow", true,
                "Health bars above your summons' heads are always visible (within MaxDistance). Off: shown as in the " +
                "base game.");
            HealthBarDistance = Config.Bind("HealthBars", "MaxDistance", 40f,
                new ConfigDescription("Maximum distance (m) at which summon health bars are shown.",
                    new AcceptableValueRange<float>(10f, 100f)));
            HealthBarColors = Config.Bind("HealthBars", "ColorByHealth", true,
                "Colors summon health bars by HP: green > 60%, yellow > 30%, red below.");

            GlowEnabled = Config.Bind("Glow", "Enabled", true,
                "Subtle glow around your summons.");
            GlowColor = Config.Bind("Glow", "Color", new Color(0.4f, 0.85f, 1f, 1f),
                "Glow color (RGBA, hex).");
            GlowIntensity = Config.Bind("Glow", "Intensity", 1.2f,
                new ConfigDescription("Glow brightness.", new AcceptableValueRange<float>(0f, 5f)));
            GlowRange = Config.Bind("Glow", "Range", 3f,
                new ConfigDescription("Glow range (m).", new AcceptableValueRange<float>(0.5f, 10f)));

            MigrateRenamedSettings();

            SummonCommands.Register();
            _harmony = Harmony.CreateAndPatchAll(typeof(SummonPatches), PluginGuid);
        }

        private void Update()
        {
            HandleHotkeys();

            _timer += Time.deltaTime;
            if (_timer < ScanInterval)
                return;
            _timer = 0f;

            var minimap = Minimap.instance;
            var player = Player.m_localPlayer;
            if (minimap != _minimap)
            {
                // New world/session: old pins went away with the previous Minimap.
                RemoveAllPins();
                _minimap = minimap;
            }
            if (minimap == null || player == null)
            {
                RemoveAllPins();
                _summons.Clear();
                Owned.Clear();
                _hud.Hide();
                return;
            }

            SummonMovement.UpdateCollisions(player);
            Summons.FindOwned(player, _summons);
            // While teleporting the player is still at the old place; TeleportTo already checked the destination.
            if (!player.IsTeleporting())
                DismissFarSummons(player, player.transform.position, _summons);
            DismissOverLimit(player, _summons);
            Owned.Clear();
            Owned.UnionWith(_summons);
            _deathHooked.RemoveWhere(c => c == null);

            int following = 0;
            foreach (var summon in _summons)
            {
                bool isFollowing = Summons.IsFollowing(summon, player);
                if (isFollowing)
                    following++;

                UpdatePin(minimap, summon, isFollowing);
                UpdateGlow(summon);
                if (_deathHooked.Add(summon))
                    summon.m_onDeath += () => OnSummonDied(summon);
            }
            RemoveStalePins(minimap);
            PinUpdateRequired(minimap) = true;

            UpdateHud(player, following);
        }

        private void HandleHotkeys()
        {
            var player = Player.m_localPlayer;
            if (player == null || !PlayerTakesInput(player))
                return;

            // Aiming at one of our summons narrows the action to just that one.
            var hovered = player.GetHoverCreature();
            var only = IsOwned(hovered) ? hovered : null;

            if (IsPressed(FollowKey.Value))
                ShowMessage(player, SummonCommands.Follow(player, only));
            else if (IsPressed(StayKey.Value))
                ShowMessage(player, SummonCommands.Stay(player, only));
            else if (IsPressed(DismissKey.Value))
                ShowMessage(player, SummonCommands.Dismiss(player, only));
            else if (IsPressed(NextSpiritKey.Value))
            {
                NextSpirit.Value = (SpiritSpecies)(((int)NextSpirit.Value + 1) % Enum.GetValues(typeof(SpiritSpecies)).Length);
                ShowMessage(player, Texts.Format("Next spirit: {0}", Summons.GetSpiritSpeciesName(NextSpirit.Value)));
            }
            else if (IsPressed(ListKey.Value) && Chat.instance != null)
            {
                SummonCommands.List(Chat.instance, player);
                // The chat window hides itself until opened; reset its timer so the list is visible.
                ChatHideTimer(Chat.instance) = 0f;
            }
        }

        // KeyboardShortcut.IsDown() fails while any other key is held (e.g. W while running),
        // so only the shortcut's own keys are checked.
        private static bool IsPressed(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None
                && UnityInput.Current.GetKeyDown(shortcut.MainKey)
                && shortcut.Modifiers.All(UnityInput.Current.GetKey);
        }

        // Skeleton Commander up to 1.9.1 does not know about this mod. Its OnDestroy removes its HUD, pins and
        // glow and undoes its Harmony patches; only its /sk command stays registered.
        private void DisableSkeletonCommander()
        {
            if (!Chainloader.PluginInfos.TryGetValue(SkeletonCommanderGuid, out var info) || info.Instance == null)
                return;
            Log.LogWarning($"Skeleton Commander {info.Metadata.Version} is installed; Summon Commander replaces it, " +
                "so it is turned off. Remove BepInEx/plugins/SkeletonCommander.");
            Destroy(info.Instance);
        }

        // Settings that only concern skeletons were renamed once the mod also commanded spirits. Values under the
        // old names (also in a config copied from Skeleton Commander) are moved to the new ones.
        private static readonly (ConfigDefinition Old, ConfigDefinition New)[] RenamedSettings =
            new[] { ("Summon", "TeamEnabled", "SkeletonTeam", "Enabled") }
                .Concat(Enumerable.Range(1, 4).Select(i => ("Summon", $"TeamSlot{i}", "SkeletonTeam", $"Slot{i}")))
                .Concat(new[]
                {
                    ("HUD", "ShowTeam", "HUD", "ShowSkeletonTeam"),
                    ("HUD", "ShowSpecies", "HUD", "ShowSpiritSpecies")
                })
                .Select(names => (new ConfigDefinition(names.Item1, names.Item2), new ConfigDefinition(names.Item3, names.Item4)))
                .ToArray();

        // BepInEx keeps the values of keys no setting is bound to, and writes them back to the file.
        private static readonly System.Reflection.PropertyInfo OrphanedEntries =
            AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");

        // Call after every setting is bound.
        private void MigrateRenamedSettings()
        {
            var orphans = (Dictionary<ConfigDefinition, string>)OrphanedEntries.GetValue(Config);
            bool migrated = false;
            foreach (var (oldName, newName) in RenamedSettings)
            {
                if (!orphans.TryGetValue(oldName, out string value))
                    continue;
                Config[newName].SetSerializedValue(value);
                orphans.Remove(oldName);
                migrated = true;
                Log.LogInfo($"Setting {oldName.Section}.{oldName.Key} moved to {newName.Section}.{newName.Key} = {value}");
            }
            if (migrated)
                Config.Save();
        }

        private void CopySkeletonCommanderConfig()
        {
            string oldPath = Path.Combine(Paths.ConfigPath, SkeletonCommanderConfigFileName);
            if (File.Exists(Config.ConfigFilePath) || !File.Exists(oldPath))
                return;
            File.Copy(oldPath, Config.ConfigFilePath);
            Config.Reload();
        }

        private static void ShowMessage(Player player, string text)
        {
            player.Message(MessageHud.MessageType.Center, text);
        }

        internal static IEnumerable<ConfigEntry<KeyboardShortcut>> Hotkeys => new[]
        {
            FollowKey, StayKey, DismissKey, ListKey, NextSpiritKey
        };

        internal static string GetHoverHints()
        {
            return HoverHint(FollowKey.Value, Texts.Get("Follow"))
                + HoverHint(StayKey.Value, Texts.Get("Stay"))
                + HoverHint(DismissKey.Value, Texts.Get("Dismiss"));
        }

        private static string HoverHint(KeyboardShortcut key, string action)
        {
            if (key.MainKey == KeyCode.None)
                return "";
            string keys = string.Join(" + ", key.Modifiers.Concat(new[] { key.MainKey }));
            return $"\n[<color=yellow><b>{keys}</b></color>] {action}";
        }

        private void UpdateHud(Player player, int following)
        {
            if (!ShowCounter.Value || _summons.Count == 0)
            {
                _hud.Hide();
                return;
            }

            var text = new StringBuilder();
            text.Append($"<align=center>{DescribeCount(_summons)}");
            if (ShowStateSummary.Value)
                text.Append("  " + Texts.Format("({0} following, {1} staying)", following, _summons.Count - following));
            text.Append("</align>");
            var skeletons = Summons.OfKind(_summons, SummonKind.Skeleton);
            if (ShowSkeletonTeam.Value && SkeletonTeamEnabled.Value && skeletons.Count > 0)
                text.Append($"\n<align=center><size=80%>{SkeletonLoadouts.DescribeTeam(skeletons)}</size></align>");
            var spirits = Summons.OfKind(_summons, SummonKind.Spirit);
            if (ShowSpiritSpecies.Value && spirits.Count > 0)
                text.Append($"\n<align=center><size=80%>{Summons.DescribeSpecies(spirits, NextSpirit.Value)}</size></align>");
            List<SummonGroupData> panels = null;
            if (ShowDetails.Value && Style.Value == HudStyle.Bars)
                panels = DescribePanels(player);
            else if (ShowDetails.Value)
            {
                // Same numbering as the chat list (/sc list).
                for (int i = 0; i < _summons.Count; i++)
                    text.Append($"\n<size=80%>#{i + 1}  ").Append(Summons.Describe(_summons[i], player,
                        ShowName.Value, ShowWeapon.Value, ShowHealth.Value, ShowDistance.Value, ShowState.Value,
                        shield: ShowShield.Value)).Append("</size>");
            }
            _hud.Show(text.ToString(), CounterPositionY.Value, HudFontSize.Value, panels, PanelInfoWidth(), PanelWidth.Value);
        }

        // "Skeletons: 3", "Spirits: 2" or "Summons: 5 (3 skeletons, 2 spirits)".
        private static string DescribeCount(List<Character> summons)
        {
            int skeletons = summons.Count(Summons.IsSkeleton);
            int spirits = summons.Count - skeletons;
            if (spirits == 0)
                return Texts.Format("Skeletons: {0}", skeletons);
            if (skeletons == 0)
                return Texts.Format("Spirits: {0}", spirits);
            return Texts.Format("Summons: {0} ({1} skeletons, {2} spirits)", summons.Count, skeletons, spirits);
        }

        // A panel per species (as _summons is grouped), headed by the species name in its color, its count against
        // the summon limit and how many stay; a row per summon numbered as in the chat list (/sc list).
        private List<SummonGroupData> DescribePanels(Player player)
        {
            var panels = new List<SummonGroupData>();
            int number = 0;
            foreach (var group in Summons.LimitGroups(_summons))
            {
                var first = group[0];
                int maxInstances = Summons.GetMaxInstances(first);
                int staying = group.Count(summon => !Summons.IsFollowing(summon, player));
                string header = Summons.Colored(Summons.GetGroupName(first), Summons.GetSpeciesColor(first))
                    + $"  {group.Count}" + (maxInstances > 0 ? $"/{maxInstances}" : "");
                if (ShowStateSummary.Value && staying > 0)
                    header += $"  <size=85%><color={StayingColor}>{Texts.Format("{0} staying", staying)}</color></size>";
                panels.Add(new SummonGroupData
                {
                    Header = header,
                    Rows = group.Select(summon => DescribeRow(summon, ++number, player)).ToList()
                });
            }
            return panels;
        }

        private const string StayingColor = "#FFA040";

        // The panel rows' right column (in font sizes) fits what it shows: "120 m", "staying" or both; without
        // either the HP bar takes its place.
        private static float PanelInfoWidth()
        {
            return (ShowDistance.Value ? 2.5f : 0f) + (ShowState.Value ? 3.2f : 0f);
        }

        // "#3 Kjetil · bow" | HP bar | "12 m · staying" (the state only for summons that stay).
        private static SummonRowData DescribeRow(Character summon, int number, Player player)
        {
            string detail = Summons.GetRowDetail(summon, ShowName.Value, ShowWeapon.Value);
            var info = new List<string>();
            if (ShowDistance.Value)
                info.Add($"{Vector3.Distance(player.transform.position, summon.transform.position):0} m");
            if (ShowState.Value && !Summons.IsFollowing(summon, player))
                info.Add($"<color={StayingColor}>{Texts.Get("staying")}</color>");
            bool hasShield = Summons.TryGetShield(summon, out float shield, out float maxShield, out float shieldSeconds);
            return new SummonRowData
            {
                Label = $"<color=#FFC74D>#{number}</color>" + (detail.Length > 0 ? $" {detail}" : ""),
                Info = string.Join(" · ", info),
                SpeciesColor = Summons.GetSpeciesColor(summon),
                ShowBar = ShowHealth.Value,
                Health = summon.GetHealth(),
                MaxHealth = summon.GetMaxHealth(),
                BarColor = Summons.GetHealthColor(summon),
                ShowShield = ShowShield.Value && hasShield,
                Shield = shield,
                MaxShield = maxShield,
                ShieldSeconds = shieldSeconds
            };
        }

        // A summon in an area that unloads keeps existing but is no longer found, so it would not count
        // toward the summon limit. Dismiss summons too far from where the player is or is about to be
        // while they are still loaded. Dismissed ones are removed from the list.
        internal static void DismissFarSummons(Player player, Vector3 center, List<Character> summons)
        {
            float maxDistance = DismissBeyondDistance.Value;
            int dismissed = summons.RemoveAll(summon =>
            {
                var position = summon.transform.position;
                if (IsNear(position, center, maxDistance))
                    return false;
                // The server hands a summon to a player it is near, whose game then keeps simulating it.
                var nview = summon.GetComponent<ZNetView>();
                if (KeepNearOtherPlayers.Value && ((nview.GetZDO().HasOwner() && !nview.IsOwner())
                    || Player.GetAllPlayers().Any(other => other != player && IsNear(position, other.transform.position, maxDistance))))
                    return false;
                float distance = Summons.HorizontalDistance(position, center);
                Log.LogInfo($"Dismissing {Summons.GetName(summon)} at {distance:0} m " +
                    $"(owner: {(nview.IsOwner() ? "you" : nview.GetZDO().HasOwner() ? "another player" : "none")})");
                Summons.Dismiss(summon);
                return true;
            });
            if (dismissed > 0)
                player.Message(MessageHud.MessageType.TopLeft, dismissed == 1
                    ? Texts.Get("Summon too far away - dismissed")
                    : Texts.Format("{0} summons too far away - dismissed", dismissed));
        }

        // Outside the active area of a player that player gives up the summon, and the area may unload soon after.
        private static bool IsNear(Vector3 position, Vector3 center, float maxDistance)
        {
            return Summons.HorizontalDistance(position, center) <= maxDistance
                && ZNetScene.InActiveArea(position, center);
        }

        // Summons left in an unloaded area don't count when a new one is summoned, so coming back to
        // them can put the player above the limit. Each type has its own limit, carried by its newest summon.
        private static void DismissOverLimit(Player player, List<Character> summons)
        {
            var groups = Summons.LimitGroups(summons).ToList();
            int dismissed = 0;
            foreach (var group in groups)
            {
                int maxInstances = Summons.GetMaxInstances(group[0]);
                if (maxInstances > 0)
                    dismissed += Summons.DismissExcess(group, maxInstances);
            }
            if (dismissed == 0)
                return;
            // DismissExcess removed the dismissed ones from their groups.
            var kept = new HashSet<Character>(groups.SelectMany(group => group));
            summons.RemoveAll(summon => !kept.Contains(summon));
            player.Message(MessageHud.MessageType.TopLeft, dismissed == 1
                ? Texts.Get("Summon over the summon limit - dismissed")
                : Texts.Format("{0} summons over the summon limit - dismissed", dismissed));
        }

        internal static void DismissFarSummons(Player player, Vector3 center)
        {
            var summons = new List<Character>();
            Summons.FindOwned(player, summons);
            DismissFarSummons(player, center, summons);
        }

        private void OnSummonDied(Character summon)
        {
            var player = Player.m_localPlayer;
            if (!DeathNotifications.Value || player == null)
                return;
            player.Message(MessageHud.MessageType.TopLeft, Texts.Format("{0} has died", Summons.GetTitle(summon)),
                0, GetPinIcon(Summons.GetKind(summon)));
        }

        private void UpdatePin(Minimap minimap, Character summon, bool following)
        {
            var pos = summon.transform.position;
            var kind = Summons.GetKind(summon);
            // Spirits are labeled by species (Wolf, ...), the same name and color as in the HUD.
            string name = (kind == SummonKind.Spirit ? Summons.GetSpeciesName(summon) : Texts.Get("Skeleton"))
                + (following ? "" : Texts.Get(" (staying)"));
            if (_pins.TryGetValue(summon, out var pin))
            {
                if (pin.m_name == name)
                {
                    pin.m_pos = pos;
                    return;
                }
                // The pin label is created once with the marker, so a state change needs a new pin.
                minimap.RemovePin(pin);
            }

            pin = minimap.AddPin(pos, Minimap.PinType.Icon3, name, save: false, isChecked: false);
            var icon = GetPinIcon(kind);
            if (icon != null)
                pin.m_icon = icon;
            if (kind == SummonKind.Spirit)
                PinColors.Add(pin, Summons.GetSpeciesColor(summon));
            _pins[summon] = pin;
        }

        private void RemoveStalePins(Minimap minimap)
        {
            _stale.Clear();
            foreach (var entry in _pins)
            {
                if (!Owned.Contains(entry.Key))
                    _stale.Add(entry.Key);
            }
            foreach (var summon in _stale)
            {
                minimap.RemovePin(_pins[summon]);
                _pins.Remove(summon);
            }
        }

        private static void UpdateGlow(Character summon)
        {
            var glow = summon.transform.Find(GlowObjectName);
            if (!GlowEnabled.Value)
            {
                if (glow != null)
                    Destroy(glow.gameObject);
                return;
            }

            if (glow == null)
            {
                glow = new GameObject(GlowObjectName).transform;
                glow.SetParent(summon.transform, worldPositionStays: false);
                glow.localPosition = Vector3.up * 1.2f;
                var newLight = glow.gameObject.AddComponent<Light>();
                newLight.type = LightType.Point;
                newLight.shadows = LightShadows.None;
            }

            // Applied every scan so config changes take effect immediately.
            var light = glow.GetComponent<Light>();
            light.color = GlowColor.Value;
            light.intensity = GlowIntensity.Value;
            light.range = GlowRange.Value;
        }

        // The skeleton trophy for skeletons and the Spirit Caller's icon for spirits make the pins easy to
        // tell apart from regular map markers.
        private Sprite GetPinIcon(SummonKind kind)
        {
            if (_pinIcons.TryGetValue(kind, out var icon) && icon != null)
                return icon;
            if (ObjectDB.instance == null)
                return null;
            var prefab = ObjectDB.instance.GetItemPrefab(kind == SummonKind.Spirit ? "StaffSpiritCaller" : "TrophySkeleton");
            var itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            icon = itemDrop != null ? itemDrop.m_itemData.GetIcon() : null;
            _pinIcons[kind] = icon;
            return icon;
        }

        private void RemoveAllPins()
        {
            if (_minimap != null)
            {
                foreach (var pin in _pins.Values)
                    _minimap.RemovePin(pin);
            }
            _pins.Clear();
        }

        private void OnDestroy()
        {
            RemoveAllPins();
            _hud.Destroy();
            SummonMovement.RestoreCollisions(Player.m_localPlayer);
            foreach (var summon in Owned)
            {
                var glow = summon != null ? summon.transform.Find(GlowObjectName) : null;
                if (glow != null)
                    Destroy(glow.gameObject);
            }
            Owned.Clear();
            _harmony?.UnpatchSelf();
        }
    }
}
