﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Roslyn.LanguageServer.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// Class which represents the server hover support.
/// <para>
/// See the <see href="https://microsoft.github.io/language-server-protocol/specifications/specification-current/#hoverOptions">Language Server Protocol specification</see> for additional information.
/// </para>
/// </summary>
internal class HoverOptions : IWorkDoneProgressOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether work done progress is supported.
    /// </summary>
    [JsonPropertyName("workDoneProgress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WorkDoneProgress { get; init; }
}
