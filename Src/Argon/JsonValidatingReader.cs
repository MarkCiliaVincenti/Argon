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

using System.Text.RegularExpressions;

#nullable disable

namespace Argon;

/// <summary>
/// <para>
/// Represents a reader that provides <see cref="JsonSchema"/> validation.
/// </para>
/// <note type="caution">
/// JSON Schema validation has been moved to its own package. See <see href="https://www.newtonsoft.com/jsonschema">https://www.newtonsoft.com/jsonschema</see> for more details.
/// </note>
/// </summary>
[Obsolete("JSON Schema validation has been moved to its own package. See https://www.newtonsoft.com/jsonschema for more details.")]
public class JsonValidatingReader : JsonReader, IJsonLineInfo
{
    class SchemaScope
    {
        public string CurrentPropertyName { get; set; }
        public int ArrayItemCount { get; set; }
        public bool IsUniqueArray { get; }
        public IList<JToken> UniqueArrayItems { get; }
        public JTokenWriter CurrentItemWriter { get; set; }

        public IList<JsonSchemaModel> Schemas { get; }

        public Dictionary<string, bool> RequiredProperties { get; }

        public JTokenType TokenType { get; }

        public SchemaScope(JTokenType tokenType, IList<JsonSchemaModel> schemas)
        {
            TokenType = tokenType;
            Schemas = schemas;

            RequiredProperties = schemas.SelectMany(GetRequiredProperties).Distinct().ToDictionary(p => p, _ => false);

            if (tokenType == JTokenType.Array && schemas.Any(s => s.UniqueItems))
            {
                IsUniqueArray = true;
                UniqueArrayItems = new List<JToken>();
            }
        }

        IEnumerable<string> GetRequiredProperties(JsonSchemaModel schema)
        {
            if (schema?.Properties == null)
            {
                return Enumerable.Empty<string>();
            }

            return schema.Properties.Where(p => p.Value.Required).Select(p => p.Key);
        }
    }

    readonly Stack<SchemaScope> _stack;
    JsonSchema _schema;
    JsonSchemaModel _model;
    SchemaScope _currentScope;

    /// <summary>
    /// Sets an event handler for receiving schema validation errors.
    /// </summary>
    public event ValidationEventHandler ValidationEventHandler;

    /// <summary>
    /// Gets the text value of the current JSON token.
    /// </summary>
    /// <value></value>
    public override object Value => Reader.Value;

    /// <summary>
    /// Gets the depth of the current token in the JSON document.
    /// </summary>
    /// <value>The depth of the current token in the JSON document.</value>
    public override int Depth => Reader.Depth;

    /// <summary>
    /// Gets the path of the current JSON token. 
    /// </summary>
    public override string Path => Reader.Path;

    /// <summary>
    /// Gets the quotation mark character used to enclose the value of a string.
    /// </summary>
    /// <value></value>
    public override char QuoteChar
    {
        get => Reader.QuoteChar;
        protected internal set { }
    }

    /// <summary>
    /// Gets the type of the current JSON token.
    /// </summary>
    /// <value></value>
    public override JsonToken TokenType => Reader.TokenType;

    /// <summary>
    /// Gets the .NET type for the current JSON token.
    /// </summary>
    /// <value></value>
    public override Type ValueType => Reader.ValueType;

    void Push(SchemaScope scope)
    {
        _stack.Push(scope);
        _currentScope = scope;
    }

    SchemaScope Pop()
    {
        var poppedScope = _stack.Pop();
        _currentScope = _stack.Count != 0
            ? _stack.Peek()
            : null;

        return poppedScope;
    }

    IList<JsonSchemaModel> CurrentSchemas => _currentScope.Schemas;

    static readonly IList<JsonSchemaModel> EmptySchemaList = new List<JsonSchemaModel>();

