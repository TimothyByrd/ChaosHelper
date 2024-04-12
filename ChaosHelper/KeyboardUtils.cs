using GregsStack.InputSimulatorStandard;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VK = GregsStack.InputSimulatorStandard.Native.VirtualKeyCode;

namespace ChaosHelper
{
    internal static class KeyboardUtils
    {
        public class Key
        {
            public Key(VK vk, string str, bool shifted = false)
            {
                VK = vk;
                String = str;
                Shifted = shifted;
            }

            public VK VK { get; }
            public string String { get; }
            public bool Shifted { get; }

            public bool Matches(string str)
            {
                return string.Equals(String, str, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static readonly Key EnterKey = new(VK.RETURN, "Enter");

        private static readonly List<Key> ValidKeys = new()
        {
            EnterKey,
            new Key(VK.F1, "F1"),
            new Key(VK.F2, "F2"),
            new Key(VK.F3, "F3"),
            new Key(VK.F4, "F4"),
            new Key(VK.F5, "F5"),
            new Key(VK.F6, "F6"),
            new Key(VK.F7, "F7"),
            new Key(VK.F8, "F8"),
            new Key(VK.F9, "F9"),
            new Key(VK.F10, "F10"),
            new Key(VK.F11, "F11"),
            new Key(VK.F12, "F12"),
            new Key(VK.F13, "F13"),
            new Key(VK.F14, "F14"),
            new Key(VK.F15, "F15"),
            new Key(VK.F16, "F16"),

            new Key(VK.VK_0, "0"),
            new Key(VK.VK_1, "1"),
            new Key(VK.VK_2, "2"),
            new Key(VK.VK_3, "3"),
            new Key(VK.VK_4, "4"),
            new Key(VK.VK_5, "5"),
            new Key(VK.VK_6, "6"),
            new Key(VK.VK_7, "7"),
            new Key(VK.VK_8, "8"),
            new Key(VK.VK_9, "9"),

            new Key(VK.VK_A, "A", shifted: true),
            new Key(VK.VK_B, "B", shifted: true),
            new Key(VK.VK_C, "C", shifted: true),
            new Key(VK.VK_D, "D", shifted: true),
            new Key(VK.VK_E, "E", shifted: true),
            new Key(VK.VK_F, "F", shifted: true),
            new Key(VK.VK_G, "G", shifted: true),
            new Key(VK.VK_H, "H", shifted: true),
            new Key(VK.VK_I, "I", shifted: true),
            new Key(VK.VK_J, "J", shifted: true),
            new Key(VK.VK_K, "K", shifted: true),
            new Key(VK.VK_L, "L", shifted: true),
            new Key(VK.VK_M, "M", shifted: true),
            new Key(VK.VK_N, "N", shifted: true),
            new Key(VK.VK_O, "O", shifted: true),
            new Key(VK.VK_P, "P", shifted: true),
            new Key(VK.VK_Q, "Q", shifted: true),
            new Key(VK.VK_R, "R", shifted: true),
            new Key(VK.VK_S, "S", shifted: true),
            new Key(VK.VK_T, "T", shifted: true),
            new Key(VK.VK_U, "U", shifted: true),
            new Key(VK.VK_V, "V", shifted: true),
            new Key(VK.VK_W, "W", shifted: true),
            new Key(VK.VK_X, "X", shifted: true),
            new Key(VK.VK_Y, "Y", shifted: true),
            new Key(VK.VK_Z, "Z", shifted: true),

            new Key(VK.VK_A, "a"),
            new Key(VK.VK_B, "b"),
            new Key(VK.VK_C, "c"),
            new Key(VK.VK_D, "d"),
            new Key(VK.VK_E, "e"),
            new Key(VK.VK_F, "f"),
            new Key(VK.VK_G, "g"),
            new Key(VK.VK_H, "h"),
            new Key(VK.VK_I, "i"),
            new Key(VK.VK_J, "j"),
            new Key(VK.VK_K, "k"),
            new Key(VK.VK_L, "l"),
            new Key(VK.VK_M, "m"),
            new Key(VK.VK_N, "n"),
            new Key(VK.VK_O, "o"),
            new Key(VK.VK_P, "p"),
            new Key(VK.VK_Q, "q"),
            new Key(VK.VK_R, "r"),
            new Key(VK.VK_S, "s"),
            new Key(VK.VK_T, "t"),
            new Key(VK.VK_U, "u"),
            new Key(VK.VK_V, "v"),
            new Key(VK.VK_W, "w"),
            new Key(VK.VK_X, "x"),
            new Key(VK.VK_Y, "y"),
            new Key(VK.VK_Z, "z"),

            new Key(VK.TAB, "Tab"),
            new Key(VK.SPACE, "Space"),
            new Key(VK.BACK, "Backspace"),
            new Key(VK.HOME, "Home"),
            new Key(VK.DELETE, "Delete"),
            new Key(VK.END, "End"),
            new Key(VK.NEXT, "PageDown"),
            new Key(VK.PRIOR, "PageUp"),
            new Key(VK.UP, "Up"),
            new Key(VK.DOWN, "Down"),
            new Key(VK.LEFT, "Left"),
            new Key(VK.RIGHT, "Right"),
            new Key(VK.ESCAPE, "Esc"),

            //new Key(VKC.CAPITAL, "CapsLock"),
            //new Key(VKC.PRINT, "PrintScreen"),
            //new Key(VKC.SCROLL, "ScrollLock"),
            //new Key(VKC.INSERT, "Insert"),
            //new Key(VKC.OEM_MINUS, "-"),
            //new Key(VKC.OEM_PLUS, "="), // unshifted
            //new Key(VKC.OEM_COMMA, ","),
            //new Key(VKC.OEM_PERIOD, "."),
            //new Key(VKC.OEM_1, ";"),
            //new Key(VKC.OEM_2, "/"),
            //new Key(VKC.OEM_3, "`"),
            //new Key(VKC.OEM_4, "["),
            //new Key(VKC.OEM_5, "\\"),
            //new Key(VKC.OEM_6, "]"),
            //new Key(VKC.OEM_7, "'"),

            //// new Key(VKC.PAUSE, "Pause"),
            //// new Key(VKC.CANCEL, "Break"),
            //// new Key(VKC.HELP, "Help"),
            //// new Key(VKC.ZOOM, "Zoom"),

            //new Key(VKC.NUMLOCK, "NumLock"),
            //new Key(VKC.NUMPAD0, "Num0"),
            //new Key(VKC.NUMPAD1, "Num1"),
            //new Key(VKC.NUMPAD2, "Num2"),
            //new Key(VKC.NUMPAD3, "Num3"),
            //new Key(VKC.NUMPAD4, "Num4"),
            //new Key(VKC.NUMPAD5, "Num5"),
            //new Key(VKC.NUMPAD6, "Num6"),
            //new Key(VKC.NUMPAD7, "Num7"),
            //new Key(VKC.NUMPAD8, "Num8"),
            //new Key(VKC.NUMPAD9, "Num9"),
        };

        //private static readonly List<VKC> shiftModifiers = new() { VKC.SHIFT };

        public class KeyEntry
        {
            public List<VK> Keys { get; set; }
            public List<VK> Modifiers { get; set; }
            public string Text { get; set; }

            public bool ModifiersAreTheSame(List<VK> modifiers)
            {
                if (Modifiers == null && modifiers == null) return true;
                if (Modifiers == null || modifiers == null) return false;
                return Modifiers.SequenceEqual(modifiers);
            }

            override public string ToString()
            {
                var modStr = string.Empty;
                if (Modifiers != null)
                {
                    if (Modifiers.Contains(VK.SHIFT)) modStr += "+";
                    if (Modifiers.Contains(VK.CONTROL)) modStr += "^";
                    if (Modifiers.Contains(VK.MENU)) modStr += "!";
                }
                return string.Join("", Keys.Select(x => $"{{{modStr}{x}}}"));
            }
        }

        public static void SendKeys(List<KeyEntry> entries)
        {
            var keyboard = new KeyboardSimulator();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Text))
                    keyboard.TextEntry(entry.Text);
                else
                    keyboard.ModifiedKeyStroke(entry.Modifiers, entry.Keys);
            }
        }

