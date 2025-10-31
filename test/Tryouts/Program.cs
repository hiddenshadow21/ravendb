#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Raven.Server.Utils;
using Xunit;
using FastTests;
using Raven.Client.Documents.Operations.AI;
using SlowTests.Issues;
using SlowTests.Server.Documents.AI;

namespace Tryouts;

public static class Program
{
    static Program()
    {
        XunitLogging.RedirectStreams = false;
    }

    public static async Task Main(string[] args)
    {
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine($"Starting run {i}");
            try
            {
                using (var testOutputHelper = new ConsoleTestOutputHelper())
                using (var test = new RavenDB_19148(testOutputHelper))
                using (var test2 = new RavenDB_21535(testOutputHelper))
                {
                    var tasks = new List<Task>();
                
                    tasks.Add(RunTestAsTask(test2));
                    
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(GenerateCerts());
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());tasks.Add(RunTestAsTask(test2));
                    tasks.Add(RunTestAsTask(test2));
                    tasks.Add(RunTestAsTask(test2));
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(GenerateCerts());
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(GenerateCerts());
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(RunTestAsTask(test2));
                    tasks.Add(RunTestAsTask(test2));
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(test.CanAuthUsingWellKnownIssuer());
                    tasks.Add(RunTestAsTask(test2));
                    tasks.Add(RunTestAsTask(test2));
                
                    Task.WaitAll(tasks.ToArray());
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }

    private static Task RunTestAsTask(RavenDB_21535 test2)
    {
        return Task.Run(() => test2.KnownIssuerCert_CanAccess_WithValidSAN(publicDomain: "a.localhost", san: "*.localhost"));
    }

    public static Task GenerateCerts()
    {
        var task = Task.Run(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                var sb = new StringBuilder();
                sb.Append("### Random generate - ");
                var ca = CertificateUtils.CreateCertificateAuthorityCertificate("auth", out var caKey, out var caName);
                CertificateUtils.CreateSelfSignedCertificateBasedOnPrivateKey("admin", caName, caKey, true, false,
                    DateTime.UtcNow.Date.AddMonths(3), out var certBytes);

                var c = new X509Certificate2(certBytes);
                sb.Append($"CA: {ca.GetDisplayName()} ({ca.Thumbprint})");
                sb.Append($"Client: {c.GetDisplayName()} ({c.Thumbprint})");
                Console.WriteLine(sb.ToString());
            }
        });

        return task;
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
