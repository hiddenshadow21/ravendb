﻿// -----------------------------------------------------------------------
//  <copyright file="Quotas.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using Tests.Infrastructure;
using Voron;
using Xunit.Abstractions;

namespace SlowTests.Voron.Storage
{
    public class RavenDB_7698 : FastTests.Voron.StorageTest
    {
        public RavenDB_7698(ITestOutputHelper output) : base(output)
        {
        }

        protected override void Configure(StorageEnvironmentOptions options)
        {
            options.ManualFlushing = true;
        }

        [MultiplatformFact(RavenArchitecture.AllX64)]
        public void CanRestartEmptyAsyncTransaction()
        {
            RequireFileBasedPager();

            var tx1 = Env.WriteTransaction();

            try
            {
                using (var tx2 = tx1.BeginAsyncCommitAndStartNewTransaction(tx1.LowLevelTransaction.PersistentContext))
                {
                    using (tx1)
                    {
                        tx2.CreateTree("test");

                        tx1.EndAsyncCommit();
                    }

                    tx1 = null;

                    tx2.Commit();
                }
            }
            finally
            {
                tx1?.Dispose();
            }
            
            RestartDatabase();
        }
    }
}
