﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Argon.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Argon.Tests.XUnitAssert;
using Argon.Converters;
using Argon.Serialization;
using Argon.Tests.TestObjects;
using Argon.Tests.TestObjects.Organization;
using Argon.Utilities;
using System.Linq;
using System.Threading.Tasks;

namespace Argon.Tests.Serialization
{ 
    public class Staff
    {
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public IList<string> Roles { get; set; }
    }

    public class RoleTrace
    {
        public string Name { get; set; }
    }

    [TestFixture]
    public class TraceWriterTests : TestFixtureBase
    {
        [Fact]
        public void DeserializedJsonWithAlreadyReadReader()
        {
            var json = @"{ 'name': 'Admin' }{ 'name': 'Publisher' }";
            IList<RoleTrace> roles = new List<RoleTrace>();
            var reader = new JsonTextReader(new StringReader(json));
            reader.SupportMultipleContent = true;
            var traceWriter = new InMemoryTraceWriter();
            while (true)
            {
                if (!reader.Read())
                {
                    break;
                }
                var serializer = new JsonSerializer();
                //the next line raise an exception
                serializer.TraceWriter = traceWriter;
                var role = serializer.Deserialize<RoleTrace>(reader);
                roles.Add(role);
            }

            Assert.AreEqual("Admin", roles[0].Name);
            Assert.AreEqual("Publisher", roles[1].Name);

            StringAssert.AreEqual(@"Deserialized JSON: 
{
  ""name"": ""Admin""
}", traceWriter.TraceRecords[2].Message);

            StringAssert.AreEqual(@"Deserialized JSON: 
{
  ""name"": ""Publisher""
}", traceWriter.TraceRecords[5].Message);
        }

        [Fact]
        public async Task DeserializedJsonWithAlreadyReadReader_Async()
        {
            var json = @"{ 'name': 'Admin' }{ 'name': 'Publisher' }";
            IList<RoleTrace> roles = new List<RoleTrace>();
            var reader = new JsonTextReader(new StringReader(json));
            reader.SupportMultipleContent = true;
            var traceWriter = new InMemoryTraceWriter();
            while (true)
            {
                if (!await reader.ReadAsync())
                {
                    break;
                }
                var serializer = new JsonSerializer();
                //the next line raise an exception
                serializer.TraceWriter = traceWriter;
                var role = serializer.Deserialize<RoleTrace>(reader);
                roles.Add(role);
            }

            Assert.AreEqual("Admin", roles[0].Name);
            Assert.AreEqual("Publisher", roles[1].Name);

            StringAssert.AreEqual(@"Deserialized JSON: 
{
  ""name"": ""Admin""
}", traceWriter.TraceRecords[2].Message);

            StringAssert.AreEqual(@"Deserialized JSON: 
{
  ""name"": ""Publisher""
}", traceWriter.TraceRecords[5].Message);
        }

        [Fact]
        public void DiagnosticsTraceWriterTest()
        {
            var sw = new StringWriter();
            var listener = new TextWriterTraceListener(sw);

            try
            {
                Trace.AutoFlush = true;
                Trace.Listeners.Add(listener);

                var traceWriter = new DiagnosticsTraceWriter();
                traceWriter.Trace(TraceLevel.Verbose, "Verbose!", null);
                traceWriter.Trace(TraceLevel.Info, "Info!", null);
                traceWriter.Trace(TraceLevel.Warning, "Warning!", null);
                traceWriter.Trace(TraceLevel.Error, "Error!", null);
                traceWriter.Trace(TraceLevel.Off, "Off!", null);

                StringAssert.AreEqual(@"Argon Verbose: 0 : Verbose!
Argon Information: 0 : Info!
Argon Warning: 0 : Warning!
Argon Error: 0 : Error!
", sw.ToString());
            }
            finally
            {
                Trace.Listeners.Remove(listener);
                Trace.AutoFlush = false;
            }
        }

        [Fact]
        public void WriteNullableByte()
        {
            var sw = new StringWriter();
            var traceJsonWriter = new TraceJsonWriter(new JsonTextWriter(sw));
            traceJsonWriter.WriteStartArray();
            traceJsonWriter.WriteValue((byte?)null);
            traceJsonWriter.WriteEndArray();

            StringAssert.AreEqual(@"Serialized JSON: 
[
  null
]", traceJsonWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public void WriteNullObject()
        {
            var sw = new StringWriter();
            var traceJsonWriter = new TraceJsonWriter(new JsonTextWriter(sw));
            traceJsonWriter.WriteStartArray();
            traceJsonWriter.WriteValue((object)null);
            traceJsonWriter.WriteEndArray();

            StringAssert.AreEqual(@"Serialized JSON: 
[
  null
]", traceJsonWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public void WriteNullString()
        {
            var sw = new StringWriter();
            var traceJsonWriter = new TraceJsonWriter(new JsonTextWriter(sw));
            traceJsonWriter.WriteStartArray();
            traceJsonWriter.WriteValue((string)null);
            traceJsonWriter.WriteEndArray();

            StringAssert.AreEqual(@"Serialized JSON: 
[
  null
]", traceJsonWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public void WriteNullUri()
        {
            var sw = new StringWriter();
            var traceJsonWriter = new TraceJsonWriter(new JsonTextWriter(sw));
            traceJsonWriter.WriteStartArray();
            traceJsonWriter.WriteValue((Uri)null);
            traceJsonWriter.WriteEndArray();

            StringAssert.AreEqual(@"Serialized JSON: 
[
  null
]", traceJsonWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public void WriteNullByteArray()
        {
            var sw = new StringWriter();
            var traceJsonWriter = new TraceJsonWriter(new JsonTextWriter(sw));
            traceJsonWriter.WriteStartArray();
            traceJsonWriter.WriteValue((byte[])null);
            traceJsonWriter.WriteEndArray();

            StringAssert.AreEqual(@"Serialized JSON: 
[
  null
]", traceJsonWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public void WriteJRaw()
        {
            ITraceWriter traceWriter = new MemoryTraceWriter();

            var settings = new JRaw("$('#element')");
            var json = JsonConvert.SerializeObject(settings, new JsonSerializerSettings
            {
                TraceWriter = traceWriter
            });

            Assert.AreEqual("$('#element')", json);

            Assert.IsTrue(traceWriter.ToString().EndsWith("Verbose Serialized JSON: " + Environment.NewLine + "$('#element')", StringComparison.Ordinal));
        }

        [Fact]
        public void WriteJRawInArray()
        {
            ITraceWriter traceWriter = new MemoryTraceWriter();

            var raws = new List<JRaw>
            {
                new JRaw("$('#element')"),
                new JRaw("$('#element')"),
                new JRaw("$('#element')")
            };

            var json = JsonConvert.SerializeObject(raws, new JsonSerializerSettings
            {
                TraceWriter = traceWriter,
                Formatting = Formatting.Indented
            });

            StringAssert.AreEqual(@"[
  $('#element'),
  $('#element'),
  $('#element')
]", json);

            Assert.IsTrue(StringAssert.Normalize(traceWriter.ToString()).EndsWith(StringAssert.Normalize(@"Verbose Serialized JSON: 
[
  $('#element'),
  $('#element'),
  $('#element')
]"), StringComparison.Ordinal));
        }

        [Fact]
        public void MemoryTraceWriterSerializeTest()
        {
            var staff = new Staff();
            staff.Name = "Arnie Admin";
            staff.Roles = new List<string> { "Administrator" };
            staff.StartDate = new DateTime(2000, 12, 12, 12, 12, 12, DateTimeKind.Utc);

            ITraceWriter traceWriter = new MemoryTraceWriter();

            JsonConvert.SerializeObject(
                staff,
                new JsonSerializerSettings { TraceWriter = traceWriter, Converters = { new JavaScriptDateTimeConverter() } });

            // 2012-11-11T12:08:42.761 Info Started serializing Argon.Tests.Serialization.Staff. Path ''.
            // 2012-11-11T12:08:42.785 Info Started serializing System.DateTime with converter Argon.Converters.JavaScriptDateTimeConverter. Path 'StartDate'.
            // 2012-11-11T12:08:42.791 Info Finished serializing System.DateTime with converter Argon.Converters.JavaScriptDateTimeConverter. Path 'StartDate'.
            // 2012-11-11T12:08:42.797 Info Started serializing System.Collections.Generic.List`1[System.String]. Path 'Roles'.
            // 2012-11-11T12:08:42.798 Info Finished serializing System.Collections.Generic.List`1[System.String]. Path 'Roles'.
            // 2012-11-11T12:08:42.799 Info Finished serializing Argon.Tests.Serialization.Staff. Path ''.

            var memoryTraceWriter = (MemoryTraceWriter)traceWriter;
            var output = memoryTraceWriter.ToString();

            Assert.AreEqual(876, output.Length);
            Assert.AreEqual(7, memoryTraceWriter.GetTraceMessages().Count());

            var json = @"Serialized JSON: 
{
  ""Name"": ""Arnie Admin"",
  ""StartDate"": new Date(
    976623132000
  ),
  ""Roles"": [
    ""Administrator""
  ]
}";

            json = StringAssert.Normalize(json);
            output = StringAssert.Normalize(output);

            Assert.IsTrue(output.Contains(json));
        }

        [Fact]
        public void MemoryTraceWriterDeserializeTest()
        {
            var json = @"{
  ""Name"": ""Arnie Admin"",
  ""StartDate"": new Date(
    976623132000
  ),
  ""Roles"": [
    ""Administrator""
  ]
}";

            var staff = new Staff();
            staff.Name = "Arnie Admin";
            staff.Roles = new List<string> { "Administrator" };
            staff.StartDate = new DateTime(2000, 12, 12, 12, 12, 12, DateTimeKind.Utc);

            ITraceWriter traceWriter = new MemoryTraceWriter();

            JsonConvert.DeserializeObject<Staff>(
                json,
                new JsonSerializerSettings
                {
                    TraceWriter = traceWriter,
                    Converters = { new JavaScriptDateTimeConverter() },
                    MetadataPropertyHandling = MetadataPropertyHandling.Default
                });

            // 2012-11-11T12:08:42.761 Info Started serializing Argon.Tests.Serialization.Staff. Path ''.
            // 2012-11-11T12:08:42.785 Info Started serializing System.DateTime with converter Argon.Converters.JavaScriptDateTimeConverter. Path 'StartDate'.
            // 2012-11-11T12:08:42.791 Info Finished serializing System.DateTime with converter Argon.Tests.TestObjects.Converters.JavaScriptDateTimeConverter. Path 'StartDate'.
            // 2012-11-11T12:08:42.797 Info Started serializing System.Collections.Generic.List`1[System.String]. Path 'Roles'.
            // 2012-11-11T12:08:42.798 Info Finished serializing System.Collections.Generic.List`1[System.String]. Path 'Roles'.
            // 2012-11-11T12:08:42.799 Info Finished serializing Argon.Tests.Serialization.Staff. Path ''.
            // 2013-05-19T00:07:24.360 Verbose Deserialized JSON: 
            // {
            //   "Name": "Arnie Admin",
            //   "StartDate": new Date(
            //     976623132000
            //   ),
            //   "Roles": [
            //     "Administrator"
            //   ]
            // }

            var memoryTraceWriter = (MemoryTraceWriter)traceWriter;
            var output = memoryTraceWriter.ToString();

            Assert.AreEqual(1018, output.Length);
            Assert.AreEqual(7, memoryTraceWriter.GetTraceMessages().Count());

            json = StringAssert.Normalize(json);
            output = StringAssert.Normalize(output);

            Assert.IsTrue(output.Contains(json));
        }

        [Fact]
        public void MemoryTraceWriterLimitTest()
        {
            var traceWriter = new MemoryTraceWriter();

            for (var i = 0; i < 1005; i++)
            {
                traceWriter.Trace(TraceLevel.Verbose, (i + 1).ToString(CultureInfo.InvariantCulture), null);
            }

            IList<string> traceMessages = traceWriter.GetTraceMessages().ToList();

            Assert.AreEqual(1000, traceMessages.Count);

            Assert.IsTrue(traceMessages.First().EndsWith(" 6"));
            Assert.IsTrue(traceMessages.Last().EndsWith(" 1005"));
        }

        [Fact]
        public async Task MemoryTraceWriterThreadSafety_Trace()
        {
            var tasks = new List<Task>();

            var traceWriter = new MemoryTraceWriter();

            for (var i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (var j = 0; j < 1005; j++)
                    {
                        traceWriter.Trace(TraceLevel.Verbose, (j + 1).ToString(CultureInfo.InvariantCulture), null);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            IList<string> traceMessages = traceWriter.GetTraceMessages().ToList();

            Assert.AreEqual(1000, traceMessages.Count);
        }

        [Fact]
        public async Task MemoryTraceWriterThreadSafety_ToString()
        {
            var tasks = new List<Task>();

            var traceWriter = new MemoryTraceWriter();

            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < 10005; j++)
                {
                    traceWriter.Trace(TraceLevel.Verbose, (j + 1).ToString(CultureInfo.InvariantCulture), null);
                }
            }));

            string s = null;

            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < 10005; j++)
                {
                    s = traceWriter.ToString();
                }
            }));

            await Task.WhenAll(tasks);

            Assert.IsNotNull(s);
        }

        [Fact]
        public void Serialize()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Info
            };

            var json =
                JsonConvert.SerializeObject(
                    new TraceTestObject
                    {
                        StringArray = new[] { "1", "2" },
                        IntList = new List<int> { 1, 2 },
                        Version = new VersionOld(1, 2, 3, 4),
                        StringDictionary =
                            new Dictionary<string, string>
                            {
                                { "1", "!" },
                                { "Two", "!!" },
                                { "III", "!!!" }
                            },
                        Double = 1.1d
                    },
                    new JsonSerializerSettings
                    {
                        TraceWriter = traceWriter,
                        Formatting = Formatting.Indented
                    });

            Assert.AreEqual("Started serializing Argon.Tests.Serialization.TraceTestObject. Path ''.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Started serializing System.Collections.Generic.List`1[System.Int32]. Path 'IntList'.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual("Finished serializing System.Collections.Generic.List`1[System.Int32]. Path 'IntList'.", traceWriter.TraceRecords[2].Message);
            Assert.AreEqual("Started serializing System.String[]. Path 'StringArray'.", traceWriter.TraceRecords[3].Message);
            Assert.AreEqual("Finished serializing System.String[]. Path 'StringArray'.", traceWriter.TraceRecords[4].Message);
            Assert.AreEqual("Started serializing Argon.Tests.TestObjects.VersionOld. Path 'Version'.", traceWriter.TraceRecords[5].Message);
            Assert.AreEqual("Finished serializing Argon.Tests.TestObjects.VersionOld. Path 'Version'.", traceWriter.TraceRecords[6].Message);
            Assert.AreEqual("Started serializing System.Collections.Generic.Dictionary`2[System.String,System.String]. Path 'StringDictionary'.", traceWriter.TraceRecords[7].Message);
            Assert.AreEqual("Finished serializing System.Collections.Generic.Dictionary`2[System.String,System.String]. Path 'StringDictionary'.", traceWriter.TraceRecords[8].Message);
            Assert.AreEqual("Finished serializing Argon.Tests.Serialization.TraceTestObject. Path ''.", traceWriter.TraceRecords[9].Message);

            Assert.IsFalse(traceWriter.TraceRecords.Any(r => r.Level == TraceLevel.Verbose));
        }

        [Fact]
        public void Deserialize()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Info
            };

            var o2 = JsonConvert.DeserializeObject<TraceTestObject>(
                @"{
  ""IntList"": [
    1,
    2
  ],
  ""StringArray"": [
    ""1"",
    ""2""
  ],
  ""Version"": {
    ""Major"": 1,
    ""Minor"": 2,
    ""Build"": 3,
    ""Revision"": 4,
    ""MajorRevision"": 0,
    ""MinorRevision"": 4
  },
  ""StringDictionary"": {
    ""1"": ""!"",
    ""Two"": ""!!"",
    ""III"": ""!!!""
  },
  ""Double"": 1.1
}",
                new JsonSerializerSettings
                {
                    TraceWriter = traceWriter
                });

            Assert.AreEqual(2, o2.IntList.Count);
            Assert.AreEqual(2, o2.StringArray.Length);
            Assert.AreEqual(1, o2.Version.Major);
            Assert.AreEqual(2, o2.Version.Minor);
            Assert.AreEqual(3, o2.StringDictionary.Count);
            Assert.AreEqual(1.1d, o2.Double);

            Assert.AreEqual("Started deserializing Argon.Tests.Serialization.TraceTestObject. Path 'IntList', line 2, position 12.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Started deserializing System.Collections.Generic.IList`1[System.Int32]. Path 'IntList', line 2, position 14.", traceWriter.TraceRecords[1].Message);
            Assert.IsTrue(traceWriter.TraceRecords[2].Message.StartsWith("Finished deserializing System.Collections.Generic.IList`1[System.Int32]. Path 'IntList'"));
            Assert.AreEqual("Started deserializing System.String[]. Path 'StringArray', line 6, position 18.", traceWriter.TraceRecords[3].Message);
            Assert.IsTrue(traceWriter.TraceRecords[4].Message.StartsWith("Finished deserializing System.String[]. Path 'StringArray'"));
            Assert.AreEqual("Deserializing Argon.Tests.TestObjects.VersionOld using creator with parameters: Major, Minor, Build, Revision. Path 'Version.Major', line 11, position 12.", traceWriter.TraceRecords[5].Message);
            Assert.IsTrue(traceWriter.TraceRecords[6].Message.StartsWith("Started deserializing Argon.Tests.TestObjects.VersionOld. Path 'Version'"));
            Assert.IsTrue(traceWriter.TraceRecords[7].Message.StartsWith("Finished deserializing Argon.Tests.TestObjects.VersionOld. Path 'Version'"));
            Assert.AreEqual("Started deserializing System.Collections.Generic.IDictionary`2[System.String,System.String]. Path 'StringDictionary.1', line 19, position 8.", traceWriter.TraceRecords[8].Message);
            Assert.IsTrue(traceWriter.TraceRecords[9].Message.StartsWith("Finished deserializing System.Collections.Generic.IDictionary`2[System.String,System.String]. Path 'StringDictionary'"));
            Assert.IsTrue(traceWriter.TraceRecords[10].Message.StartsWith("Finished deserializing Argon.Tests.Serialization.TraceTestObject. Path ''"));

            Assert.IsFalse(traceWriter.TraceRecords.Any(r => r.Level == TraceLevel.Verbose));
        }

        [Fact]
        public void Populate()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Info
            };

            var o2 = new TraceTestObject();

            JsonConvert.PopulateObject(@"{
  ""IntList"": [
    1,
    2
  ],
  ""StringArray"": [
    ""1"",
    ""2""
  ],
  ""Version"": {
    ""Major"": 1,
    ""Minor"": 2,
    ""Build"": 3,
    ""Revision"": 4,
    ""MajorRevision"": 0,
    ""MinorRevision"": 4
  },
  ""StringDictionary"": {
    ""1"": ""!"",
    ""Two"": ""!!"",
    ""III"": ""!!!""
  },
  ""Double"": 1.1
}",
                o2,
                new JsonSerializerSettings
                {
                    TraceWriter = traceWriter,
                    MetadataPropertyHandling = MetadataPropertyHandling.Default
                });

            Assert.AreEqual(2, o2.IntList.Count);
            Assert.AreEqual(2, o2.StringArray.Length);
            Assert.AreEqual(1, o2.Version.Major);
            Assert.AreEqual(2, o2.Version.Minor);
            Assert.AreEqual(3, o2.StringDictionary.Count);
            Assert.AreEqual(1.1d, o2.Double);

            Assert.AreEqual("Started deserializing Argon.Tests.Serialization.TraceTestObject. Path 'IntList', line 2, position 12.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Started deserializing System.Collections.Generic.IList`1[System.Int32]. Path 'IntList', line 2, position 14.", traceWriter.TraceRecords[1].Message);
            Assert.IsTrue(traceWriter.TraceRecords[2].Message.StartsWith("Finished deserializing System.Collections.Generic.IList`1[System.Int32]. Path 'IntList'"));
            Assert.AreEqual("Started deserializing System.String[]. Path 'StringArray', line 6, position 18.", traceWriter.TraceRecords[3].Message);
            Assert.IsTrue(traceWriter.TraceRecords[4].Message.StartsWith("Finished deserializing System.String[]. Path 'StringArray'"));
            Assert.AreEqual("Deserializing Argon.Tests.TestObjects.VersionOld using creator with parameters: Major, Minor, Build, Revision. Path 'Version.Major', line 11, position 12.", traceWriter.TraceRecords[5].Message);
            Assert.IsTrue(traceWriter.TraceRecords[6].Message.StartsWith("Started deserializing Argon.Tests.TestObjects.VersionOld. Path 'Version'"));
            Assert.IsTrue(traceWriter.TraceRecords[7].Message.StartsWith("Finished deserializing Argon.Tests.TestObjects.VersionOld. Path 'Version'"));
            Assert.AreEqual("Started deserializing System.Collections.Generic.IDictionary`2[System.String,System.String]. Path 'StringDictionary.1', line 19, position 8.", traceWriter.TraceRecords[8].Message);
            Assert.IsTrue(traceWriter.TraceRecords[9].Message.StartsWith("Finished deserializing System.Collections.Generic.IDictionary`2[System.String,System.String]. Path 'StringDictionary'"));
            Assert.IsTrue(traceWriter.TraceRecords[10].Message.StartsWith("Finished deserializing Argon.Tests.Serialization.TraceTestObject. Path ''"));

            Assert.IsFalse(traceWriter.TraceRecords.Any(r => r.Level == TraceLevel.Verbose));
        }

        [Fact]
        public void ErrorDeserializing()
        {
            var json = @"{""Integer"":""hi""}";

            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Info
            };

            ExceptionAssert.Throws<Exception>(() =>
            {
                JsonConvert.DeserializeObject<IntegerTestClass>(
                    json,
                    new JsonSerializerSettings
                    {
                        TraceWriter = traceWriter
                    });
            }, "Could not convert string to integer: hi. Path 'Integer', line 1, position 15.");

            Assert.AreEqual(2, traceWriter.TraceRecords.Count);

            Assert.AreEqual(TraceLevel.Info, traceWriter.TraceRecords[0].Level);
            Assert.AreEqual("Started deserializing Argon.Tests.Serialization.IntegerTestClass. Path 'Integer', line 1, position 11.", traceWriter.TraceRecords[0].Message);

            Assert.AreEqual(TraceLevel.Error, traceWriter.TraceRecords[1].Level);
            Assert.AreEqual("Error deserializing Argon.Tests.Serialization.IntegerTestClass. Could not convert string to integer: hi. Path 'Integer', line 1, position 15.", traceWriter.TraceRecords[1].Message);
        }

        [Fact]
        public void ErrorDeserializingNested()
        {
            var json = @"{""IntList"":[1, ""two""]}";

            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Info
            };

            ExceptionAssert.Throws<Exception>(() =>
            {
                JsonConvert.DeserializeObject<TraceTestObject>(
                    json,
                    new JsonSerializerSettings
                    {
                        TraceWriter = traceWriter
                    });
            }, "Could not convert string to integer: two. Path 'IntList[1]', line 1, position 20.");

            Assert.AreEqual(3, traceWriter.TraceRecords.Count);

            Assert.AreEqual(TraceLevel.Info, traceWriter.TraceRecords[0].Level);
            Assert.AreEqual("Started deserializing Argon.Tests.Serialization.TraceTestObject. Path 'IntList', line 1, position 11.", traceWriter.TraceRecords[0].Message);

            Assert.AreEqual(TraceLevel.Info, traceWriter.TraceRecords[1].Level);
            Assert.AreEqual("Started deserializing System.Collections.Generic.IList`1[System.Int32]. Path 'IntList', line 1, position 12.", traceWriter.TraceRecords[1].Message);

            Assert.AreEqual(TraceLevel.Error, traceWriter.TraceRecords[2].Level);
            Assert.AreEqual("Error deserializing System.Collections.Generic.IList`1[System.Int32]. Could not convert string to integer: two. Path 'IntList[1]', line 1, position 20.", traceWriter.TraceRecords[2].Message);
        }

        [Fact]
        public void SerializeDictionarysWithPreserveObjectReferences()
        {
            var circularDictionary = new PreserveReferencesHandlingTests.CircularDictionary();
            circularDictionary.Add("other", new PreserveReferencesHandlingTests.CircularDictionary { { "blah", null } });
            circularDictionary.Add("self", circularDictionary);

            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            JsonConvert.SerializeObject(
                circularDictionary,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.All,
                    TraceWriter = traceWriter
                });

            Assert.IsTrue(traceWriter.TraceRecords.Any(r => r.Message == "Writing object reference Id '1' for Argon.Tests.Serialization.PreserveReferencesHandlingTests+CircularDictionary. Path ''."));
            Assert.IsTrue(traceWriter.TraceRecords.Any(r => r.Message == "Writing object reference Id '2' for Argon.Tests.Serialization.PreserveReferencesHandlingTests+CircularDictionary. Path 'other'."));
            Assert.IsTrue(traceWriter.TraceRecords.Any(r => r.Message == "Writing object reference to Id '1' for Argon.Tests.Serialization.PreserveReferencesHandlingTests+CircularDictionary. Path 'self'."));
        }

        [Fact]
        public void DeserializeDictionarysWithPreserveObjectReferences()
        {
            var json = @"{
  ""$id"": ""1"",
  ""other"": {
    ""$id"": ""2"",
    ""blah"": null
  },
  ""self"": {
    ""$ref"": ""1""
  }
}";

            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            JsonConvert.DeserializeObject<PreserveReferencesHandlingTests.CircularDictionary>(json,
                new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.All,
                    MetadataPropertyHandling = MetadataPropertyHandling.Default,
                    TraceWriter = traceWriter
                });

