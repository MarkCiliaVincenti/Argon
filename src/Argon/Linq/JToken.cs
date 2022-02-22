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

using System.Dynamic;
using System.Linq.Expressions;
using Argon.Utilities;

namespace Argon.Linq;

/// <summary>
/// Represents an abstract JSON token.
/// </summary>
public abstract partial class JToken : IJEnumerable<JToken>, IJsonLineInfo
    , ICloneable
    , IDynamicMetaObjectProvider
{
    static JTokenEqualityComparer? equalityComparer;

    object? _annotations;

    static readonly JTokenType[] BooleanTypes = { JTokenType.Integer, JTokenType.Float, JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.Boolean };
    static readonly JTokenType[] NumberTypes = { JTokenType.Integer, JTokenType.Float, JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.Boolean };
    static readonly JTokenType[] BigIntegerTypes = { JTokenType.Integer, JTokenType.Float, JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.Boolean, JTokenType.Bytes };
    static readonly JTokenType[] StringTypes = { JTokenType.Date, JTokenType.Integer, JTokenType.Float, JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.Boolean, JTokenType.Bytes, JTokenType.Guid, JTokenType.TimeSpan, JTokenType.Uri };
    static readonly JTokenType[] GuidTypes = { JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.Guid, JTokenType.Bytes };
    static readonly JTokenType[] TimeSpanTypes = { JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.TimeSpan };
    static readonly JTokenType[] UriTypes = { JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.Uri };
    static readonly JTokenType[] CharTypes = { JTokenType.Integer, JTokenType.Float, JTokenType.String, JTokenType.Comment, JTokenType.Raw };
    static readonly JTokenType[] DateTimeTypes = { JTokenType.Date, JTokenType.String, JTokenType.Comment, JTokenType.Raw };
    static readonly JTokenType[] BytesTypes = { JTokenType.Bytes, JTokenType.String, JTokenType.Comment, JTokenType.Raw, JTokenType.Integer };

    /// <summary>
    /// Gets a comparer that can compare two tokens for value equality.
    /// </summary>
    public static JTokenEqualityComparer EqualityComparer => equalityComparer ??= new JTokenEqualityComparer();

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public JContainer? Parent { [DebuggerStepThrough] get; internal set; }

    /// <summary>
    /// Gets the root <see cref="JToken"/> of this <see cref="JToken"/>.
    /// </summary>
    public JToken Root
    {
        get
        {
            var parent = Parent;
            if (parent == null)
            {
                return this;
            }

            while (parent.Parent != null)
            {
                parent = parent.Parent;
            }

            return parent;
        }
    }

    internal abstract JToken CloneToken();
    internal abstract bool DeepEquals(JToken node);

    /// <summary>
    /// Gets the node type for this <see cref="JToken"/>.
    /// </summary>
    public abstract JTokenType Type { get; }

    /// <summary>
    /// Gets a value indicating whether this token has child tokens.
    /// </summary>
    public abstract bool HasValues { get; }

    /// <summary>
    /// Compares the values of two tokens, including the values of all descendant tokens.
    /// </summary>
    /// <param name="t1">The first <see cref="JToken"/> to compare.</param>
    /// <param name="t2">The second <see cref="JToken"/> to compare.</param>
    /// <returns><c>true</c> if the tokens are equal; otherwise <c>false</c>.</returns>
    public static bool DeepEquals(JToken? t1, JToken? t2)
    {
        return t1 == t2 || (t1 != null && t2 != null && t1.DeepEquals(t2));
    }

    /// <summary>
    /// Gets the next sibling token of this node.
    /// </summary>
    public JToken? Next { get; internal set; }

    /// <summary>
    /// Gets the previous sibling token of this node.
    /// </summary>
    public JToken? Previous { get; internal set; }

    /// <summary>
    /// Gets the path of the JSON token.
    /// </summary>
    public string Path
    {
        get
        {
            if (Parent == null)
            {
                return string.Empty;
            }

            var positions = new List<JsonPosition>();
            JToken? previous = null;
            for (var current = this; current != null; current = current.Parent)
            {
                switch (current.Type)
                {
                    case JTokenType.Property:
                        var property = (JProperty)current;
                        positions.Add(new JsonPosition(JsonContainerType.Object) { PropertyName = property.Name });
                        break;
                    case JTokenType.Array:
                    case JTokenType.Constructor:
                        if (previous != null)
                        {
                            var index = ((IList<JToken>)current).IndexOf(previous);

                            positions.Add(new JsonPosition(JsonContainerType.Array) { Position = index });
                        }
                        break;
                }

                previous = current;
            }

            positions.FastReverse();

            return JsonPosition.BuildPath(positions, null);
        }
    }

    internal JToken()
    {
    }

    /// <summary>
    /// Adds the specified content immediately after this token.
    /// </summary>
    /// <param name="content">A content object that contains simple content or a collection of content objects to be added after this token.</param>
    public void AddAfterSelf(object? content)
    {
        if (Parent == null)
        {
            throw new InvalidOperationException("The parent is missing.");
        }

        var index = Parent.IndexOfItem(this);
        Parent.TryAddInternal(index + 1, content, false);
    }

    /// <summary>
    /// Adds the specified content immediately before this token.
    /// </summary>
    /// <param name="content">A content object that contains simple content or a collection of content objects to be added before this token.</param>
    public void AddBeforeSelf(object? content)
    {
        if (Parent == null)
        {
            throw new InvalidOperationException("The parent is missing.");
        }

        var index = Parent.IndexOfItem(this);
        Parent.TryAddInternal(index, content, false);
    }

    /// <summary>
    /// Returns a collection of the ancestor tokens of this token.
    /// </summary>
    /// <returns>A collection of the ancestor tokens of this token.</returns>
    public IEnumerable<JToken> Ancestors()
    {
        return GetAncestors(false);
    }

    /// <summary>
    /// Returns a collection of tokens that contain this token, and the ancestors of this token.
    /// </summary>
    /// <returns>A collection of tokens that contain this token, and the ancestors of this token.</returns>
    public IEnumerable<JToken> AncestorsAndSelf()
    {
        return GetAncestors(true);
    }

    internal IEnumerable<JToken> GetAncestors(bool self)
    {
        for (var current = self ? this : Parent; current != null; current = current.Parent)
        {
            yield return current;
        }
    }

    /// <summary>
    /// Returns a collection of the sibling tokens after this token, in document order.
    /// </summary>
    /// <returns>A collection of the sibling tokens after this tokens, in document order.</returns>
    public IEnumerable<JToken> AfterSelf()
    {
        if (Parent == null)
        {
            yield break;
        }

        for (var o = Next; o != null; o = o.Next)
        {
            yield return o;
        }
    }

    /// <summary>
    /// Returns a collection of the sibling tokens before this token, in document order.
    /// </summary>
    /// <returns>A collection of the sibling tokens before this token, in document order.</returns>
    public IEnumerable<JToken> BeforeSelf()
    {
        if (Parent == null)
        {
            yield break;
        }

        for (var o = Parent.First; o != this && o != null; o = o.Next)
        {
            yield return o;
        }
    }

    /// <summary>
    /// Gets the <see cref="JToken"/> with the specified key.
    /// </summary>
    public virtual JToken? this[object key]
    {
        get => throw new InvalidOperationException($"Cannot access child value on {GetType()}.");
        set => throw new InvalidOperationException($"Cannot set child value on {GetType()}.");
    }

    /// <summary>
    /// Gets the <see cref="JToken"/> with the specified key converted to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to convert the token to.</typeparam>
    /// <param name="key">The token key.</param>
    /// <returns>The converted token value.</returns>
    public virtual T? Value<T>(object key)
    {
        var token = this[key];

        // null check to fix MonoTouch issue - https://github.com/dolbz/Newtonsoft.Json/commit/a24e3062846b30ee505f3271ac08862bb471b822
        return token == null ? default : token.Convert<JToken, T>();
    }

    /// <summary>
    /// Get the first child token of this token.
    /// </summary>
    public virtual JToken? First => throw new InvalidOperationException($"Cannot access child value on {GetType()}.");

    /// <summary>
    /// Get the last child token of this token.
    /// </summary>
    public virtual JToken? Last => throw new InvalidOperationException($"Cannot access child value on {GetType()}.");

    /// <summary>
    /// Returns a collection of the child tokens of this token, in document order.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="JToken"/> containing the child tokens of this <see cref="JToken"/>, in document order.</returns>
    public virtual JEnumerable<JToken> Children()
    {
        return JEnumerable<JToken>.Empty;
    }

    /// <summary>
    /// Returns a collection of the child tokens of this token, in document order, filtered by the specified type.
    /// </summary>
    /// <typeparam name="T">The type to filter the child tokens on.</typeparam>
    /// <returns>A <see cref="JEnumerable{T}"/> containing the child tokens of this <see cref="JToken"/>, in document order.</returns>
    public JEnumerable<T> Children<T>() where T : JToken
    {
        return new JEnumerable<T>(Children().OfType<T>());
    }

    /// <summary>
    /// Returns a collection of the child values of this token, in document order.
    /// </summary>
    /// <typeparam name="T">The type to convert the values to.</typeparam>
    /// <returns>A <see cref="IEnumerable{T}"/> containing the child values of this <see cref="JToken"/>, in document order.</returns>
    public virtual IEnumerable<T?> Values<T>()
    {
        throw new InvalidOperationException($"Cannot access child value on {GetType()}.");
    }

    /// <summary>
    /// Removes this token from its parent.
    /// </summary>
    public void Remove()
    {
        if (Parent == null)
        {
            throw new InvalidOperationException("The parent is missing.");
        }

        Parent.RemoveItem(this);
    }

    /// <summary>
    /// Replaces this token with the specified token.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Replace(JToken value)
    {
        if (Parent == null)
        {
            throw new InvalidOperationException("The parent is missing.");
        }

        Parent.ReplaceItem(this, value);
    }

    /// <summary>
    /// Writes this token to a <see cref="JsonWriter"/>.
    /// </summary>
    /// <param name="writer">A <see cref="JsonWriter"/> into which this method will write.</param>
    /// <param name="converters">A collection of <see cref="JsonConverter"/> which will be used when writing the token.</param>
    public abstract void WriteTo(JsonWriter writer, params JsonConverter[] converters);

    /// <summary>
    /// Returns the indented JSON for this token.
    /// </summary>
    /// <remarks>
    /// <c>ToString()</c> returns a non-JSON string value for tokens with a type of <see cref="JTokenType.String"/>.
    /// If you want the JSON for all token types then you should use <see cref="WriteTo(JsonWriter, JsonConverter[])"/>.
    /// </remarks>
    /// <returns>
    /// The indented JSON for this token.
    /// </returns>
    public override string ToString()
    {
        return ToString(Formatting.Indented);
    }

    /// <summary>
    /// Returns the JSON for this token using the given formatting and converters.
    /// </summary>
    /// <param name="formatting">Indicates how the output should be formatted.</param>
    /// <param name="converters">A collection of <see cref="JsonConverter"/>s which will be used when writing the token.</param>
    /// <returns>The JSON for this token using the given formatting and converters.</returns>
    public string ToString(Formatting formatting, params JsonConverter[] converters)
    {
        using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        var jsonWriter = new JsonTextWriter(stringWriter)
        {
            Formatting = formatting
        };

        WriteTo(jsonWriter, converters);

        return stringWriter.ToString();
    }

    static JValue? EnsureValue(JToken value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value is JProperty property)
        {
            value = property.Value;
        }

        var v = value as JValue;

        return v;
    }

    static string GetType(JToken token)
    {
        if (token is JProperty p)
        {
            token = p.Value;
        }

        return token.Type.ToString();
    }

    static bool ValidateToken(JToken o, JTokenType[] validTypes, bool nullable)
    {
        return Array.IndexOf(validTypes, o.Type) != -1 || (nullable && o.Type is JTokenType.Null or JTokenType.Undefined);
    }

    #region Cast from operators
    /// <summary>
    /// Performs an explicit conversion from <see cref="Argon.Linq.JToken"/> to <see cref="System.Boolean"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator bool(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, BooleanTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Boolean.");
        }

        if (v.Value is BigInteger integer)
        {
            return Convert.ToBoolean((int)integer);
        }

        return Convert.ToBoolean(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="Argon.Linq.JToken"/> to <see cref="System.DateTimeOffset"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator DateTimeOffset(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, DateTimeTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to DateTimeOffset.");
        }

        if (v.Value is DateTimeOffset offset)
        {
            return offset;
        }

        if (v.Value is string s)
        {
            return DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
        }

        return new DateTimeOffset(Convert.ToDateTime(v.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Boolean"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator bool?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, BooleanTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Boolean.");
        }

        if (v.Value is BigInteger integer)
        {
            return Convert.ToBoolean((int)integer);
        }

        return v.Value != null ? Convert.ToBoolean(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Int64"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator long(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Int64.");
        }

        if (v.Value is BigInteger integer)
        {
            return (long)integer;
        }

        return Convert.ToInt64(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator DateTime?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, DateTimeTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to DateTime.");
        }

        if (v.Value is DateTimeOffset offset)
        {
            return offset.DateTime;
        }

        return v.Value != null ? Convert.ToDateTime(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator DateTimeOffset?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, DateTimeTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to DateTimeOffset.");
        }

        if (v.Value == null)
        {
            return null;
        }
        if (v.Value is DateTimeOffset offset)
        {
            return offset;
        }

        if (v.Value is string s)
        {
            return DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
        }

        return new DateTimeOffset(Convert.ToDateTime(v.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Decimal"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator decimal?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Decimal.");
        }

        if (v.Value is BigInteger integer)
        {
            return (decimal?)integer;
        }

        return v.Value != null ? Convert.ToDecimal(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Double"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator double?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Double.");
        }

        if (v.Value is BigInteger integer)
        {
            return (double?)integer;
        }

        return v.Value != null ? Convert.ToDouble(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Char"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator char?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, CharTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Char.");
        }

        if (v.Value is BigInteger integer)
        {
            return (char?)integer;
        }

        return v.Value != null ? Convert.ToChar(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Int32"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator int(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Int32.");
        }

        if (v.Value is BigInteger integer)
        {
            return (int)integer;
        }

        return Convert.ToInt32(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Int16"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator short(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Int16.");
        }

        if (v.Value is BigInteger integer)
        {
            return (short)integer;
        }

        return Convert.ToInt16(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="UInt16"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator ushort(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to UInt16.");
        }

        if (v.Value is BigInteger integer)
        {
            return (ushort)integer;
        }

        return Convert.ToUInt16(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Char"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator char(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, CharTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Char.");
        }

        if (v.Value is BigInteger integer)
        {
            return (char)integer;
        }

        return Convert.ToChar(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Byte"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator byte(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Byte.");
        }

        if (v.Value is BigInteger integer)
        {
            return (byte)integer;
        }

        return Convert.ToByte(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="Argon.Linq.JToken"/> to <see cref="System.SByte"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator sbyte(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to SByte.");
        }

        if (v.Value is BigInteger integer)
        {
            return (sbyte)integer;
        }

        return Convert.ToSByte(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Int32"/> .
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator int?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Int32.");
        }

        if (v.Value is BigInteger integer)
        {
            return (int?)integer;
        }

        return v.Value != null ? Convert.ToInt32(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Int16"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator short?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Int16.");
        }

        if (v.Value is BigInteger integer)
        {
            return (short?)integer;
        }

        return v.Value != null ? Convert.ToInt16(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="UInt16"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator ushort?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to UInt16.");
        }

        if (v.Value is BigInteger integer)
        {
            return (ushort?)integer;
        }

        return v.Value != null ? Convert.ToUInt16(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Byte"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator byte?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Byte.");
        }

        if (v.Value is BigInteger integer)
        {
            return (byte?)integer;
        }

        return v.Value != null ? Convert.ToByte(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="SByte"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator sbyte?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to SByte.");
        }

        if (v.Value is BigInteger integer)
        {
            return (sbyte?)integer;
        }

        return v.Value != null ? Convert.ToSByte(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator DateTime(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, DateTimeTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to DateTime.");
        }

        if (v.Value is DateTimeOffset offset)
        {
            return offset.DateTime;
        }

        return Convert.ToDateTime(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Int64"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator long?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Int64.");
        }

        if (v.Value is BigInteger integer)
        {
            return (long?)integer;
        }

        return v.Value != null ? Convert.ToInt64(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Single"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator float?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Single.");
        }

        if (v.Value is BigInteger integer)
        {
            return (float?)integer;
        }

        return v.Value != null ? Convert.ToSingle(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Decimal"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator decimal(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Decimal.");
        }

        if (v.Value is BigInteger integer)
        {
            return (decimal)integer;
        }

        return Convert.ToDecimal(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="UInt32"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator uint?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to UInt32.");
        }

        if (v.Value is BigInteger integer)
        {
            return (uint?)integer;
        }

        return v.Value != null ? Convert.ToUInt32(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="UInt64"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator ulong?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to UInt64.");
        }

        if (v.Value is BigInteger integer)
        {
            return (ulong?)integer;
        }

        return v.Value != null ? Convert.ToUInt64(v.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Double"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator double(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Double.");
        }

        if (v.Value is BigInteger integer)
        {
            return (double)integer;
        }

        return Convert.ToDouble(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Single"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator float(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Single.");
        }

        if (v.Value is BigInteger integer)
        {
            return (float)integer;
        }

        return Convert.ToSingle(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="String"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator string?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, StringTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to String.");
        }

        if (v.Value == null)
        {
            return null;
        }

        if (v.Value is byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        if (v.Value is BigInteger integer)
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        return Convert.ToString(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="UInt32"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator uint(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to UInt32.");
        }

        if (v.Value is BigInteger integer)
        {
            return (uint)integer;
        }

        return Convert.ToUInt32(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="Argon.Linq.JToken"/> to <see cref="System.UInt64"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    [CLSCompliant(false)]
    public static explicit operator ulong(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, NumberTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to UInt64.");
        }

        if (v.Value is BigInteger integer)
        {
            return (ulong)integer;
        }

        return Convert.ToUInt64(v.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Byte"/>[].
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator byte[]?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, BytesTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to byte array.");
        }

        if (v.Value is string)
        {
            return Convert.FromBase64String(Convert.ToString(v.Value, CultureInfo.InvariantCulture));
        }
        if (v.Value is BigInteger integer)
        {
            return integer.ToByteArray();
        }

        if (v.Value is byte[] bytes)
        {
            return bytes;
        }

        throw new ArgumentException($"Can not convert {GetType(value)} to byte array.");
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Guid"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator Guid(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, GuidTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Guid.");
        }

        if (v.Value is byte[] bytes)
        {
            return new Guid(bytes);
        }

        return v.Value is Guid guid ? guid : new Guid(Convert.ToString(v.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="Guid"/> .
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator Guid?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, GuidTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Guid.");
        }

        if (v.Value == null)
        {
            return null;
        }

        if (v.Value is byte[] bytes)
        {
            return new Guid(bytes);
        }

        return v.Value is Guid guid ? guid : new Guid(Convert.ToString(v.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator TimeSpan(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, TimeSpanTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to TimeSpan.");
        }

        return v.Value is TimeSpan span ? span : ConvertUtils.ParseTimeSpan(Convert.ToString(v.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Nullable{T}"/> of <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator TimeSpan?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, TimeSpanTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to TimeSpan.");
        }

        if (v.Value == null)
        {
            return null;
        }

        return v.Value is TimeSpan span ? span : ConvertUtils.ParseTimeSpan(Convert.ToString(v.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="JToken"/> to <see cref="Uri"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator Uri?(JToken? value)
    {
        if (value == null)
        {
            return null;
        }

        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, UriTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to Uri.");
        }

        if (v.Value == null)
        {
            return null;
        }

        return v.Value is Uri uri ? uri : new Uri(Convert.ToString(v.Value, CultureInfo.InvariantCulture));
    }

    static BigInteger ToBigInteger(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, BigIntegerTypes, false))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to BigInteger.");
        }

        return ConvertUtils.ToBigInteger(v.Value!);
    }

    static BigInteger? ToBigIntegerNullable(JToken value)
    {
        var v = EnsureValue(value);
        if (v == null || !ValidateToken(v, BigIntegerTypes, true))
        {
            throw new ArgumentException($"Can not convert {GetType(value)} to BigInteger.");
        }

        if (v.Value == null)
        {
            return null;
        }

        return ConvertUtils.ToBigInteger(v.Value);
    }
    #endregion

    #region Cast to operators
    /// <summary>
    /// Performs an implicit conversion from <see cref="Boolean"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(bool value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="DateTimeOffset"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(DateTimeOffset value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Byte"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(byte value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Byte"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(byte? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="SByte"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(sbyte value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="SByte"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(sbyte? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Boolean"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(bool? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Int64"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(long value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="DateTime"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(DateTime? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(DateTimeOffset? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Decimal"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(decimal? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Double"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(double? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Int16"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(short value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="UInt16"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(ushort value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Int32"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(int value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Int32"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(int? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="DateTime"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(DateTime value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Int64"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(long? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Single"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(float? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Decimal"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(decimal value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Int16"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(short? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="UInt16"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(ushort? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="UInt32"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(uint? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="UInt64"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(ulong? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Double"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(double value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Single"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(float value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="String"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(string? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="UInt32"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(uint value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="UInt64"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    [CLSCompliant(false)]
    public static implicit operator JToken(ulong value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Byte"/>[] to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(byte[] value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Uri"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(Uri? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="TimeSpan"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(TimeSpan value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="TimeSpan"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(TimeSpan? value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Guid"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(Guid value)
    {
        return new JValue(value);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="Nullable{T}"/> of <see cref="Guid"/> to <see cref="JToken"/>.
    /// </summary>
    /// <param name="value">The value to create a <see cref="JValue"/> from.</param>
    /// <returns>The <see cref="JValue"/> initialized with the specified value.</returns>
    public static implicit operator JToken(Guid? value)
    {
        return new JValue(value);
    }
    #endregion

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<JToken>)this).GetEnumerator();
    }

    IEnumerator<JToken> IEnumerable<JToken>.GetEnumerator()
    {
        return Children().GetEnumerator();
    }

    internal abstract int GetDeepHashCode();

    IJEnumerable<JToken> IJEnumerable<JToken>.this[object key] => this[key]!;

    /// <summary>
    /// Creates a <see cref="JsonReader"/> for this token.
    /// </summary>
    /// <returns>A <see cref="JsonReader"/> that can be used to read this token and its descendants.</returns>
    public JsonReader CreateReader()
    {
        return new JTokenReader(this);
    }

    internal static JToken FromObjectInternal(object o, JsonSerializer jsonSerializer)
    {
        JToken token;
        using var jsonWriter = new JTokenWriter();
        jsonSerializer.Serialize(jsonWriter, o);
        token = jsonWriter.Token!;

        return token;
    }

    /// <summary>
    /// Creates a <see cref="JToken"/> from an object.
    /// </summary>
    /// <param name="o">The object that will be used to create <see cref="JToken"/>.</param>
    /// <returns>A <see cref="JToken"/> with the value of the specified object.</returns>
    public static JToken FromObject(object o)
    {
        return FromObjectInternal(o, JsonSerializer.CreateDefault());
    }

    /// <summary>
    /// Creates a <see cref="JToken"/> from an object using the specified <see cref="JsonSerializer"/>.
    /// </summary>
    /// <param name="o">The object that will be used to create <see cref="JToken"/>.</param>
    /// <param name="jsonSerializer">The <see cref="JsonSerializer"/> that will be used when reading the object.</param>
    /// <returns>A <see cref="JToken"/> with the value of the specified object.</returns>
    public static JToken FromObject(object o, JsonSerializer jsonSerializer)
    {
        return FromObjectInternal(o, jsonSerializer);
    }

    /// <summary>
    /// Creates an instance of the specified .NET type from the <see cref="JToken"/>.
    /// </summary>
    /// <typeparam name="T">The object type that the token will be deserialized to.</typeparam>
    /// <returns>The new object created from the JSON value.</returns>
    public T? ToObject<T>()
    {
        return (T?)ToObject(typeof(T));
    }

    /// <summary>
    /// Creates an instance of the specified .NET type from the <see cref="JToken"/>.
    /// </summary>
    /// <param name="type">The object type that the token will be deserialized to.</param>
    /// <returns>The new object created from the JSON value.</returns>
    public object? ToObject(Type type)
    {
        if (JsonConvert.DefaultSettings == null)
        {
            var typeCode = ConvertUtils.GetTypeCode(type, out var isEnum);

            if (isEnum)
            {
                if (Type == JTokenType.String)
                {
                    try
                    {
                        // use serializer so JsonConverter(typeof(StringEnumConverter)) + EnumMemberAttributes are respected
                        return ToObject(type, JsonSerializer.CreateDefault());
                    }
                    catch (Exception ex)
                    {
                        var enumType = type.IsEnum ? type : Nullable.GetUnderlyingType(type);
                        throw new ArgumentException($"Could not convert '{(string?) this}' to {enumType.Name}.", ex);
                    }
                }

                if (Type == JTokenType.Integer)
                {
                    var enumType = type.IsEnum ? type : Nullable.GetUnderlyingType(type);
                    return Enum.ToObject(enumType, ((JValue)this).Value);
                }
            }

            switch (typeCode)
            {
                case PrimitiveTypeCode.BooleanNullable:
                    return (bool?)this;
                case PrimitiveTypeCode.Boolean:
                    return (bool)this;
                case PrimitiveTypeCode.CharNullable:
                    return (char?)this;
                case PrimitiveTypeCode.Char:
                    return (char)this;
                case PrimitiveTypeCode.SByte:
                    return (sbyte)this;
                case PrimitiveTypeCode.SByteNullable:
                    return (sbyte?)this;
                case PrimitiveTypeCode.ByteNullable:
                    return (byte?)this;
                case PrimitiveTypeCode.Byte:
                    return (byte)this;
                case PrimitiveTypeCode.Int16Nullable:
                    return (short?)this;
                case PrimitiveTypeCode.Int16:
                    return (short)this;
                case PrimitiveTypeCode.UInt16Nullable:
                    return (ushort?)this;
                case PrimitiveTypeCode.UInt16:
                    return (ushort)this;
                case PrimitiveTypeCode.Int32Nullable:
                    return (int?)this;
                case PrimitiveTypeCode.Int32:
                    return (int)this;
                case PrimitiveTypeCode.UInt32Nullable:
                    return (uint?)this;
                case PrimitiveTypeCode.UInt32:
                    return (uint)this;
                case PrimitiveTypeCode.Int64Nullable:
                    return (long?)this;
                case PrimitiveTypeCode.Int64:
                    return (long)this;
                case PrimitiveTypeCode.UInt64Nullable:
                    return (ulong?)this;
                case PrimitiveTypeCode.UInt64:
                    return (ulong)this;
                case PrimitiveTypeCode.SingleNullable:
                    return (float?)this;
                case PrimitiveTypeCode.Single:
                    return (float)this;
                case PrimitiveTypeCode.DoubleNullable:
                    return (double?)this;
                case PrimitiveTypeCode.Double:
                    return (double)this;
                case PrimitiveTypeCode.DecimalNullable:
                    return (decimal?)this;
                case PrimitiveTypeCode.Decimal:
                    return (decimal)this;
                case PrimitiveTypeCode.DateTimeNullable:
                    return (DateTime?)this;
                case PrimitiveTypeCode.DateTime:
                    return (DateTime)this;
                case PrimitiveTypeCode.DateTimeOffsetNullable:
                    return (DateTimeOffset?)this;
                case PrimitiveTypeCode.DateTimeOffset:
                    return (DateTimeOffset)this;
                case PrimitiveTypeCode.String:
                    return (string?)this;
                case PrimitiveTypeCode.GuidNullable:
                    return (Guid?)this;
                case PrimitiveTypeCode.Guid:
                    return (Guid)this;
                case PrimitiveTypeCode.Uri:
                    return (Uri?)this;
                case PrimitiveTypeCode.TimeSpanNullable:
                    return (TimeSpan?)this;
                case PrimitiveTypeCode.TimeSpan:
                    return (TimeSpan)this;
                case PrimitiveTypeCode.BigIntegerNullable:
                    return ToBigIntegerNullable(this);
                case PrimitiveTypeCode.BigInteger:
                    return ToBigInteger(this);
            }
        }

        return ToObject(type, JsonSerializer.CreateDefault());
    }

    /// <summary>
    /// Creates an instance of the specified .NET type from the <see cref="JToken"/> using the specified <see cref="JsonSerializer"/>.
    /// </summary>
    /// <typeparam name="T">The object type that the token will be deserialized to.</typeparam>
    /// <param name="jsonSerializer">The <see cref="JsonSerializer"/> that will be used when creating the object.</param>
    /// <returns>The new object created from the JSON value.</returns>
    public T? ToObject<T>(JsonSerializer jsonSerializer)
    {
        return (T?)ToObject(typeof(T), jsonSerializer);
    }

    /// <summary>
    /// Creates an instance of the specified .NET type from the <see cref="JToken"/> using the specified <see cref="JsonSerializer"/>.
    /// </summary>
    /// <param name="type">The object type that the token will be deserialized to.</param>
    /// <param name="jsonSerializer">The <see cref="JsonSerializer"/> that will be used when creating the object.</param>
    /// <returns>The new object created from the JSON value.</returns>
    public object? ToObject(Type? type, JsonSerializer jsonSerializer)
    {
        using var jsonReader = new JTokenReader(this);
        // Hacky fix to ensure the serializer settings are set onto the new reader.
        // This is required because the serializer won't update settings when used inside of a converter.
        if (jsonSerializer is JsonSerializerProxy proxy)
        {
            proxy.serializer.SetupReader(jsonReader, out _, out _, out _, out _, out _, out _);
        }

        return jsonSerializer.Deserialize(jsonReader, type);
    }

    /// <summary>
    /// Creates a <see cref="JToken"/> from a <see cref="JsonReader"/>.
    /// </summary>
    /// <param name="reader">A <see cref="JsonReader"/> positioned at the token to read into this <see cref="JToken"/>.</param>
    /// <returns>
    /// A <see cref="JToken"/> that contains the token and its descendant tokens
    /// that were read from the reader. The runtime type of the token is determined
    /// by the token type of the first token encountered in the reader.
    /// </returns>
    public static JToken ReadFrom(JsonReader reader)
    {
        return ReadFrom(reader, null);
    }

    /// <summary>
    /// Creates a <see cref="JToken"/> from a <see cref="JsonReader"/>.
    /// </summary>
    /// <param name="reader">An <see cref="JsonReader"/> positioned at the token to read into this <see cref="JToken"/>.</param>
    /// <param name="settings">The <see cref="JsonLoadSettings"/> used to load the JSON.
    /// If this is <c>null</c>, default load settings will be used.</param>
    /// <returns>
    /// A <see cref="JToken"/> that contains the token and its descendant tokens
    /// that were read from the reader. The runtime type of the token is determined
    /// by the token type of the first token encountered in the reader.
    /// </returns>
    public static JToken ReadFrom(JsonReader reader, JsonLoadSettings? settings)
    {
        bool hasContent;
        if (reader.TokenType == JsonToken.None)
        {
            hasContent = settings is {CommentHandling: CommentHandling.Ignore}
                ? reader.ReadAndMoveToContent()
                : reader.Read();
        }
        else if (reader.TokenType == JsonToken.Comment && settings?.CommentHandling == CommentHandling.Ignore)
        {
            hasContent = reader.ReadAndMoveToContent();
        }
        else
        {
            hasContent = true;
        }

        if (!hasContent)
        {
            throw JsonReaderException.Create(reader, "Error reading JToken from JsonReader.");
        }

        var lineInfo = reader as IJsonLineInfo;

        switch (reader.TokenType)
        {
            case JsonToken.StartObject:
                return JObject.Load(reader, settings);
            case JsonToken.StartArray:
                return JArray.Load(reader, settings);
            case JsonToken.StartConstructor:
                return JConstructor.Load(reader, settings);
            case JsonToken.PropertyName:
                return JProperty.Load(reader, settings);
            case JsonToken.String:
            case JsonToken.Integer:
            case JsonToken.Float:
            case JsonToken.Date:
            case JsonToken.Boolean:
            case JsonToken.Bytes:
                var v = new JValue(reader.Value);
                v.SetLineInfo(lineInfo, settings);
                return v;
            case JsonToken.Comment:
                v = JValue.CreateComment(reader.Value!.ToString());
                v.SetLineInfo(lineInfo, settings);
                return v;
            case JsonToken.Null:
                v = JValue.CreateNull();
                v.SetLineInfo(lineInfo, settings);
                return v;
            case JsonToken.Undefined:
                v = JValue.CreateUndefined();
                v.SetLineInfo(lineInfo, settings);
                return v;
            default:
                throw JsonReaderException.Create(reader, $"Error reading JToken from JsonReader. Unexpected token: {reader.TokenType}");
        }
    }

    /// <summary>
    /// Load a <see cref="JToken"/> from a string that contains JSON.
    /// </summary>
    /// <param name="json">A <see cref="String"/> that contains JSON.</param>
    /// <returns>A <see cref="JToken"/> populated from the string that contains JSON.</returns>
    public static JToken Parse(string json)
    {
        return Parse(json, null);
    }

    /// <summary>
    /// Load a <see cref="JToken"/> from a string that contains JSON.
    /// </summary>
    /// <param name="json">A <see cref="String"/> that contains JSON.</param>
    /// <param name="settings">The <see cref="JsonLoadSettings"/> used to load the JSON.
    /// If this is <c>null</c>, default load settings will be used.</param>
    /// <returns>A <see cref="JToken"/> populated from the string that contains JSON.</returns>
    public static JToken Parse(string json, JsonLoadSettings? settings)
    {
        using JsonReader reader = new JsonTextReader(new StringReader(json));
        var t = Load(reader, settings);

        while (reader.Read())
        {
            // Any content encountered here other than a comment will throw in the reader.
        }

        return t;
    }

    /// <summary>
    /// Creates a <see cref="JToken"/> from a <see cref="JsonReader"/>.
    /// </summary>
    /// <param name="reader">A <see cref="JsonReader"/> positioned at the token to read into this <see cref="JToken"/>.</param>
    /// <param name="settings">The <see cref="JsonLoadSettings"/> used to load the JSON.
    /// If this is <c>null</c>, default load settings will be used.</param>
    /// <returns>
    /// A <see cref="JToken"/> that contains the token and its descendant tokens
    /// that were read from the reader. The runtime type of the token is determined
    /// by the token type of the first token encountered in the reader.
    /// </returns>
    public static JToken Load(JsonReader reader, JsonLoadSettings? settings)
    {
        return ReadFrom(reader, settings);
    }

    /// <summary>
    /// Creates a <see cref="JToken"/> from a <see cref="JsonReader"/>.
    /// </summary>
    /// <param name="reader">A <see cref="JsonReader"/> positioned at the token to read into this <see cref="JToken"/>.</param>
    /// <returns>
    /// A <see cref="JToken"/> that contains the token and its descendant tokens
    /// that were read from the reader. The runtime type of the token is determined
    /// by the token type of the first token encountered in the reader.
    /// </returns>
    public static JToken Load(JsonReader reader)
    {
        return Load(reader, null);
    }

    internal void SetLineInfo(IJsonLineInfo? lineInfo, JsonLoadSettings? settings)
    {
        if (settings != null && settings.LineInfoHandling != LineInfoHandling.Load)
        {
            return;
        }

        if (lineInfo == null || !lineInfo.HasLineInfo())
        {
            return;
        }

        SetLineInfo(lineInfo.LineNumber, lineInfo.LinePosition);
    }

    class LineInfoAnnotation
    {
        internal readonly int LineNumber;
        internal readonly int LinePosition;

        public LineInfoAnnotation(int lineNumber, int linePosition)
        {
            LineNumber = lineNumber;
            LinePosition = linePosition;
        }
    }

    internal void SetLineInfo(int lineNumber, int linePosition)
    {
        AddAnnotation(new LineInfoAnnotation(lineNumber, linePosition));
    }

    bool IJsonLineInfo.HasLineInfo()
    {
        return Annotation<LineInfoAnnotation>() != null;
    }

    int IJsonLineInfo.LineNumber
    {
        get
        {
            var annotation = Annotation<LineInfoAnnotation>();
            if (annotation == null)
            {
                return 0;
            }

            return annotation.LineNumber;
        }
    }

    int IJsonLineInfo.LinePosition
    {
        get
        {
            var annotation = Annotation<LineInfoAnnotation>();
            if (annotation == null)
            {
                return 0;
            }

            return annotation.LinePosition;
        }
    }

    /// <summary>
    /// Returns the <see cref="DynamicMetaObject"/> responsible for binding operations performed on this object.
    /// </summary>
    /// <param name="parameter">The expression tree representation of the runtime value.</param>
    /// <returns>
    /// The <see cref="DynamicMetaObject"/> to bind this object.
    /// </returns>
    protected virtual DynamicMetaObject GetMetaObject(Expression parameter)
    {
        return new DynamicProxyMetaObject<JToken>(parameter, this, new DynamicProxy<JToken>());
    }

    /// <summary>
    /// Returns the <see cref="DynamicMetaObject"/> responsible for binding operations performed on this object.
    /// </summary>
    /// <param name="parameter">The expression tree representation of the runtime value.</param>
    /// <returns>
    /// The <see cref="DynamicMetaObject"/> to bind this object.
    /// </returns>
    DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
    {
        return GetMetaObject(parameter);
    }

    object ICloneable.Clone()
    {
        return DeepClone();
    }

    /// <summary>
    /// Creates a new instance of the <see cref="JToken"/>. All child tokens are recursively cloned.
    /// </summary>
    /// <returns>A new instance of the <see cref="JToken"/>.</returns>
    public JToken DeepClone()
    {
        return CloneToken();
    }

    /// <summary>
    /// Adds an object to the annotation list of this <see cref="JToken"/>.
    /// </summary>
    /// <param name="annotation">The annotation to add.</param>
    public void AddAnnotation(object annotation)
    {
        if (annotation == null)
        {
            throw new ArgumentNullException(nameof(annotation));
        }

        if (_annotations == null)
        {
            _annotations = annotation is object[] ? new[] { annotation } : annotation;
        }
        else
        {
            if (_annotations is object[] annotations)
            {
                var index = 0;
                while (index < annotations.Length && annotations[index] != null)
                {
                    index++;
                }

                if (index == annotations.Length)
                {
                    Array.Resize(ref annotations, index * 2);
                    _annotations = annotations;
                }

                annotations[index] = annotation;
            }
            else
            {
                _annotations = new[] {_annotations, annotation};
            }
        }
    }

    /// <summary>
    /// Get the first annotation object of the specified type from this <see cref="JToken"/>.
    /// </summary>
    /// <typeparam name="T">The type of the annotation to retrieve.</typeparam>
    /// <returns>The first annotation object that matches the specified type, or <c>null</c> if no annotation is of the specified type.</returns>
    public T? Annotation<T>() where T : class
    {
        if (_annotations != null)
        {
            if (_annotations is not object[] annotations)
            {
                return _annotations as T;
            }
            for (var i = 0; i < annotations.Length; i++)
            {
                var annotation = annotations[i];
                if (annotation == null)
                {
                    break;
                }

                if (annotation is T local)
                {
                    return local;
                }
            }
        }

        return default;
    }

    /// <summary>
    /// Gets the first annotation object of the specified type from this <see cref="JToken"/>.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> of the annotation to retrieve.</param>
    /// <returns>The first annotation object that matches the specified type, or <c>null</c> if no annotation is of the specified type.</returns>
    public object? Annotation(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (_annotations != null)
        {
            if (_annotations is object[] annotations)
            {
                for (var i = 0; i < annotations.Length; i++)
                {
                    var o = annotations[i];
                    if (o == null)
                    {
                        break;
                    }

                    if (type.IsInstanceOfType(o))
                    {
                        return o;
                    }
                }
            }
            else
            {
                if (type.IsInstanceOfType(_annotations))
                {
                    return _annotations;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a collection of annotations of the specified type for this <see cref="JToken"/>.
    /// </summary>
    /// <typeparam name="T">The type of the annotations to retrieve.</typeparam>
    /// <returns>An <see cref="IEnumerable{T}"/> that contains the annotations for this <see cref="JToken"/>.</returns>
    public IEnumerable<T> Annotations<T>() where T : class
    {
        if (_annotations == null)
        {
            yield break;
        }

        if (_annotations is object[] annotations)
        {
            for (var i = 0; i < annotations.Length; i++)
            {
                var o = annotations[i];
                if (o == null)
                {
                    break;
                }

                if (o is T casted)
                {
                    yield return casted;
                }
            }
            yield break;
        }

        if (_annotations is not T annotation)
        {
            yield break;
        }

        yield return annotation;
    }

    /// <summary>
    /// Gets a collection of annotations of the specified type for this <see cref="JToken"/>.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> of the annotations to retrieve.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="Object"/> that contains the annotations that match the specified type for this <see cref="JToken"/>.</returns>
    public IEnumerable<object> Annotations(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (_annotations == null)
        {
            yield break;
        }

        if (_annotations is object[] annotations)
        {
            for (var i = 0; i < annotations.Length; i++)
            {
                var o = annotations[i];
                if (o == null)
                {
                    break;
                }

                if (type.IsInstanceOfType(o))
                {
                    yield return o;
                }
            }
            yield break;
        }

        if (!type.IsInstanceOfType(_annotations))
        {
            yield break;
        }

        yield return _annotations;
    }

    /// <summary>
    /// Removes the annotations of the specified type from this <see cref="JToken"/>.
    /// </summary>
    /// <typeparam name="T">The type of annotations to remove.</typeparam>
    public void RemoveAnnotations<T>() where T : class
    {
        if (_annotations != null)
        {
            if (_annotations is object?[] annotations)
            {
                var index = 0;
                var keepCount = 0;
                while (index < annotations.Length)
                {
                    var obj2 = annotations[index];
                    if (obj2 == null)
                    {
                        break;
                    }

                    if (obj2 is not T)
                    {
                        annotations[keepCount++] = obj2;
                    }

                    index++;
                }

                if (keepCount == 0)
                {
                    _annotations = null;
                }
                else
                {
                    while (keepCount < index)
                    {
                        annotations[keepCount++] = null;
                    }
                }
            }
            else
            {
                if (_annotations is T)
                {
                    _annotations = null;
                }
            }
        }
    }

    /// <summary>
    /// Removes the annotations of the specified type from this <see cref="JToken"/>.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> of annotations to remove.</param>
    public void RemoveAnnotations(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (_annotations != null)
        {
            if (_annotations is object?[] annotations)
            {
                var index = 0;
                var keepCount = 0;
                while (index < annotations.Length)
                {
                    var o = annotations[index];
                    if (o == null)
                    {
                        break;
                    }

                    if (!type.IsInstanceOfType(o))
                    {
                        annotations[keepCount++] = o;
                    }

                    index++;
                }

                if (keepCount == 0)
                {
                    _annotations = null;
                }
                else
                {
                    while (keepCount < index)
                    {
                        annotations[keepCount++] = null;
                    }
                }
            }
            else
            {
                if (type.IsInstanceOfType(_annotations))
                {
                    _annotations = null;
                }
            }
        }
    }

    internal void CopyAnnotations(JToken target, JToken source)
    {
        if (source._annotations is object[] annotations)
        {
            target._annotations = annotations.ToArray();
        }
        else
        {
            target._annotations = source._annotations;
        }
    }
}