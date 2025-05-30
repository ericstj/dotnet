﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.Shared.Preview;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.Preview;

internal partial class PreviewUpdater
{
    // internal for testing
    internal sealed class PreviewDialogWorkspace : PreviewWorkspace
    {
        public PreviewDialogWorkspace(Solution solution) : base(solution)
        {
        }

        public void CloseDocument(TextDocument document, SourceText text)
        {
            switch (document.Kind)
            {
                case TextDocumentKind.Document:
                    OnDocumentClosed(document.Id, new PreviewTextLoader(text));
                    break;

                case TextDocumentKind.AnalyzerConfigDocument:
                    OnAnalyzerConfigDocumentClosed(document.Id, new PreviewTextLoader(text));
                    break;

                case TextDocumentKind.AdditionalDocument:
                    OnAdditionalDocumentClosed(document.Id, new PreviewTextLoader(text));
                    break;

                default:
                    throw ExceptionUtilities.UnexpectedValue(document.Kind);
            }
        }

        private sealed class PreviewTextLoader : TextLoader
        {
            private readonly SourceText _text;

            internal PreviewTextLoader(SourceText documentText)
                => _text = documentText;

            public override Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken)
                => Task.FromResult(LoadTextAndVersionSynchronously(options, cancellationToken));

            internal override TextAndVersion LoadTextAndVersionSynchronously(LoadTextOptions options, CancellationToken cancellationToken)
                => TextAndVersion.Create(_text, VersionStamp.Create());
        }
    }
}
