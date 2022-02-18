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

using System.ComponentModel;

#nullable disable

namespace Argon.Schema;

/// <summary>
/// <para>
/// Generates a <see cref="JsonSchema"/> from a specified <see cref="Type"/>.
/// </para>
/// <note type="caution">
/// JSON Schema validation has been moved to its own package. See <see href="https://www.newtonsoft.com/jsonschema">https://www.newtonsoft.com/jsonschema</see> for more details.
/// </note>
/// </summary>
[Obsolete("JSON Schema validation has been moved to its own package. See https://www.newtonsoft.com/jsonschema for more details.")]
public class JsonSchemaGenerator
{
    /// <summary>
    /// Gets or sets how undefined schemas are handled by the serializer.
    /// </summary>
    public UndefinedSchemaIdHandling UndefinedSchemaIdHandling { get; set; }

    IContractResolver _contractResolver;

    /// <summary>
    /// Gets or sets the contract resolver.
    /// </summary>
    /// <value>The contract resolver.</value>
    public IContractResolver ContractResolver
    {
        get
        {
            if (_contractResolver == null)
            {
                return DefaultContractResolver.Instance;
            }

            return _contractResolver;
        }
        set => _contractResolver = value;
    }

    class TypeSchema
    {
        public Type Type { get; }
        public JsonSchema Schema { get; }

        public TypeSchema(Type type, JsonSchema schema)
        {
            ValidationUtils.ArgumentNotNull(type, nameof(type));
            ValidationUtils.ArgumentNotNull(schema, nameof(schema));

            Type = type;
            Schema = schema;
        }
    }

    JsonSchemaResolver _resolver;
    readonly IList<TypeSchema> _stack = new List<TypeSchema>();

    JsonSchema CurrentSchema { get; set; }

    void Push(TypeSchema typeSchema)
    {
        CurrentSchema = typeSchema.Schema;
        _stack.Add(typeSchema);
        _resolver.LoadedSchemas.Add(typeSchema.Schema);
    }

    TypeSchema Pop()
    {
        var popped = _stack[_stack.Count - 1];
        _stack.RemoveAt(_stack.Count - 1);
        var newValue = _stack.LastOrDefault();
        if (newValue != null)
        {
            CurrentSchema = newValue.Schema;
        }
        else
        {
            CurrentSchema = null;
        }

        return popped;
    }

    /// <summary>
    /// Generate a <see cref="JsonSchema"/> from the specified type.
    /// </summary>
    /// <param name="type">The type to generate a <see cref="JsonSchema"/> from.</param>
    /// <returns>A <see cref="JsonSchema"/> generated from the specified type.</returns>
    public JsonSchema Generate(Type type)
    {
        return Generate(type, new JsonSchemaResolver(), false);
    }

    /// <summary>
    /// Generate a <see cref="JsonSchema"/> from the specified type.
    /// </summary>
    /// <param name="type">The type to generate a <see cref="JsonSchema"/> from.</param>
    /// <param name="resolver">The <see cref="JsonSchemaResolver"/> used to resolve schema references.</param>
    /// <returns>A <see cref="JsonSchema"/> generated from the specified type.</returns>
    public JsonSchema Generate(Type type, JsonSchemaResolver resolver)
    {
        return Generate(type, resolver, false);
    }

    /// <summary>
    /// Generate a <see cref="JsonSchema"/> from the specified type.
    /// </summary>
    /// <param name="type">The type to generate a <see cref="JsonSchema"/> from.</param>
    /// <param name="rootSchemaNullable">Specify whether the generated root <see cref="JsonSchema"/> will be nullable.</param>
    /// <returns>A <see cref="JsonSchema"/> generated from the specified type.</returns>
    public JsonSchema Generate(Type type, bool rootSchemaNullable)
    {
        return Generate(type, new JsonSchemaResolver(), rootSchemaNullable);
    }

    /// <summary>
    /// Generate a <see cref="JsonSchema"/> from the specified type.
    /// </summary>
    /// <param name="type">The type to generate a <see cref="JsonSchema"/> from.</param>
    /// <param name="resolver">The <see cref="JsonSchemaResolver"/> used to resolve schema references.</param>
    /// <param name="rootSchemaNullable">Specify whether the generated root <see cref="JsonSchema"/> will be nullable.</param>
    /// <returns>A <see cref="JsonSchema"/> generated from the specified type.</returns>
    public JsonSchema Generate(Type type, JsonSchemaResolver resolver, bool rootSchemaNullable)
    {
        ValidationUtils.ArgumentNotNull(type, nameof(type));
        ValidationUtils.ArgumentNotNull(resolver, nameof(resolver));

        _resolver = resolver;

        return GenerateInternal(type, !rootSchemaNullable ? Required.Always : Required.Default, false);
    }

    string GetTitle(Type type)
    {
        var containerAttribute = JsonTypeReflector.GetCachedAttribute<JsonContainerAttribute>(type);

        if (!StringUtils.IsNullOrEmpty(containerAttribute?.Title))
        {
            return containerAttribute.Title;
        }

        return null;
    }

