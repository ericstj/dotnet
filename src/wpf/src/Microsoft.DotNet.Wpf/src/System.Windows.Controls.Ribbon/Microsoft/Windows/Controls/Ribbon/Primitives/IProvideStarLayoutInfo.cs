// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#if RIBBON_IN_FRAMEWORK
namespace System.Windows.Controls.Ribbon.Primitives
#else
namespace Microsoft.Windows.Controls.Ribbon.Primitives
#endif
{
    /// <summary>
    ///     The interface is the star layout contract which provides
    ///     the data needed to do the star layout.
    /// </summary>
    public interface IProvideStarLayoutInfo : IProvideStarLayoutInfoBase
    {
        /// <summary>
        ///     The enumeration of spatially non-overlapping
        ///     star combinations.
        /// </summary>
        IEnumerable<StarLayoutInfo> StarLayoutCombinations { get; }

        /// <summary>
        ///     Method which gets called every time star allocated
        ///     is completed.
        /// </summary>
        void OnStarSizeAllocationCompleted();
    }
}