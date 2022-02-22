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

namespace Argon;

public abstract partial class JsonWriter
{
    internal Task AutoCompleteAsync(JsonToken tokenBeingWritten, CancellationToken cancellation)
    {
        var oldState = _currentState;

        // gets new state based on the current state and what is being written
        var newState = StateArray[(int)tokenBeingWritten][(int)oldState];

        if (newState == State.Error)
        {
            throw JsonWriterException.Create(this, $"Token {tokenBeingWritten.ToString()} in state {oldState.ToString()} would result in an invalid JSON object.", null);
        }

        _currentState = newState;

        if (_formatting == Formatting.Indented)
        {
            switch (oldState)
            {
                case State.Start:
                    break;
                case State.Property:
                    return WriteIndentSpaceAsync(cancellation);
                case State.ArrayStart:
                case State.ConstructorStart:
                    return WriteIndentAsync(cancellation);
                case State.Array:
                case State.Constructor:
                    return tokenBeingWritten == JsonToken.Comment ? WriteIndentAsync(cancellation) : AutoCompleteAsync(cancellation);
                case State.Object:
                    switch (tokenBeingWritten)
                    {
                        case JsonToken.Comment:
                            break;
                        case JsonToken.PropertyName:
                            return AutoCompleteAsync(cancellation);
                        default:
                            return WriteValueDelimiterAsync(cancellation);
                    }

                    break;
                default:
                    if (tokenBeingWritten == JsonToken.PropertyName)
                    {
                        return WriteIndentAsync(cancellation);
                    }

                    break;
            }
        }
        else if (tokenBeingWritten != JsonToken.Comment)
        {
            switch (oldState)
            {
                case State.Object:
                case State.Array:
                case State.Constructor:
                    return WriteValueDelimiterAsync(cancellation);
            }
        }

        return AsyncUtils.CompletedTask;
    }

