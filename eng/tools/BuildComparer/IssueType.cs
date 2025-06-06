// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/// <summary>
/// Defines types of issues that can be identified during asset comparison.
/// </summary>
public enum IssueType
{
    /// <summary>
    /// Indicates a shipping asset is missing in the VMR build.
    /// </summary>
    MissingShipping,
    
    /// <summary>
    /// Indicates a non-shipping asset is missing in the VMR build.
    /// </summary>
    MissingNonShipping,
    
    /// <summary>
    /// Indicates an asset is classified differently between base and VMR builds.
    /// </summary>
    MisclassifiedAsset,
    
    /// <summary>
    /// Indicates a version mismatch between assemblies in base and VMR builds.
    /// </summary>
    AssemblyVersionMismatch,
    MissingPackageContent,
    ExtraPackageContent,
    PackageMetadataDifference,
    PackageTFMs,
    PackageDependencies,

    /// <summary>
    /// Indicates that the MSFT asset is signed, but the VMR asset is not.
    /// </summary>
    Unsigned
}
