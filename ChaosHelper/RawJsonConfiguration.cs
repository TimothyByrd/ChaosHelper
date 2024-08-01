using Process.NET.Native.Types;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChaosHelper
{
    public class RawJsonConfiguration(string fileName)
    {
        readonly JsonElement rawConfig = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(fileName), SerializerOptions);

        static public JsonSerializerOptions SerializerOptions { get; private set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        public string this[string s]
        {
            get { return GetString(s); }
        }

        public string GetString(string s)
        {
            if (rawConfig.TryGetProperty(s, out var value))
                return value.ToString();
            return String.Empty;
        }

        public int GetInt(string s, int defaultValue = 0)
        {
            if (rawConfig.TryGetProperty(s, out var value) && value.TryGetInt32(out var intVal))
                return intVal;
            return defaultValue;
        }

        public double GetDouble(string s, double defaultValue = 0.0)
        {
            if (rawConfig.TryGetProperty(s, out var value) && value.TryGetDouble(out var doubleVal))
                return doubleVal;
            return defaultValue;
        }

        public bool GetBoolean(string s, bool defaultValue = false)
        {
            return GetBoolean(rawConfig, s, defaultValue);
        }

        public static bool GetBoolean(JsonElement e, string s, bool defaultValue = false)
        {
            if (e.TryGetProperty(s, out var value))
            {
                if (value.ValueKind == JsonValueKind.True) return true;
                if (value.ValueKind == JsonValueKind.False) return false;
            }
            return defaultValue;
        }

        public JsonElement.ArrayEnumerator GetArray(string s)
        {
            if (rawConfig.TryGetProperty(s, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray();
            return default;
        }

        public List<string> GetStringList(string s)
        {
            var result = new List<string>();
            var array = GetArray(s);
            while (array.MoveNext())
                result.Add(array.Current.GetString());
            return result;
        }

        public List<int> GetColorList(string s)
        {
            var result = new List<int>();
            var array = GetArray(s);
            while (array.MoveNext())
            {
                var colorStr = array.Current.GetString().CheckColorString();
                var intVal = colorStr.ColorStringToRGB();
                if (intVal >= 0)
                    result.Add(intVal);
            }
            return result;
        }

        public System.Drawing.Rectangle GetRectangle(string s)
        {
            var array = GetArray(s);
            array.MoveNext();
            var x = array.Current.GetInt32();
            array.MoveNext();
            var y = array.Current.GetInt32();
            array.MoveNext();
            var w = array.Current.GetInt32();
            array.MoveNext();
            var h = array.Current.GetInt32();
            return new System.Drawing.Rectangle(x, y, w, h);
        }

        public List<HotkeyEntry> GetHotkeys()
        {
            var result = new List<HotkeyEntry>();
            var array = GetArray("hotkeys");
            while (array.MoveNext())
            {
                var enabled = GetBoolean(array.Current, "enabled", true);
                if (!enabled) continue;

                var key = array.Current.GetStringOrDefault("key");
                var binding = HotKeyBinding.FromString(key);
                if (binding == null)
                    continue;

                var command = array.Current.GetStringOrDefault("command");
                var text = array.Current.GetStringOrDefault("text");
                if (string.IsNullOrEmpty(command) && string.IsNullOrEmpty(text))
                    continue;
                var entry = new HotkeyEntry()
                {
                    Binding = binding,
                    Command = command,
                    Text = text,
                    Enabled = enabled,
                };
                result.Add(entry);
            }
            return result;
        }

        public bool TryGetProperty(string propertyName, out JsonElement value)
        {
            return rawConfig.TryGetProperty(propertyName, out value);
        }
    }

    public class HotkeyEntry
    {
        public HotKeyBinding Binding { get; set; }
        public string Command { get; set; }
        public string Text { get; set; }
        public bool Enabled { get; set; } = true;
        public Keys[] Keys { get; set; }

        public bool Matches(ConsoleHotKey.HotKeyEventArgs e)
        {
            return e.Key == Binding.Key && e.Modifiers == Binding.Modifiers;
        }

        public bool CommandIs(string command)
        {
            return string.Equals(Command, command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class HotKeyBinding
    {
        public System.Windows.Forms.Keys Key { get; set; }
        public ConsoleHotKey.KeyModifiers Modifiers { get; set; }

        public static HotKeyBinding FromString(string valueStr)
        {
            ConsoleHotKey.KeyModifiers modifiers = ConsoleHotKey.KeyModifiers.None;
            while (valueStr.Length > 0 && "^+!#".Contains(valueStr[0]))
            {
                switch (valueStr[0])
                {
                    case '!': // alt
                        modifiers |= ConsoleHotKey.KeyModifiers.Alt;
                        break;
                    case '^': // ctrl
                        modifiers |= ConsoleHotKey.KeyModifiers.Control;
                        break;
                    case '+': // shift
                        modifiers |= ConsoleHotKey.KeyModifiers.Shift;
                        break;
                    case '#': // windows
                        modifiers |= ConsoleHotKey.KeyModifiers.Windows;
                        break;
                }
                valueStr = valueStr[1..];
            }

            if (Enum.TryParse(valueStr, true, out System.Windows.Forms.Keys key)
                && Enum.IsDefined(typeof(System.Windows.Forms.Keys), key))
            {

                return new HotKeyBinding
                {
                    Key = key,
                    Modifiers = modifiers,
                };
            }

            return null;
        }
    }
}