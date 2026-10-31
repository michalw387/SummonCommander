using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SummonCommander
{
    [HarmonyPatch]
    internal static class SummonPatches
    {
        private static readonly System.Type HudDataType = AccessTools.Inner(typeof(EnemyHud), "HudData");
        private static readonly FieldInfo HudHoverTimer = AccessTools.Field(HudDataType, "m_hoverTimer");
        private static readonly FieldInfo HudHealthFast = AccessTools.Field(HudDataType, "m_healthFast");
        private static readonly FieldInfo HudHealthFastFriendly = AccessTools.Field(HudDataType, "m_healthFastFriendly");

        // The Spirit Caller's spawn ability picks one of its prefabs at random (SpawnAbility.Spawn, started by
        // Setup); with Summon.NextSpirit set, leave it only the chosen species. The ability is a per-cast instance.
        [HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.Setup))]
        [HarmonyPrefix]
        private static void ChooseSpiritSpecies(SpawnAbility __instance, Character owner)
        {
            string prefabName = Summons.GetSpiritPrefab(SummonCommanderPlugin.NextSpirit.Value);
            if (prefabName == null || owner == null || owner != Player.m_localPlayer || __instance.m_spawnPrefab == null)
                return;
            foreach (var prefab in __instance.m_spawnPrefab)
            {
                if (prefab != null && prefab.name == prefabName)
                {
                    __instance.m_spawnPrefab = new[] { prefab };
                    return;
                }
            }
        }

        // The game shows creature health bars only within 10 m; extend that for our summons.
        [HarmonyPatch(typeof(EnemyHud), "TestShow")]
        [HarmonyPostfix]
        private static void ExtendHealthBarRange(Character c, Vector3 ___m_refPoint, ref bool __result)
        {
            if (__result || !SummonCommanderPlugin.HealthBars.Value || !SummonCommanderPlugin.IsOwned(c))
                return;
            float max = SummonCommanderPlugin.HealthBarDistance.Value;
            __result = (c.transform.position - ___m_refPoint).sqrMagnitude < max * max;
        }

        // Colors our summons' bars by health (ColorByHealth). Also, a creature's bar hides a while after it was
        // last hovered, so with AlwaysShow keep resetting that timer for our summons.
        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        [HarmonyPrefix]
        private static void UpdateOwnedHealthBars(object ___m_huds)
        {
            foreach (DictionaryEntry hud in (IDictionary)___m_huds)
            {
                var character = (Character)hud.Key;
                if (!SummonCommanderPlugin.IsOwned(character))
                    continue;

                if (SummonCommanderPlugin.HealthBars.Value)
                    HudHoverTimer.SetValue(hud.Value, 0f);

                SetBarColor((GuiBar)HudHealthFast.GetValue(hud.Value), character);
                SetBarColor((GuiBar)HudHealthFastFriendly.GetValue(hud.Value), character);
            }
        }

        // Resetting when disabled restores the game's colors if the option is turned off mid-game.
        private static void SetBarColor(GuiBar bar, Character character)
        {
            if (bar == null)
                return;
            if (SummonCommanderPlugin.HealthBarColors.Value)
                bar.SetColor(Summons.GetHealthColor(character));
            else
                bar.ResetColor();
        }

        // When a summon starts following its player the game unsummons the oldest ones of its type above the
        // limit, but it only counts summons following that player, so ones told to stay let you exceed the
        // limit. For Dead Raiser and Spirit Caller summons count all of the player's summons of that type
        // (same m_name, as the game groups them), staying ones included, and dismiss the weakest
        // (Summon.DismissWeakest); a new skeleton then gets the weapon of the dismissed one's slot.
        [HarmonyPatch(typeof(Tameable), "UnsummonMaxInstances")]
        [HarmonyPrefix]
        private static bool EnforceSummonLimit(Tameable __instance, int maxInstances,
            ZNetView ___m_nview, MonsterAI ___m_monsterAI, Character ___m_character)
        {
            if (!Summons.IsSummon(__instance))
                return true;
            if (___m_character == null)
                return false;
            if (___m_nview == null || !___m_nview.IsValid() || !___m_nview.IsOwner() || ___m_monsterAI == null)
                return false;

            var followTarget = ___m_monsterAI.GetFollowTarget();
            var player = followTarget != null ? followTarget.GetComponent<Player>() : null;
            if (player == null)
                return false;

            var summons = new List<Character>();
            Summons.FindOwned(player, summons);
            var group = Summons.SameLimitGroup(summons, ___m_character);
            if (Summons.DismissExcess(group, maxInstances) > 0 && Player.m_localPlayer != null)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, __instance.m_maxSummonReached);
            return false;
        }

        // Summons do not come along through portals, and their area unloads once the player is gone.
        [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
        [HarmonyPostfix]
        private static void DismissSummonsLeftByTeleport(Player __instance, Vector3 pos, bool __result)
        {
            if (__result && __instance == Player.m_localPlayer)
                SummonCommanderPlugin.DismissFarSummons(__instance, pos);
        }

        // The player respawns at their bed, or at the home point without one (as the game assumes in
        // PlayerProfile when saving), and the area where they died unloads.
        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
        [HarmonyPostfix]
        private static void DismissSummonsLeftByDeath(Player __instance)
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (__instance != Player.m_localPlayer || profile == null)
                return;
            var spawnPoint = profile.HaveCustomSpawnPoint() ? profile.GetCustomSpawnPoint() : profile.GetHomePoint();
            SummonCommanderPlugin.DismissFarSummons(__instance, spawnPoint);
        }

        // The game reads its key bindings through these; see HotkeyInputBlocker.
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
        [HarmonyPrefix]
        private static bool BlockButtonDown(string name, ref bool __result) => AllowButton(name, ref __result);

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButton))]
        [HarmonyPrefix]
        private static bool BlockButton(string name, ref bool __result) => AllowButton(name, ref __result);

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonUp))]
        [HarmonyPrefix]
        private static bool BlockButtonUp(string name, ref bool __result) => AllowButton(name, ref __result);

        private static bool AllowButton(string name, ref bool __result)
        {
            if (!HotkeyInputBlocker.IsBlocked(name))
                return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
        [HarmonyPostfix]
        private static void ChooseSummonLoadout(Humanoid __instance)
        {
            SkeletonLoadouts.Apply(__instance);
        }

        // Summons following a player stop at Movement.FollowDistance and keep Movement.PersonalSpace; see
        // SummonMovement.Follow. Applies to every summon this game simulates, not only the local player's.
        [HarmonyPatch(typeof(BaseAI), "Follow")]
        [HarmonyPrefix]
        private static bool FollowAtDistance(BaseAI __instance, GameObject go, float dt, Character ___m_character)
        {
            if (!SummonCommanderPlugin.ImprovedFollow.Value || go == null || ___m_character == null
                || !Summons.IsSummon(___m_character))
                return true;
            SummonMovement.Follow(__instance, ___m_character, go.transform.position, dt);
            return false;
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        [HarmonyPostfix]
        private static void AddHotkeyHints(Tameable __instance, ref string __result)
        {
            if (SummonCommanderPlugin.IsOwned(__instance.GetComponent<Character>()))
                __result += SummonCommanderPlugin.GetHoverHints();
        }
    }
}