    IList<JsonSchemaModel> CurrentMemberSchemas
    {
        get
        {
            if (_currentScope == null)
            {
                return new List<JsonSchemaModel>(new[] { _model });
            }

            if (_currentScope.Schemas == null || _currentScope.Schemas.Count == 0)
            {
                return EmptySchemaList;
            }

            switch (_currentScope.TokenType)
            {
                case JTokenType.None:
                    return _currentScope.Schemas;
                case JTokenType.Object:
                {
                    if (_currentScope.CurrentPropertyName == null)
                    {
                        throw new JsonReaderException("CurrentPropertyName has not been set on scope.");
                    }

                    IList<JsonSchemaModel> schemas = new List<JsonSchemaModel>();

                    foreach (var schema in CurrentSchemas)
                    {
                        if (schema.Properties != null && schema.Properties.TryGetValue(_currentScope.CurrentPropertyName, out var propertySchema))
                        {
                            schemas.Add(propertySchema);
                        }
                        if (schema.PatternProperties != null)
                        {
                            foreach (var patternProperty in schema.PatternProperties)
                            {
                                if (Regex.IsMatch(_currentScope.CurrentPropertyName, patternProperty.Key))
                                {
                                    schemas.Add(patternProperty.Value);
                                }
                            }
                        }

                        if (schemas.Count == 0 && schema.AllowAdditionalProperties && schema.AdditionalProperties != null)
                        {
                            schemas.Add(schema.AdditionalProperties);
                        }
                    }

                    return schemas;
                }
                case JTokenType.Array:
                {
                    IList<JsonSchemaModel> schemas = new List<JsonSchemaModel>();

                    foreach (var schema in CurrentSchemas)
                    {
                        if (!schema.PositionalItemsValidation)
                        {
                            if (schema.Items is {Count: > 0})
                            {
                                schemas.Add(schema.Items[0]);
                            }
                        }
                        else
                        {
                            if (schema.Items is {Count: > 0})
                            {
                                if (schema.Items.Count > _currentScope.ArrayItemCount - 1)
                                {
                                    schemas.Add(schema.Items[_currentScope.ArrayItemCount - 1]);
                                }
                            }

                            if (schema.AllowAdditionalItems && schema.AdditionalItems != null)
                            {
                                schemas.Add(schema.AdditionalItems);
                            }
                        }
                    }

                    return schemas;
                }
                case JTokenType.Constructor:
                    return EmptySchemaList;
                default:
                    throw new ArgumentOutOfRangeException("TokenType", "Unexpected token type: {0}".FormatWith(CultureInfo.InvariantCulture, _currentScope.TokenType));
            }
        }
    }

    void RaiseError(string message, JsonSchemaModel schema)
    {
        IJsonLineInfo lineInfo = this;

        var exceptionMessage = lineInfo.HasLineInfo()
            ? message + " Line {0}, position {1}.".FormatWith(CultureInfo.InvariantCulture, lineInfo.LineNumber, lineInfo.LinePosition)
            : message;

        OnValidationEvent(new JsonSchemaException(exceptionMessage, null, Path, lineInfo.LineNumber, lineInfo.LinePosition));
    }

