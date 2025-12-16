using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AutoRegressionVM.Helpers
{
    /// <summary>
    /// 간단한 JSON 직렬화/역직렬화 헬퍼
    /// 외부 라이브러리 없이 기본 기능 제공
    /// </summary>
    public static class SimpleJson
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";
            
            var sb = new StringBuilder();
            SerializeValue(obj, sb);
            return sb.ToString();
        }

        private static void SerializeValue(object value, StringBuilder sb)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            var type = value.GetType();

            if (type == typeof(string))
            {
                sb.Append($"\"{EscapeString((string)value)}\"");
            }
            else if (type == typeof(bool))
            {
                sb.Append((bool)value ? "true" : "false");
            }
            else if (type.IsPrimitive || type == typeof(decimal))
            {
                sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (type == typeof(DateTime))
            {
                sb.Append($"\"{((DateTime)value):yyyy-MM-ddTHH:mm:ss}\"");
            }
            else if (type == typeof(TimeSpan))
            {
                sb.Append($"\"{value}\"");
            }
            else if (type.IsEnum)
            {
                sb.Append($"\"{value}\"");
            }
            else if (value is IDictionary dict)
            {
                SerializeDictionary(dict, sb);
            }
            else if (value is IEnumerable enumerable)
            {
                SerializeArray(enumerable, sb);
            }
            else
            {
                SerializeObject(value, sb);
            }
        }

        private static void SerializeObject(object obj, StringBuilder sb)
        {
            sb.Append("{");
            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            bool first = true;

            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;
                
                try
                {
                    var value = prop.GetValue(obj);
                    if (!first) sb.Append(",");
                    first = false;
                    
                    sb.Append($"\"{prop.Name}\":");
                    SerializeValue(value, sb);
                }
                catch
                {
                    // Skip properties that throw exceptions
                }
            }
            sb.Append("}");
        }

        private static void SerializeArray(IEnumerable array, StringBuilder sb)
        {
            sb.Append("[");
            bool first = true;
            foreach (var item in array)
            {
                if (!first) sb.Append(",");
                first = false;
                SerializeValue(item, sb);
            }
            sb.Append("]");
        }

        private static void SerializeDictionary(IDictionary dict, StringBuilder sb)
        {
            sb.Append("{");
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"\"{entry.Key}\":");
                SerializeValue(entry.Value, sb);
            }
            sb.Append("}");
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        public static T Deserialize<T>(string json) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var obj = new T();
                json = json.Trim();
                
                if (json.StartsWith("{") && json.EndsWith("}"))
                {
                    json = json.Substring(1, json.Length - 2);
                    ParseObject(json, obj);
                }
                
                return obj;
            }
            catch
            {
                return new T();
            }
        }

        private static void ParseObject(string json, object obj)
        {
            var type = obj.GetType();
            int i = 0;

            while (i < json.Length)
            {
                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;

                // Find property name
                if (json[i] != '"') { i++; continue; }
                i++;
                int nameStart = i;
                while (i < json.Length && json[i] != '"') i++;
                string propName = json.Substring(nameStart, i - nameStart);
                i++;

                // Skip to colon
                while (i < json.Length && json[i] != ':') i++;
                i++;

                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                // Parse value
                string value = ParseValue(json, ref i);

                // Set property
                var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        SetPropertyValue(prop, obj, value);
                    }
                    catch { }
                }

                // Skip comma
                while (i < json.Length && (json[i] == ',' || char.IsWhiteSpace(json[i]))) i++;
            }
        }

        private static string ParseValue(string json, ref int i)
        {
            if (i >= json.Length) return "";

            char c = json[i];
            
            if (c == '"')
            {
                // String value
                i++;
                int start = i;
                while (i < json.Length)
                {
                    if (json[i] == '"' && json[i-1] != '\\') break;
                    i++;
                }
                string result = json.Substring(start, i - start);
                i++;
                return result.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            else if (c == '{')
            {
                // Object - find matching brace
                int depth = 1;
                int start = i;
                i++;
                while (i < json.Length && depth > 0)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') depth--;
                    i++;
                }
                return json.Substring(start, i - start);
            }
            else if (c == '[')
            {
                // Array - find matching bracket
                int depth = 1;
                int start = i;
                i++;
                while (i < json.Length && depth > 0)
                {
                    if (json[i] == '[') depth++;
                    else if (json[i] == ']') depth--;
                    i++;
                }
                return json.Substring(start, i - start);
            }
            else
            {
                // Number, bool, null
                int start = i;
                while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']' && !char.IsWhiteSpace(json[i]))
                {
                    i++;
                }
                return json.Substring(start, i - start);
            }
        }

        private static void SetPropertyValue(PropertyInfo prop, object obj, string value)
        {
            if (value == "null" || string.IsNullOrEmpty(value))
            {
                if (!prop.PropertyType.IsValueType)
                    prop.SetValue(obj, null);
                return;
            }

            var propType = prop.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;

            if (underlyingType == typeof(string))
            {
                prop.SetValue(obj, value);
            }
            else if (underlyingType == typeof(int))
            {
                if (int.TryParse(value, out int intVal))
                    prop.SetValue(obj, intVal);
            }
            else if (underlyingType == typeof(long))
            {
                if (long.TryParse(value, out long longVal))
                    prop.SetValue(obj, longVal);
            }
            else if (underlyingType == typeof(double))
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
                    prop.SetValue(obj, dblVal);
            }
            else if (underlyingType == typeof(bool))
            {
                prop.SetValue(obj, value.ToLower() == "true");
            }
            else if (underlyingType == typeof(DateTime))
            {
                if (DateTime.TryParse(value, out DateTime dtVal))
                    prop.SetValue(obj, dtVal);
            }
            else if (underlyingType.IsEnum)
            {
                try
                {
                    var enumVal = Enum.Parse(underlyingType, value, true);
                    prop.SetValue(obj, enumVal);
                }
                catch { }
            }
        }
    }
}
