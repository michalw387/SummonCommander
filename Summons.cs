using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SummonCommander
{
    // The species of the next Spirit Caller spirit (Summon.NextSpirit), in the order of Summons.SpiritPrefabs.
    internal enum SpiritSpecies
    {
        Random,
        Wolf,
        Boar,
        Moose,
        Bear
    }

    internal enum SummonKind
    {
        None,
        // Dead Raiser: Skeleton_Friendly.
        Skeleton,
        // Spirit Caller: Wolf_spiritcaller, Boar_spiritcaller, Moose_spiritcaller, Bjorn_spiritcaller.
        Spirit
    }

    // Finds and commands the creatures the local player summoned with the Dead Raiser or the Spirit Caller.
    internal static class Summons
    {
        private const string SkeletonPrefab = "Skeleton_Friendly";
        private const string SpiritPrefabSuffix = "_spiritcaller";
        // The Spirit Caller's species, in the HUD's order; it picks one at random on every cast.
        private static readonly string[] SpiritPrefabs =
            { "Wolf_spiritcaller", "Boar_spiritcaller", "Moose_spiritcaller", "Bjorn_spiritcaller" };
        // Lists and the HUD show skeletons first, then the spirits in SpiritPrefabs' order, then any other
        // spirit; each species has its own color.
        private static readonly string[] DisplayOrder = new[] { SkeletonPrefab }.Concat(SpiritPrefabs).ToArray();
        // The HUD panels' headers, in DisplayOrder (translated by Texts). The game only has singular names.
        private static readonly string[] GroupNames = { "Skeletons", "Wolves", "Boars", "Moose", "Bears" };
        private static readonly Color[] SpeciesColors =
        {
            new Color(0.91f, 0.88f, 0.78f), // skeleton: bone
            new Color(0.55f, 0.78f, 1f),    // wolf: ice blue
            new Color(0.85f, 0.63f, 0.40f), // boar: tan
            new Color(0.56f, 0.83f, 0.42f), // moose: green
            new Color(0.78f, 0.61f, 1f)     // bear: lavender
        };
        private static readonly Color OtherSpeciesColor = new Color(1f, 0.62f, 0.81f);
        // Count of a species at its summon limit.
        private const string FullSpeciesColor = "#FFE066";
        private const string DigitWidth = "0.55em";

        // The game only records the follow target (by player name) and clears it when a summon is told to
        // stay, so we remember the summoner's ID on the summon's ZDO ourselves. Same key as Skeleton
        // Commander (and its former name, Skeleton Tracker), so summons from those mods are recognised.
        private const string OwnerKey = "SkeletonTracker_owner";

        public static SummonKind GetKind(Component component)
        {
            string prefab = Utils.GetPrefabName(component.gameObject);
            if (prefab == SkeletonPrefab)
                return SummonKind.Skeleton;
            if (prefab.EndsWith(SpiritPrefabSuffix, StringComparison.OrdinalIgnoreCase))
                return SummonKind.Spirit;
            return SummonKind.None;
        }

        public static bool IsSummon(Component component) => GetKind(component) != SummonKind.None;

        public static bool IsSkeleton(Component component) => GetKind(component) == SummonKind.Skeleton;

        // Summons of the player, of all kinds: grouped by species in DisplayOrder, newest first within a species
        // (so the first of each species carries its current summon limit).
        public static void FindOwned(Player player, List<Character> result)
        {
            result.Clear();
            long playerId = player.GetPlayerID();
            string playerName = player.GetPlayerName();

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character.IsDead() || !IsSummon(character))
                    continue;

                var nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid())
                    continue;

                var zdo = nview.GetZDO();
                bool following = zdo.GetString(ZDOVars.s_follow) == playerName;
                if (following && nview.IsOwner() && zdo.GetLong(OwnerKey) != playerId)
                    zdo.Set(OwnerKey, playerId);

                if (following || zdo.GetLong(OwnerKey) == playerId)
                    result.Add(character);
            }

            // Newest first, so the oldest summon of a species ends up at the bottom of its group.
            result.Sort((a, b) =>
            {
                int bySpecies = GetDisplayRank(a).CompareTo(GetDisplayRank(b));
                if (bySpecies == 0)
                    bySpecies = string.CompareOrdinal(a.m_name, b.m_name);
                return bySpecies != 0 ? bySpecies : GetSpawnTime(b).CompareTo(GetSpawnTime(a));
            });
        }

        private static int GetDisplayRank(Character summon)
        {
            int rank = Array.IndexOf(DisplayOrder, Utils.GetPrefabName(summon.gameObject));
            return rank >= 0 ? rank : DisplayOrder.Length;
        }

        public static Color GetSpeciesColor(Character summon)
        {
            return GetSpeciesColor(Utils.GetPrefabName(summon.gameObject));
        }

        private static Color GetSpeciesColor(string prefabName)
        {
            int rank = Array.IndexOf(DisplayOrder, prefabName);
            return rank >= 0 ? SpeciesColors[rank] : OtherSpeciesColor;
        }

        // Rich text in the species' color.
        public static string Colored(string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }

        // Kept in FindOwned's order (newest first).
        public static List<Character> OfKind(IEnumerable<Character> summons, SummonKind kind)
        {
            return summons.Where(summon => GetKind(summon) == kind).ToList();
        }

        // The game's summon limit counts summons with the same m_name (Tameable.UnsummonMaxInstances), so
        // every creature type has its own limit; the mod keeps those groups. Kept in the input's order.
        public static IEnumerable<List<Character>> LimitGroups(IEnumerable<Character> summons)
        {
            return summons.GroupBy(summon => summon.m_name).Select(group => group.ToList());
        }

        public static List<Character> SameLimitGroup(IEnumerable<Character> summons, Character summon)
        {
            return summons.Where(other => other.m_name == summon.m_name).ToList();
        }

        // "Spirits: wolf 2/3 · boar 3/3 · moose 1/3 · bear 0/3" for the HUD, from the player's spirits (newest
        // first). The Spirit Caller allows N of each species at upgrade level N; the limit is read from the
        // newest spirit, which got it from the staff it was summoned with. Species at the limit are highlighted.
        public static string DescribeSpecies(List<Character> spirits, SpiritSpecies next)
        {
            int maxInstances = GetMaxInstances(spirits[0]);
            // Known species first (also those you have none of), then any other spirit the game adds;
            // m_name -> prefab name.
            var species = new List<KeyValuePair<string, string>>();
            if (ZNetScene.instance != null)
            {
                foreach (string prefabName in SpiritPrefabs)
                {
                    var prefab = ZNetScene.instance.GetPrefab(prefabName);
                    var character = prefab != null ? prefab.GetComponent<Character>() : null;
                    if (character != null)
                        species.Add(new KeyValuePair<string, string>(character.m_name, prefabName));
                }
            }
            foreach (var spirit in spirits)
            {
                if (species.All(entry => entry.Key != spirit.m_name))
                    species.Add(new KeyValuePair<string, string>(spirit.m_name, Utils.GetPrefabName(spirit.gameObject)));
            }

            var parts = species.Select(entry =>
            {
                int count = spirits.Count(spirit => spirit.m_name == entry.Key);
                string label = Colored(GetSpeciesName(entry.Key, entry.Value).ToLowerInvariant(), GetSpeciesColor(entry.Value));
                if (maxInstances <= 0)
                    return $"{label} {count}";
                string limit = $"{count}/{maxInstances}";
                return count >= maxInstances ? $"{label} <color={FullSpeciesColor}><b>{limit}</b></color>" : $"{label} {limit}";
            });
            string line = Texts.Get("Spirits: ") + string.Join(" · ", parts);
            if (next != SpiritSpecies.Random)
                line += "  (" + Texts.Format("next: {0}", GetSpiritSpeciesName(next)) + ")";
            return line;
        }

        private static long GetSpawnTime(Character summon)
        {
            return summon.GetComponent<ZNetView>().GetZDO().GetLong(ZDOVars.s_spawnTime);
        }

        // Zones load by horizontal position, so distances that decide whether a summon stays loaded ignore
        // height (dungeon interiors sit high above their entrance, in the same zone).
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static bool IsFollowing(Character summon, Player player)
        {
            var nview = summon.GetComponent<ZNetView>();
            return nview != null && nview.IsValid()
                && nview.GetZDO().GetString(ZDOVars.s_follow) == player.GetPlayerName();
        }

        // Tameable.Command toggles between following and staying, so only send it when the state differs.
        public static void SetFollowing(Character summon, Player player, bool follow)
        {
            if (IsFollowing(summon, player) == follow)
                return;
            var tameable = summon.GetComponent<Tameable>();
            if (tameable != null)
                tameable.Command(player, message: false);
        }

        // Same RPC the game uses when a summon is unsummoned (Tameable.UnSummon is private). Only the owner
        // of the summon's ZDO removes it, and a summon outside the player's active area can still be
        // loaded with no owner at all, so take it over first.
        public static void Dismiss(Character summon)
        {
            var nview = summon.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
                return;
            if (!nview.IsOwner() && !nview.GetZDO().HasOwner())
                nview.ClaimOwnership();
            nview.InvokeRPC(ZNetView.Everybody, "RPC_UnSummon");
        }

        // Dismisses the summons above maxInstances from one limit group sorted newest first (as FindOwned
        // returns it) and removes them from the list: the ones with the least HP (Summon.DismissWeakest), the
        // oldest on a tie, or just the oldest. The newest summon, the one just summoned, always stays. Returns
        // how many were dismissed.
        public static int DismissExcess(List<Character> group, int maxInstances)
        {
            int excess = group.Count - maxInstances;
            for (int i = 0; i < excess; i++)
            {
                int victim = group.Count - 1;
                if (SummonCommanderPlugin.DismissWeakest.Value)
                {
                    for (int j = victim - 1; j > 0; j--)
                    {
                        if (group[j].GetHealth() < group[victim].GetHealth())
                            victim = j;
                    }
                }
                Dismiss(group[victim]);
                group.RemoveAt(victim);
            }
            return Math.Max(excess, 0);
        }

        // The summon limit the summon was created with (the staff's upgrade level); 0 means no limit.
        public static int GetMaxInstances(Character summon)
        {
            return summon.GetComponent<ZNetView>().GetZDO().GetInt(ZDOVars.s_maxInstances);
        }

        // The summon's own name when the player named it, otherwise its species.
        public static string GetName(Character summon)
        {
            string name = Localization.instance.Localize(summon.GetHoverName());
            return string.IsNullOrWhiteSpace(name) ? GetSpeciesName(summon) : name;
        }

        // "Wolves", ... for a species panel's header; the game's singular name for a species the mod doesn't know.
        public static string GetGroupName(Character summon)
        {
            int index = Array.IndexOf(DisplayOrder, Utils.GetPrefabName(summon.gameObject));
            return index >= 0 ? Texts.Get(GroupNames[index]) : GetSpeciesName(summon);
        }

        public static string GetSpeciesName(Character summon)
        {
            return GetSpeciesName(summon.m_name, Utils.GetPrefabName(summon.gameObject));
        }

        // The spirits' names ($spiritcaller_wolf, ...) have no translation in the game, in any language, so they
        // localize to an empty string. Such a summon is named after the creature it is made from
        // (Wolf_spiritcaller -> Wolf, in the game's language), or after that prefab when even that has no name.
        // Wolf_spiritcaller, ...; null for Random.
        public static string GetSpiritPrefab(SpiritSpecies species)
        {
            return species == SpiritSpecies.Random ? null : SpiritPrefabs[(int)species - 1];
        }

        // "wolf", ... in the game's language; "random" for Random.
        public static string GetSpiritSpeciesName(SpiritSpecies species)
        {
            string prefabName = GetSpiritPrefab(species);
            if (prefabName == null)
                return Texts.Get("random");
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            var character = prefab != null ? prefab.GetComponent<Character>() : null;
            return GetSpeciesName(character != null ? character.m_name : "", prefabName).ToLowerInvariant();
        }

        private static string GetSpeciesName(string nameToken, string prefabName)
        {
            string name = Localization.instance.Localize(nameToken);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            string basePrefab = prefabName.EndsWith(SpiritPrefabSuffix, StringComparison.OrdinalIgnoreCase)
                ? prefabName.Substring(0, prefabName.Length - SpiritPrefabSuffix.Length)
                : prefabName;
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(basePrefab) : null;
            var character = prefab != null ? prefab.GetComponent<Character>() : null;
            name = character != null ? Localization.instance.Localize(character.m_name) : "";
            return string.IsNullOrWhiteSpace(name) ? basePrefab : name;
        }

        // The row label in the HUD panels: the summon's own name, when it was given one, and a skeleton's weapon.
        public static string GetRowDetail(Character summon, bool name, bool weapon)
        {
            var parts = new List<string>();
            if (name && GetName(summon) != GetSpeciesName(summon))
                parts.Add(GetName(summon));
            if (weapon && IsSkeleton(summon))
                parts.Add(GetWeaponName(summon));
            return string.Join(" · ", parts);
        }

        // "Wolf has died", "Skeleton (bow) has died".
        public static string GetTitle(Character summon)
        {
            string detail = GetDetail(summon);
            return detail != null ? $"{GetName(summon)} ({detail})" : GetName(summon);
        }

        // Shown next to the name: a skeleton's weapon, or a renamed spirit's species (an unnamed spirit is
        // already called by its species).
        private static string GetDetail(Character summon)
        {
            if (IsSkeleton(summon))
                return GetWeaponName(summon);
            string species = GetSpeciesName(summon);
            return GetName(summon) == species ? null : species;
        }

        // Classified by what the skeleton carries rather than the weapon in hand (the AI swaps weapons),
        // and not by m_skillType, which monster weapons leave at its default (Swords).
        public static string GetWeaponName(Character skeleton)
        {
            if (!(skeleton is Humanoid humanoid))
                return Texts.Get("unarmed");
            Classify(humanoid.GetInventory().GetAllItems(), out bool bow, out bool melee, out bool shield);
            if (bow)
                return Texts.Get("bow");
            if (!melee)
                return Texts.Get("unarmed");
            return Texts.Get(shield ? "sword & shield" : "sword");
        }

        // Bow for archers, SwordAndShield for any melee skeleton (with or without a shield), Random when unarmed.
        public static SkeletonLoadout GetLoadout(Character skeleton)
        {
            if (!(skeleton is Humanoid humanoid))
                return SkeletonLoadout.Random;
            Classify(humanoid.GetInventory().GetAllItems(), out bool bow, out bool melee, out _);
            if (bow)
                return SkeletonLoadout.Bow;
            return melee ? SkeletonLoadout.SwordAndShield : SkeletonLoadout.Random;
        }

        public static void Classify(List<ItemDrop.ItemData> items, out bool bow, out bool melee, out bool shield)
        {
            bow = melee = shield = false;
            foreach (var item in items)
            {
                if (IsRangedWeapon(item))
                    bow = true;
                else if (item.IsWeapon())
                    melee = true;
                else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                    shield = true;
            }
        }

        public static bool IsRangedWeapon(ItemDrop.ItemData item)
        {
            return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow
                || (item.IsWeapon() && item.m_shared.m_attack != null
                    && item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
        }

        // Green above 60% health, yellow above 30%, red below.
        public static Color GetHealthColor(Character summon)
        {
            float health = summon.GetHealthPercentage();
            if (health > 0.6f)
                return new Color(0.3f, 0.9f, 0.3f);
            if (health > 0.3f)
                return new Color(1f, 0.85f, 0.2f);
            return new Color(1f, 0.25f, 0.2f);
        }

        // One line used by both the chat list and the detailed HUD; the HUD can leave parts out. The weapon
        // is shown for skeletons only (spirits have none to choose).
        // padded: pad numbers to a fixed width so a line keeps its width as they change (for text lists).
        public static string Describe(Character summon, Player player, bool name = true, bool weapon = true,
            bool health = true, bool distance = true, bool state = true, bool padded = true, bool shield = true)
        {
            var parts = new List<string>();
            if (name)
                parts.Add(Colored(GetName(summon), GetSpeciesColor(summon)));
            string detail = weapon ? GetDetail(summon) : null;
            if (detail != null)
                parts.Add(detail);
            // Numbers are padded and monospaced so the HUD line keeps its width as they change.
            if (health)
            {
                string hpColor = ColorUtility.ToHtmlStringRGB(GetHealthColor(summon));
                parts.Add($"<color=#{hpColor}>HP <mspace={DigitWidth}>{summon.GetHealth(),3:0}/{summon.GetMaxHealth(),-3:0}</mspace></color>");
            }
            if (distance)
            {
                float meters = Vector3.Distance(player.transform.position, summon.transform.position);
                parts.Add(padded ? $"<mspace={DigitWidth}>{meters,3:0}</mspace> m" : $"{meters:0} m");
            }
            if (shield && TryGetShield(summon, out float remaining, out float total, out float seconds))
                parts.Add("<color=#73BFFF>" + Texts.Format("shield {0:0}/{1:0} ({2:0} s)", remaining, total, seconds) + "</color>");
            if (state)
                parts.Add(Texts.Get(IsFollowing(summon, player) ? "following" : "staying"));
            return string.Join(" · ", parts);
        }

        private static readonly AccessTools.FieldRef<SE_Shield, float> ShieldTotal =
            AccessTools.FieldRefAccess<SE_Shield, float>("m_totalAbsorbDamage");
        private static readonly AccessTools.FieldRef<SE_Shield, float> ShieldDamage =
            AccessTools.FieldRefAccess<SE_Shield, float>("m_damage");

        // The barrier from the Staff of Protection (SE_Shield is the game's only shield effect). Status
        // effects exist only on the summon's owner, normally its summoner; others see no shield.
        public static bool TryGetShield(Character summon, out float remaining, out float total, out float seconds)
        {
            foreach (var effect in summon.GetSEMan().GetStatusEffects())
            {
                if (!(effect is SE_Shield shield))
                    continue;
                total = ShieldTotal(shield);
                remaining = Mathf.Max(0f, total - ShieldDamage(shield));
                seconds = Mathf.Max(0f, shield.GetRemaningTime());
                return true;
            }
            remaining = total = seconds = 0f;
            return false;
        }
    }
}
