﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Query.Relationships.Projection;

public abstract class NavigationReferenceProjectionRelationalTestBase<TFixture>(TFixture fixture)
    : NavigationReferenceProjectionTestBase<TFixture>(fixture)
        where TFixture : NavigationRelationshipsRelationalFixtureBase, new()
{
}
