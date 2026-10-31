using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace SummonCommander
{
    internal enum SkeletonLoadout
    {
        Random,
        SwordAndShield,
        Bow
    }

    // Choices for one team member (SkeletonTeam.Slot1-4); Random accepts any weapon.
    internal enum TeamSlot
    {
        Random,
        SwordAndShield,
        Bow
    }

    // Humanoid.GiveDefaultItems rolls a creature's equipment from m_seed (stored in its ZDO) every time
    // it loads. For a freshly summoned skeleton we reroll with other seeds until it gets the preferred
    // loadout and store that seed, so the choice persists across reloads and is the same for every client.
    // The roll is checked on the items actually given rather than predicted: giving an item instantiates
    // it, which can consume random numbers between the rolls.
    internal static class SkeletonLoadouts
    {
        private const double NewSpawnSeconds = 5.0;
        private const int MaxAttempts = 200;

        private static readonly AccessTools.FieldRef<Humanoid, int> Seed =
            AccessTools.FieldRefAccess<Humanoid, int>("m_seed");

        private static bool _rerolling;

        // Called after Humanoid.GiveDefaultItems.
        public static void Apply(Humanoid humanoid)
        {
            if (_rerolling || !SummonCommanderPlugin.SkeletonTeamEnabled.Value || !Summons.IsSkeleton(humanoid))
                return;

            // Only the summoner owns a new skeleton; older skeletons keep the weapon they already have.
            var nview = humanoid.GetComponent<ZNetView>();
            var ai = humanoid.GetBaseAI();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()
                || ai == null || ai.GetTimeSinceSpawned().TotalSeconds > NewSpawnSeconds)
                return;
            var loadout = ChooseForTeam(humanoid, nview.GetZDO().GetInt(ZDOVars.s_maxInstances));
            if (loadout == SkeletonLoadout.Random)
                return;

            int seed = Seed(humanoid);
            var inventory = humanoid.GetInventory();
            _rerolling = true;
            try
            {
                for (int i = 1; i < MaxAttempts && !Matches(loadout, inventory.GetAllItems()); i++)
                {
                    int candidate = unchecked(seed + i * 7919);
                    if (candidate == 0)
                        continue;
                    humanoid.UnequipAllItems();
                    inventory.RemoveAll();
                    Seed(humanoid) = candidate;
                    nview.GetZDO().Set(ZDOVars.s_seed, candidate);
                    humanoid.GiveDefaultItems();
                }
            }
            finally
            {
                _rerolling = false;
            }
        }

        // A melee roll does not always include a shield, so SwordAndShield requires both a melee weapon and a shield.
        private static bool Matches(SkeletonLoadout loadout, List<ItemDrop.ItemData> items)
        {
            Summons.Classify(items, out bool bow, out bool melee, out bool shield);
            return loadout == SkeletonLoadout.Bow ? bow : !bow && melee && shield;
        }

        // By the time the new skeleton gets its items, the summon limit has already dismissed the oldest
        // skeletons, and their ZNetViews are invalid, so FindOwned leaves them out.
        private static SkeletonLoadout ChooseForTeam(Humanoid newSkeleton, int maxInstances)
        {
            var player = Player.m_localPlayer;
            if (player == null)
                return SkeletonLoadout.Random;

            var summons = new List<Character>();
            Summons.FindOwned(player, summons);
            var others = Summons.OfKind(summons, SummonKind.Skeleton);
            others.Remove(newSkeleton);
            var target = GetTeamTarget(maxInstances);
            var filled = FillSlots(target, others);
            // The first slot not taken by an existing skeleton; Random leaves the game's roll as it is.
            int free = filled.IndexOf(false);
            return free >= 0 ? target[free] : target[0];
        }

        // The first maxInstances team slots; all of them when the staff sets no limit.
        public static List<SkeletonLoadout> GetTeamTarget(int maxInstances)
        {
            var slots = SummonCommanderPlugin.SkeletonTeamSlots.Select(slot => ToLoadout(slot.Value));
            return (maxInstances > 0 ? slots.Take(maxInstances) : slots).ToList();
        }

        // Which slots the skeletons occupy: each one takes a slot of its own type first, the rest take
        // Random slots ("any").
        private static List<bool> FillSlots(List<SkeletonLoadout> target, List<Character> skeletons)
        {
            var filled = target.Select(_ => false).ToList();
            var unplaced = new List<SkeletonLoadout>();
            foreach (var type in skeletons.Select(Summons.GetLoadout))
            {
                int slot = type == SkeletonLoadout.Random ? -1 : FindSlot(target, filled, type);
                if (slot >= 0)
                    filled[slot] = true;
                else
                    unplaced.Add(type);
            }
            foreach (var _ in unplaced)
            {
                int slot = FindSlot(target, filled, SkeletonLoadout.Random);
                if (slot >= 0)
                    filled[slot] = true;
            }
            return filled;
        }

        private static int FindSlot(List<SkeletonLoadout> target, List<bool> filled, SkeletonLoadout type)
        {
            for (int i = 0; i < target.Count; i++)
            {
                if (!filled[i] && target[i] == type)
                    return i;
            }
            return -1;
        }

        private static SkeletonLoadout ToLoadout(TeamSlot slot)
        {
            switch (slot)
            {
                case TeamSlot.SwordAndShield: return SkeletonLoadout.SwordAndShield;
                case TeamSlot.Bow: return SkeletonLoadout.Bow;
                default: return SkeletonLoadout.Random;
            }
        }

        // "Team: sword & shield 1/2 · bow 2/2 · any 0/1" for the HUD, from the player's skeletons (newest
        // first). The limit is read from the newest skeleton, which got it from the staff it was summoned with.
        public static string DescribeTeam(List<Character> skeletons)
        {
            int maxInstances = skeletons.Count > 0
                ? Summons.GetMaxInstances(skeletons[0])
                : 0;
            var target = GetTeamTarget(maxInstances);
            var filled = FillSlots(target, skeletons);
            var parts = new[] { SkeletonLoadout.SwordAndShield, SkeletonLoadout.Bow, SkeletonLoadout.Random }
                .Select(type => (type,
                    count: Enumerable.Range(0, target.Count).Count(i => target[i] == type && filled[i]),
                    wanted: target.Count(t => t == type)))
                .Where(entry => entry.wanted > 0)
                .Select(entry => $"{DescribeSlot(entry.type)} {entry.count}/{entry.wanted}");
            return Texts.Get("Team: ") + string.Join(" · ", parts);
        }

        private static string DescribeSlot(SkeletonLoadout type)
        {
            switch (type)
            {
                case SkeletonLoadout.SwordAndShield: return Texts.Get("sword & shield");
                case SkeletonLoadout.Bow: return Texts.Get("bow");
                default: return Texts.Get("any");
            }
        }
    }
}
