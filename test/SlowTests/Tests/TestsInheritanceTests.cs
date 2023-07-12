﻿// -----------------------------------------------------------------------
//  <copyright file="NoNonDisposableTests.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastTests;
using Raven.Server.Documents;
using Raven.Server.Web;
using Raven.TestDriver;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Tests
{
    public class TestsInheritanceTests : NoDisposalNeeded
    {
        public TestsInheritanceTests(ITestOutputHelper output) : base(output)
        {
        }

        private readonly HashSet<Assembly> _assemblies = new HashSet<Assembly>();

        // In linux we might encounter Microsoft's VisualStudio assembly types, so we skip this test in linux, and rely on the windows tests result as good for linux too
        [NonLinuxFact]
        public void NonDisposableTestShouldNotExist()
        {
            var types = from assembly in GetAssemblies(typeof(TestsInheritanceTests).Assembly)
                        from test in GetAssemblyTypes(assembly)
                        where test.GetMethods().Any(x => x.GetCustomAttributes(typeof(FactAttribute), true).Count() != 0 || x.GetCustomAttributes(typeof(TheoryAttribute), true).Count() != 0)
                        where typeof(IDisposable).IsAssignableFrom(test) == false
                        select test;

            var array = types.ToArray();
            if (array.Length == 0)
                return;

            var userMessage = string.Join(Environment.NewLine, array.Select(x => x.FullName));
            throw new Exception(userMessage);
        }

        [NonLinuxFact]
        public void TestsShouldInheritFromRightBaseClasses()
        {
            var types = from assembly in GetAssemblies(typeof(TestsInheritanceTests).Assembly)
                from test in GetAssemblyTypes(assembly)
                where test.GetMethods().Any(x => x.GetCustomAttributes(typeof(FactAttribute), true).Count() != 0 || x.GetCustomAttributes(typeof(TheoryAttribute), true).Count() != 0)
                where test.IsSubclassOf(typeof(ParallelTestBase)) == false && test.IsSubclassOf(typeof(RavenTestDriver)) == false && test.Namespace.StartsWith("EmbeddedTests") == false
                select test;

            var array = types.ToArray();
            if (array.Length == 0)
                return;

            var userMessage = string.Join(Environment.NewLine, array.Select(x => x.FullName));
            throw new Exception(userMessage);
        }

        [NonLinuxFact]
        public void HandlersShouldNotInheritStraightFromRequestHandler()
        {
            var types = from assembly in GetAssemblies(typeof(TestsInheritanceTests).Assembly)
                from handler in GetAssemblyTypes(assembly)
                where handler != typeof(DatabaseRequestHandler) && handler != typeof(ServerRequestHandler)
                where handler.IsSubclassOf(typeof(RequestHandler)) && handler.IsSubclassOf(typeof(ServerRequestHandler)) == false && handler.IsSubclassOf(typeof(DatabaseRequestHandler)) == false
                select handler;

            var array = types.ToArray();
            if (array.Length == 0)
                return;

            var userMessage = string.Join(Environment.NewLine, array.Select(x => x.FullName));
            throw new Exception(userMessage);
        }

        private IEnumerable<Assembly> GetAssemblies(Assembly assemblyToScan)
        {
            if (_assemblies.Add(assemblyToScan) == false)
                yield break;

            yield return assemblyToScan;

            foreach (var asm in assemblyToScan.GetReferencedAssemblies())
            {

                Assembly load;
                try
                {
                    load = Assembly.Load(asm);
                }
                catch
                {
                    continue;
                }
                foreach (var assembly in GetAssemblies(load))
                    yield return assembly;
            }
        }

        private static Type[] GetAssemblyTypes(Assembly assemblyToScan)
        {
            try
            {
                return assemblyToScan.GetTypes();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
