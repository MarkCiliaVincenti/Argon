﻿// Copyright (c) 2007 James Newton-King. All rights reserved.
// Use of this source code is governed by The MIT License,
// as found in the license.md file.

namespace Argon.Tests;

public class JsonArrayAttributeTests : TestFixtureBase
{
    [Fact]
    public void IsReferenceTest()
    {
        var attribute = new JsonPropertyAttribute();
        Assert.Null(attribute.isReference);
        Assert.False( attribute.IsReference);

        attribute.IsReference = false;
        Assert.False(attribute.isReference);
        Assert.False( attribute.IsReference);

        attribute.IsReference = true;
        Assert.True(attribute.isReference);
        Assert.True(attribute.IsReference);
    }

    [Fact]
    public void NullValueHandlingTest()
    {
        var attribute = new JsonPropertyAttribute();
        Assert.Null(attribute.nullValueHandling);
        Assert.Equal(NullValueHandling.Include, attribute.NullValueHandling);

        attribute.NullValueHandling = NullValueHandling.Ignore;
        Assert.Equal(NullValueHandling.Ignore, attribute.nullValueHandling);
        Assert.Equal(NullValueHandling.Ignore, attribute.NullValueHandling);
    }

    [Fact]
    public void DefaultValueHandlingTest()
    {
        var attribute = new JsonPropertyAttribute();
        Assert.Null(attribute.defaultValueHandling);
        Assert.Equal(DefaultValueHandling.Include, attribute.DefaultValueHandling);

        attribute.DefaultValueHandling = DefaultValueHandling.Ignore;
        Assert.Equal(DefaultValueHandling.Ignore, attribute.defaultValueHandling);
        Assert.Equal(DefaultValueHandling.Ignore, attribute.DefaultValueHandling);
    }
}