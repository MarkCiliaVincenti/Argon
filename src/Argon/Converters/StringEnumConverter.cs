﻿#region License

// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

#endregion

namespace Argon;

/// <summary>
/// Converts an <see cref="Enum"/> to and from its name string value.
/// </summary>
public class StringEnumConverter : JsonConverter
{
    /// <summary>
    /// Gets or sets the naming strategy used to resolve how enum text is written.
    /// </summary>
    public NamingStrategy? NamingStrategy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether integer values are allowed when serializing and deserializing.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool AllowIntegerValues { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="StringEnumConverter"/> class.
    /// </summary>
    public StringEnumConverter()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringEnumConverter"/> class.
    /// </summary>
    /// <param name="namingStrategy">The naming strategy used to resolve how enum text is written.</param>
    /// <param name="allowIntegerValues"><c>true</c> if integers are allowed when serializing and deserializing; otherwise, <c>false</c>.</param>
    public StringEnumConverter(NamingStrategy namingStrategy, bool allowIntegerValues = true)
    {
        NamingStrategy = namingStrategy;
        AllowIntegerValues = allowIntegerValues;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringEnumConverter"/> class.
    /// </summary>
    /// <param name="namingStrategyType">The <see cref="System.Type"/> of the <see cref="Argon.NamingStrategy"/> used to write enum text.</param>
    public StringEnumConverter(Type namingStrategyType)
    {
        NamingStrategy = JsonTypeReflector.CreateNamingStrategyInstance(namingStrategyType, null);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringEnumConverter"/> class.
    /// </summary>
    /// <param name="namingStrategyType">The <see cref="System.Type"/> of the <see cref="Argon.NamingStrategy"/> used to write enum text.</param>
    /// <param name="namingStrategyParameters">
    /// The parameter list to use when constructing the <see cref="Argon.NamingStrategy"/> described by <paramref name="namingStrategyType"/>.
    /// If <c>null</c>, the default constructor is used.
    /// When non-<c>null</c>, there must be a constructor defined in the <see cref="Argon.NamingStrategy"/> that exactly matches the number,
    /// order, and type of these parameters.
    /// </param>
    public StringEnumConverter(Type namingStrategyType, object[] namingStrategyParameters)
    {
        NamingStrategy = JsonTypeReflector.CreateNamingStrategyInstance(namingStrategyType, namingStrategyParameters);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringEnumConverter"/> class.
    /// </summary>
    /// <param name="namingStrategyType">The <see cref="System.Type"/> of the <see cref="Argon.NamingStrategy"/> used to write enum text.</param>
    /// <param name="namingStrategyParameters">
    /// The parameter list to use when constructing the <see cref="Argon.NamingStrategy"/> described by <paramref name="namingStrategyType"/>.
    /// If <c>null</c>, the default constructor is used.
    /// When non-<c>null</c>, there must be a constructor defined in the <see cref="Argon.NamingStrategy"/> that exactly matches the number,
    /// order, and type of these parameters.
    /// </param>
    /// <param name="allowIntegerValues"><c>true</c> if integers are allowed when serializing and deserializing; otherwise, <c>false</c>.</param>
    public StringEnumConverter(Type namingStrategyType, object[] namingStrategyParameters, bool allowIntegerValues)
    {
        NamingStrategy = JsonTypeReflector.CreateNamingStrategyInstance(namingStrategyType, namingStrategyParameters);
        AllowIntegerValues = allowIntegerValues;
    }

    /// <summary>
    /// Writes the JSON representation of the object.
    /// </summary>
    /// <param name="writer">The <see cref="JsonWriter"/> to write to.</param>
    /// <param name="value">The value.</param>
    /// <param name="serializer">The calling serializer.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var e = (Enum)value;

        if (!EnumUtils.TryToString(e.GetType(), value, NamingStrategy, out var enumName))
        {
            if (!AllowIntegerValues)
            {
                throw JsonSerializationException.Create(null, writer.ContainerPath, $"Integer value {e.ToString("D")} is not allowed.", null);
            }

            // enum value has no name so write number
            writer.WriteValue(value);
        }
        else
        {
            writer.WriteValue(enumName);
        }
    }

    /// <summary>
    /// Reads the JSON representation of the object.
    /// </summary>
    /// <param name="reader">The <see cref="JsonReader"/> to read from.</param>
    /// <param name="type">Type of the object.</param>
    /// <param name="existingValue">The existing value of object being read.</param>
    /// <param name="serializer">The calling serializer.</param>
    /// <returns>The object value.</returns>
    public override object? ReadJson(JsonReader reader, Type type, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            if (!ReflectionUtils.IsNullableType(type))
            {
                throw JsonSerializationException.Create(reader, $"Cannot convert null value to {type}.");
            }

            return null;
        }

        var isNullable = ReflectionUtils.IsNullableType(type);
        var t = isNullable ? Nullable.GetUnderlyingType(type) : type;

        try
        {
            if (reader.TokenType == JsonToken.String)
            {
                var enumText = reader.Value?.ToString();

                if (StringUtils.IsNullOrEmpty(enumText) && isNullable)
                {
                    return null;
                }

                return EnumUtils.ParseEnum(t, NamingStrategy, enumText!, !AllowIntegerValues);
            }

            if (reader.TokenType == JsonToken.Integer)
            {
                if (!AllowIntegerValues)
                {
                    throw JsonSerializationException.Create(reader, $"Integer value {reader.Value} is not allowed.");
                }

                return ConvertUtils.ConvertOrCast(reader.Value, CultureInfo.InvariantCulture, t);
            }
        }
        catch (Exception ex)
        {
            throw JsonSerializationException.Create(reader, $"Error converting value {MiscellaneousUtils.ToString(reader.Value)} to type '{type}'.", ex);
        }

        // we don't actually expect to get here.
        throw JsonSerializationException.Create(reader, $"Unexpected token {reader.TokenType} when parsing enum.");
    }

    /// <summary>
    /// Determines whether this instance can convert the specified object type.
    /// </summary>
    /// <param name="type">Type of the object.</param>
    /// <returns>
    /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
    /// </returns>
    public override bool CanConvert(Type type)
    {
        var t = ReflectionUtils.IsNullableType(type)
            ? Nullable.GetUnderlyingType(type)
            : type;

        return t.IsEnum;
    }
}