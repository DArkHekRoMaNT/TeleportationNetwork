using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    public static class ConfigUtil
    {
        public static void LoadConfig<T>(ICoreAPI api, ref T config) where T : class
        {
            config = (T)LoadConfig(api, typeof(T), config);
        }

        public static object LoadConfig(ICoreAPI api, Type type, object config)
        {
            var configAttr = GetAttribute<ConfigAttribute>(type);
            if (configAttr == null)
            {
                throw new ArgumentException($"{type} is not a config");
            }

            Dictionary<string, ConfigItem<object>> jsonConfig = new();
            string filename = configAttr.Filename;

            jsonConfig = api.LoadOrCreateConfig(filename, jsonConfig);

            foreach (PropertyInfo prop in type.GetProperties())
            {
                var attr = GetAttribute<ConfigItemAttribute>(prop);
                if (attr != null)
                {
                    if (jsonConfig.TryGetValue(prop.Name, out var value))
                    {
                        prop.SetValue(config, Convert.ChangeType(value.Value, attr.Type));
                    }
                    else
                    {
                        prop.SetValue(config, attr.DefaultValue);
                    }
                }
            }

            return config;
        }

        public static void SaveConfig<T>(ICoreAPI api, T config) where T : class
        {
            SaveConfig(api, typeof(T), config);
        }

        public static void SaveConfig(ICoreAPI api, Type type, object config)
        {
            var configAttr = GetAttribute<ConfigAttribute>(type);
            if (configAttr == null)
            {
                throw new ArgumentException($"{type} is not a config");
            }

            Dictionary<string, object> jsonConfig = new();

            foreach (PropertyInfo prop in GetConfigItems(type))
            {
                var attr = GetAttribute<ConfigItemAttribute>(prop);
                if (attr != null)
                {
                    var value = prop.GetValue(config);

                    Type itemType = typeof(ConfigItem<>).MakeGenericType(typeof(object));
                    var item = Activator.CreateInstance(itemType, value, attr.DefaultValue);

                    PropertyInfo desc = itemType.GetProperty(nameof(ConfigItem<object>.Description));
                    desc.SetValue(item, attr.Description);

                    PropertyInfo range = itemType.GetProperty(nameof(ConfigItem<object>.Range));
                    if (attr.MinValue != null && attr.MaxValue != null)
                    {
                        range.SetValue(item, $"[{attr.MinValue}, {attr.MaxValue}]");
                    }
                    else
                    {
                        if (attr.MinValue != null)
                        {
                            range.SetValue(item, $"above {attr.MinValue}");
                        }
                        else if (attr.MaxValue != null)
                        {
                            range.SetValue(item, $"belove {attr.MaxValue}");
                        }
                    }

                    PropertyInfo values = itemType.GetProperty(nameof(ConfigItem<object>.Values));
                    values.SetValue(item, attr.Values);

                    jsonConfig.Add(prop.Name, item);
                }
            }

            string filename = configAttr.Filename;
            api.StoreModConfig(jsonConfig, filename);
        }

        public static void CheckConfig<T>(ref T config) where T : class
        {
            config = (T)CheckConfig(typeof(T), config);
        }

        public static object CheckConfig(Type type, object config)
        {
            foreach (PropertyInfo prop in GetConfigItems(type))
            {
                var attr = GetAttribute<ConfigItemAttribute>(prop);
                if (attr != null)
                {
                    if (prop.GetValue(config) is IComparable value)
                    {
                        // lower min value
                        if (attr.MinValue != null && value.CompareTo(attr.MinValue) < 0)
                        {
                            prop.SetValue(config, attr.MinValue);
                        }

                        // greater max value
                        if (attr.MaxValue != null && value.CompareTo(attr.MaxValue) > 0)
                        {
                            prop.SetValue(config, attr.MaxValue);
                        }
                    }

                    // not allowable
                    if (attr.Values != null && !attr.Values.Contains(prop.GetValue(config)))
                    {
                        prop.SetValue(config, attr.DefaultValue);
                    }
                }
            }

            return config;
        }

        private static IEnumerable<PropertyInfo> GetConfigItems(Type type)
        {
            foreach (PropertyInfo prop in type.GetProperties())
            {
                if (prop.GetCustomAttributes(typeof(ConfigItemAttribute), true).Length > 0)
                {
                    yield return prop;
                }
            }
        }

        private static T? GetAttribute<T>(PropertyInfo prop) where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(prop, typeof(T));
        }

        private static T? GetAttribute<T>(Type type) where T : Attribute
        {
            return (T)type.GetCustomAttribute(typeof(T), true);
        }

        public static byte[] Serialize(object config)
        {
            var dict = new Dictionary<string, object>();
            foreach (PropertyInfo prop in config.GetType().GetProperties())
            {
                var attr = GetAttribute<ConfigItemAttribute>(prop);
                if (attr != null && !attr.ClientOnly)
                {
                    dict.Add(prop.Name, prop.GetValue(config));
                }
            }
            string json = JsonConvert.SerializeObject(dict);
            return Encoding.UTF8.GetBytes(json);
        }

        public static object Deserialize(object config, byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            foreach (PropertyInfo prop in config.GetType().GetProperties().Reverse())
            {
                var attr = GetAttribute<ConfigItemAttribute>(prop);
                if (attr != null && dict.TryGetValue(prop.Name, out object value))
                {
                    prop.SetValue(config, Convert.ChangeType(value, attr.Type));
                }
            }
            return config;
        }

        public static bool TrySetValue<T>(T config, string name, string value, ref string? error) where T : class
        {
            return TrySetValue(typeof(T), config, name, value, ref error);
        }

        public static bool TrySetValue(Type type, object config, string name, string value, ref string? error)
        {
            foreach (PropertyInfo prop in type.GetProperties())
            {
                if (prop.Name == name)
                {
                    var attr = GetAttribute<ConfigItemAttribute>(prop);
                    if (attr != null)
                    {
                        if (attr.Converter == null)
                        {
                            error = name + " converter is null";
                            return false;
                        }

                        object? parsedValue = attr.Converter.Parse(value);
                        if (parsedValue == null)
                        {
                            error = "error value \"" + value + "\"";
                            return false;
                        }

                        prop.SetValue(config, parsedValue);
                        return true;
                    }
                }
            }

            error = name + " not found";
            return false;
        }

        public static IEnumerable<string> GetAll<T>(T config) where T : class
        {
            return GetAll(typeof(T), config);
        }

        public static IEnumerable<string> GetAll(Type type, object config)
        {
            foreach (PropertyInfo prop in type.GetProperties())
            {
                var attr = GetAttribute<ConfigItemAttribute>(prop);
                if (attr != null)
                {
                    yield return prop.Name + ": " + prop.GetValue(config);
                }
            }
        }

        private class ConfigItem<T>
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? Description { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? Range { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public T[]? Values { get; set; }

            public T Default { get; }

            public T Value { get; }

            public ConfigItem(T value, T defaultValue)
            {
                Value = value;
                Default = defaultValue;
            }
        }
    }
}
