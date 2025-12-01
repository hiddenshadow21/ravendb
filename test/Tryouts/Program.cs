#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Raven.Server.Utils;
using Xunit;
using FastTests;
using Orders;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Operations.AI;
using Raven.Client.Documents.Subscriptions;
using Raven.Client.Exceptions.Documents.Subscriptions;
using Raven.Client.Http;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using SlowTests.Server.Documents.AI;

namespace Tryouts;

public static class Program
{
    static Program()
    {
        XunitLogging.RedirectStreams = false;
    }
    
    private const string SubscriptionName = "OrdersProcessingSubscription";

    public static async Task Main(string[] args)
    {
        using var store = new DocumentStore
        {
            Urls = new[] { "https://a.certs-test-bartek.ravendb.run" },
            Certificate = new X509Certificate2(@"C:\Users\bartosz.piekarski\Downloads\certs-test-bartek.Cluster.Settings 2025-11-21 10-22\admin.client.certificate.certs-test-bartek.pfx"),
            Conventions =
            {
                HttpVersion = HttpVersion.Version11,
                HttpVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            },
            Database = "test"
        };
        store.Initialize();

        DefaultRavenHttpClientFactory.UseCredentials = true;
        await TestHttp(store);

        await TestSub(store);
    }

    private static async Task TestSub(DocumentStore store)
    {
        SubscriptionState subState;
        try
        {
            subState = await store.Subscriptions.GetSubscriptionStateAsync(SubscriptionName);
        }
        catch (Exception e)
        {
            subState = null;
            Console.WriteLine(e);
        }
        if (subState == null)
            await CreateSub(store);

        var options = new SubscriptionWorkerOptions(SubscriptionName)
        {
            Strategy = SubscriptionOpeningStrategy.WaitForFree,
            MaxDocsPerBatch = 20
        };

        using (var worker = store.Subscriptions.GetSubscriptionWorker<Order>(options))
        {
            Console.WriteLine($"Worker '{SubscriptionName}' started. Listening for 'Order' documents...");

            try
            {
                await worker.Run(async batch =>
                {
                    Console.WriteLine($"Received batch of {batch.Items.Count} items.");

                    foreach (var item in batch.Items)
                    {
                        Order order = item.Result;

                        Console.WriteLine($"Processing Order: {order.Id}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker failed: {ex.Message}");
            }
        }
    }

    private static async Task CreateSub(IDocumentStore store)
    {
        try
        {
            var subscriptionCreationOptions = new SubscriptionCreationOptions
            {
                Name = SubscriptionName,
                Query = "from Orders"
            };

            await store.Subscriptions.CreateAsync(subscriptionCreationOptions);
            Console.WriteLine($"Subscription '{SubscriptionName}' created.");
        }
        catch (SubscriptionCreationException)
        {
            Console.WriteLine($"Subscription '{SubscriptionName}' already exists. Resuming...");
        }
    }

    private static async Task TestHttp(DocumentStore store)
    {
        const string databaseName = "test-db";
        try
        {
            var result = await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseName)));
            Console.WriteLine("Database create sent");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
        const string id = "orders/1";
        using (var session = store.OpenAsyncSession())
        {
            var doc = new Order { Company = "test" };
            await session.StoreAsync(doc, id);
            await session.SaveChangesAsync();
        }
        
        using (var session = store.OpenAsyncSession())
        {
            var doc = await session.LoadAsync<Order>(id);
            Console.WriteLine(doc.Company);
        }

        try
        {
            var result = await store.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters
            {
                DatabaseNames = new[] {
                    databaseName
                },
                HardDelete = true,
            }));
            Console.WriteLine("Database delete sent");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static (RavenTestBase.Options Options, GenAiConfiguration Configuration) GetGenAiConfig(RavenAiIntegration type, RavenDatabaseMode databaseMode = RavenDatabaseMode.Single)
    {
        var att = new RavenGenAiDataAttribute();
        var connector = att.GetAiConnectionStringsSingleton(type).First();
        var config = connector.GetAiConfiguration();
        var options = RavenTestBase.Options.ForMode(databaseMode);
        return (options, config);
    }

    private static (RavenTestBase.Options Options, EmbeddingsGenerationConfiguration Configuration) GetEmbeddingsConfig(RavenAiIntegration type, RavenDatabaseMode databaseMode = RavenDatabaseMode.Single)
    {
        var att = new RavenAiEmbeddingsDataAttribute();
        var connector = att.GetAiConnectionStringsSingleton(type).First();
        var config = connector.GetAiConfiguration();
        var options = RavenTestBase.Options.ForMode(databaseMode);
        return (options, config);
    }

    private static void TryRemoveDatabasesFolder()
    {
        var p = System.AppDomain.CurrentDomain.BaseDirectory;
        var dbPath = Path.Combine(p, "Databases");
        if (Directory.Exists(dbPath))
        {
            try
            {
                Directory.Delete(dbPath, true);
                Assert.False(Directory.Exists(dbPath), "Directory.Exists(dbPath)");
            }
            catch
            {
                Console.WriteLine($"Could not remove Databases folder on path '{dbPath}'");
            }
        }
    }
}
