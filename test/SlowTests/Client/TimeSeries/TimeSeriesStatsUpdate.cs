﻿using System;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Operations.TimeSeries;
using Raven.Client.Documents.Session;
using Raven.Tests.Core.Utils.Entities;
using Sparrow;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Client.TimeSeries
{
    public class TimeSeriesStatsUpdate : RavenTestBase
    {
        public TimeSeriesStatsUpdate(ITestOutputHelper output) : base(output)
        {
        }

        [RavenFact(RavenTestCategory.TimeSeries)]
        public void RavenDB_14877()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new User(),"users/1-A");
                    session.SaveChanges();
                }

                var op = new TimeSeriesOperation.AppendOperation
                {
                    Timestamp = DateTime.Now,
                    Tag = "as",
                    Values = new double[] {73}
                };
                var op2 = new TimeSeriesOperation.AppendOperation
                {
                    Timestamp = DateTime.Now,
                    Tag = "as",
                    Values = new double[] { 78 }
                };
                var op3 = new TimeSeriesOperation.AppendOperation
                {
                    Timestamp = new DateTime(2019, 4, 23) + TimeSpan.FromMinutes(5),
                    Tag = "as",
                    Values = new double[] { 798 }
                };

                var a = new TimeSeriesOperation 
                { 
                    Name = "test",
                };
                a.Append(op3);
                a.Append(op2);
                a.Append(op);
                store.Operations.Send(new TimeSeriesBatchOperation("users/1-A", a));

                var opDelete = new TimeSeriesOperation.DeleteOperation
                {
                    From = DateTime.Now - TimeSpan.FromDays(2),
                    To = DateTime.Now + TimeSpan.FromDays(2)
                };

                var ab = new TimeSeriesOperation
                {
                    Name = "test",
                };
                ab.Delete(opDelete);
                store.Operations.Send(new TimeSeriesBatchOperation("users/1-A", ab));

                var abc = new TimeSeriesOperation
                {
                    Name = "test",
                };
                abc.Delete(new TimeSeriesOperation.DeleteOperation {From = DateTime.MinValue, To = DateTime.MaxValue});
                store.Operations.Send(new TimeSeriesBatchOperation("users/1-A", abc));
                var ts = store.Operations.Send(new GetTimeSeriesOperation("users/1-A", "test", DateTime.MinValue, DateTime.MaxValue));

                // GetTimeSeriesOperation should return null on non-existing timeseries 
                Assert.Null(ts);
            }
        }

        [RavenFact(RavenTestCategory.TimeSeries)]
        public void UpdateStatsAfterEndDeletion()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession(new SessionOptions {NoCaching = true}))
                {
                    var user = new User();
                    session.Store(user, "users/1-A");
                    var ts = session.TimeSeriesFor(user, "HR");

                    ts.Append(DateTime.Now, 73);
                    var oldTime = new DateTime(2019, 4, 23) + TimeSpan.FromMinutes(5);
                    ts.Append(oldTime, 1);

                    session.SaveChanges();

                    ts.Delete(DateTime.Now - TimeSpan.FromDays(2), DateTime.Now + TimeSpan.FromDays(2));
                    session.SaveChanges();

                    var after1delete = ts.Get(DateTime.MinValue, DateTime.MaxValue).ToList();
                    Assert.Equal(1, after1delete.Count);
                    Assert.Equal(oldTime, after1delete[0].Timestamp, RavenTestHelper.DateTimeComparer.Instance);

                    ts.Delete(DateTime.MinValue, DateTime.MaxValue);
                    session.SaveChanges();
                }
                using (var session = store.OpenSession(new SessionOptions
                {
                    NoCaching = true
                }))
                {
                    var ts = session.TimeSeriesFor("users/1-A", "HR");
                    var after2delete = ts.Get(DateTime.MinValue, DateTime.MaxValue)?.ToList();
                    Assert.Null(after2delete);
                }
            }
        }

        [RavenFact(RavenTestCategory.TimeSeries)]
        public void UpdateStatsAfterStartDeletion()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession(new SessionOptions
                {
                    NoCaching = true
                }))
                {
                    var user = new User();
                    session.Store(user,"users/1-A");
                    var ts = session.TimeSeriesFor(user, "HR");

                    var now = DateTime.Now;
                    ts.Append(now, 73);
                    var oldTime = new DateTime(2019, 4, 23) + TimeSpan.FromMinutes(5);
                    ts.Append(oldTime, 1);
                    
                    session.SaveChanges();

                    ts.Delete(oldTime);
                    session.SaveChanges();

                    var after1delete = ts.Get(DateTime.MinValue, DateTime.MaxValue).ToList();
                    Assert.Equal(1, after1delete.Count);
                    Assert.Equal(now.EnsureMilliseconds(), after1delete[0].Timestamp, RavenTestHelper.DateTimeComparer.Instance);

                    ts.Delete(DateTime.MinValue, DateTime.MaxValue);
                    session.SaveChanges();
                }

                using (var session = store.OpenSession(new SessionOptions
                {
                    NoCaching = true
                }))
                {
                    var ts = session.TimeSeriesFor("users/1-A", "HR");
                    var after2delete = ts.Get(DateTime.MinValue, DateTime.MaxValue)?.ToList();
                    Assert.Null(after2delete);
                }
            }
        }

        [RavenFact(RavenTestCategory.TimeSeries)]
        public void UpdateStatsAfterEndReplacement()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession(new SessionOptions
                {
                    NoCaching = true
                }))
                {
                    var user = new User();
                    session.Store(user,"users/1-A");
                    var ts = session.TimeSeriesFor(user, "HR");

                    var now = DateTime.Now;
                    ts.Append(now, 73);
                    var oldTime = new DateTime(2019, 4, 23) + TimeSpan.FromMinutes(5);
                    ts.Append(oldTime, 1);
                    
                    session.SaveChanges();

                    ts.Delete(now);
                    now = DateTime.Now;
                    ts.Append(now, 76);
                    session.SaveChanges();

                    var values = ts.Get(DateTime.MinValue, DateTime.MaxValue).ToList();
                    Assert.Equal(2, values.Count);
                    Assert.Equal(now.EnsureMilliseconds(), values[1].Timestamp, RavenTestHelper.DateTimeComparer.Instance);
                    Assert.Equal(76, values[1].Value);
                }
            }
        }

        [RavenFact(RavenTestCategory.TimeSeries)]
        public void UpdateStatsAfterStartReplacement()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession(new SessionOptions
                {
                    NoCaching = true
                }))
                {
                    var user = new User();
                    session.Store(user,"users/1-A");
                    var ts = session.TimeSeriesFor(user, "HR");

                    var now = DateTime.Now;
                    ts.Append(now, 73);
                    var oldTime = new DateTime(2019, 4, 23) + TimeSpan.FromMinutes(5);
                    ts.Append(oldTime, 1);
                    
                    session.SaveChanges();

                    var first = oldTime - TimeSpan.FromMinutes(1);
                    ts.Append(first, 76);
                    session.SaveChanges();

                    var values = ts.Get(DateTime.MinValue, DateTime.MaxValue).ToList();
                    Assert.Equal(3, values.Count);
                    Assert.Equal(first.EnsureMilliseconds(), values[0].Timestamp, RavenTestHelper.DateTimeComparer.Instance);
                }
            }
        }
    }
}
