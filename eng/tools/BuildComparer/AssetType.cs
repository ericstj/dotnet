// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/// <summary>
/// Defines the type of asset being processed in the build comparison tool.
/// </summary>
public enum AssetType
{
    /// <summary>
    /// Represents a random non-package file in the build.
    /// </summary>
    Blob,
    
    /// <summary>
    /// Represents a NuGet package asset.
    /// </summary>
    Package,
    
    /// <summary>
    /// Represents an asset of unknown type.
    /// </summary>
    Unknown
}
