using System.Collections.Generic;

namespace SummonCommander
{
    // Summon actions shared by the chat/console command (/sc [list|follow|stay|dismiss] [all|skeletons|spirits])
    // and the hotkeys. Actions apply to all of the player's summons of the given kind (None = all kinds), or
    // only to `only` when given.
    internal static class SummonCommands
    {
        private const string Syntax = "sc [list|follow|stay|dismiss] [all|skeletons|spirits]";

        private static readonly List<string> Subcommands = new List<string> { "list", "follow", "stay", "dismiss" };

        public static void Register()
        {
            new Terminal.ConsoleCommand("sc",
                "[list|follow|stay|dismiss] [all|skeletons|spirits] - manage your Dead Raiser skeletons and Spirit Caller spirits",
                Run, optionsFetcher: () => Subcommands);
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                args.Context.AddString(Texts.Get("No player in the world."));
                return;
            }

            string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "list";
            if (!TryParseKind(args.Length > 2 ? args[2].ToLowerInvariant() : "all", out var kind))
            {
                args.Context.AddString(Texts.Format("Usage: {0}", Syntax));
                return;
            }
            switch (sub)
            {
                case "list":
                    List(args.Context, player, kind);
                    break;
                case "follow":
                    args.Context.AddString(Follow(player, kind: kind));
                    break;
                case "stay":
                    args.Context.AddString(Stay(player, kind: kind));
                    break;
                case "dismiss":
                    args.Context.AddString(Dismiss(player, kind: kind));
                    break;
                default:
                    args.Context.AddString(Texts.Format("Usage: {0}", Syntax));
                    break;
            }
        }

        private static bool TryParseKind(string text, out SummonKind kind)
        {
            switch (text)
            {
                case "all":
                    kind = SummonKind.None;
                    return true;
                case "skeletons":
                case "skeleton":
                case "sk":
                    kind = SummonKind.Skeleton;
                    return true;
                case "spirits":
                case "spirit":
                    kind = SummonKind.Spirit;
                    return true;
                default:
                    kind = SummonKind.None;
                    return false;
            }
        }

        public static string Follow(Player player, Character only = null, SummonKind kind = SummonKind.None)
        {
            var summons = Targets(player, only, kind);
            foreach (var summon in summons)
                Summons.SetFollowing(summon, player, follow: true);
            return only != null
                ? Texts.Format("{0} is following you", Summons.GetName(only))
                : Texts.Format("{0} following you: {1}", Label(kind), summons.Count);
        }

        public static string Stay(Player player, Character only = null, SummonKind kind = SummonKind.None)
        {
            var summons = Targets(player, only, kind);
            foreach (var summon in summons)
                Summons.SetFollowing(summon, player, follow: false);
            return only != null
                ? Texts.Format("{0} is staying", Summons.GetName(only))
                : Texts.Format("{0} staying: {1}", Label(kind), summons.Count);
        }

        public static string Dismiss(Player player, Character only = null, SummonKind kind = SummonKind.None)
        {
            var summons = Targets(player, only, kind);
            foreach (var summon in summons)
                Summons.Dismiss(summon);
            return only != null
                ? Texts.Format("Dismissed: {0}", Summons.GetName(only))
                : Texts.Format("{0} dismissed: {1}", Label(kind), summons.Count);
        }

        public static void List(Terminal terminal, Player player, SummonKind kind = SummonKind.None)
        {
            var summons = Targets(player, null, kind);
            terminal.AddString(Texts.Format("Your {0}: {1}", Label(kind).ToLowerInvariant(), summons.Count));
            for (int i = 0; i < summons.Count; i++)
                terminal.AddString($"  #{i + 1}  {Summons.Describe(summons[i], player)}");
        }

        private static string Label(SummonKind kind)
        {
            switch (kind)
            {
                case SummonKind.Skeleton: return Texts.Get("Skeletons");
                case SummonKind.Spirit: return Texts.Get("Spirits");
                default: return Texts.Get("Summons");
            }
        }

        private static List<Character> Targets(Player player, Character only, SummonKind kind)
        {
            if (only != null)
                return new List<Character> { only };
            var summons = new List<Character>();
            Summons.FindOwned(player, summons);
            return kind == SummonKind.None ? summons : Summons.OfKind(summons, kind);
        }
    }
}
