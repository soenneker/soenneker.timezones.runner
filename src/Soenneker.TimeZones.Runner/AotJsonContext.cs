using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Soenneker.TimeZones.Runner;

[JsonSerializable(typeof(TimeZonesRunner.PreparedPublication))]
[JsonSerializable(typeof(Configuration.ExtractManifest))]
internal partial class AotJsonContext : JsonSerializerContext
{
    internal static JsonTypeInfo<T> Get<T>(JsonSerializerOptions? options = null) =>
        (JsonTypeInfo<T>)((options is null ? Default : new AotJsonContext(new JsonSerializerOptions(options))).GetTypeInfo(typeof(T))
            ?? throw new NotSupportedException($"No generated JSON metadata for {typeof(T)}."));
}
