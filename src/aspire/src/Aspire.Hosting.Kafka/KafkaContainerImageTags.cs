// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

internal static class KafkaContainerImageTags
{
    public const string Registry = "docker.io";
    public const string Image = "confluentinc/confluent-local";
    public const string Tag = "7.7.1";
    public const string KafkaUiImage = "provectuslabs/kafka-ui";
    public const string KafkaUiTag = "v0.7.2";
}
