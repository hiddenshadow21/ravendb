﻿using System;
using Raven.Tests.Core.Utils.Entities;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Server.Documents.ETL.Raven
{
    public class MultipleCollectionsRavenEtlTests : EtlTestBase
    {
        public MultipleCollectionsRavenEtlTests(ITestOutputHelper output) : base(output)
        {
        }

        //RavenDB-20417
        [RavenTheory(RavenTestCategory.Etl)]
        [InlineData(RavenDatabaseMode.Single, RavenDatabaseMode.Single)]
        [InlineData(RavenDatabaseMode.Single, RavenDatabaseMode.Sharded)]
        [InlineData(RavenDatabaseMode.Sharded, RavenDatabaseMode.Single)]
        [InlineData(RavenDatabaseMode.Sharded, RavenDatabaseMode.Sharded)]
        public void Docs_from_two_collections_loaded_to_single_one(RavenDatabaseMode srcDbMode, RavenDatabaseMode dstDbMode)
        {
            using (var src = GetDocumentStore(Options.ForMode(srcDbMode)))
            using (var dest = GetDocumentStore(Options.ForMode(dstDbMode)))
            {
                AddEtl(src, dest, new [] { "Users", "People" }, script: @"loadToUsers({Name: this.Name});");

                var etlDone = WaitForEtlToComplete(src, (n, s) => s.LoadSuccesses > 0, numOfBatches: 2);

                using (var session = src.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "Joe Doe"
                    },"users/1");

                    session.Store(new Person
                    {
                        Name = "James Smith"
                    },"people/1");

                    session.SaveChanges();
                }

                etlDone.Wait(TimeSpan.FromMinutes(1));

                //WaitForUserToContinueTheTest(dest);

                using (var session = dest.OpenSession())
                {
                    var user = session.Load<User>("users/1");

                    Assert.NotNull(user);
                    Assert.Equal("Joe Doe", user.Name);

                    var docs = session.Advanced.LoadStartingWith<User>("people/1/users/");
                    var userFromPerson = docs[0];

                    Assert.NotNull(userFromPerson);
                    Assert.Equal("James Smith", userFromPerson.Name);
                }

                // update
                etlDone.Reset();

                using (var session = src.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "Doe Joe"
                    }, "users/1");

                    session.Store(new Person
                    {
                        Name = "Smith James"
                    }, "people/1");

                    session.SaveChanges();
                }

                etlDone.Wait(TimeSpan.FromMinutes(1));

                WaitForUserToContinueTheTest(dest);

                using (var session = dest.OpenSession())
                {
                    /*var user = session.Load<User>("users/1");

                    Assert.NotNull(user);
                    Assert.Equal("Doe Joe", user.Name);*/

                    var loadedDocs = session.Advanced.LoadStartingWith<User>("people/1/users/");
                    var userFromPerson = loadedDocs[0];

                    Assert.NotNull(userFromPerson);
                    //Assert.Equal("Smith James", userFromPerson.Name);
                }

                /*var ids = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    using (var session = dest.OpenSession(ShardHelper.ToShardName(dest.Database, i)))
                    {
                        var loadedDocs = session.Advanced.LoadStartingWith<User>("people/1/users/");
                        if (loadedDocs == null)
                            continue;
                        /*var userFromPerson = loadedDocs[0];

                        Assert.NotNull(userFromPerson);#1#

                        foreach (var doc in loadedDocs)
                        {
                            ids.Add(doc.Id);
                        }
                    }
                }

                // delete
                etlDone.Reset();

                using (var session = dest.OpenSession())
                {
                    // this document must not be deleted by ETL

                    session.Store(new Person
                    {
                        Name = "James Smith"
                    }, "people/1");

                    session.SaveChanges();
                }

                using (var session = src.OpenSession())
                {
                    session.Delete("users/1");
                    session.Delete("people/1");

                    session.SaveChanges();
                }

                etlDone.Wait(TimeSpan.FromMinutes(1));

                using (var session = dest.OpenSession())
                {
                    var user = session.Load<User>("users/1");
                    Assert.Null(user);

                    var userFromPerson = session.Advanced.LoadStartingWith<User>("people/1/users/");
                    Assert.Equal(0, userFromPerson.Length);

                    var person = session.Load<Person>("people/1");
                    Assert.NotNull(person);
                }*/
            }
        }
    }
}