    void OnValidationEvent(JsonSchemaException exception)
    {
        var handler = ValidationEventHandler;
        if (handler != null)
        {
            handler(this, new ValidationEventArgs(exception));
        }
        else
        {
            throw exception;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValidatingReader"/> class that
    /// validates the content returned from the given <see cref="JsonReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="JsonReader"/> to read from while validating.</param>
    public JsonValidatingReader(JsonReader reader)
    {
        ValidationUtils.ArgumentNotNull(reader, nameof(reader));
        Reader = reader;
        _stack = new Stack<SchemaScope>();
    }

    /// <summary>
    /// Gets or sets the schema.
    /// </summary>
    /// <value>The schema.</value>
    public JsonSchema Schema
    {
        get => _schema;
        set
        {
            if (TokenType != JsonToken.None)
            {
                throw new InvalidOperationException("Cannot change schema while validating JSON.");
            }

            _schema = value;
            _model = null;
        }
    }

    /// <summary>
    /// Gets the <see cref="JsonReader"/> used to construct this <see cref="JsonValidatingReader"/>.
    /// </summary>
    /// <value>The <see cref="JsonReader"/> specified in the constructor.</value>
    public JsonReader Reader { get; }

    /// <summary>
    /// Changes the reader's state to <see cref="JsonReader.State.Closed"/>.
    /// If <see cref="JsonReader.CloseInput"/> is set to <c>true</c>, the underlying <see cref="JsonReader"/> is also closed.
    /// </summary>
    public override void Close()
    {
        base.Close();
        if (CloseInput)
        {
            Reader?.Close();
        }
    }

    void ValidateNotDisallowed(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        var currentNodeType = GetCurrentNodeSchemaType();
        if (currentNodeType != null)
        {
            if (JsonSchemaGenerator.HasFlag(schema.Disallow, currentNodeType.GetValueOrDefault()))
            {
                RaiseError("Type {0} is disallowed.".FormatWith(CultureInfo.InvariantCulture, currentNodeType), schema);
            }
        }
    }

    JsonSchemaType? GetCurrentNodeSchemaType()
    {
        switch (Reader.TokenType)
        {
            case JsonToken.StartObject:
                return JsonSchemaType.Object;
            case JsonToken.StartArray:
                return JsonSchemaType.Array;
            case JsonToken.Integer:
                return JsonSchemaType.Integer;
            case JsonToken.Float:
                return JsonSchemaType.Float;
            case JsonToken.String:
                return JsonSchemaType.String;
            case JsonToken.Boolean:
                return JsonSchemaType.Boolean;
            case JsonToken.Null:
                return JsonSchemaType.Null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="Nullable{T}"/> of <see cref="Int32"/>.
    /// </summary>
    /// <returns>A <see cref="Nullable{T}"/> of <see cref="Int32"/>.</returns>
    public override int? ReadAsInt32()
    {
        var i = Reader.ReadAsInt32();

        ValidateCurrentToken();
        return i;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="Byte"/>[].
    /// </summary>
    /// <returns>
    /// A <see cref="Byte"/>[] or <c>null</c> if the next JSON token is null.
    /// </returns>
    public override byte[] ReadAsBytes()
    {
        var data = Reader.ReadAsBytes();

        ValidateCurrentToken();
        return data;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="Nullable{T}"/> of <see cref="Decimal"/>.
    /// </summary>
    /// <returns>A <see cref="Nullable{T}"/> of <see cref="Decimal"/>.</returns>
    public override decimal? ReadAsDecimal()
    {
        var d = Reader.ReadAsDecimal();

        ValidateCurrentToken();
        return d;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="Nullable{T}"/> of <see cref="Double"/>.
    /// </summary>
    /// <returns>A <see cref="Nullable{T}"/> of <see cref="Double"/>.</returns>
    public override double? ReadAsDouble()
    {
        var d = Reader.ReadAsDouble();

        ValidateCurrentToken();
        return d;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="Nullable{T}"/> of <see cref="Boolean"/>.
    /// </summary>
    /// <returns>A <see cref="Nullable{T}"/> of <see cref="Boolean"/>.</returns>
    public override bool? ReadAsBoolean()
    {
        var b = Reader.ReadAsBoolean();

        ValidateCurrentToken();
        return b;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="String"/>.
    /// </summary>
    /// <returns>A <see cref="String"/>. This method will return <c>null</c> at the end of an array.</returns>
    public override string ReadAsString()
    {
        var s = Reader.ReadAsString();

        ValidateCurrentToken();
        return s;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="Nullable{T}"/> of <see cref="DateTime"/>.
    /// </summary>
    /// <returns>A <see cref="Nullable{T}"/> of <see cref="DateTime"/>. This method will return <c>null</c> at the end of an array.</returns>
    public override DateTime? ReadAsDateTime()
    {
        var dateTime = Reader.ReadAsDateTime();

        ValidateCurrentToken();
        return dateTime;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/> as a <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns>A <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>.</returns>
    public override DateTimeOffset? ReadAsDateTimeOffset()
    {
        var dateTimeOffset = Reader.ReadAsDateTimeOffset();

        ValidateCurrentToken();
        return dateTimeOffset;
    }

    /// <summary>
    /// Reads the next JSON token from the underlying <see cref="JsonReader"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the next token was read successfully; <c>false</c> if there are no more tokens to read.
    /// </returns>
    public override bool Read()
    {
        if (!Reader.Read())
        {
            return false;
        }

        if (Reader.TokenType == JsonToken.Comment)
        {
            return true;
        }

        ValidateCurrentToken();
        return true;
    }

    void ValidateCurrentToken()
    {
        // first time validate has been called. build model
        if (_model == null)
        {
            var builder = new JsonSchemaModelBuilder();
            _model = builder.Build(_schema);

            if (!JsonTokenUtils.IsStartToken(Reader.TokenType))
            {
                Push(new SchemaScope(JTokenType.None, CurrentMemberSchemas));
            }
        }

        switch (Reader.TokenType)
        {
            case JsonToken.StartObject:
                ProcessValue();
                IList<JsonSchemaModel> objectSchemas = CurrentMemberSchemas.Where(ValidateObject).ToList();
                Push(new SchemaScope(JTokenType.Object, objectSchemas));
                WriteToken(CurrentSchemas);
                break;
            case JsonToken.StartArray:
                ProcessValue();
                IList<JsonSchemaModel> arraySchemas = CurrentMemberSchemas.Where(ValidateArray).ToList();
                Push(new SchemaScope(JTokenType.Array, arraySchemas));
                WriteToken(CurrentSchemas);
                break;
            case JsonToken.StartConstructor:
                ProcessValue();
                Push(new SchemaScope(JTokenType.Constructor, null));
                WriteToken(CurrentSchemas);
                break;
            case JsonToken.PropertyName:
                WriteToken(CurrentSchemas);
                foreach (var schema in CurrentSchemas)
                {
                    ValidatePropertyName(schema);
                }
                break;
            case JsonToken.Raw:
                ProcessValue();
                break;
            case JsonToken.Integer:
                ProcessValue();
                WriteToken(CurrentMemberSchemas);
                foreach (var schema in CurrentMemberSchemas)
                {
                    ValidateInteger(schema);
                }
                break;
            case JsonToken.Float:
                ProcessValue();
                WriteToken(CurrentMemberSchemas);
                foreach (var schema in CurrentMemberSchemas)
                {
                    ValidateFloat(schema);
                }
                break;
            case JsonToken.String:
                ProcessValue();
                WriteToken(CurrentMemberSchemas);
                foreach (var schema in CurrentMemberSchemas)
                {
                    ValidateString(schema);
                }
                break;
            case JsonToken.Boolean:
                ProcessValue();
                WriteToken(CurrentMemberSchemas);
                foreach (var schema in CurrentMemberSchemas)
                {
                    ValidateBoolean(schema);
                }
                break;
            case JsonToken.Null:
                ProcessValue();
                WriteToken(CurrentMemberSchemas);
                foreach (var schema in CurrentMemberSchemas)
                {
                    ValidateNull(schema);
                }
                break;
            case JsonToken.EndObject:
                WriteToken(CurrentSchemas);
                foreach (var schema in CurrentSchemas)
                {
                    ValidateEndObject(schema);
                }
                Pop();
                break;
            case JsonToken.EndArray:
                WriteToken(CurrentSchemas);
                foreach (var schema in CurrentSchemas)
                {
                    ValidateEndArray(schema);
                }
                Pop();
                break;
            case JsonToken.EndConstructor:
                WriteToken(CurrentSchemas);
                Pop();
                break;
            case JsonToken.Undefined:
            case JsonToken.Date:
            case JsonToken.Bytes:
                // these have no equivalent in JSON schema
                WriteToken(CurrentMemberSchemas);
                break;
            case JsonToken.None:
                // no content, do nothing
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    void WriteToken(IList<JsonSchemaModel> schemas)
    {
        foreach (var schemaScope in _stack)
        {
            var isInUniqueArray = schemaScope.TokenType == JTokenType.Array && schemaScope.IsUniqueArray && schemaScope.ArrayItemCount > 0;

            if (isInUniqueArray || schemas.Any(s => s.Enum != null))
            {
                if (schemaScope.CurrentItemWriter == null)
                {
                    if (JsonTokenUtils.IsEndToken(Reader.TokenType))
                    {
                        continue;
                    }

                    schemaScope.CurrentItemWriter = new JTokenWriter();
                }

                schemaScope.CurrentItemWriter.WriteToken(Reader, false);

                // finished writing current item
                if (schemaScope.CurrentItemWriter.Top == 0 && Reader.TokenType != JsonToken.PropertyName)
                {
                    var finishedItem = schemaScope.CurrentItemWriter.Token;

                    // start next item with new writer
                    schemaScope.CurrentItemWriter = null;

                    if (isInUniqueArray)
                    {
                        if (schemaScope.UniqueArrayItems.Contains(finishedItem, JToken.EqualityComparer))
                        {
                            RaiseError("Non-unique array item at index {0}.".FormatWith(CultureInfo.InvariantCulture, schemaScope.ArrayItemCount - 1), schemaScope.Schemas.First(s => s.UniqueItems));
                        }

                        schemaScope.UniqueArrayItems.Add(finishedItem);
                    }
                    else if (schemas.Any(s => s.Enum != null))
                    {
                        foreach (var schema in schemas)
                        {
                            if (schema.Enum != null)
                            {
                                if (!schema.Enum.ContainsValue(finishedItem, JToken.EqualityComparer))
                                {
                                    var sw = new StringWriter(CultureInfo.InvariantCulture);
                                    finishedItem.WriteTo(new JsonTextWriter(sw));

                                    RaiseError("Value {0} is not defined in enum.".FormatWith(CultureInfo.InvariantCulture, sw.ToString()), schema);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void ValidateEndObject(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        var requiredProperties = _currentScope.RequiredProperties;

        if (requiredProperties != null && requiredProperties.Values.Any(v => !v))
        {
            var unmatchedRequiredProperties = requiredProperties.Where(kv => !kv.Value).Select(kv => kv.Key);
            RaiseError("Required properties are missing from object: {0}.".FormatWith(CultureInfo.InvariantCulture, string.Join(", ", unmatchedRequiredProperties)), schema);
        }
    }

    void ValidateEndArray(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        var arrayItemCount = _currentScope.ArrayItemCount;

        if (schema.MaximumItems != null && arrayItemCount > schema.MaximumItems)
        {
            RaiseError("Array item count {0} exceeds maximum count of {1}.".FormatWith(CultureInfo.InvariantCulture, arrayItemCount, schema.MaximumItems), schema);
        }

        if (schema.MinimumItems != null && arrayItemCount < schema.MinimumItems)
        {
            RaiseError("Array item count {0} is less than minimum count of {1}.".FormatWith(CultureInfo.InvariantCulture, arrayItemCount, schema.MinimumItems), schema);
        }
    }

    void ValidateNull(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        if (!TestType(schema, JsonSchemaType.Null))
        {
            return;
        }

        ValidateNotDisallowed(schema);
    }

    void ValidateBoolean(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        if (!TestType(schema, JsonSchemaType.Boolean))
        {
            return;
        }

        ValidateNotDisallowed(schema);
    }

    void ValidateString(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        if (!TestType(schema, JsonSchemaType.String))
        {
            return;
        }

        ValidateNotDisallowed(schema);

        var value = Reader.Value.ToString();

        if (schema.MaximumLength != null && value.Length > schema.MaximumLength)
        {
            RaiseError("String '{0}' exceeds maximum length of {1}.".FormatWith(CultureInfo.InvariantCulture, value, schema.MaximumLength), schema);
        }

        if (schema.MinimumLength != null && value.Length < schema.MinimumLength)
        {
            RaiseError("String '{0}' is less than minimum length of {1}.".FormatWith(CultureInfo.InvariantCulture, value, schema.MinimumLength), schema);
        }

        if (schema.Patterns != null)
        {
            foreach (var pattern in schema.Patterns)
            {
                if (!Regex.IsMatch(value, pattern))
                {
                    RaiseError("String '{0}' does not match regex pattern '{1}'.".FormatWith(CultureInfo.InvariantCulture, value, pattern), schema);
                }
            }
        }
    }

    void ValidateInteger(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        if (!TestType(schema, JsonSchemaType.Integer))
        {
            return;
        }

        ValidateNotDisallowed(schema);

        var value = Reader.Value;

        if (schema.Maximum != null)
        {
            if (JValue.Compare(JTokenType.Integer, value, schema.Maximum) > 0)
            {
                RaiseError("Integer {0} exceeds maximum value of {1}.".FormatWith(CultureInfo.InvariantCulture, value, schema.Maximum), schema);
            }
            if (schema.ExclusiveMaximum && JValue.Compare(JTokenType.Integer, value, schema.Maximum) == 0)
            {
                RaiseError("Integer {0} equals maximum value of {1} and exclusive maximum is true.".FormatWith(CultureInfo.InvariantCulture, value, schema.Maximum), schema);
            }
        }

        if (schema.Minimum != null)
        {
            if (JValue.Compare(JTokenType.Integer, value, schema.Minimum) < 0)
            {
                RaiseError("Integer {0} is less than minimum value of {1}.".FormatWith(CultureInfo.InvariantCulture, value, schema.Minimum), schema);
            }
            if (schema.ExclusiveMinimum && JValue.Compare(JTokenType.Integer, value, schema.Minimum) == 0)
            {
                RaiseError("Integer {0} equals minimum value of {1} and exclusive minimum is true.".FormatWith(CultureInfo.InvariantCulture, value, schema.Minimum), schema);
            }
        }

        if (schema.DivisibleBy != null)
        {
            bool notDivisible;
            if (value is BigInteger i)
            {
                // not that this will lose any decimal point on DivisibleBy
                // so manually raise an error if DivisibleBy is not an integer and value is not zero
                var divisibleNonInteger = !Math.Abs(schema.DivisibleBy.Value - Math.Truncate(schema.DivisibleBy.Value)).Equals(0);
                if (divisibleNonInteger)
                {
                    notDivisible = i != 0;
                }
                else
                {
                    notDivisible = i % new BigInteger(schema.DivisibleBy.Value) != 0;
                }
            }
            else
            {
                notDivisible = !IsZero(Convert.ToInt64(value, CultureInfo.InvariantCulture) % schema.DivisibleBy.GetValueOrDefault());
            }

            if (notDivisible)
            {
                RaiseError("Integer {0} is not evenly divisible by {1}.".FormatWith(CultureInfo.InvariantCulture, JsonConvert.ToString(value), schema.DivisibleBy), schema);
            }
        }
    }

    void ProcessValue()
    {
        if (_currentScope is {TokenType: JTokenType.Array})
        {
            _currentScope.ArrayItemCount++;

            foreach (var currentSchema in CurrentSchemas)
            {
                // if there is positional validation and the array index is past the number of item validation schemas and there are no additional items then error
                if (currentSchema is {PositionalItemsValidation: true, AllowAdditionalItems: false} && (currentSchema.Items == null || _currentScope.ArrayItemCount - 1 >= currentSchema.Items.Count))
                {
                    RaiseError("Index {0} has not been defined and the schema does not allow additional items.".FormatWith(CultureInfo.InvariantCulture, _currentScope.ArrayItemCount), currentSchema);
                }
            }
        }
    }

    void ValidateFloat(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        if (!TestType(schema, JsonSchemaType.Float))
        {
            return;
        }

        ValidateNotDisallowed(schema);

        var value = Convert.ToDouble(Reader.Value, CultureInfo.InvariantCulture);

        if (schema.Maximum != null)
        {
            if (value > schema.Maximum)
            {
                RaiseError("Float {0} exceeds maximum value of {1}.".FormatWith(CultureInfo.InvariantCulture, JsonConvert.ToString(value), schema.Maximum), schema);
            }
            if (schema.ExclusiveMaximum && value == schema.Maximum)
            {
                RaiseError("Float {0} equals maximum value of {1} and exclusive maximum is true.".FormatWith(CultureInfo.InvariantCulture, JsonConvert.ToString(value), schema.Maximum), schema);
            }
        }

        if (schema.Minimum != null)
        {
            if (value < schema.Minimum)
            {
                RaiseError("Float {0} is less than minimum value of {1}.".FormatWith(CultureInfo.InvariantCulture, JsonConvert.ToString(value), schema.Minimum), schema);
            }
            if (schema.ExclusiveMinimum && value == schema.Minimum)
            {
                RaiseError("Float {0} equals minimum value of {1} and exclusive minimum is true.".FormatWith(CultureInfo.InvariantCulture, JsonConvert.ToString(value), schema.Minimum), schema);
            }
        }

        if (schema.DivisibleBy != null)
        {
            var remainder = FloatingPointRemainder(value, schema.DivisibleBy.GetValueOrDefault());

            if (!IsZero(remainder))
            {
                RaiseError("Float {0} is not evenly divisible by {1}.".FormatWith(CultureInfo.InvariantCulture, JsonConvert.ToString(value), schema.DivisibleBy), schema);
            }
        }
    }

    static double FloatingPointRemainder(double dividend, double divisor)
    {
        return dividend - Math.Floor(dividend / divisor) * divisor;
    }

    static bool IsZero(double value)
    {
        const double epsilon = 2.2204460492503131e-016;

        return Math.Abs(value) < 20.0 * epsilon;
    }

    void ValidatePropertyName(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        var propertyName = Convert.ToString(Reader.Value, CultureInfo.InvariantCulture);

        if (_currentScope.RequiredProperties.ContainsKey(propertyName))
        {
            _currentScope.RequiredProperties[propertyName] = true;
        }

        if (!schema.AllowAdditionalProperties)
        {
            var propertyDefinied = IsPropertyDefinied(schema, propertyName);

            if (!propertyDefinied)
            {
                RaiseError("Property '{0}' has not been defined and the schema does not allow additional properties.".FormatWith(CultureInfo.InvariantCulture, propertyName), schema);
            }
        }

        _currentScope.CurrentPropertyName = propertyName;
    }

    bool IsPropertyDefinied(JsonSchemaModel schema, string propertyName)
    {
        if (schema.Properties != null && schema.Properties.ContainsKey(propertyName))
        {
            return true;
        }

        if (schema.PatternProperties != null)
        {
            foreach (var pattern in schema.PatternProperties.Keys)
            {
                if (Regex.IsMatch(propertyName, pattern))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool ValidateArray(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return true;
        }

        return TestType(schema, JsonSchemaType.Array);
    }

    bool ValidateObject(JsonSchemaModel schema)
    {
        if (schema == null)
        {
            return true;
        }

        return TestType(schema, JsonSchemaType.Object);
    }

    bool TestType(JsonSchemaModel currentSchema, JsonSchemaType currentType)
    {
        if (!JsonSchemaGenerator.HasFlag(currentSchema.Type, currentType))
        {
            RaiseError("Invalid type. Expected {0} but got {1}.".FormatWith(CultureInfo.InvariantCulture, currentSchema.Type, currentType), currentSchema);
            return false;
        }

        return true;
    }

    bool IJsonLineInfo.HasLineInfo()
    {
        return Reader is IJsonLineInfo lineInfo && lineInfo.HasLineInfo();
    }

    int IJsonLineInfo.LineNumber => Reader is IJsonLineInfo lineInfo ? lineInfo.LineNumber : 0;

    int IJsonLineInfo.LinePosition => Reader is IJsonLineInfo lineInfo ? lineInfo.LinePosition : 0;
}