        public static string ToString(this List<KeyEntry> entries)
        {
            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Text))
                    sb.Append($" '{entry.Text}'");
                else
                    sb.Append($" {entry.ToString()}");
            }

            return sb.ToString();
        }

        private static readonly char[] specialChars = "{\r".ToCharArray();

        public static (List<KeyEntry> entries, bool didSubstitution) StringToKeys(string s)
        {
            var result = new List<KeyEntry>();

            KeyEntry currentEntry = null;

            bool didSubstitution = false;
            int i = 0;
            int len = s.Length;
            while (i < len)
            {
                int k = s.IndexOfAny(specialChars, i);
                if (k == -1)
                {
                    AddText(s.Substring(i));
                    i = len;
                    break;
                }
                else if (k > 0)
                {
                    AddText(s.Substring(i, k - i));
                    i = k;
                }

                if (s[i] == '\r')
                {
                    AddKey(EnterKey, null);
                    ++i;
                    continue;
                }

                if (i < len - 1 && s[i] == '{')
                {
                    if (s[i + 1] == '{') // use "{{" for an open brace
                    {
                        AddChar('{');
                        i += 2;
                        continue;
                    }
                    if (s[i + 1] == '}' && i < len - 2 && s[i + 2] == '}') // use "{}}" for a close brace
                    {
                        AddChar('}');
                        i += 3;
                        continue;
                    }

                    int j = s.IndexOf('}', i + 2);
                    if (j != -1)
                    {
                        (int i2, var modifiers) = ParseKeyModifiers(s, i + 1);
                        var subStr = s.Substring(i2, j - i2);
                        if (string.Equals(subStr, "Character", StringComparison.OrdinalIgnoreCase))
                        {
                            AddText(Config.Character);
                            didSubstitution = true;
                            i = j + 1;
                            continue;
                        }
                        if (string.Equals(subStr, "Whisper", StringComparison.OrdinalIgnoreCase))
                        {
                            AddText(Config.LastWhisper);
                            didSubstitution = true;
                            i = j + 1;
                            continue;
                        }
                       
                        var key = ValidKeys.FirstOrDefault(x => x.Matches(subStr));
                        if (key != null)
                        {
                            AddKey(key, modifiers);
                            i = j + 1;
                            continue;
                        }
                    }
                }
                AddChar(s[i]);
                ++i;
            }

            if (currentEntry != null)
                result.Add(currentEntry);

            return (result, didSubstitution);

            static (int pos, List<VK> modifiers) ParseKeyModifiers(string s, int pos)
            {
                List<VK> modifiers = new();
                var len = s.Length;
                while (pos < len && "^+!#".Contains(s[pos]))
                {
                    switch (s[pos])
                    {
                    case '!': // alt
                        modifiers.Add(VK.MENU);
                        break;
                    case '^': // ctrl
                        modifiers.Add(VK.CONTROL);
                        break;
                    case '+': // shift
                        modifiers.Add(VK.SHIFT);
                        break;
                    }
                    ++pos;
                }
                if (modifiers.Count == 0)
                    modifiers = null;
                return (pos, modifiers);
            }

            void WantEntryForCharacter()
            {
                var matches = currentEntry != null && currentEntry.Text != null;
                if (!matches)
                    CommitCurrentEntry();
                currentEntry.Text ??= string.Empty;
            }

            void CommitCurrentEntry()
            {
                if (currentEntry != null)
                    result.Add(currentEntry);
                currentEntry = new();
            }

            void AddChar(char c)
            {
                //var key = ValidKeys.FirstOrDefault(x => x.String.Length == 1 && x.String[0] == c);
                //if (key != null)
                //{
                //    var modifiers = key.Shifted ? shiftModifiers : null;
                //    AddKey(key, modifiers);
                //}

                WantEntryForCharacter();
                currentEntry.Text += c;
            }

            void AddText(string s)
            {
                WantEntryForCharacter();
                currentEntry.Text += s;
            }

            void AddKey(Key k, List<VK> modifiers)
            {
                WantEntryWithModifiers(modifiers);
                currentEntry.Keys.Add(k.VK);
            }

            void WantEntryWithModifiers(List<VK> modifiers)
            {
                var matches = currentEntry != null && currentEntry.Text == null && currentEntry.ModifiersAreTheSame(modifiers);
                if (!matches)
                    CommitCurrentEntry();
                currentEntry.Keys ??= new();
                currentEntry.Modifiers ??= modifiers;
            }
        }
    }
}
