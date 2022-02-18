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

#nullable disable

abstract class BsonToken
{
    public abstract BsonType Type { get; }
    public BsonToken Parent { get; set; }
    public int CalculatedSize { get; set; }
}

class BsonObject : BsonToken, IEnumerable<BsonProperty>
{
    private readonly List<BsonProperty> _children = new();

    public void Add(string name, BsonToken token)
    {
        _children.Add(new BsonProperty { Name = new BsonString(name, false), Value = token });
        token.Parent = this;
    }

    public override BsonType Type => BsonType.Object;

    public IEnumerator<BsonProperty> GetEnumerator()
    {
        return _children.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

class BsonArray : BsonToken, IEnumerable<BsonToken>
{
    private readonly List<BsonToken> _children = new();

    public void Add(BsonToken token)
    {
        _children.Add(token);
        token.Parent = this;
    }

    public override BsonType Type => BsonType.Array;

    public IEnumerator<BsonToken> GetEnumerator()
    {
        return _children.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

class BsonEmpty : BsonToken
{
    public static readonly BsonToken Null = new BsonEmpty(BsonType.Null);
    public static readonly BsonToken Undefined = new BsonEmpty(BsonType.Undefined);

    private BsonEmpty(BsonType type)
    {
        Type = type;
    }

    public override BsonType Type { get; }
}

class BsonValue : BsonToken
{
    public BsonValue(object value, BsonType type)
    {
        Value = value;
        Type = type;
    }

    public object Value { get; }

    public override BsonType Type { get; }
}

class BsonBoolean : BsonValue
{
    public static readonly BsonBoolean False = new(false);
    public static readonly BsonBoolean True = new(true);

    private BsonBoolean(bool value)
        : base(value, BsonType.Boolean)
    {
    }
}

class BsonString : BsonValue
{
    public int ByteCount { get; set; }
    public bool IncludeLength { get; }

    public BsonString(object value, bool includeLength)
        : base(value, BsonType.String)
    {
        IncludeLength = includeLength;
    }
}

class BsonBinary : BsonValue
{
    public BsonBinaryType BinaryType { get; set; }

    public BsonBinary(byte[] value, BsonBinaryType binaryType)
        : base(value, BsonType.Binary)
    {
        BinaryType = binaryType;
    }
}

class BsonRegex : BsonToken
{
    public BsonString Pattern { get; set; }
    public BsonString Options { get; set; }

    public BsonRegex(string pattern, string options)
    {
        Pattern = new BsonString(pattern, false);
        Options = new BsonString(options, false);
    }

    public override BsonType Type => BsonType.Regex;
}

 class BsonProperty
{
    public BsonString Name { get; set; }
    public BsonToken Value { get; set; }
}