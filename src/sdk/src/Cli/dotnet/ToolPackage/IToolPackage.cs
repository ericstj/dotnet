// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal interface IToolPackage
{
    PackageId Id { get; }

    NuGetVersion Version { get; }

    DirectoryPath PackageDirectory { get; }

    RestoredCommand Command { get; }

    IEnumerable<string> Warnings { get; }

    IReadOnlyList<FilePath> PackagedShims { get; }

    IEnumerable<NuGetFramework> Frameworks { get; }
}
