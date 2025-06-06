// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ShellShim;

internal class ShellShimTemplateFinder(
    INuGetPackageDownloader nugetPackageDownloader,
    DirectoryPath tempDir,
    PackageSourceLocation packageSourceLocation)
{
    private readonly DirectoryPath _tempDir = tempDir;
    private readonly INuGetPackageDownloader _nugetPackageDownloader = nugetPackageDownloader;
    private readonly PackageSourceLocation _packageSourceLocation = packageSourceLocation;

    public async Task<string> ResolveAppHostSourceDirectoryAsync(string archOption, NuGetFramework targetFramework, Architecture arch)
    {
        string rid;
        var validRids = new string[] { "win-x64", "win-arm64", "osx-x64", "osx-arm64" };
        if (string.IsNullOrEmpty(archOption))
        {
            if (targetFramework != null &&
                (targetFramework.Version.Major < 6 && OperatingSystem.IsMacOS() ||
                targetFramework.Version.Major < 5 && OperatingSystem.IsWindows())
                && !arch.Equals(Architecture.X64))
            {
                rid = OperatingSystem.IsWindows() ? "win-x64" : "osx-x64";
            }
            else
            {
                // Use the default app host
                return GetDefaultAppHostSourceDirectory();
            }
        }
        else
        {
            rid = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(null, archOption);
        }

        if (!validRids.Contains(rid))
        {
            throw new GracefulException(string.Format(CliStrings.InvalidRuntimeIdentifier, rid, string.Join(" ", validRids)));
        }

        var packageId = new PackageId($"microsoft.netcore.app.host.{rid}");
        NuGetVersion packageVersion = null;
        var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(packageId, packageVersion, packageSourceLocation: _packageSourceLocation);
        _ = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, _tempDir);

        return Path.Combine(_tempDir.Value, "runtimes", rid, "native");
    }

    public static string GetDefaultAppHostSourceDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "AppHostTemplate");
    }
}
