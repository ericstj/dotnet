// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ReSharper disable once CheckNamespace

namespace Microsoft.EntityFrameworkCore.Cosmos.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class CosmosStringMethodTranslator(ISqlExpressionFactory sqlExpressionFactory) : IMethodCallTranslator
{
    private static readonly MethodInfo IndexOfMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(string)])!;

    private static readonly MethodInfo IndexOfMethodInfoChar
        = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(char)])!;

    private static readonly MethodInfo IndexOfMethodInfoWithStartingPositionString
        = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(string), typeof(int)])!;

    private static readonly MethodInfo IndexOfMethodInfoWithStartingPositionChar
        = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(char), typeof(int)])!;

    private static readonly MethodInfo ReplaceMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;

    private static readonly MethodInfo ReplaceMethodInfoChar
        = typeof(string).GetRuntimeMethod(nameof(string.Replace), [typeof(char), typeof(char)])!;

    private static readonly MethodInfo ContainsMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly MethodInfo ContainsMethodInfoChar
        = typeof(string).GetRuntimeMethod(nameof(string.Contains), [typeof(char)])!;

    private static readonly MethodInfo ContainsWithStringComparisonMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.Contains), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo ContainsWithStringComparisonMethodInfoChar
        = typeof(string).GetRuntimeMethod(nameof(string.Contains), [typeof(char), typeof(StringComparison)])!;

    private static readonly MethodInfo StartsWithMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.StartsWith), [typeof(string)])!;

    private static readonly MethodInfo StartsWithMethodInfoChar
        = typeof(string).GetRuntimeMethod(nameof(string.StartsWith), [typeof(char)])!;

    private static readonly MethodInfo StartsWithWithStringComparisonMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.StartsWith), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo EndsWithMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.EndsWith), [typeof(string)])!;

    private static readonly MethodInfo EndsWithMethodInfoChar
        = typeof(string).GetRuntimeMethod(nameof(string.EndsWith), [typeof(char)])!;

    private static readonly MethodInfo EndsWithWithStringComparisonMethodInfoString
        = typeof(string).GetRuntimeMethod(nameof(string.EndsWith), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo ToLowerMethodInfo
        = typeof(string).GetRuntimeMethod(nameof(string.ToLower), [])!;

    private static readonly MethodInfo ToUpperMethodInfo
        = typeof(string).GetRuntimeMethod(nameof(string.ToUpper), [])!;

    private static readonly MethodInfo TrimStartMethodInfoWithoutArgs
        = typeof(string).GetRuntimeMethod(nameof(string.TrimStart), [])!;

    private static readonly MethodInfo TrimEndMethodInfoWithoutArgs
        = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), [])!;

    private static readonly MethodInfo TrimMethodInfoWithoutArgs
        = typeof(string).GetRuntimeMethod(nameof(string.Trim), [])!;

    private static readonly MethodInfo TrimStartMethodInfoWithCharArrayArg
        = typeof(string).GetRuntimeMethod(nameof(string.TrimStart), [typeof(char[])])!;

    private static readonly MethodInfo TrimEndMethodInfoWithCharArrayArg
        = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), [typeof(char[])])!;

    private static readonly MethodInfo TrimMethodInfoWithCharArrayArg
        = typeof(string).GetRuntimeMethod(nameof(string.Trim), [typeof(char[])])!;

    private static readonly MethodInfo SubstringMethodInfoWithOneArg
        = typeof(string).GetRuntimeMethod(nameof(string.Substring), [typeof(int)])!;

    private static readonly MethodInfo SubstringMethodInfoWithTwoArgs
        = typeof(string).GetRuntimeMethod(nameof(string.Substring), [typeof(int), typeof(int)])!;

    private static readonly MethodInfo FirstOrDefaultMethodInfoWithoutArgs
        = typeof(Enumerable).GetRuntimeMethods().Single(
            m => m.Name == nameof(Enumerable.FirstOrDefault)
                && m.GetParameters().Length == 1).MakeGenericMethod(typeof(char));

    private static readonly MethodInfo LastOrDefaultMethodInfoWithoutArgs
        = typeof(Enumerable).GetRuntimeMethods().Single(
            m => m.Name == nameof(Enumerable.LastOrDefault)
                && m.GetParameters().Length == 1).MakeGenericMethod(typeof(char));

    private static readonly MethodInfo StringConcatWithTwoArguments =
        typeof(string).GetRuntimeMethod(nameof(string.Concat), [typeof(string), typeof(string)])!;

    private static readonly MethodInfo StringConcatWithThreeArguments =
        typeof(string).GetRuntimeMethod(nameof(string.Concat), [typeof(string), typeof(string), typeof(string)])!;

    private static readonly MethodInfo StringConcatWithFourArguments =
        typeof(string).GetRuntimeMethod(nameof(string.Concat), [typeof(string), typeof(string), typeof(string), typeof(string)])!;

    private static readonly MethodInfo StringComparisonWithComparisonTypeArgumentInstance
        = typeof(string).GetRuntimeMethod(nameof(string.Equals), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo StringComparisonWithComparisonTypeArgumentStatic
        = typeof(string).GetRuntimeMethod(nameof(string.Equals), [typeof(string), typeof(string), typeof(StringComparison)])!;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance != null)
        {
            if (IndexOfMethodInfoString.Equals(method) || IndexOfMethodInfoChar.Equals(method))
            {
                return TranslateSystemFunction("INDEX_OF", typeof(int), instance, arguments[0]);
            }

            if (IndexOfMethodInfoWithStartingPositionString.Equals(method) || IndexOfMethodInfoWithStartingPositionChar.Equals(method))
            {
                return TranslateSystemFunction("INDEX_OF", typeof(int), instance, arguments[0], arguments[1]);
            }

            if (ReplaceMethodInfoString.Equals(method) || ReplaceMethodInfoChar.Equals(method))
            {
                return TranslateSystemFunction("REPLACE", method.ReturnType, instance, arguments[0], arguments[1]);
            }

            if (ContainsMethodInfoString.Equals(method) || ContainsMethodInfoChar.Equals(method))
            {
                return TranslateSystemFunction("CONTAINS", typeof(bool), instance, arguments[0]);
            }

            if (ContainsWithStringComparisonMethodInfoString.Equals(method) || ContainsWithStringComparisonMethodInfoChar.Equals(method))
            {
                if (arguments[1] is SqlConstantExpression { Value: StringComparison comparisonType })
                {
                    return comparisonType switch
                    {
                        StringComparison.Ordinal
                            => TranslateSystemFunction(
                                "CONTAINS", typeof(bool), instance, arguments[0], sqlExpressionFactory.Constant(false)),
                        StringComparison.OrdinalIgnoreCase
                            => TranslateSystemFunction(
                                "CONTAINS", typeof(bool), instance, arguments[0], sqlExpressionFactory.Constant(true)),

                        _ => null // TODO: Explicit translation error for unsupported StringComparison argument (depends on #26410)
                    };
                }

                // TODO: Explicit translation error for non-constant StringComparison argument (depends on #26410)
                return null;
            }

            if (StartsWithMethodInfoString.Equals(method) || StartsWithMethodInfoChar.Equals(method))
            {
                return TranslateSystemFunction("STARTSWITH", typeof(bool), instance, arguments[0]);
            }

            if (StartsWithWithStringComparisonMethodInfoString.Equals(method))
            {
                if (arguments[1] is SqlConstantExpression { Value: StringComparison comparisonType })
                {
                    return comparisonType switch
                    {
                        StringComparison.Ordinal
                            => TranslateSystemFunction(
                                "STARTSWITH", typeof(bool), instance, arguments[0], sqlExpressionFactory.Constant(false)),
                        StringComparison.OrdinalIgnoreCase
                            => TranslateSystemFunction(
                                "STARTSWITH", typeof(bool), instance, arguments[0], sqlExpressionFactory.Constant(true)),

                        _ => null // TODO: Explicit translation error for unsupported StringComparison argument (depends on #26410)
                    };
                }

                // TODO: Explicit translation error for non-constant StringComparison argument (depends on #26410)
                return null;
            }

            if (EndsWithMethodInfoString.Equals(method) || EndsWithMethodInfoChar.Equals(method))
            {
                return TranslateSystemFunction("ENDSWITH", typeof(bool), instance, arguments[0]);
            }

            if (EndsWithWithStringComparisonMethodInfoString.Equals(method))
            {
                if (arguments[1] is SqlConstantExpression { Value: StringComparison comparisonType })
                {
                    return comparisonType switch
                    {
                        StringComparison.Ordinal
                            => TranslateSystemFunction(
                                "ENDSWITH", typeof(bool), instance, arguments[0], sqlExpressionFactory.Constant(false)),
                        StringComparison.OrdinalIgnoreCase
                            => TranslateSystemFunction(
                                "ENDSWITH", typeof(bool), instance, arguments[0], sqlExpressionFactory.Constant(true)),

                        _ => null // TODO: Explicit translation error for unsupported StringComparison argument (depends on #26410)
                    };
                }

                // TODO: Explicit translation error for non-constant StringComparison argument (depends on #26410)
                return null;
            }

            if (ToLowerMethodInfo.Equals(method))
            {
                return TranslateSystemFunction("LOWER", method.ReturnType, instance);
            }

            if (ToUpperMethodInfo.Equals(method))
            {
                return TranslateSystemFunction("UPPER", method.ReturnType, instance);
            }

            if (TrimStartMethodInfoWithoutArgs.Equals(method)
                || (TrimStartMethodInfoWithCharArrayArg.Equals(method)
                    // Cosmos DB LTRIM does not take arguments
                    && ((arguments[0] as SqlConstantExpression)?.Value as Array)?.Length == 0))
            {
                return TranslateSystemFunction("LTRIM", method.ReturnType, instance);
            }

            if (TrimEndMethodInfoWithoutArgs.Equals(method)
                || (TrimEndMethodInfoWithCharArrayArg.Equals(method)
                    // Cosmos DB RTRIM does not take arguments
                    && ((arguments[0] as SqlConstantExpression)?.Value as Array)?.Length == 0))
            {
                return TranslateSystemFunction("RTRIM", method.ReturnType, instance);
            }

            if (TrimMethodInfoWithoutArgs.Equals(method)
                || (TrimMethodInfoWithCharArrayArg.Equals(method)
                    // Cosmos DB TRIM does not take arguments
                    && ((arguments[0] as SqlConstantExpression)?.Value as Array)?.Length == 0))
            {
                return TranslateSystemFunction("TRIM", method.ReturnType, instance);
            }

            if (SubstringMethodInfoWithOneArg.Equals(method))
            {
                return TranslateSystemFunction(
                    "SUBSTRING",
                    method.ReturnType,
                    instance,
                    arguments[0],
                    TranslateSystemFunction("LENGTH", typeof(int), instance));
            }

            if (SubstringMethodInfoWithTwoArgs.Equals(method))
            {
                return arguments[0] is SqlConstantExpression { Value: 0 }
                    ? TranslateSystemFunction("LEFT", method.ReturnType, instance, arguments[1])
                    : TranslateSystemFunction("SUBSTRING", method.ReturnType, instance, arguments[0], arguments[1]);
            }
        }

        if (FirstOrDefaultMethodInfoWithoutArgs.Equals(method))
        {
            return TranslateSystemFunction("LEFT", typeof(char), arguments[0], sqlExpressionFactory.Constant(1));
        }

        if (LastOrDefaultMethodInfoWithoutArgs.Equals(method))
        {
            return TranslateSystemFunction("RIGHT", typeof(char), arguments[0], sqlExpressionFactory.Constant(1));
        }

        if (StringConcatWithTwoArguments.Equals(method))
        {
            return sqlExpressionFactory.Add(
                arguments[0],
                arguments[1]);
        }

        if (StringConcatWithThreeArguments.Equals(method))
        {
            return sqlExpressionFactory.Add(
                arguments[0],
                sqlExpressionFactory.Add(
                    arguments[1],
                    arguments[2]));
        }

        if (StringConcatWithFourArguments.Equals(method))
        {
            return sqlExpressionFactory.Add(
                arguments[0],
                sqlExpressionFactory.Add(
                    arguments[1],
                    sqlExpressionFactory.Add(
                        arguments[2],
                        arguments[3])));
        }

        if (StringComparisonWithComparisonTypeArgumentInstance.Equals(method)
            || StringComparisonWithComparisonTypeArgumentStatic.Equals(method))
        {
            var comparisonTypeArgument = arguments[^1];

            if (comparisonTypeArgument is SqlConstantExpression
                {
                    Value: StringComparison comparisonTypeArgumentValue and (StringComparison.OrdinalIgnoreCase or StringComparison.Ordinal)
                })
            {
                return StringComparisonWithComparisonTypeArgumentInstance.Equals(method)
                    ? comparisonTypeArgumentValue == StringComparison.OrdinalIgnoreCase
                        ? TranslateSystemFunction(
                            "STRINGEQUALS", typeof(bool), instance!, arguments[0], sqlExpressionFactory.Constant(true))
                        : TranslateSystemFunction("STRINGEQUALS", typeof(bool), instance!, arguments[0])
                    : comparisonTypeArgumentValue == StringComparison.OrdinalIgnoreCase
                        ? TranslateSystemFunction(
                            "STRINGEQUALS", typeof(bool), arguments[0], arguments[1], sqlExpressionFactory.Constant(true))
                        : TranslateSystemFunction("STRINGEQUALS", typeof(bool), arguments[0], arguments[1]);
            }
        }

        return null;
    }

    private SqlExpression TranslateSystemFunction(string function, Type returnType, params SqlExpression[] arguments)
        => sqlExpressionFactory.Function(function, arguments, returnType);
}