    async Task AutoCompleteAsync(CancellationToken cancellation)
    {
        await WriteValueDelimiterAsync(cancellation).ConfigureAwait(false);
        await WriteIndentAsync(cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously closes this writer.
    /// If <see cref="JsonWriter.CloseOutput"/> is set to <c>true</c>, the destination is also closed.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task CloseAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        Close();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously flushes whatever is in the buffer to the destination and also flushes the destination.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task FlushAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        Flush();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the specified end token.
    /// </summary>
    /// <param name="token">The end token to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    protected virtual Task WriteEndAsync(JsonToken token, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteEnd(token);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes indent characters.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    protected virtual Task WriteIndentAsync(CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteIndent();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the JSON value delimiter.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    protected virtual Task WriteValueDelimiterAsync(CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValueDelimiter();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes an indent space.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    protected virtual Task WriteIndentSpaceAsync(CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteIndentSpace();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes raw JSON without changing the writer's state.
    /// </summary>
    /// <param name="json">The raw JSON to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteRawAsync(string? json, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteRaw(json);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the end of the current JSON object or array.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteEndAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteEnd();
        return AsyncUtils.CompletedTask;
    }

    internal Task WriteEndInternalAsync(CancellationToken cancellation)
    {
        var type = Peek();
        switch (type)
        {
            case JsonContainerType.Object:
                return WriteEndObjectAsync(cancellation);
            case JsonContainerType.Array:
                return WriteEndArrayAsync(cancellation);
            case JsonContainerType.Constructor:
                return WriteEndConstructorAsync(cancellation);
            default:
                if (cancellation.IsCancellationRequested)
                {
                    return cancellation.FromCanceled();
                }

                throw JsonWriterException.Create(this, $"Unexpected type when writing end: {type}", null);
        }
    }

    internal Task InternalWriteEndAsync(JsonContainerType type, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        var levelsToComplete = CalculateLevelsToComplete(type);
        while (levelsToComplete-- > 0)
        {
            var token = GetCloseTokenForType(Pop());

            Task t;
            if (_currentState == State.Property)
            {
                t = WriteNullAsync(cancellation);
                if (!t.IsCompletedSucessfully())
                {
                    return AwaitProperty(t, levelsToComplete, token, cancellation);
                }
            }

            if (_formatting == Formatting.Indented)
            {
                if (_currentState != State.ObjectStart && _currentState != State.ArrayStart)
                {
                    t = WriteIndentAsync(cancellation);
                    if (!t.IsCompletedSucessfully())
                    {
                        return AwaitIndent(t, levelsToComplete, token, cancellation);
                    }
                }
            }

            t = WriteEndAsync(token, cancellation);
            if (!t.IsCompletedSucessfully())
            {
                return AwaitEnd(t, levelsToComplete, cancellation);
            }

            UpdateCurrentState();
        }

        return AsyncUtils.CompletedTask;

        // Local functions, params renamed (capitalized) so as not to capture and allocate when calling async
        async Task AwaitProperty(Task task, int LevelsToComplete, JsonToken token, CancellationToken cancellation)
        {
            await task.ConfigureAwait(false);

            //  Finish current loop
            if (_formatting == Formatting.Indented)
            {
                if (_currentState != State.ObjectStart && _currentState != State.ArrayStart)
                {
                    await WriteIndentAsync(cancellation).ConfigureAwait(false);
                }
            }

            await WriteEndAsync(token, cancellation).ConfigureAwait(false);

            UpdateCurrentState();

            await AwaitRemaining(LevelsToComplete, cancellation).ConfigureAwait(false);
        }

        async Task AwaitIndent(Task task, int LevelsToComplete, JsonToken token, CancellationToken cancellation)
        {
            await task.ConfigureAwait(false);

            //  Finish current loop

            await WriteEndAsync(token, cancellation).ConfigureAwait(false);

            UpdateCurrentState();

            await AwaitRemaining(LevelsToComplete, cancellation).ConfigureAwait(false);
        }

        async Task AwaitEnd(Task task, int LevelsToComplete, CancellationToken cancellation)
        {
            await task.ConfigureAwait(false);

            //  Finish current loop

            UpdateCurrentState();

            await AwaitRemaining(LevelsToComplete, cancellation).ConfigureAwait(false);
        }

        async Task AwaitRemaining(int LevelsToComplete, CancellationToken cancellation)
        {
            while (LevelsToComplete-- > 0)
            {
                var token = GetCloseTokenForType(Pop());

                if (_currentState == State.Property)
                {
                    await WriteNullAsync(cancellation).ConfigureAwait(false);
                }

                if (_formatting == Formatting.Indented)
                {
                    if (_currentState != State.ObjectStart && _currentState != State.ArrayStart)
                    {
                        await WriteIndentAsync(cancellation).ConfigureAwait(false);
                    }
                }

                await WriteEndAsync(token, cancellation).ConfigureAwait(false);

                UpdateCurrentState();
            }
        }
    }

    /// <summary>
    /// Asynchronously writes the end of an array.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteEndArrayAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteEndArray();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the end of a constructor.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteEndConstructorAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteEndConstructor();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the end of a JSON object.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteEndObjectAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteEndObject();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a null value.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteNullAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteNull();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the property name of a name/value pair of a JSON object.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WritePropertyNameAsync(string name, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WritePropertyName(name);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the property name of a name/value pair of a JSON object.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="escape">A flag to indicate whether the text should be escaped when it is written as a JSON property name.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WritePropertyNameAsync(string name, bool escape, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WritePropertyName(name, escape);
        return AsyncUtils.CompletedTask;
    }

    internal Task InternalWritePropertyNameAsync(string name, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        _currentPosition.PropertyName = name;
        return AutoCompleteAsync(JsonToken.PropertyName, cancellation);
    }

