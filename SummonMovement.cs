using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SummonCommander
{
    // Keeps summons from boxing the player in: the player walks through them (Movement.PassThrough*), and
    // following summons stop at a distance from the player instead of pushing towards the player's position
    // (Movement.FollowDistance, Movement.PersonalSpace).
    internal static class SummonMovement
    {
        // Vanilla BaseAI.Follow runs above 10 m and stops within 3 m of the player, whatever the creature's size.
        private const float RunBeyondStop = 7f;
        // PersonalSpace stays this far inside FollowDistance, so a summon backing off stops before it would
        // have to come closer again.
        private const float MinBackOffGap = 1f;

        private static readonly Func<BaseAI, float, Vector3, float, bool, bool> MoveTo =
            AccessTools.MethodDelegate<Func<BaseAI, float, Vector3, float, bool, bool>>(
                AccessTools.Method(typeof(BaseAI), "MoveTo"));

        private static readonly List<Collider> PlayerColliders = new List<Collider>();
        private static readonly List<Collider> SummonColliders = new List<Collider>();

        // Called on every scan, for every loaded summon of any player (a friend's spirits block you just the
        // same). Unity forgets an ignored pair when either collider is disabled, so pairs are set again each
        // time; turning an option off restores the collisions on the next scan.
        public static void UpdateCollisions(Player player) => SetCollisions(player, IsPassThrough);

        // When the mod unloads.
        public static void RestoreCollisions(Player player) => SetCollisions(player, _ => false);

        private static void SetCollisions(Player player, Func<SummonKind, bool> ignore)
        {
            if (player == null)
                return;
            GetSolidColliders(player, PlayerColliders);
            if (PlayerColliders.Count == 0)
                return;
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character == player)
                    continue;
                var kind = Summons.GetKind(character);
                if (kind != SummonKind.None)
                    SetCollisionIgnored(character, ignore(kind));
            }
        }

        private static bool IsPassThrough(SummonKind kind)
        {
            return kind == SummonKind.Spirit
                ? SummonCommanderPlugin.PassThroughSpirits.Value
                : SummonCommanderPlugin.PassThroughSkeletons.Value;
        }

        // Uses PlayerColliders as filled by the caller.
        private static void SetCollisionIgnored(Character summon, bool ignore)
        {
            GetSolidColliders(summon, SummonColliders);
            foreach (var summonCollider in SummonColliders)
            {
                foreach (var playerCollider in PlayerColliders)
                    Physics.IgnoreCollision(playerCollider, summonCollider, ignore);
            }
        }

        // Triggers (hit boxes, interaction areas) don't block movement, and IgnoreCollision fails on inactive colliders.
        private static void GetSolidColliders(Component root, List<Collider> result)
        {
            root.GetComponentsInChildren(includeInactive: false, result);
            result.RemoveAll(collider => collider.isTrigger || !collider.enabled);
        }

        // Replaces BaseAI.Follow (stop within 3 m of the player's position) for summons. The summon heads for
        // the player and stops once its edge is within FollowDistance of the player, so a big moose stops
        // farther away than a skeleton, and summons coming from different sides spread around the player
        // instead of crowding onto one point. A summon closer than PersonalSpace (e.g. after the player
        // walked into the group) steps away. The AI calls Follow only while the summon has no enemy to fight.
        public static void Follow(BaseAI ai, Character summon, Vector3 target, float dt)
        {
            float radius = summon.GetRadius();
            float followDistance = SummonCommanderPlugin.FollowDistance.Value;
            float stopDistance = followDistance + radius;

            Vector3 away = summon.transform.position - target;
            float distance = away.magnitude;

            float personalSpace = Mathf.Min(SummonCommanderPlugin.PersonalSpace.Value, followDistance - MinBackOffGap);
            if (personalSpace > 0f && distance < personalSpace + radius)
            {
                away.y = 0f;
                // Standing right on the player: step back the way the summon is facing from.
                if (away.sqrMagnitude < 0.01f)
                    away = -summon.transform.forward;
                ai.MoveTowards(away, run: false);
                return;
            }

            if (distance <= stopDistance)
            {
                ai.StopMoving();
                return;
            }
            MoveTo(ai, dt, target, stopDistance, distance > stopDistance + RunBeyondStop);
        }
    }
}
