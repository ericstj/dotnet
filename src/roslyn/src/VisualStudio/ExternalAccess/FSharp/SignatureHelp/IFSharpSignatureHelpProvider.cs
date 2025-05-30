﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.ExternalAccess.FSharp.SignatureHelp;

[Obsolete("Use Microsoft.CodeAnalysis.ExternalAccess.FSharp.SignatureHelp.AbstractFSharpSignatureHelpProvider instead.")]
internal interface IFSharpSignatureHelpProvider
{
    /// <summary>
    /// Returns true if the character might trigger completion, 
    /// e.g. '(' and ',' for method invocations 
    /// </summary>
    bool IsTriggerCharacter(char ch);

    /// <summary>
    /// Returns true if the character might end a Signature Help session, 
    /// e.g. ')' for method invocations.  
    /// </summary>
    bool IsRetriggerCharacter(char ch);

    /// <summary>
    /// Returns valid signature help items at the specified position in the document.
    /// </summary>
    Task<FSharpSignatureHelpItems> GetItemsAsync(Document document, int position, FSharpSignatureHelpTriggerInfo triggerInfo, CancellationToken cancellationToken);
}
