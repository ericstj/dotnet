﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.BulkUpdates;

#nullable disable

public class TPHFiltersInheritanceBulkUpdatesSqlServerTest(
    TPHFiltersInheritanceBulkUpdatesSqlServerFixture fixture,
    ITestOutputHelper testOutputHelper)
    : FiltersInheritanceBulkUpdatesRelationalTestBase<
        TPHFiltersInheritanceBulkUpdatesSqlServerFixture>(fixture, testOutputHelper)
{
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => TestHelpers.AssertAllMethodsOverridden(GetType());

    public override async Task Delete_where_hierarchy(bool async)
    {
        await base.Delete_where_hierarchy(async);

        AssertSql(
            """
DELETE FROM [a]
FROM [Animals] AS [a]
WHERE [a].[CountryId] = 1 AND [a].[Name] = N'Great spotted kiwi'
""");
    }

    public override async Task Delete_where_hierarchy_derived(bool async)
    {
        await base.Delete_where_hierarchy_derived(async);

        AssertSql(
            """
DELETE FROM [a]
FROM [Animals] AS [a]
WHERE [a].[Discriminator] = N'Kiwi' AND [a].[CountryId] = 1 AND [a].[Name] = N'Great spotted kiwi'
""");
    }

    public override async Task Delete_where_using_hierarchy(bool async)
    {
        await base.Delete_where_using_hierarchy(async);

        AssertSql(
            """
DELETE FROM [c]
FROM [Countries] AS [c]
WHERE (
    SELECT COUNT(*)
    FROM [Animals] AS [a]
    WHERE [a].[CountryId] = 1 AND [c].[Id] = [a].[CountryId] AND [a].[CountryId] > 0) > 0
""");
    }

    public override async Task Delete_where_using_hierarchy_derived(bool async)
    {
        await base.Delete_where_using_hierarchy_derived(async);

        AssertSql(
            """
DELETE FROM [c]
FROM [Countries] AS [c]
WHERE (
    SELECT COUNT(*)
    FROM [Animals] AS [a]
    WHERE [a].[CountryId] = 1 AND [c].[Id] = [a].[CountryId] AND [a].[Discriminator] = N'Kiwi' AND [a].[CountryId] > 0) > 0
""");
    }

    public override async Task Delete_GroupBy_Where_Select_First(bool async)
    {
        await base.Delete_GroupBy_Where_Select_First(async);

        AssertSql();
    }

    public override async Task Delete_GroupBy_Where_Select_First_2(bool async)
    {
        await base.Delete_GroupBy_Where_Select_First_2(async);

        AssertSql();
    }

    public override async Task Delete_GroupBy_Where_Select_First_3(bool async)
    {
        await base.Delete_GroupBy_Where_Select_First_3(async);

        AssertSql(
            """
DELETE FROM [a]
FROM [Animals] AS [a]
WHERE [a].[CountryId] = 1 AND [a].[Id] IN (
    SELECT (
        SELECT TOP(1) [a1].[Id]
        FROM [Animals] AS [a1]
        WHERE [a1].[CountryId] = 1 AND [a0].[CountryId] = [a1].[CountryId])
    FROM [Animals] AS [a0]
    WHERE [a0].[CountryId] = 1
    GROUP BY [a0].[CountryId]
    HAVING COUNT(*) < 3
)
""");
    }

    public override async Task Delete_where_keyless_entity_mapped_to_sql_query(bool async)
    {
        await base.Delete_where_keyless_entity_mapped_to_sql_query(async);

        AssertSql();
    }

    public override async Task Delete_where_hierarchy_subquery(bool async)
    {
        await base.Delete_where_hierarchy_subquery(async);

        AssertSql(
            """
@p='0'
@p0='3'

DELETE FROM [a]
FROM [Animals] AS [a]
WHERE [a].[Id] IN (
    SELECT [a0].[Id]
    FROM [Animals] AS [a0]
    WHERE [a0].[CountryId] = 1 AND [a0].[Name] = N'Great spotted kiwi'
    ORDER BY [a0].[Name]
    OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY
)
""");
    }

    public override async Task Update_base_type(bool async)
    {
        await base.Update_base_type(async);

        AssertExecuteUpdateSql(
            """
@p='Animal' (Size = 4000)

UPDATE [a]
SET [a].[Name] = @p
FROM [Animals] AS [a]
WHERE [a].[CountryId] = 1 AND [a].[Name] = N'Great spotted kiwi'
""");
    }

    public override async Task Update_base_type_with_OfType(bool async)
    {
        await base.Update_base_type_with_OfType(async);

        AssertExecuteUpdateSql(
            """
@p='NewBird' (Size = 4000)

UPDATE [a]
SET [a].[Name] = @p
FROM [Animals] AS [a]
WHERE [a].[CountryId] = 1 AND [a].[Discriminator] = N'Kiwi'
""");
    }

    public override async Task Update_where_hierarchy_subquery(bool async)
    {
        await base.Update_where_hierarchy_subquery(async);

        AssertExecuteUpdateSql();
    }

    public override async Task Update_base_property_on_derived_type(bool async)
    {
        await base.Update_base_property_on_derived_type(async);

        AssertExecuteUpdateSql(
            """
@p='SomeOtherKiwi' (Size = 4000)

UPDATE [a]
SET [a].[Name] = @p
FROM [Animals] AS [a]
WHERE [a].[Discriminator] = N'Kiwi' AND [a].[CountryId] = 1
""");
    }

    public override async Task Update_derived_property_on_derived_type(bool async)
    {
        await base.Update_derived_property_on_derived_type(async);

        AssertExecuteUpdateSql(
            """
@p='0' (Size = 1)

UPDATE [a]
SET [a].[FoundOn] = @p
FROM [Animals] AS [a]
WHERE [a].[Discriminator] = N'Kiwi' AND [a].[CountryId] = 1
""");
    }

    public override async Task Update_base_and_derived_types(bool async)
    {
        await base.Update_base_and_derived_types(async);

        AssertExecuteUpdateSql(
            """
@p='Kiwi' (Size = 4000)
@p0='0' (Size = 1)

UPDATE [a]
SET [a].[Name] = @p,
    [a].[FoundOn] = @p0
FROM [Animals] AS [a]
WHERE [a].[Discriminator] = N'Kiwi' AND [a].[CountryId] = 1
""");
    }

    public override async Task Update_where_using_hierarchy(bool async)
    {
        await base.Update_where_using_hierarchy(async);

        AssertExecuteUpdateSql(
            """
@p='Monovia' (Size = 4000)

UPDATE [c]
SET [c].[Name] = @p
FROM [Countries] AS [c]
WHERE (
    SELECT COUNT(*)
    FROM [Animals] AS [a]
    WHERE [a].[CountryId] = 1 AND [c].[Id] = [a].[CountryId] AND [a].[CountryId] > 0) > 0
""");
    }

    public override async Task Update_where_using_hierarchy_derived(bool async)
    {
        await base.Update_where_using_hierarchy_derived(async);

        AssertExecuteUpdateSql(
            """
@p='Monovia' (Size = 4000)

UPDATE [c]
SET [c].[Name] = @p
FROM [Countries] AS [c]
WHERE (
    SELECT COUNT(*)
    FROM [Animals] AS [a]
    WHERE [a].[CountryId] = 1 AND [c].[Id] = [a].[CountryId] AND [a].[Discriminator] = N'Kiwi' AND [a].[CountryId] > 0) > 0
""");
    }

    public override async Task Update_where_keyless_entity_mapped_to_sql_query(bool async)
    {
        await base.Update_where_keyless_entity_mapped_to_sql_query(async);

        AssertExecuteUpdateSql();
    }

    protected override void ClearLog()
        => Fixture.TestSqlLoggerFactory.Clear();

    private void AssertSql(params string[] expected)
        => Fixture.TestSqlLoggerFactory.AssertBaseline(expected);

    private void AssertExecuteUpdateSql(params string[] expected)
        => Fixture.TestSqlLoggerFactory.AssertBaseline(expected, forUpdate: true);
}
