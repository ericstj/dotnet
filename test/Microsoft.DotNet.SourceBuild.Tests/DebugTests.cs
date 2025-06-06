// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

public class DebugTests : SdkTests
{
    private record ScanResult(string FileName, bool HasDebugInfo, bool HasDebugAbbrevs, bool HasGnuDebugLink);

    public DebugTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <Summary>
    /// Verifies that all generated native files include native debug symbols.
    /// </Summary>
    [Fact]
    public void SourceBuiltSdkContainsNativeDebugSymbols()
    {

        var fileNames = Directory.EnumerateFiles(Config.DotNetDirectory, "*", SearchOption.AllDirectories);
        var foundIssue = false;
        StringBuilder issueDetails = new();
        foreach (var fileName in fileNames)
        {
            if (!IsElfFile(fileName))
            {
                continue;
            }

            var result = ScanFile(fileName);

            string newLine = Environment.NewLine;

            if (!result.HasDebugInfo)
            {
                foundIssue = true;
                issueDetails.Append($"missing .debug_info section in {fileName}{newLine}");
            }
            if (!result.HasDebugAbbrevs)
            {
                foundIssue = true;
                issueDetails.Append($"missing .debug_abbrev section in {fileName}{newLine}");
            }
            if (result.HasGnuDebugLink)
            {
                foundIssue = true;
                issueDetails.Append($"unexpected .gnu_debuglink section in {fileName}{newLine}");
            }
        }

        Assert.False(foundIssue, issueDetails.ToString());
    }

    private bool IsElfFile(string fileName)
    {
        string fileStdOut = ExecuteHelper.ExecuteProcessValidateExitCode("file", $"{fileName}", OutputHelper);
        return Regex.IsMatch(fileStdOut, @"ELF 64-bit [LM]SB (?:pie )?(?:executable|shared object)");
    }

    private ScanResult ScanFile(string fileName)
    {
        string readelfSStdOut = ExecuteHelper.ExecuteProcessValidateExitCode("eu-readelf", $"-S {fileName}", OutputHelper);

        // Test for .debug_* sections in the shared object. This is the  main test.
        // Stripped objects will not contain these.

        bool hasDebugInfo = readelfSStdOut
            .Split("\n")
            .Where(line => line.Contains("] .debug_info"))
            .Any();

        bool hasDebugAbbrev = readelfSStdOut.Split("\n")
            .Where(line => line.Contains("] .debug_abbrev"))
            .Any();

        string readelfsStdOut = ExecuteHelper.ExecuteProcessValidateExitCode("eu-readelf", $"-s {fileName}", OutputHelper);

        // Test that there are no .gnu_debuglink sections pointing to another
        // debuginfo file. There shouldn't be any debuginfo files, so the link makes
        // no sense either.
        bool hasGnuDebuglink = readelfsStdOut.Split("\n").Where(line => line.Contains("] .gnu_debuglink")).Any();

        return new ScanResult(fileName, hasDebugInfo, hasDebugAbbrev, hasGnuDebuglink);
    }
}
