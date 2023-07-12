using System.Linq;
using FastTests;
using Raven.Client.Documents.Queries.Explanation;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Smuggler;
using Raven.Server.Config;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues;

public class RavenDB_20444 : RavenTestBase
{
    public RavenDB_20444(ITestOutputHelper output) : base(output)
    {
    }
    
    private record Doc(string StrVal, int? NumVal);
    
    [RavenFact(RavenTestCategory.Querying)]
    public void BoostingIsNotAppliedToCorrectSubclause()
    {
        using var store = GetDocumentStore();
        using var session = store.OpenSession();
        var query = session.Advanced.DocumentQuery<Doc>()
            .OpenSubclause() // boost(
                .Search(x => x.StrVal, "match") // search(StrVa;, $p0)
                .AndAlso() // and
                .OpenSubclause() // (
                    .WhereGreaterThanOrEqual(x => x.NumVal, 0) //NumVal >= $p1
                    .OrElse() // or
                    .WhereEquals(x => x.NumVal, (int?)null) // NumVal = $p2
                .CloseSubclause() // )
            .CloseSubclause() // 
            .Boost(0) // , $p3)
            .OrderByScore()
            .OrderByDescending(x => x.NumVal)
            .IncludeExplanations(out Explanations explanations);

        var rql = query.ToString();
        Assert.Equal("from 'Docs' where boost(search(StrVal, $p0) and (NumVal >= $p1 or NumVal = $p2), $p3) order by score(), NumVal as long desc include explanations()", rql);
    }

    [RavenTheory(RavenTestCategory.Querying)]
    [RavenData(SearchEngineMode = RavenSearchEngineMode.Lucene, Data = new object[] {"true"})]
    [RavenData(SearchEngineMode = RavenSearchEngineMode.Lucene, Data = new object[] { "false" })]
    public void QueryCacheHasNoImpactOnCalculatingScoring(Options options, string queryClauseCacheDisabled)
    {
        using var store = GetDocumentStore(new Options()
        {
            ModifyDatabaseRecord = record =>
            {
                options.ModifyDatabaseRecord(record);
                record.Settings[RavenConfiguration.GetKey(x => x.Indexing.QueryClauseCacheDisabled)] = queryClauseCacheDisabled;
            }
        });

        store.Maintenance.Send(new CreateSampleDataOperation(DatabaseItemType.Documents | DatabaseItemType.Indexes ));

        Indexes.WaitForIndexing(store);

        using (var session = store.OpenSession())
        {
            var queryObject = session.Advanced.DocumentQuery<object>("Product/Search")
                .Search("Name", "De*").Boost(0)
                .AndAlso()
                .OpenSubclause()
                .WhereLessThanOrEqual("PricePerUnit", 20).Boost(100)
                .OrElse()
                .WhereGreaterThan("PricePerUnit", 20).Boost(1)
                .CloseSubclause()
                .OrderByScore()
                .OrderByDescending("PricePerUnit", OrderingType.Double)
                .IncludeExplanations(out var explanations)
                .ToList();

            Assert.Contains("0.499975", explanations.GetExplanations("products/58-A").First());
            Assert.Contains("0.00499975", explanations.GetExplanations("products/38-A").First());
        }
    }

}
