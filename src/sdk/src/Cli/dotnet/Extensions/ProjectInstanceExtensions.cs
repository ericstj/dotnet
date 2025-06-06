﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;

namespace Microsoft.DotNet.Cli.Extensions;

public static class ProjectInstanceExtensions
{
    public static string GetProjectId(this ProjectInstance projectInstance)
    {
        var projectGuidProperty = projectInstance.GetPropertyValue("ProjectGuid");
        var projectGuid = string.IsNullOrEmpty(projectGuidProperty)
            ? Guid.NewGuid()
            : new Guid(projectGuidProperty);
        return projectGuid.ToString("B").ToUpper();
    }

    public static string GetDefaultProjectTypeGuid(this ProjectInstance projectInstance)
    {
        string projectTypeGuid = projectInstance.GetPropertyValue("DefaultProjectTypeGuid");
        if (string.IsNullOrEmpty(projectTypeGuid) && projectInstance.FullPath.EndsWith(".shproj", StringComparison.OrdinalIgnoreCase))
        {
            projectTypeGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";
        }
        return projectTypeGuid;
    }

    public static IEnumerable<string> GetPlatforms(this ProjectInstance projectInstance)
    {
        return (projectInstance.GetPropertyValue("Platforms") ?? "")
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .DefaultIfEmpty("AnyCPU");
    }

    public static IEnumerable<string> GetConfigurations(this ProjectInstance projectInstance)
    {
        string foundConfig = projectInstance.GetPropertyValue("Configurations") ?? "Debug;Release";
        if (string.IsNullOrWhiteSpace(foundConfig))
        {
            foundConfig = "Debug;Release";
        }

        return foundConfig
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .DefaultIfEmpty("Debug");
    }
}
