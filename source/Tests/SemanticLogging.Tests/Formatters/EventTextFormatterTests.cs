﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Formatters
{
    [TestClass]
    public class EventTextFormatterTests
    {
        private static readonly MyCompanyEventSource Logger = MyCompanyEventSource.Log;

        [TestMethod]
        public void WritesEventData()
        {
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator) { VerbosityThreshold = EventLevel.Critical };
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);

                Logger.Failure("Failure message");
                formatter.VerbosityThreshold = EventLevel.Informational;
                Logger.DBQueryStart("select * from table");
                Logger.DBQueryStop();
                Logger.LogColor(MyColor.Red);

                var entries = Regex.Split(listener.ToString(), formatter.Header + "\r\n").Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

                StringAssert.Contains(entries[0], "EventId : 1");
                StringAssert.Contains(entries[0], "Level : Error");
                StringAssert.Contains(entries[0], "Payload : [message : Failure message]");
                StringAssert.Contains(entries[1],
@"EventId : 5
Keywords : 2
Level : Informational
Message : 
Opcode : Start
Task : 2
Version : 0
Payload : [sqlQuery : select * from table]");

                StringAssert.Contains(entries[2],
@"EventId : 6
Keywords : 2
Level : Informational
Message : 
Opcode : Stop
Task : 2
Version : 0
Payload :");
                StringAssert.Contains(entries[3],
@"EventId : 8
Keywords : None
Level : Informational
Message : 
Opcode : Info
Task : 65526
Version : 0
Payload : [color : 0]");
            }
        }

        [TestMethod]
        public void IncludesTaskName()
        {
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, verbosityThreshold: EventLevel.LogAlways);
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);

                Logger.DBQueryStart("select * from table");
                Logger.DBQueryStop();

                var entries = Regex.Split(listener.ToString(), formatter.Header + "\r\n").Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                StringAssert.Matches(entries[0], new Regex("EventName : .*DBQuery"));
                StringAssert.Matches(entries[1], new Regex("Task : .*" + MyCompanyEventSource.Tasks.DBQuery.ToString()));
            }
        }

        [TestMethod]
        public void WritesCustomHeader()
        {
            var formatter = new EventTextFormatter("*** header ***");
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);

                Logger.DBQueryStart("select * from table");

                Assert.IsTrue(listener.ToString().Contains(formatter.Header));
            }
        }

        [TestMethod]
        public void WritesCustomFooter()
        {
            var formatter = new EventTextFormatter(null, "___footer___");
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);

                Logger.Startup();

                Assert.IsTrue(listener.ToString().Contains(formatter.Footer));
            }
        }

        [TestMethod]
        public void WritesCustomHeaderAndFooter()
        {
            var formatter = new EventTextFormatter("---header---", "___footer___");
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);

                Logger.Startup();

                var contents = listener.ToString();
                Assert.IsTrue(contents.Contains(formatter.Header));
                Assert.IsTrue(contents.Contains(formatter.Footer));
            }
        }

        [TestMethod]
        public void WritesDetailedOnEventLevel()
        {
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator) { VerbosityThreshold = EventLevel.Critical };
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);

                Logger.Failure("Summary");
                formatter.VerbosityThreshold = EventLevel.Error;
                Logger.Failure("Detailed");

                var entries = Regex.Split(listener.ToString(), formatter.Header + "\r\n").Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

                StringAssert.Contains(entries[0], "EventId : 1");
                StringAssert.Contains(entries[0], "Level : Error");
                StringAssert.Contains(entries[0], "Payload : [message : Summary]");

                StringAssert.Contains(entries[1],
@"ProviderId : 659518be-d338-564b-2759-c63c10ef82e2
EventId : 1
Keywords : 4
Level : Error
Message : Application Failure: Detailed
Opcode : Info
Task : 65533
Version : 0
Payload : [message : Detailed] 
EventName : FailureInfo");
            }
        }

        [TestMethod]
        public void ShouldWriteTimestampWithDefaultDateTimeFormat()
        {
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);
                Logger.Failure("error");

                var logged = listener.ToString();
                var lookup = "Timestamp : ";
                var lookupIndex = logged.IndexOf(lookup);
                var dateLength = logged.IndexOf("\r\n", lookupIndex) - lookupIndex - lookup.Length;
                var ts = logged.Substring(lookupIndex + lookup.Length, dateLength);
                DateTime dt;
                Assert.IsTrue(DateTime.TryParseExact(ts, formatter.DateTimeFormat ?? EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            }
        }

        [TestMethod]
        public void ShouldWriteTimestampWithCustomDateTimeFormat()
        {
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator) { DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffZ" };
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);
                Logger.Failure("error");

                var logged = listener.ToString();
                var lookup = "Timestamp : ";
                var lookupIndex = logged.IndexOf(lookup);
                var dateLength = logged.IndexOf("\r\n", lookupIndex) - lookupIndex - lookup.Length;
                var ts = logged.Substring(lookupIndex + lookup.Length, dateLength);
                DateTime dt;
                Assert.IsTrue(DateTime.TryParseExact(ts, formatter.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            }
        }

        [TestMethod]
        public void ShouldWriteTimestampWithDefaultDateTimeFormatWhenNull()
        {
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator) { DateTimeFormat = null };
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(Logger, EventLevel.LogAlways, Keywords.All);
                Logger.Failure("error");

                var logged = listener.ToString();
                var lookup = "Timestamp : ";
                var lookupIndex = logged.IndexOf(lookup);
                var dateLength = logged.IndexOf("\r\n", lookupIndex) - lookupIndex - lookup.Length;
                var ts = logged.Substring(lookupIndex + lookup.Length, dateLength);
                DateTime dt;
                Assert.IsTrue(DateTime.TryParseExact(ts, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            }
        }

        [TestMethod]
        public void WritesProcessIdAndThreadId()
        {
            var formatter = new EventTextFormatter();
            using (var writer = new StringWriter())
            {
                var processId = 200;
                var threadId = 300;

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        processId,
                        threadId,
                        Guid.Empty,
                        Guid.Empty,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var processIdMatch = Regex.Match(message, "\\bProcessId : (?<id>\\d+)\\w?").Groups["id"].Value;
                var threadIdMatch = Regex.Match(message, "\\bThreadId : (?<id>\\d+)\\w?").Groups["id"].Value;

                Assert.AreEqual(processId.ToString(), processIdMatch);
                Assert.AreEqual(threadId.ToString(), threadIdMatch);
            }
        }

        [TestMethod]
        public void WritesProcessIdAndThreadIdOnSummary()
        {
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.Critical);
            using (var writer = new StringWriter())
            {
                var processId = 200;
                var threadId = 300;

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        processId,
                        threadId,
                        Guid.Empty,
                        Guid.Empty,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var processIdMatch = Regex.Match(message, "\\bProcessId : (?<id>\\d+)\\w?").Groups["id"].Value;
                var threadIdMatch = Regex.Match(message, "\\bThreadId : (?<id>\\d+)\\w?").Groups["id"].Value;

                Assert.AreEqual(processId.ToString(), processIdMatch);
                Assert.AreEqual(threadId.ToString(), threadIdMatch);
            }
        }

        [TestMethod]
        public void WritesNonEmptyActivityIds()
        {
            var formatter = new EventTextFormatter();
            using (var writer = new StringWriter())
            {
                var activityId = Guid.NewGuid();
                var relatedActivityId = Guid.NewGuid();

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        200,
                        300,
                        activityId,
                        relatedActivityId,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(activityId.ToString(), activityIdMatch);
                Assert.AreEqual(relatedActivityId.ToString(), relatedActivityIdMatch);
            }
        }

        [TestMethod]
        public void WritesNonEmptyActivityId()
        {
            var formatter = new EventTextFormatter();
            using (var writer = new StringWriter())
            {
                var activityId = Guid.NewGuid();

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        activityId,
                        Guid.Empty,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(activityId.ToString(), activityIdMatch);
                Assert.AreEqual(string.Empty, relatedActivityIdMatch);
            }
        }

        [TestMethod]
        public void WritesNonEmptyRelatedActivityId()
        {
            var formatter = new EventTextFormatter();
            using (var writer = new StringWriter())
            {
                var relatedActivityId = Guid.NewGuid();

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        Guid.Empty,
                        relatedActivityId,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(string.Empty, activityIdMatch);
                Assert.AreEqual(relatedActivityId.ToString(), relatedActivityIdMatch);
            }
        }

        [TestMethod]
        public void SkipsEmptyActivityIds()
        {
            var formatter = new EventTextFormatter();
            using (var writer = new StringWriter())
            {
                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        Guid.Empty,
                        Guid.Empty,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(string.Empty, relatedActivityIdMatch);
                Assert.AreEqual(string.Empty, relatedActivityIdMatch);
            }
        }

        [TestMethod]
        public void WritesNonEmptyActivityIdsOnSummary()
        {
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.Critical);
            using (var writer = new StringWriter())
            {
                var activityId = Guid.NewGuid();
                var relatedActivityId = Guid.NewGuid();

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        activityId,
                        relatedActivityId,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(activityId.ToString(), activityIdMatch);
                Assert.AreEqual(relatedActivityId.ToString(), relatedActivityIdMatch);
            }
        }

        [TestMethod]
        public void WritesNonEmptyActivityIdOnSummary()
        {
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.Critical);
            using (var writer = new StringWriter())
            {
                var activityId = Guid.NewGuid();

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        activityId,
                        Guid.Empty,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(activityId.ToString(), activityIdMatch);
                Assert.AreEqual(string.Empty, relatedActivityIdMatch);
            }
        }

        [TestMethod]
        public void WritesNonEmptyRelatedActivityIdOnSummary()
        {
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.Critical);
            using (var writer = new StringWriter())
            {
                var relatedActivityId = Guid.NewGuid();

                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        Guid.Empty,
                        relatedActivityId,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(string.Empty, activityIdMatch);
                Assert.AreEqual(relatedActivityId.ToString(), relatedActivityIdMatch);
            }
        }

        [TestMethod]
        public void SkipsEmptyActivityIdsOnSummary()
        {
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.Critical);
            using (var writer = new StringWriter())
            {
                formatter.WriteEvent(
                    new EventEntry(
                        Guid.NewGuid(),
                        0,
                        string.Empty,
                        new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]),
                        DateTimeOffset.MaxValue,
                        Guid.Empty,
                        Guid.Empty,
                        new EventSourceSchemaReader().GetSchema(Logger).Values.First()),
                    writer);

                var message = writer.ToString();

                var activityIdMatch = Regex.Match(message, "\\bActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;
                var relatedActivityIdMatch = Regex.Match(message, "\\bRelatedActivityId : (?<id>[-A-Fa-f0-9]+)\\w?").Groups["id"].Value;

                Assert.AreEqual(string.Empty, relatedActivityIdMatch);
                Assert.AreEqual(string.Empty, relatedActivityIdMatch);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WritingToANullWriterThrows()
        {
            var formatter = new EventTextFormatter();
            formatter.WriteEvent(new EventEntry(Guid.NewGuid(), 0, string.Empty, new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]), DateTimeOffset.MaxValue, new EventSourceSchemaReader().GetSchema(Logger).Values.First()), null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WritingNullEntryThrows()
        {
            var formatter = new EventTextFormatter();
            using (var writer = new StringWriter())
            {
                formatter.WriteEvent(null, writer);
            }
        }
    }
}