    /// <summary>
    /// Asynchronously writes the beginning of a JSON array.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteStartArrayAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteStartArray();
        return AsyncUtils.CompletedTask;
    }

    internal async Task InternalWriteStartAsync(JsonToken token, JsonContainerType container, CancellationToken cancellation)
    {
        UpdateScopeWithFinishedValue();
        await AutoCompleteAsync(token, cancellation).ConfigureAwait(false);
        Push(container);
    }

    /// <summary>
    /// Asynchronously writes a comment <c>/*...*/</c> containing the specified text.
    /// </summary>
    /// <param name="text">Text to place inside the comment.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteCommentAsync(string? text, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteComment(text);
        return AsyncUtils.CompletedTask;
    }

    internal Task InternalWriteCommentAsync(CancellationToken cancellation)
    {
        return AutoCompleteAsync(JsonToken.Comment, cancellation);
    }

    /// <summary>
    /// Asynchronously writes raw JSON where a value is expected and updates the writer's state.
    /// </summary>
    /// <param name="json">The raw JSON to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteRawValueAsync(string? json, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteRawValue(json);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the start of a constructor with the given name.
    /// </summary>
    /// <param name="name">The name of the constructor.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteStartConstructorAsync(string name, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteStartConstructor(name);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the beginning of a JSON object.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteStartObjectAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteStartObject();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the current <see cref="JsonReader"/> token.
    /// </summary>
    /// <param name="reader">The <see cref="JsonReader"/> to read the token from.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public Task WriteTokenAsync(JsonReader reader, CancellationToken cancellation = default)
    {
        return WriteTokenAsync(reader, true, cancellation);
    }

    /// <summary>
    /// Asynchronously writes the current <see cref="JsonReader"/> token.
    /// </summary>
    /// <param name="reader">The <see cref="JsonReader"/> to read the token from.</param>
    /// <param name="writeChildren">A flag indicating whether the current token's children should be written.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public Task WriteTokenAsync(JsonReader reader, bool writeChildren, CancellationToken cancellation = default)
    {
        return WriteTokenAsync(reader, writeChildren, true, true, cancellation);
    }

    /// <summary>
    /// Asynchronously writes the <see cref="JsonToken"/> token and its value.
    /// </summary>
    /// <param name="token">The <see cref="JsonToken"/> to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public Task WriteTokenAsync(JsonToken token, CancellationToken cancellation = default)
    {
        return WriteTokenAsync(token, null, cancellation);
    }

    /// <summary>
    /// Asynchronously writes the <see cref="JsonToken"/> token and its value.
    /// </summary>
    /// <param name="token">The <see cref="JsonToken"/> to write.</param>
    /// <param name="value">
    /// The value to write.
    /// A value is only required for tokens that have an associated value, e.g. the <see cref="String"/> property name for <see cref="JsonToken.PropertyName"/>.
    /// <c>null</c> can be passed to the method for tokens that don't have a value, e.g. <see cref="JsonToken.StartObject"/>.
    /// </param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public Task WriteTokenAsync(JsonToken token, object? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        switch (token)
        {
            case JsonToken.None:
                // read to next
                return AsyncUtils.CompletedTask;
            case JsonToken.StartObject:
                return WriteStartObjectAsync(cancellation);
            case JsonToken.StartArray:
                return WriteStartArrayAsync(cancellation);
            case JsonToken.StartConstructor:
                return WriteStartConstructorAsync(value!.ToString(), cancellation);
            case JsonToken.PropertyName:
                return WritePropertyNameAsync(value!.ToString(), cancellation);
            case JsonToken.Comment:
                return WriteCommentAsync(value?.ToString(), cancellation);
            case JsonToken.Integer:
                return value is BigInteger integer ? WriteValueAsync(integer, cancellation) :
                        WriteValueAsync(Convert.ToInt64(value, CultureInfo.InvariantCulture), cancellation);
            case JsonToken.Float:
                if (value is decimal dec)
                {
                    return WriteValueAsync(dec, cancellation);
                }

                if (value is double doub)
                {
                    return WriteValueAsync(doub, cancellation);
                }

                if (value is float f)
                {
                    return WriteValueAsync(f, cancellation);
                }

                return WriteValueAsync(Convert.ToDouble(value, CultureInfo.InvariantCulture), cancellation);
            case JsonToken.String:
                return WriteValueAsync(value!.ToString(), cancellation);
            case JsonToken.Boolean:
                return WriteValueAsync(Convert.ToBoolean(value, CultureInfo.InvariantCulture), cancellation);
            case JsonToken.Null:
                return WriteNullAsync(cancellation);
            case JsonToken.Undefined:
                return WriteUndefinedAsync(cancellation);
            case JsonToken.EndObject:
                return WriteEndObjectAsync(cancellation);
            case JsonToken.EndArray:
                return WriteEndArrayAsync(cancellation);
            case JsonToken.EndConstructor:
                return WriteEndConstructorAsync(cancellation);
            case JsonToken.Date:
                if (value is DateTimeOffset offset)
                {
                    return WriteValueAsync(offset, cancellation);
                }

                return WriteValueAsync(Convert.ToDateTime(value, CultureInfo.InvariantCulture), cancellation);
            case JsonToken.Raw:
                return WriteRawValueAsync(value?.ToString(), cancellation);
            case JsonToken.Bytes:
                if (value is Guid guid)
                {
                    return WriteValueAsync(guid, cancellation);
                }

                return WriteValueAsync((byte[]?)value, cancellation);
            default:
                throw MiscellaneousUtils.CreateArgumentOutOfRangeException(nameof(token), token, "Unexpected token type.");
        }
    }

    internal virtual async Task WriteTokenAsync(JsonReader reader, bool writeChildren, bool writeDateConstructorAsDate, bool writeComments, CancellationToken cancellation)
    {
        var initialDepth = CalculateWriteTokenInitialDepth(reader);

        do
        {
            // write a JValue date when the constructor is for a date
            if (writeDateConstructorAsDate && reader.TokenType == JsonToken.StartConstructor && string.Equals(reader.Value?.ToString(), "Date", StringComparison.Ordinal))
            {
                await WriteConstructorDateAsync(reader, cancellation).ConfigureAwait(false);
            }
            else
            {
                if (writeComments || reader.TokenType != JsonToken.Comment)
                {
                    await WriteTokenAsync(reader.TokenType, reader.Value, cancellation).ConfigureAwait(false);
                }
            }
        } while (
            // stop if we have reached the end of the token being read
            initialDepth - 1 < reader.Depth - (JsonTokenUtils.IsEndToken(reader.TokenType) ? 1 : 0)
            && writeChildren
            && await reader.ReadAsync(cancellation).ConfigureAwait(false));

        if (IsWriteTokenIncomplete(reader, writeChildren, initialDepth))
        {
            throw JsonWriterException.Create(this, "Unexpected end when reading token.", null);
        }
    }

    // For internal use, when we know the writer does not offer true async support (e.g. when backed
    // by a StringWriter) and therefore async write methods are always in practice just a less efficient
    // path through the sync version.
    internal async Task WriteTokenSyncReadingAsync(JsonReader reader, CancellationToken cancellation)
    {
        var initialDepth = CalculateWriteTokenInitialDepth(reader);

        do
        {
            // write a JValue date when the constructor is for a date
            if (reader.TokenType == JsonToken.StartConstructor && string.Equals(reader.Value?.ToString(), "Date", StringComparison.Ordinal))
            {
                WriteConstructorDate(reader);
            }
            else
            {
                WriteToken(reader.TokenType, reader.Value);
            }
        } while (
            // stop if we have reached the end of the token being read
            initialDepth - 1 < reader.Depth - (JsonTokenUtils.IsEndToken(reader.TokenType) ? 1 : 0)
            && await reader.ReadAsync(cancellation).ConfigureAwait(false));

        if (initialDepth < CalculateWriteTokenFinalDepth(reader))
        {
            throw JsonWriterException.Create(this, "Unexpected end when reading token.", null);
        }
    }

    async Task WriteConstructorDateAsync(JsonReader reader, CancellationToken cancellation)
    {
        if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
        {
            throw JsonWriterException.Create(this, "Unexpected end when reading date constructor.", null);
        }
        if (reader.TokenType != JsonToken.Integer)
        {
            throw JsonWriterException.Create(this, $"Unexpected token when reading date constructor. Expected Integer, got {reader.TokenType}", null);
        }

        var date = DateTimeUtils.ConvertJavaScriptTicksToDateTime((long)reader.Value!);

        if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
        {
            throw JsonWriterException.Create(this, "Unexpected end when reading date constructor.", null);
        }
        if (reader.TokenType != JsonToken.EndConstructor)
        {
            throw JsonWriterException.Create(this, $"Unexpected token when reading date constructor. Expected EndConstructor, got {reader.TokenType}", null);
        }

        await WriteValueAsync(date, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="bool"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="bool"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(bool value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="bool"/> value.
    /// </summary>
    /// <param name="value">The <see cref="bool"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(bool? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="byte"/> value.
    /// </summary>
    /// <param name="value">The <see cref="byte"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(byte value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="byte"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="byte"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(byte? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="byte"/>[] value.
    /// </summary>
    /// <param name="value">The <see cref="byte"/>[] value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(byte[]? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="char"/> value.
    /// </summary>
    /// <param name="value">The <see cref="char"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(char value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="char"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="char"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(char? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="DateTime"/> value.
    /// </summary>
    /// <param name="value">The <see cref="DateTime"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(DateTime value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="DateTime"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="DateTime"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(DateTime? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="DateTimeOffset"/> value.
    /// </summary>
    /// <param name="value">The <see cref="DateTimeOffset"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(DateTimeOffset value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(DateTimeOffset? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="decimal"/> value.
    /// </summary>
    /// <param name="value">The <see cref="decimal"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(decimal value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="decimal"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="decimal"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(decimal? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="double"/> value.
    /// </summary>
    /// <param name="value">The <see cref="double"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(double value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="double"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="double"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(double? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="float"/> value.
    /// </summary>
    /// <param name="value">The <see cref="float"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(float value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="float"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="float"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(float? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Guid"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Guid"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(Guid value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="Guid"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="Guid"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(Guid? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="int"/> value.
    /// </summary>
    /// <param name="value">The <see cref="int"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(int value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="int"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="int"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(int? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="long"/> value.
    /// </summary>
    /// <param name="value">The <see cref="long"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(long value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="long"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="long"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(long? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="object"/> value.
    /// </summary>
    /// <param name="value">The <see cref="object"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(object? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="sbyte"/> value.
    /// </summary>
    /// <param name="value">The <see cref="sbyte"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(sbyte value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="sbyte"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="sbyte"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(sbyte? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="short"/> value.
    /// </summary>
    /// <param name="value">The <see cref="short"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(short value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="short"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="short"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(short? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="string"/> value.
    /// </summary>
    /// <param name="value">The <see cref="string"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(string? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="TimeSpan"/> value.
    /// </summary>
    /// <param name="value">The <see cref="TimeSpan"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(TimeSpan value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="TimeSpan"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="TimeSpan"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(TimeSpan? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="uint"/> value.
    /// </summary>
    /// <param name="value">The <see cref="uint"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(uint value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="uint"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="uint"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(uint? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="ulong"/> value.
    /// </summary>
    /// <param name="value">The <see cref="ulong"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(ulong value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="ulong"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="ulong"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(ulong? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Uri"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Uri"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteValueAsync(Uri? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="ushort"/> value.
    /// </summary>
    /// <param name="value">The <see cref="ushort"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(ushort value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes a <see cref="Nullable{T}"/> of <see cref="ushort"/> value.
    /// </summary>
    /// <param name="value">The <see cref="Nullable{T}"/> of <see cref="ushort"/> value to write.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    [CLSCompliant(false)]
    public virtual Task WriteValueAsync(ushort? value, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteValue(value);
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes an undefined value.
    /// </summary>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteUndefinedAsync(CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteUndefined();
        return AsyncUtils.CompletedTask;
    }

    /// <summary>
    /// Asynchronously writes the given white space.
    /// </summary>
    /// <param name="ws">The string of white space characters.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    public virtual Task WriteWhitespaceAsync(string ws, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        WriteWhitespace(ws);
        return AsyncUtils.CompletedTask;
    }

    internal Task InternalWriteValueAsync(JsonToken token, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        UpdateScopeWithFinishedValue();
        return AutoCompleteAsync(token, cancellation);
    }

    /// <summary>
    /// Asynchronously ets the state of the <see cref="JsonWriter"/>.
    /// </summary>
    /// <param name="token">The <see cref="JsonToken"/> being written.</param>
    /// <param name="value">The value being written.</param>
    /// <param name="cancellation">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>The default behaviour is to execute synchronously, returning an already-completed task. Derived
    /// classes can override this behaviour for true asynchronicity.</remarks>
    protected Task SetWriteStateAsync(JsonToken token, object value, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return cancellation.FromCanceled();
        }

        switch (token)
        {
            case JsonToken.StartObject:
                return InternalWriteStartAsync(token, JsonContainerType.Object, cancellation);
            case JsonToken.StartArray:
                return InternalWriteStartAsync(token, JsonContainerType.Array, cancellation);
            case JsonToken.StartConstructor:
                return InternalWriteStartAsync(token, JsonContainerType.Constructor, cancellation);
            case JsonToken.PropertyName:
                if (value is not string s)
                {
                    throw new ArgumentException("A name is required when setting property name state.", nameof(value));
                }

                return InternalWritePropertyNameAsync(s, cancellation);
            case JsonToken.Comment:
                return InternalWriteCommentAsync(cancellation);
            case JsonToken.Raw:
                return AsyncUtils.CompletedTask;
            case JsonToken.Integer:
            case JsonToken.Float:
            case JsonToken.String:
            case JsonToken.Boolean:
            case JsonToken.Date:
            case JsonToken.Bytes:
            case JsonToken.Null:
            case JsonToken.Undefined:
                return InternalWriteValueAsync(token, cancellation);
            case JsonToken.EndObject:
                return InternalWriteEndAsync(JsonContainerType.Object, cancellation);
            case JsonToken.EndArray:
                return InternalWriteEndAsync(JsonContainerType.Array, cancellation);
            case JsonToken.EndConstructor:
                return InternalWriteEndAsync(JsonContainerType.Constructor, cancellation);
            default:
                throw new ArgumentOutOfRangeException(nameof(token));
        }
    }

    internal static Task WriteValueAsync(JsonWriter writer, PrimitiveTypeCode typeCode, object value, CancellationToken cancellation)
    {
        while (true)
        {
            switch (typeCode)
            {
                case PrimitiveTypeCode.Char:
                    return writer.WriteValueAsync((char)value, cancellation);
                case PrimitiveTypeCode.CharNullable:
                    return writer.WriteValueAsync(value == null ? null : (char)value, cancellation);
                case PrimitiveTypeCode.Boolean:
                    return writer.WriteValueAsync((bool)value, cancellation);
                case PrimitiveTypeCode.BooleanNullable:
                    return writer.WriteValueAsync(value == null ? null : (bool)value, cancellation);
                case PrimitiveTypeCode.SByte:
                    return writer.WriteValueAsync((sbyte)value, cancellation);
                case PrimitiveTypeCode.SByteNullable:
                    return writer.WriteValueAsync(value == null ? null : (sbyte)value, cancellation);
                case PrimitiveTypeCode.Int16:
                    return writer.WriteValueAsync((short)value, cancellation);
                case PrimitiveTypeCode.Int16Nullable:
                    return writer.WriteValueAsync(value == null ? null : (short)value, cancellation);
                case PrimitiveTypeCode.UInt16:
                    return writer.WriteValueAsync((ushort)value, cancellation);
                case PrimitiveTypeCode.UInt16Nullable:
                    return writer.WriteValueAsync(value == null ? null : (ushort)value, cancellation);
                case PrimitiveTypeCode.Int32:
                    return writer.WriteValueAsync((int)value, cancellation);
                case PrimitiveTypeCode.Int32Nullable:
                    return writer.WriteValueAsync(value == null ? null : (int)value, cancellation);
                case PrimitiveTypeCode.Byte:
                    return writer.WriteValueAsync((byte)value, cancellation);
                case PrimitiveTypeCode.ByteNullable:
                    return writer.WriteValueAsync(value == null ? null : (byte)value, cancellation);
                case PrimitiveTypeCode.UInt32:
                    return writer.WriteValueAsync((uint)value, cancellation);
                case PrimitiveTypeCode.UInt32Nullable:
                    return writer.WriteValueAsync(value == null ? null : (uint)value, cancellation);
                case PrimitiveTypeCode.Int64:
                    return writer.WriteValueAsync((long)value, cancellation);
                case PrimitiveTypeCode.Int64Nullable:
                    return writer.WriteValueAsync(value == null ? null : (long)value, cancellation);
                case PrimitiveTypeCode.UInt64:
                    return writer.WriteValueAsync((ulong)value, cancellation);
                case PrimitiveTypeCode.UInt64Nullable:
                    return writer.WriteValueAsync(value == null ? null : (ulong)value, cancellation);
                case PrimitiveTypeCode.Single:
                    return writer.WriteValueAsync((float)value, cancellation);
                case PrimitiveTypeCode.SingleNullable:
                    return writer.WriteValueAsync(value == null ? null : (float)value, cancellation);
                case PrimitiveTypeCode.Double:
                    return writer.WriteValueAsync((double)value, cancellation);
                case PrimitiveTypeCode.DoubleNullable:
                    return writer.WriteValueAsync(value == null ? null : (double)value, cancellation);
                case PrimitiveTypeCode.DateTime:
                    return writer.WriteValueAsync((DateTime)value, cancellation);
                case PrimitiveTypeCode.DateTimeNullable:
                    return writer.WriteValueAsync(value == null ? null : (DateTime)value, cancellation);
                case PrimitiveTypeCode.DateTimeOffset:
                    return writer.WriteValueAsync((DateTimeOffset)value, cancellation);
                case PrimitiveTypeCode.DateTimeOffsetNullable:
                    return writer.WriteValueAsync(value == null ? null : (DateTimeOffset)value, cancellation);
                case PrimitiveTypeCode.Decimal:
                    return writer.WriteValueAsync((decimal)value, cancellation);
                case PrimitiveTypeCode.DecimalNullable:
                    return writer.WriteValueAsync(value == null ? null : (decimal)value, cancellation);
                case PrimitiveTypeCode.Guid:
                    return writer.WriteValueAsync((Guid)value, cancellation);
                case PrimitiveTypeCode.GuidNullable:
                    return writer.WriteValueAsync(value == null ? null : (Guid)value, cancellation);
                case PrimitiveTypeCode.TimeSpan:
                    return writer.WriteValueAsync((TimeSpan)value, cancellation);
                case PrimitiveTypeCode.TimeSpanNullable:
                    return writer.WriteValueAsync(value == null ? null : (TimeSpan)value, cancellation);
                case PrimitiveTypeCode.BigInteger:

                    // this will call to WriteValueAsync(object)
                    return writer.WriteValueAsync((BigInteger)value, cancellation);
                case PrimitiveTypeCode.BigIntegerNullable:

                    // this will call to WriteValueAsync(object)
                    return writer.WriteValueAsync(value == null ? null : (BigInteger)value, cancellation);
                case PrimitiveTypeCode.Uri:
                    return writer.WriteValueAsync((Uri)value, cancellation);
                case PrimitiveTypeCode.String:
                    return writer.WriteValueAsync((string)value, cancellation);
                case PrimitiveTypeCode.Bytes:
                    return writer.WriteValueAsync((byte[])value, cancellation);
                case PrimitiveTypeCode.DBNull:
                    return writer.WriteNullAsync(cancellation);
                default:
                    if (value is IConvertible convertible)
                    {
                        ResolveConvertibleValue(convertible, out typeCode, out value);
                        continue;
                    }

                    // write an unknown null value, fix https://github.com/JamesNK/Newtonsoft.Json/issues/1460
                    if (value == null)
                    {
                        return writer.WriteNullAsync(cancellation);
                    }

                    throw CreateUnsupportedTypeException(writer, value);
            }
        }
    }
}