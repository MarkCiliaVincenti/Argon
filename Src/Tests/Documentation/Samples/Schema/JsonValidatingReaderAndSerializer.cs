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

#pragma warning disable 618
using Argon.Schema;
using System;
using System.Collections.Generic;
using System.IO;

namespace Argon.Tests.Documentation.Samples.Schema
{
    public class JsonValidatingReaderAndSerializer
    {
        #region Types
        public class Person
        {
            public string Name { get; set; }
            public IList<string> Hobbies { get; set; }
        }
        #endregion

        public void Example()
        {
            #region Usage
            var schemaJson = @"{
              'description': 'A person',
              'type': 'object',
              'properties': {
                'name': {'type':'string'},
                'hobbies': {
                  'type': 'array',
                  'items': {'type':'string'}
                }
              }
            }";

            var json = @"{
              'name': 'James',
              'hobbies': ['.NET', 'Blogging', 'Reading', 'Xbox', 'LOLCATS']
            }";

            var reader = new JsonTextReader(new StringReader(json));

            var validatingReader = new JsonValidatingReader(reader);
            validatingReader.Schema = JsonSchema.Parse(schemaJson);

            IList<string> messages = new List<string>();
            validatingReader.ValidationEventHandler += (o, a) => messages.Add(a.Message);

            var serializer = new JsonSerializer();
            var p = serializer.Deserialize<Person>(validatingReader);

            Console.WriteLine(p.Name);
            // James

            var isValid = (messages.Count == 0);

            Console.WriteLine(isValid);
            // true
            #endregion
        }
    }
}

#pragma warning restore 618