    string GetDescription(Type type)
    {
        var containerAttribute = JsonTypeReflector.GetCachedAttribute<JsonContainerAttribute>(type);

        if (!StringUtils.IsNullOrEmpty(containerAttribute?.Description))
        {
            return containerAttribute.Description;
        }

        var descriptionAttribute = ReflectionUtils.GetAttribute<DescriptionAttribute>(type);
        return descriptionAttribute?.Description;
    }

    string GetTypeId(Type type, bool explicitOnly)
    {
        var containerAttribute = JsonTypeReflector.GetCachedAttribute<JsonContainerAttribute>(type);

        if (!StringUtils.IsNullOrEmpty(containerAttribute?.Id))
        {
            return containerAttribute.Id;
        }

        if (explicitOnly)
        {
            return null;
        }

        switch (UndefinedSchemaIdHandling)
        {
            case UndefinedSchemaIdHandling.UseTypeName:
                return type.FullName;
            case UndefinedSchemaIdHandling.UseAssemblyQualifiedName:
                return type.AssemblyQualifiedName;
            default:
                return null;
        }
    }

    JsonSchema GenerateInternal(Type type, Required valueRequired, bool required)
    {
        ValidationUtils.ArgumentNotNull(type, nameof(type));

        var resolvedId = GetTypeId(type, false);
        var explicitId = GetTypeId(type, true);

        if (!StringUtils.IsNullOrEmpty(resolvedId))
        {
            var resolvedSchema = _resolver.GetSchema(resolvedId);
            if (resolvedSchema != null)
            {
                // resolved schema is not null but referencing member allows nulls
                // change resolved schema to allow nulls. hacky but what are ya gonna do?
                if (valueRequired != Required.Always && !HasFlag(resolvedSchema.Type, JsonSchemaType.Null))
                {
                    resolvedSchema.Type |= JsonSchemaType.Null;
                }
                if (required && resolvedSchema.Required != true)
                {
                    resolvedSchema.Required = true;
                }

                return resolvedSchema;
            }
        }

        // test for unresolved circular reference
        if (_stack.Any(tc => tc.Type == type))
        {
            throw new JsonException("Unresolved circular reference for type '{0}'. Explicitly define an Id for the type using a JsonObject/JsonArray attribute or automatically generate a type Id using the UndefinedSchemaIdHandling property.".FormatWith(CultureInfo.InvariantCulture, type));
        }

        var contract = ContractResolver.ResolveContract(type);
        var converter = contract.Converter ?? contract.InternalConverter;

        Push(new TypeSchema(type, new JsonSchema()));

        if (explicitId != null)
        {
            CurrentSchema.Id = explicitId;
        }

        if (required)
        {
            CurrentSchema.Required = true;
        }
        CurrentSchema.Title = GetTitle(type);
        CurrentSchema.Description = GetDescription(type);

        if (converter != null)
        {
            // todo: Add GetSchema to JsonConverter and use here?
            CurrentSchema.Type = JsonSchemaType.Any;
        }
        else
        {
            switch (contract.ContractType)
            {
                case JsonContractType.Object:
                    CurrentSchema.Type = AddNullType(JsonSchemaType.Object, valueRequired);
                    CurrentSchema.Id = GetTypeId(type, false);
                    GenerateObjectSchema(type, (JsonObjectContract)contract);
                    break;
                case JsonContractType.Array:
                    CurrentSchema.Type = AddNullType(JsonSchemaType.Array, valueRequired);

                    CurrentSchema.Id = GetTypeId(type, false);

                    var arrayAttribute = JsonTypeReflector.GetCachedAttribute<JsonArrayAttribute>(type);
                    var allowNullItem = arrayAttribute == null || arrayAttribute.AllowNullItems;

                    var collectionItemType = ReflectionUtils.GetCollectionItemType(type);
                    if (collectionItemType != null)
                    {
                        CurrentSchema.Items = new List<JsonSchema>();
                        CurrentSchema.Items.Add(GenerateInternal(collectionItemType, !allowNullItem ? Required.Always : Required.Default, false));
                    }
                    break;
                case JsonContractType.Primitive:
                    CurrentSchema.Type = GetJsonSchemaType(type, valueRequired);

                    if (CurrentSchema.Type == JsonSchemaType.Integer && type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), true))
                    {
                        CurrentSchema.Enum = new List<JToken>();

                        var enumValues = EnumUtils.GetEnumValuesAndNames(type);
                        for (var i = 0; i < enumValues.Names.Length; i++)
                        {
                            var v = enumValues.Values[i];
                            var value = JToken.FromObject(Enum.ToObject(type, v));

                            CurrentSchema.Enum.Add(value);
                        }
                    }
                    break;
                case JsonContractType.String:
                    var schemaType = !ReflectionUtils.IsNullable(contract.UnderlyingType)
                        ? JsonSchemaType.String
                        : AddNullType(JsonSchemaType.String, valueRequired);

                    CurrentSchema.Type = schemaType;
                    break;
                case JsonContractType.Dictionary:
                    CurrentSchema.Type = AddNullType(JsonSchemaType.Object, valueRequired);

                    ReflectionUtils.GetDictionaryKeyValueTypes(type, out var keyType, out var valueType);

                    if (keyType != null)
                    {
                        var keyContract = ContractResolver.ResolveContract(keyType);

                        // can be converted to a string
                        if (keyContract.ContractType == JsonContractType.Primitive)
                        {
                            CurrentSchema.AdditionalProperties = GenerateInternal(valueType, Required.Default, false);
                        }
                    }
                    break;
                case JsonContractType.Serializable:
                    CurrentSchema.Type = AddNullType(JsonSchemaType.Object, valueRequired);
                    CurrentSchema.Id = GetTypeId(type, false);
                    GenerateISerializableContract(type, (JsonISerializableContract)contract);
                    break;
                case JsonContractType.Dynamic:
                case JsonContractType.Linq:
                    CurrentSchema.Type = JsonSchemaType.Any;
                    break;
                default:
                    throw new JsonException("Unexpected contract type: {0}".FormatWith(CultureInfo.InvariantCulture, contract));
            }
        }

        return Pop().Schema;
    }

    JsonSchemaType AddNullType(JsonSchemaType type, Required valueRequired)
    {
        if (valueRequired != Required.Always)
        {
            return type | JsonSchemaType.Null;
        }

        return type;
    }

    bool HasFlag(DefaultValueHandling value, DefaultValueHandling flag)
    {
        return (value & flag) == flag;
    }

    void GenerateObjectSchema(Type type, JsonObjectContract contract)
    {
        CurrentSchema.Properties = new Dictionary<string, JsonSchema>();
        foreach (var property in contract.Properties)
        {
            if (!property.Ignored)
            {
                var optional = property.NullValueHandling == NullValueHandling.Ignore ||
                               HasFlag(property.DefaultValueHandling.GetValueOrDefault(), DefaultValueHandling.Ignore) ||
                               property.ShouldSerialize != null ||
                               property.GetIsSpecified != null;

                var propertySchema = GenerateInternal(property.PropertyType, property.Required, !optional);

                if (property.DefaultValue != null)
                {
                    propertySchema.Default = JToken.FromObject(property.DefaultValue);
                }

                CurrentSchema.Properties.Add(property.PropertyName, propertySchema);
            }
        }

        if (type.IsSealed)
        {
            CurrentSchema.AllowAdditionalProperties = false;
        }
    }

    void GenerateISerializableContract(Type type, JsonISerializableContract contract)
    {
        CurrentSchema.AllowAdditionalProperties = true;
    }

    internal static bool HasFlag(JsonSchemaType? value, JsonSchemaType flag)
    {
        // default value is Any
        if (value == null)
        {
            return true;
        }

        var match = (value & flag) == flag;
        if (match)
        {
            return true;
        }

        // integer is a subset of float
        if (flag == JsonSchemaType.Integer && (value & JsonSchemaType.Float) == JsonSchemaType.Float)
        {
            return true;
        }

        return false;
    }

    JsonSchemaType GetJsonSchemaType(Type type, Required valueRequired)
    {
        var schemaType = JsonSchemaType.None;
        if (valueRequired != Required.Always && ReflectionUtils.IsNullable(type))
        {
            schemaType = JsonSchemaType.Null;
            if (ReflectionUtils.IsNullableType(type))
            {
                type = Nullable.GetUnderlyingType(type);
            }
        }

        var typeCode = ConvertUtils.GetTypeCode(type);

        switch (typeCode)
        {
            case PrimitiveTypeCode.Empty:
            case PrimitiveTypeCode.Object:
                return schemaType | JsonSchemaType.String;
            case PrimitiveTypeCode.DBNull:
                return schemaType | JsonSchemaType.Null;
            case PrimitiveTypeCode.Boolean:
                return schemaType | JsonSchemaType.Boolean;
            case PrimitiveTypeCode.Char:
                return schemaType | JsonSchemaType.String;
            case PrimitiveTypeCode.SByte:
            case PrimitiveTypeCode.Byte:
            case PrimitiveTypeCode.Int16:
            case PrimitiveTypeCode.UInt16:
            case PrimitiveTypeCode.Int32:
            case PrimitiveTypeCode.UInt32:
            case PrimitiveTypeCode.Int64:
            case PrimitiveTypeCode.UInt64:
            case PrimitiveTypeCode.BigInteger:
                return schemaType | JsonSchemaType.Integer;
            case PrimitiveTypeCode.Single:
            case PrimitiveTypeCode.Double:
            case PrimitiveTypeCode.Decimal:
                return schemaType | JsonSchemaType.Float;
            // convert to string?
            case PrimitiveTypeCode.DateTime:
            case PrimitiveTypeCode.DateTimeOffset:
                return schemaType | JsonSchemaType.String;
            case PrimitiveTypeCode.String:
            case PrimitiveTypeCode.Uri:
            case PrimitiveTypeCode.Guid:
            case PrimitiveTypeCode.TimeSpan:
            case PrimitiveTypeCode.Bytes:
                return schemaType | JsonSchemaType.String;
            default:
                throw new JsonException("Unexpected type code '{0}' for type '{1}'.".FormatWith(CultureInfo.InvariantCulture, typeCode, type));
        }
    }
}