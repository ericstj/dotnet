﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Query;

public class QueryFilterFuncletizationInMemoryTest(QueryFilterFuncletizationInMemoryTest.QueryFilterFuncletizationInMemoryFixture fixture)
    : QueryFilterFuncletizationTestBase<QueryFilterFuncletizationInMemoryTest.QueryFilterFuncletizationInMemoryFixture>(fixture)
{
    public class QueryFilterFuncletizationInMemoryFixture : QueryFilterFuncletizationFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory
            => InMemoryTestStoreFactory.Instance;
    }
}
