// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

public abstract class TestBase
{
    public ITestOutputHelper OutputHelper { get; }

    public TestBase(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;

        if (!Directory.Exists(Config.LogsDirectory))
        {
            Directory.CreateDirectory(Config.LogsDirectory);
        }
    }
}