            Assert.IsTrue(traceWriter.TraceRecords.Any(r => r.Message == "Read object reference Id '1' for Argon.Tests.Serialization.PreserveReferencesHandlingTests+CircularDictionary. Path 'other', line 3, position 10."));
            Assert.IsTrue(traceWriter.TraceRecords.Any(r => r.Message == "Read object reference Id '2' for Argon.Tests.Serialization.PreserveReferencesHandlingTests+CircularDictionary. Path 'other.blah', line 5, position 11."));
            Assert.IsTrue(traceWriter.TraceRecords.Any(r => r.Message.StartsWith("Resolved object reference '1' to Argon.Tests.Serialization.PreserveReferencesHandlingTests+CircularDictionary. Path 'self'")));
        }

        [Fact]
        public void WriteTypeNameForObjects()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            IList<object> l = new List<object>
            {
                new Dictionary<string, string> { { "key!", "value!" } },
                new VersionOld(1, 2, 3, 4)
            };

            JsonConvert.SerializeObject(l, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                TraceWriter = traceWriter
            });

            Assert.AreEqual("Started serializing System.Collections.Generic.List`1[System.Object]. Path ''.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Writing type name '" + ReflectionUtils.GetTypeName(typeof(List<object>), 0, DefaultSerializationBinder.Instance) + "' for System.Collections.Generic.List`1[System.Object]. Path ''.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual("Started serializing System.Collections.Generic.Dictionary`2[System.String,System.String]. Path '$values'.", traceWriter.TraceRecords[2].Message);
            Assert.AreEqual("Writing type name '" + ReflectionUtils.GetTypeName(typeof(Dictionary<string, string>), 0, DefaultSerializationBinder.Instance) + "' for System.Collections.Generic.Dictionary`2[System.String,System.String]. Path '$values[0]'.", traceWriter.TraceRecords[3].Message);
            Assert.AreEqual("Finished serializing System.Collections.Generic.Dictionary`2[System.String,System.String]. Path '$values[0]'.", traceWriter.TraceRecords[4].Message);
            Assert.AreEqual("Started serializing Argon.Tests.TestObjects.VersionOld. Path '$values[0]'.", traceWriter.TraceRecords[5].Message);
            Assert.AreEqual("Writing type name '" + ReflectionUtils.GetTypeName(typeof(VersionOld), 0, DefaultSerializationBinder.Instance) + "' for Argon.Tests.TestObjects.VersionOld. Path '$values[1]'.", traceWriter.TraceRecords[6].Message);
            Assert.AreEqual("Finished serializing Argon.Tests.TestObjects.VersionOld. Path '$values[1]'.", traceWriter.TraceRecords[7].Message);
            Assert.AreEqual("Finished serializing System.Collections.Generic.List`1[System.Object]. Path ''.", traceWriter.TraceRecords[8].Message);
        }

        [Fact]
        public void SerializeConverter()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            IList<DateTime> d = new List<DateTime>
            {
                new DateTime(2000, 12, 12, 12, 12, 12, DateTimeKind.Utc)
            };

            var json = JsonConvert.SerializeObject(d, Formatting.Indented, new JsonSerializerSettings
            {
                Converters = { new JavaScriptDateTimeConverter() },
                TraceWriter = traceWriter
            });

            Assert.AreEqual("Started serializing System.Collections.Generic.List`1[System.DateTime]. Path ''.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Started serializing System.DateTime with converter Argon.Converters.JavaScriptDateTimeConverter. Path ''.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual("Finished serializing System.DateTime with converter Argon.Converters.JavaScriptDateTimeConverter. Path '[0]'.", traceWriter.TraceRecords[2].Message);
            Assert.AreEqual("Finished serializing System.Collections.Generic.List`1[System.DateTime]. Path ''.", traceWriter.TraceRecords[3].Message);
        }

        [Fact]
        public void DeserializeConverter()
        {
            var json = @"[new Date(976623132000)]";

            var traceWriter =
                new InMemoryTraceWriter
                {
                    LevelFilter = TraceLevel.Verbose
                };

            JsonConvert.DeserializeObject<List<DateTime>>(
                json,
                new JsonSerializerSettings
                {
                    Converters = { new JavaScriptDateTimeConverter() },
                    TraceWriter = traceWriter
                });

            Assert.AreEqual("Started deserializing System.Collections.Generic.List`1[System.DateTime]. Path '', line 1, position 1.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Started deserializing System.DateTime with converter Argon.Converters.JavaScriptDateTimeConverter. Path '[0]', line 1, position 10.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual("Finished deserializing System.DateTime with converter Argon.Converters.JavaScriptDateTimeConverter. Path '[0]', line 1, position 23.", traceWriter.TraceRecords[2].Message);
            Assert.AreEqual("Finished deserializing System.Collections.Generic.List`1[System.DateTime]. Path '', line 1, position 24.", traceWriter.TraceRecords[3].Message);
        }

        [Fact]
        public void DeserializeTypeName()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            var json = @"{
  ""$type"": ""System.Collections.Generic.List`1[[System.Object, mscorlib]], mscorlib"",
  ""$values"": [
    {
      ""$type"": ""System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib"",
      ""key!"": ""value!""
    },
    {
      ""$type"": ""Argon.Tests.TestObjects.VersionOld, Tests"",
      ""Major"": 1,
      ""Minor"": 2,
      ""Build"": 3,
      ""Revision"": 4,
      ""MajorRevision"": 0,
      ""MinorRevision"": 4
    }
  ]
}";

            JsonConvert.DeserializeObject(json, null, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                MetadataPropertyHandling = MetadataPropertyHandling.Default,
                TraceWriter = traceWriter
            });

            Assert.AreEqual("Resolved type 'System.Collections.Generic.List`1[[System.Object, mscorlib]], mscorlib' to System.Collections.Generic.List`1[System.Object]. Path '$type', line 2, position 83.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Started deserializing System.Collections.Generic.List`1[System.Object]. Path '$values', line 3, position 14.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual("Resolved type 'System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib' to System.Collections.Generic.Dictionary`2[System.String,System.String]. Path '$values[0].$type', line 5, position 119.", traceWriter.TraceRecords[2].Message);
            Assert.AreEqual("Started deserializing System.Collections.Generic.Dictionary`2[System.String,System.String]. Path '$values[0].key!', line 6, position 13.", traceWriter.TraceRecords[3].Message);
            Assert.IsTrue(traceWriter.TraceRecords[4].Message.StartsWith("Finished deserializing System.Collections.Generic.Dictionary`2[System.String,System.String]. Path '$values[0]'"));
            Assert.AreEqual("Resolved type 'Argon.Tests.TestObjects.VersionOld, Tests' to Argon.Tests.TestObjects.VersionOld. Path '$values[1].$type', line 9, position 58.", traceWriter.TraceRecords[5].Message);
            Assert.AreEqual("Deserializing Argon.Tests.TestObjects.VersionOld using creator with parameters: Major, Minor, Build, Revision. Path '$values[1].Major', line 10, position 14.", traceWriter.TraceRecords[6].Message);
            Assert.IsTrue(traceWriter.TraceRecords[7].Message.StartsWith("Started deserializing Argon.Tests.TestObjects.VersionOld. Path '$values[1]'"));
            Assert.IsTrue(traceWriter.TraceRecords[8].Message.StartsWith("Finished deserializing Argon.Tests.TestObjects.VersionOld. Path '$values[1]'"));
            Assert.IsTrue(traceWriter.TraceRecords[9].Message.StartsWith("Finished deserializing System.Collections.Generic.List`1[System.Object]. Path '$values'"));
        }

        [Fact]
        public void DeserializeISerializable()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            ExceptionAssert.Throws<SerializationException>(() =>
            {
                JsonConvert.DeserializeObject<Exception>(
                    "{}",
                    new JsonSerializerSettings
                    {
                        TraceWriter = traceWriter
                    });
            },
            "Member 'ClassName' was not found.",
            "Member 'Message' was not found.");

            Assert.IsTrue(traceWriter.TraceRecords[0].Message.StartsWith("Deserializing System.Exception using ISerializable constructor. Path ''"));
            Assert.AreEqual(TraceLevel.Info, traceWriter.TraceRecords[0].Level);

