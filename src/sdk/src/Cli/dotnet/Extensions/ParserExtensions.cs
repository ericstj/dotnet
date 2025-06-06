// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Extensions;

public static class ParserExtensions
{
    public static ParseResult ParseFrom(
        this CommandLineConfiguration parser,
        string context,
        string[] args = null) =>
        parser.Parse([.. context.Split(' '), .. args]);
}
