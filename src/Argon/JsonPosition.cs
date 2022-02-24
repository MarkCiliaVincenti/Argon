#region License
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

enum JsonContainerType
{
    None = 0,
    Object = 1,
    Array = 2,
    Constructor = 3
}

struct JsonPosition
{
    static readonly char[] specialCharacters = { '.', ' ', '\'', '/', '"', '[', ']', '(', ')', '\t', '\n', '\r', '\f', '\b', '\\', '\u0085', '\u2028', '\u2029' };

    internal JsonContainerType Type;
    internal int Position;
    internal string? PropertyName;
    internal bool HasIndex;

    public JsonPosition(JsonContainerType type)
    {
        Type = type;
        HasIndex = TypeHasIndex(type);
        Position = -1;
        PropertyName = null;
    }

    internal int CalculateLength()
    {
        switch (Type)
        {
            case JsonContainerType.Object:
                return PropertyName!.Length + 5;
            case JsonContainerType.Array:
            case JsonContainerType.Constructor:
                return MathUtils.IntLength((ulong)Position) + 2;
            default:
                throw new ArgumentOutOfRangeException(nameof(Type));
        }
    }

    internal void WriteTo(StringBuilder sb, ref StringWriter? writer, ref char[]? buffer)
    {
        switch (Type)
        {
            case JsonContainerType.Object:
                var propertyName = PropertyName!;
                if (propertyName.IndexOfAny(specialCharacters) != -1)
                {
                    sb.Append(@"['");

                    writer ??= new StringWriter(sb);

                    JavaScriptUtils.WriteEscapedJavaScriptString(writer, propertyName, '\'', false, JavaScriptUtils.SingleQuoteCharEscapeFlags, StringEscapeHandling.Default, null, ref buffer);

                    sb.Append(@"']");
                }
                else
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('.');
                    }

                    sb.Append(propertyName);
                }
                break;
            case JsonContainerType.Array:
            case JsonContainerType.Constructor:
                sb.Append('[');
                sb.Append(Position);
                sb.Append(']');
                break;
        }
    }

    internal static bool TypeHasIndex(JsonContainerType type)
    {
        return type is JsonContainerType.Array or JsonContainerType.Constructor;
    }

    internal static string BuildPath(List<JsonPosition> positions, JsonPosition? currentPosition)
    {
        var capacity = 0;
        for (var i = 0; i < positions.Count; i++)
        {
            capacity += positions[i].CalculateLength();
        }

        if (currentPosition != null)
        {
            capacity += currentPosition.GetValueOrDefault().CalculateLength();
        }

        var stringBuilder = new StringBuilder(capacity);
        StringWriter? writer = null;
        char[]? buffer = null;
        foreach (var state in positions)
        {
            state.WriteTo(stringBuilder, ref writer, ref buffer);
        }

        if (currentPosition != null)
        {
            currentPosition.GetValueOrDefault().WriteTo(stringBuilder, ref writer, ref buffer);
        }

        return stringBuilder.ToString();
    }

    internal static string FormatMessage(IJsonLineInfo? lineInfo, string path, string message)
    {
        // don't add a fullstop and space when message ends with a new line
        if (!message.EndsWith(Environment.NewLine, StringComparison.Ordinal))
        {
            message = message.Trim();

            if (!message.EndsWith('.'))
            {
                message += ".";
            }

            message += " ";
        }

        message += $"Path '{path}'";

        if (lineInfo != null && lineInfo.HasLineInfo())
        {
            message += $", line {lineInfo.LineNumber}, position {lineInfo.LinePosition}";
        }

        message += ".";

        return message;
    }
}