#if !NETSTANDARD2_0
            Assert.AreEqual("Error deserializing System.Exception. Member 'ClassName' was not found. Path '', line 1, position 2.", traceWriter.TraceRecords[1].Message);
#else
            Assert.AreEqual("Error deserializing System.Exception. Member 'Message' was not found. Path '', line 1, position 2.", traceWriter.TraceRecords[1].Message);
#endif

            Assert.AreEqual(TraceLevel.Error, traceWriter.TraceRecords[1].Level);
        }

        [Fact]
        public void DeserializeMissingMember()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            JsonConvert.DeserializeObject<Person>(
                "{'MissingMemberProperty':'!!'}",
                new JsonSerializerSettings
                {
                    TraceWriter = traceWriter
                });

            Assert.AreEqual("Started deserializing Argon.Tests.TestObjects.Organization.Person. Path 'MissingMemberProperty', line 1, position 25.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Could not find member 'MissingMemberProperty' on Argon.Tests.TestObjects.Organization.Person. Path 'MissingMemberProperty', line 1, position 25.", traceWriter.TraceRecords[1].Message);
            Assert.IsTrue(traceWriter.TraceRecords[2].Message.StartsWith("Finished deserializing Argon.Tests.TestObjects.Organization.Person. Path ''"));
        }

        [Fact]
        public void DeserializeMissingMemberConstructor()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            var json = @"{
  ""Major"": 1,
  ""Minor"": 2,
  ""Build"": 3,
  ""Revision"": 4,
  ""MajorRevision"": 0,
  ""MinorRevision"": 4,
  ""MissingMemberProperty"": null
}";

            JsonConvert.DeserializeObject<VersionOld>(json, new JsonSerializerSettings
            {
                TraceWriter = traceWriter
            });

            Assert.AreEqual("Deserializing Argon.Tests.TestObjects.VersionOld using creator with parameters: Major, Minor, Build, Revision. Path 'Major', line 2, position 10.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("Could not find member 'MissingMemberProperty' on Argon.Tests.TestObjects.VersionOld. Path 'MissingMemberProperty', line 8, position 31.", traceWriter.TraceRecords[1].Message);
            Assert.IsTrue(traceWriter.TraceRecords[2].Message.StartsWith("Started deserializing Argon.Tests.TestObjects.VersionOld. Path ''"));
            Assert.IsTrue(traceWriter.TraceRecords[3].Message.StartsWith("Finished deserializing Argon.Tests.TestObjects.VersionOld. Path ''"));
        }

        [Fact]
        public void PublicParameterizedConstructorWithPropertyNameConflictWithAttribute()
        {
            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            var json = @"{name:""1""}";

            var c = JsonConvert.DeserializeObject<PublicParameterizedConstructorWithPropertyNameConflictWithAttribute>(json, new JsonSerializerSettings
            {
                TraceWriter = traceWriter
            });

            Assert.IsNotNull(c);
            Assert.AreEqual(1, c.Name);

            Assert.AreEqual("Deserializing Argon.Tests.TestObjects.PublicParameterizedConstructorWithPropertyNameConflictWithAttribute using creator with parameters: name. Path 'name', line 1, position 6.", traceWriter.TraceRecords[0].Message);
        }

        [Fact]
        public void ShouldSerializeTestClass()
        {
            var c = new ShouldSerializeTestClass();
            c.Age = 29;
            c.Name = "Jim";
            c._shouldSerializeName = true;

            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            JsonConvert.SerializeObject(c, new JsonSerializerSettings { TraceWriter = traceWriter });

            Assert.AreEqual("ShouldSerialize result for property 'Name' on Argon.Tests.Serialization.ShouldSerializeTestClass: True. Path ''.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual(TraceLevel.Verbose, traceWriter.TraceRecords[1].Level);

            traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            c._shouldSerializeName = false;

            JsonConvert.SerializeObject(c, new JsonSerializerSettings { TraceWriter = traceWriter });

            Assert.AreEqual("ShouldSerialize result for property 'Name' on Argon.Tests.Serialization.ShouldSerializeTestClass: False. Path ''.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual(TraceLevel.Verbose, traceWriter.TraceRecords[1].Level);
        }

        [Fact]
        public void SpecifiedTest()
        {
            var c = new SpecifiedTestClass();
            c.Name = "James";
            c.Age = 27;
            c.NameSpecified = false;

            var traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            var json = JsonConvert.SerializeObject(c, Formatting.Indented, new JsonSerializerSettings { TraceWriter = traceWriter });

            Assert.AreEqual("Started serializing Argon.Tests.Serialization.SpecifiedTestClass. Path ''.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("IsSpecified result for property 'Name' on Argon.Tests.Serialization.SpecifiedTestClass: False. Path ''.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual("IsSpecified result for property 'Weight' on Argon.Tests.Serialization.SpecifiedTestClass: False. Path 'Age'.", traceWriter.TraceRecords[2].Message);
            Assert.AreEqual("IsSpecified result for property 'Height' on Argon.Tests.Serialization.SpecifiedTestClass: False. Path 'Age'.", traceWriter.TraceRecords[3].Message);
            Assert.AreEqual("IsSpecified result for property 'FavoriteNumber' on Argon.Tests.Serialization.SpecifiedTestClass: False. Path 'Age'.", traceWriter.TraceRecords[4].Message);
            Assert.AreEqual("Finished serializing Argon.Tests.Serialization.SpecifiedTestClass. Path ''.", traceWriter.TraceRecords[5].Message);

            StringAssert.AreEqual(@"{
  ""Age"": 27
}", json);

            traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            var deserialized = JsonConvert.DeserializeObject<SpecifiedTestClass>(json, new JsonSerializerSettings { TraceWriter = traceWriter });

            Assert.AreEqual("Started deserializing Argon.Tests.Serialization.SpecifiedTestClass. Path 'Age', line 2, position 8.", traceWriter.TraceRecords[0].Message);
            Assert.IsTrue(traceWriter.TraceRecords[1].Message.StartsWith("Finished deserializing Argon.Tests.Serialization.SpecifiedTestClass. Path ''"));

            Assert.IsNull(deserialized.Name);
            Assert.IsFalse(deserialized.NameSpecified);
            Assert.IsFalse(deserialized.WeightSpecified);
            Assert.IsFalse(deserialized.HeightSpecified);
            Assert.IsFalse(deserialized.FavoriteNumberSpecified);
            Assert.AreEqual(27, deserialized.Age);

            c.NameSpecified = true;
            c.WeightSpecified = true;
            c.HeightSpecified = true;
            c.FavoriteNumber = 23;
            json = JsonConvert.SerializeObject(c, Formatting.Indented);

            StringAssert.AreEqual(@"{
  ""Name"": ""James"",
  ""Age"": 27,
  ""Weight"": 0,
  ""Height"": 0,
  ""FavoriteNumber"": 23
}", json);

            traceWriter = new InMemoryTraceWriter
            {
                LevelFilter = TraceLevel.Verbose
            };

            deserialized = JsonConvert.DeserializeObject<SpecifiedTestClass>(json, new JsonSerializerSettings { TraceWriter = traceWriter });

            Assert.AreEqual("Started deserializing Argon.Tests.Serialization.SpecifiedTestClass. Path 'Name', line 2, position 9.", traceWriter.TraceRecords[0].Message);
            Assert.AreEqual("IsSpecified for property 'Name' on Argon.Tests.Serialization.SpecifiedTestClass set to true. Path 'Name', line 2, position 17.", traceWriter.TraceRecords[1].Message);
            Assert.AreEqual("IsSpecified for property 'Weight' on Argon.Tests.Serialization.SpecifiedTestClass set to true. Path 'Weight', line 4, position 13.", traceWriter.TraceRecords[2].Message);
            Assert.AreEqual("IsSpecified for property 'Height' on Argon.Tests.Serialization.SpecifiedTestClass set to true. Path 'Height', line 5, position 13.", traceWriter.TraceRecords[3].Message);
            Assert.IsTrue(traceWriter.TraceRecords[4].Message.StartsWith("Finished deserializing Argon.Tests.Serialization.SpecifiedTestClass. Path ''"));

            Assert.AreEqual("James", deserialized.Name);
            Assert.IsTrue(deserialized.NameSpecified);
            Assert.IsTrue(deserialized.WeightSpecified);
            Assert.IsTrue(deserialized.HeightSpecified);
            Assert.IsTrue(deserialized.FavoriteNumberSpecified);
            Assert.AreEqual(27, deserialized.Age);
            Assert.AreEqual(23, deserialized.FavoriteNumber);
        }

        [Fact]
        public void TraceJsonWriterTest_WriteObjectInObject()
        {
            var sw = new StringWriter(CultureInfo.InvariantCulture);
            var w = new JsonTextWriter(sw);
            var traceWriter = new TraceJsonWriter(w);

            traceWriter.WriteStartObject();
            traceWriter.WritePropertyName("Prop1");
            traceWriter.WriteValue((object)1);
            traceWriter.WriteEndObject();
            traceWriter.Flush();
            traceWriter.Close();

            var json = @"{
  ""Prop1"": 1
}";

            StringAssert.AreEqual("Serialized JSON: " + Environment.NewLine + json, traceWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public async Task TraceJsonWriterTest_WriteObjectInObjectAsync()
        {
            var sw = new StringWriter(CultureInfo.InvariantCulture);
            var w = new JsonTextWriter(sw);
            var traceWriter = new TraceJsonWriter(w);

            await traceWriter.WriteStartObjectAsync();
            await traceWriter.WritePropertyNameAsync("Prop1");
            await traceWriter.WriteValueAsync((object)1);
            await traceWriter.WriteEndObjectAsync();
            await traceWriter.FlushAsync();
            traceWriter.Close();

            var json = @"{
  ""Prop1"": 1
}";

            StringAssert.AreEqual("Serialized JSON: " + Environment.NewLine + json, traceWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public void TraceJsonWriterTest()
        {
            var sw = new StringWriter(CultureInfo.InvariantCulture);
            var w = new JsonTextWriter(sw);
            var traceWriter = new TraceJsonWriter(w);

            traceWriter.WriteStartObject();
            traceWriter.WritePropertyName("Array");
            traceWriter.WriteStartArray();
            traceWriter.WriteValue("String!");
            traceWriter.WriteValue(new DateTime(2000, 12, 12, 12, 12, 12, DateTimeKind.Utc));
            traceWriter.WriteValue(new DateTimeOffset(2000, 12, 12, 12, 12, 12, TimeSpan.FromHours(2)));
            traceWriter.WriteValue(1.1f);
            traceWriter.WriteValue(1.1d);
            traceWriter.WriteValue(1.1m);
            traceWriter.WriteValue(1);
            traceWriter.WriteValue((char)'!');
            traceWriter.WriteValue((short)1);
            traceWriter.WriteValue((ushort)1);
            traceWriter.WriteValue((int)1);
            traceWriter.WriteValue((uint)1);
            traceWriter.WriteValue((sbyte)1);
            traceWriter.WriteValue((byte)1);
            traceWriter.WriteValue((long)1);
            traceWriter.WriteValue((ulong)1);
            traceWriter.WriteValue((bool)true);

            traceWriter.WriteValue((DateTime?)new DateTime(2000, 12, 12, 12, 12, 12, DateTimeKind.Utc));
            traceWriter.WriteValue((DateTimeOffset?)new DateTimeOffset(2000, 12, 12, 12, 12, 12, TimeSpan.FromHours(2)));
            traceWriter.WriteValue((float?)1.1f);
            traceWriter.WriteValue((double?)1.1d);
            traceWriter.WriteValue((decimal?)1.1m);
            traceWriter.WriteValue((int?)1);
            traceWriter.WriteValue((char?)'!');
            traceWriter.WriteValue((short?)1);
            traceWriter.WriteValue((ushort?)1);
            traceWriter.WriteValue((int?)1);
            traceWriter.WriteValue((uint?)1);
            traceWriter.WriteValue((sbyte?)1);
            traceWriter.WriteValue((byte?)1);
            traceWriter.WriteValue((long?)1);
            traceWriter.WriteValue((ulong?)1);
            traceWriter.WriteValue((bool?)true);
            traceWriter.WriteValue(BigInteger.Parse("9999999990000000000000000000000000000000000"));

            traceWriter.WriteValue((object)true);
            traceWriter.WriteValue(TimeSpan.FromMinutes(1));
            traceWriter.WriteValue(Guid.Empty);
            traceWriter.WriteValue(new Uri("http://www.google.com/"));
            traceWriter.WriteValue(Encoding.UTF8.GetBytes("String!"));
            traceWriter.WriteRawValue("[1],");
            traceWriter.WriteRaw("[2]");
            traceWriter.WriteNull();
            traceWriter.WriteUndefined();
            traceWriter.WriteStartConstructor("ctor");
            traceWriter.WriteValue(1);
            traceWriter.WriteEndConstructor();
            traceWriter.WriteComment("A comment");
            traceWriter.WriteWhitespace("       ");
            traceWriter.WriteEnd();
            traceWriter.WriteEndObject();
            traceWriter.Flush();
            traceWriter.Close();

            var json = @"{
  ""Array"": [
    ""String!"",
    ""2000-12-12T12:12:12Z"",
    ""2000-12-12T12:12:12+02:00"",
    1.1,
    1.1,
    1.1,
    1,
    ""!"",
    1,
    1,
    1,
    1,
    1,
    1,
    1,
    1,
    true,
    ""2000-12-12T12:12:12Z"",
    ""2000-12-12T12:12:12+02:00"",
    1.1,
    1.1,
    1.1,
    1,
    ""!"",
    1,
    1,
    1,
    1,
    1,
    1,
    1,
    1,
    true,
    9999999990000000000000000000000000000000000,
    true,
    ""00:01:00"",
    ""00000000-0000-0000-0000-000000000000"",
    ""http://www.google.com/"",
    ""U3RyaW5nIQ=="",
    [1],[2],
    null,
    undefined,
    new ctor(
      1
    )
    /*A comment*/       
  ]
}";

            StringAssert.AreEqual("Serialized JSON: " + Environment.NewLine + json, traceWriter.GetSerializedJsonMessage());
        }

        [Fact]
        public void TraceJsonReaderTest()
        {
            var json = @"{
  ""Array"": [
    ""String!"",
    ""2000-12-12T12:12:12Z"",
    ""2000-12-12T12:12:12Z"",
    ""2000-12-12T12:12:12+00:00"",
    ""U3RyaW5nIQ=="",
    1,
    1.1,
    1.2,
    9999999990000000000000000000000000000000000,
    null,
    undefined,
    new ctor(
      1
    )
    /*A comment*/
  ]
}";

            var sw = new StringReader(json);
            var w = new JsonTextReader(sw);
            var traceReader = new TraceJsonReader(w);

            traceReader.Read();
            Assert.AreEqual(JsonToken.StartObject, traceReader.TokenType);

            traceReader.Read();
            Assert.AreEqual(JsonToken.PropertyName, traceReader.TokenType);
            Assert.AreEqual("Array", traceReader.Value);

            traceReader.Read();
            Assert.AreEqual(JsonToken.StartArray, traceReader.TokenType);
            Assert.AreEqual(null, traceReader.Value);

            traceReader.ReadAsString();
            Assert.AreEqual(JsonToken.String, traceReader.TokenType);
            Assert.AreEqual('"', traceReader.QuoteChar);
            Assert.AreEqual("String!", traceReader.Value);

            // for great code coverage justice!
            traceReader.QuoteChar = '\'';
            Assert.AreEqual('\'', traceReader.QuoteChar);

            traceReader.ReadAsString();
            Assert.AreEqual(JsonToken.String, traceReader.TokenType);
            Assert.AreEqual("2000-12-12T12:12:12Z", traceReader.Value);

            traceReader.ReadAsDateTime();
            Assert.AreEqual(JsonToken.Date, traceReader.TokenType);
            Assert.AreEqual(new DateTime(2000, 12, 12, 12, 12, 12, DateTimeKind.Utc), traceReader.Value);

            traceReader.ReadAsDateTimeOffset();
            Assert.AreEqual(JsonToken.Date, traceReader.TokenType);
            Assert.AreEqual(new DateTimeOffset(2000, 12, 12, 12, 12, 12, TimeSpan.Zero), traceReader.Value);

            traceReader.ReadAsBytes();
            Assert.AreEqual(JsonToken.Bytes, traceReader.TokenType);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("String!"), (byte[])traceReader.Value);

            traceReader.ReadAsInt32();
            Assert.AreEqual(JsonToken.Integer, traceReader.TokenType);
            Assert.AreEqual(1, traceReader.Value);

            traceReader.ReadAsDecimal();
            Assert.AreEqual(JsonToken.Float, traceReader.TokenType);
            Assert.AreEqual(1.1m, traceReader.Value);

            traceReader.ReadAsDouble();
            Assert.AreEqual(JsonToken.Float, traceReader.TokenType);
            Assert.AreEqual(1.2d, traceReader.Value);

            traceReader.Read();
            Assert.AreEqual(JsonToken.Integer, traceReader.TokenType);
            Assert.AreEqual(typeof(BigInteger), traceReader.ValueType);
            Assert.AreEqual(BigInteger.Parse("9999999990000000000000000000000000000000000"), traceReader.Value);

            traceReader.Read();
            Assert.AreEqual(JsonToken.Null, traceReader.TokenType);

            traceReader.Read();
            Assert.AreEqual(JsonToken.Undefined, traceReader.TokenType);

            traceReader.Read();
            Assert.AreEqual(JsonToken.StartConstructor, traceReader.TokenType);

            traceReader.Read();
            Assert.AreEqual(JsonToken.Integer, traceReader.TokenType);
            Assert.AreEqual(1L, traceReader.Value);

            traceReader.Read();
            Assert.AreEqual(JsonToken.EndConstructor, traceReader.TokenType);

            traceReader.Read();
            Assert.AreEqual(JsonToken.Comment, traceReader.TokenType);
            Assert.AreEqual("A comment", traceReader.Value);

            traceReader.Read();
            Assert.AreEqual(JsonToken.EndArray, traceReader.TokenType);

            traceReader.Read();
            Assert.AreEqual(JsonToken.EndObject, traceReader.TokenType);

            Assert.IsFalse(traceReader.Read());

            traceReader.Close();

            StringAssert.AreEqual("Deserialized JSON: " + Environment.NewLine + json, traceReader.GetDeserializedJsonMessage());
        }
    }

    public class TraceRecord
    {
        public string Message { get; set; }
        public TraceLevel Level { get; set; }
        public Exception Exception { get; set; }

        public override string ToString()
        {
            return Level + " - " + Message;
        }
    }

    public class InMemoryTraceWriter : ITraceWriter
    {
        public TraceLevel LevelFilter { get; set; }
        public IList<TraceRecord> TraceRecords { get; set; }

        public InMemoryTraceWriter()
        {
            LevelFilter = TraceLevel.Verbose;
            TraceRecords = new List<TraceRecord>();
        }

        public void Trace(TraceLevel level, string message, Exception ex)
        {
            TraceRecords.Add(
                new TraceRecord
                {
                    Level = level,
                    Message = message,
                    Exception = ex
                });
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var traceRecord in TraceRecords)
            {
                sb.AppendLine(traceRecord.Message);
            }

            return sb.ToString();
        }
    }

    public class TraceTestObject
    {
        public IList<int> IntList { get; set; }
        public string[] StringArray { get; set; }
        public VersionOld Version { get; set; }
        public IDictionary<string, string> StringDictionary { get; set; }
        public double Double { get; set; }
    }

    public class IntegerTestClass
    {
        public int Integer { get; set; }
    }
}