﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class DotnetToolsCommandResolver : ICommandResolver
{
    private readonly string _dotnetToolPath;

    public DotnetToolsCommandResolver(string dotnetToolPath = null)
    {
        if (dotnetToolPath == null)
        {
            _dotnetToolPath = Path.Combine(AppContext.BaseDirectory,
                "DotnetTools");
        }
        else
        {
            _dotnetToolPath = dotnetToolPath;
        }
    }

    public CommandSpec Resolve(CommandResolverArguments arguments)
    {
        if (string.IsNullOrEmpty(arguments.CommandName))
        {
            return null;
        }

        var packageId = new DirectoryInfo(Path.Combine(_dotnetToolPath, arguments.CommandName));
        if (!packageId.Exists)
        {
            return null;
        }

        var version = packageId.GetDirectories()[0];
        var dll = version.GetDirectories("tools")[0]
            .GetDirectories()[0] // TFM
            .GetDirectories()[0] // RID
            .GetFiles($"{arguments.CommandName}.dll")[0];

        return MuxerCommandSpecMaker.CreatePackageCommandSpecUsingMuxer(
                dll.FullName,
                arguments.CommandArguments);
    }
}
