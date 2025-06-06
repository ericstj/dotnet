// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace System.Windows.Markup
{
    /// <summary>
    /// An attribute that identifies the value of the RootNamespace property in a
    /// project file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    [TypeForwardedFrom("WindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")]
    public sealed class RootNamespaceAttribute : Attribute
    {
        /// <summary>
        /// Creates a new RootNamespaceAttribute that describes the value of the
        /// RootNamespace property in a project file.
        /// </summary>
        /// <param name="nameSpace">The root namespace value</param>
        public RootNamespaceAttribute(string? nameSpace)
        {
            Namespace = nameSpace;
        }

        /// <summary>
        /// The root namespace value corresponding to the value of the RootNamespace
        /// property in a project file.
        /// </summary>
        public string? Namespace { get; }
    }
}
