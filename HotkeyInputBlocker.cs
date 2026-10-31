using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SummonCommander
{
    // While a shortcut's modifiers are held, the game must not react to its main key: with Alt held,
    // F (Forsaken power) and G (emote wheel) should only trigger the mod's action.
    internal static class HotkeyInputBlocker
    {
        private delegate bool KeyCodeToKey(KeyCode keyCode, out Key key);

        // The game's own KeyCode -> Input System key mapping.
        private static readonly KeyCodeToKey TryKeyCodeToKey =
            AccessTools.MethodDelegate<KeyCodeToKey>(AccessTools.Method(typeof(ZInput), "TryKeyCodeToKey"));

        private static readonly HashSet<Key> Blocked = new HashSet<Key>();
        private static int _blockedFrame = -1;

        public static bool IsBlocked(string buttonName)
        {
            UpdateBlockedKeys();
            if (Blocked.Count == 0 || ZInput.instance == null)
                return false;

            var button = ZInput.instance.GetButtonDef(buttonName);
            if (button == null)
                return false;
            foreach (var control in button.ButtonAction.controls)
            {
                if (control is KeyControl key && Blocked.Contains(key.keyCode))
                    return true;
            }
            return false;
        }

        private static void UpdateBlockedKeys()
        {
            if (_blockedFrame == Time.frameCount)
                return;
            _blockedFrame = Time.frameCount;

            Blocked.Clear();
            foreach (var entry in SummonCommanderPlugin.Hotkeys)
            {
                var shortcut = entry.Value;
                // A shortcut without modifiers would take its key away from the game entirely.
                if (!shortcut.Modifiers.Any() || !shortcut.Modifiers.All(UnityInput.Current.GetKey))
                    continue;
                if (TryKeyCodeToKey(shortcut.MainKey, out var key))
                    Blocked.Add(key);
            }
        }
    }
}
