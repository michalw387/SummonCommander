using System.Collections.Generic;

namespace SummonCommander
{
    // The mod's own texts in the language chosen in the game (names of creatures and items come from the game's
    // own translations). The English text is the key; a language without a translation, or a text missing from
    // it, falls back to English. Read on every use, so a language change in the settings applies at once.
    internal static class Texts
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Translations =
            new Dictionary<string, Dictionary<string, string>>
            {
                ["Polish"] = new Dictionary<string, string>
                {
                    ["Skeletons"] = "Szkielety",
                    ["Spirits"] = "Duchy",
                    ["Summons"] = "Przywołańce",
                    ["Wolves"] = "Wilki",
                    ["Boars"] = "Dziki",
                    ["Moose"] = "Łosie",
                    ["Bears"] = "Niedźwiedzie",
                    ["Skeletons: {0}"] = "Szkielety: {0}",
                    ["Spirits: {0}"] = "Duchy: {0}",
                    ["Summons: {0} ({1} skeletons, {2} spirits)"] = "Przywołańce: {0} (szkielety: {1}, duchy: {2})",
                    ["({0} following, {1} staying)"] = "(podąża: {0}, czeka: {1})",
                    ["{0} staying"] = "czeka: {0}",
                    ["following"] = "podąża",
                    ["staying"] = "czeka",
                    ["Spirits: "] = "Duchy: ",
                    ["next: {0}"] = "następny: {0}",
                    ["random"] = "losowy",
                    ["Next spirit: {0}"] = "Następny duch: {0}",
                    ["Team: "] = "Drużyna: ",
                    ["sword & shield"] = "miecz i tarcza",
                    ["sword"] = "miecz",
                    ["bow"] = "łuk",
                    ["unarmed"] = "bez broni",
                    ["any"] = "dowolna",
                    ["shield {0:0}/{1:0} ({2:0} s)"] = "osłona {0:0}/{1:0} ({2:0} s)",
                    ["{0} is following you"] = "{0} podąża za tobą",
                    ["{0} following you: {1}"] = "{0} podążają za tobą: {1}",
                    ["{0} is staying"] = "{0} czeka",
                    ["{0} staying: {1}"] = "{0} czekają: {1}",
                    ["Dismissed: {0}"] = "Odesłano: {0}",
                    ["{0} dismissed: {1}"] = "{0} odesłane: {1}",
                    ["Your {0}: {1}"] = "Twoje {0}: {1}",
                    ["No player in the world."] = "Brak gracza w świecie.",
                    ["Usage: {0}"] = "Użycie: {0}",
                    ["Summon too far away - dismissed"] = "Przywołaniec za daleko - odesłany",
                    ["{0} summons too far away - dismissed"] = "Przywołańce za daleko - odesłane: {0}",
                    ["Summon over the summon limit - dismissed"] = "Przywołaniec ponad limit - odesłany",
                    ["{0} summons over the summon limit - dismissed"] = "Przywołańce ponad limit - odesłane: {0}",
                    // The name may be a woman's (a renamed summon), so no gendered verb.
                    ["{0} has died"] = "{0} nie żyje",
                    ["Skeleton"] = "Szkielet",
                    [" (staying)"] = " (czeka)",
                    ["Follow"] = "Podążaj",
                    ["Stay"] = "Czekaj",
                    ["Dismiss"] = "Odeślij"
                }
            };

        public static string Get(string english)
        {
            string language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : null;
            return language != null && Translations.TryGetValue(language, out var texts)
                && texts.TryGetValue(english, out var text) ? text : english;
        }

        public static string Format(string english, params object[] args)
        {
            return string.Format(Get(english), args);
        }
    }
}
