using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChaosHelper
{
    public class RawJsonConfiguration
    {
        readonly JsonElement rawConfig;

        public RawJsonConfiguration(string fileName)
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            rawConfig = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(fileName), options);
        }

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

        public bool GetBoolean(string s, bool defaultValue = false)
        {
            if (rawConfig.TryGetProperty(s, out var value))
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

        public HotKeyBinding GetHotKey(string s)
        {
            if (rawConfig.TryGetProperty(s, out var value))
            {
                var valueStr = value.GetString();
                int modifiers = 0;
                while (valueStr.Length > 0 && "^+!".IndexOf(valueStr[0]) >= 0)
                {
                    switch (valueStr[0])
                    {
                    case '^': // ctrl
                        modifiers |= 2;
                        break;
                    case '+': // shift
                        modifiers |= 4;
                        break;
                    case '!': // alt
                        modifiers |= 1;
                        break;
                    }
                    valueStr = valueStr.Substring(1);
                }

                if (Enum.TryParse(valueStr, true, out System.Windows.Forms.Keys key)
                    && Enum.IsDefined(typeof(System.Windows.Forms.Keys), key))
                {
                    return new HotKeyBinding
                    {
                        Key = key,
                        Modifiers = (ConsoleHotKey.KeyModifiers)modifiers,
                    };
                }
            }

           return null;
        }
    }
    public class HotKeyBinding
    {
        public System.Windows.Forms.Keys Key { get; set; }
        public ConsoleHotKey.KeyModifiers Modifiers { get; set; }
    }
}