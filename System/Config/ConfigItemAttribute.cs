using System;
using System.Collections.Generic;

namespace TeleportationNetwork
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ConfigItemAttribute : Attribute
    {
        public static Dictionary<Type, IValueConverter> DefaultConverters { get; } = new()
        {
            { typeof(int), new IntValueConverter() },
            { typeof(long), new LongValueConverter() },
            { typeof(float), new FloatValueConverter() },
            { typeof(double), new DoubleValueConverter() },
            { typeof(bool), new BoolValueConverter() },
            { typeof(string), new StringValueConverter() }
        };

        public Type Type { get; }
        public object DefaultValue { get; }
        public string? Description { get; set; }
        public bool ClientOnly { get; set; } = false;

        private object[]? _values;
        public object[]? Values
        {
            get => _values;
            set
            {
                _values = value;
                if (_values != null)
                {
                    for (int i = 0; i < _values.Length; i++)
                    {
                        _values[i] = Convert.ChangeType(_values[i], Type);
                    }
                }
            }
        }

        private object? _minValue;
        public object? MinValue
        {
            get => _minValue;
            set => _minValue = Convert.ChangeType(value, Type);
        }

        private object? _maxValue;
        public object? MaxValue
        {
            get => _maxValue;
            set => _maxValue = Convert.ChangeType(value, Type);
        }

        public IValueConverter? Converter { get; set; }

        public ConfigItemAttribute(Type type, object defaultValue)
        {
            Type = type;
            DefaultValue = Convert.ChangeType(defaultValue, Type);

            if (DefaultConverters.TryGetValue(type, out var converter))
            {
                Converter = converter;
            }
        }
    }

    public interface IValueConverter
    {
        object? Parse(string value);
    }

    public class IntValueConverter : IValueConverter
    {
        public object? Parse(string value)
        {
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return null;
        }
    }

    public class LongValueConverter : IValueConverter
    {
        public object? Parse(string value)
        {
            if (long.TryParse(value, out long result))
            {
                return result;
            }
            return null;
        }
    }

    public class FloatValueConverter : IValueConverter
    {
        public object? Parse(string value)
        {
            if (float.TryParse(value, out float result))
            {
                return result;
            }
            return null;
        }
    }

    public class DoubleValueConverter : IValueConverter
    {
        public object? Parse(string value)
        {
            if (double.TryParse(value, out double result))
            {
                return result;
            }
            return null;
        }
    }

    public class BoolValueConverter : IValueConverter
    {
        public object? Parse(string value)
        {
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }
            return null;
        }
    }

    public class StringValueConverter : IValueConverter
    {
        public object? Parse(string value) => value;
    }
}
