using System.Globalization;
using System.Text.Json;
using Soenneker.TimeZones.Runner.Models;
using Soenneker.Utils.File.Abstract;

namespace Soenneker.TimeZones.Runner.GeoJson;

/// <summary>
/// Represents the time zone geo json writer.
/// </summary>
public static class TimeZoneGeoJsonWriter
{
    /// <summary>
    /// Writes time Zone Geo JSON Writer.
    /// </summary>
    /// <param name="outputPath">Path of the output to use.</param>
    /// <param name="features">features to process.</param>
    /// <param name="fileUtil">File Util for the write operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the write operation is complete.</returns>
    public static async Task Write(string outputPath, IReadOnlyList<TimeZoneFeature> features, IFileUtil fileUtil, CancellationToken cancellationToken)
    {
        string fullOutputPath = Path.GetFullPath(outputPath);
        await fileUtil.WriteAtomically(fullOutputPath, async (stream, ct) =>
        {
            await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

            writer.WriteStartObject();
            writer.WriteString("type", "FeatureCollection");
            writer.WritePropertyName("features");
            writer.WriteStartArray();

            foreach (TimeZoneFeature feature in features)
                WriteFeature(writer, feature);

            writer.WriteEndArray();
            writer.WriteEndObject();

            await writer.FlushAsync(ct);
        }, log: false, cancellationToken);
    }

    private static void WriteFeature(Utf8JsonWriter writer, TimeZoneFeature feature)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteString("tzid", feature.Tzid);
        writer.WriteNumber("minLat", feature.BoundingBox.MinLat);
        writer.WriteNumber("maxLat", feature.BoundingBox.MaxLat);
        writer.WriteNumber("minLon", feature.BoundingBox.MinLon);
        writer.WriteNumber("maxLon", feature.BoundingBox.MaxLon);
        writer.WriteEndObject();

        writer.WritePropertyName("geometry");
        writer.WriteStartObject();
        writer.WriteString("type", "MultiPolygon");
        writer.WritePropertyName("coordinates");
        WriteCoordinates(writer, feature.MultiPolygon);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteCoordinates(Utf8JsonWriter writer, List<List<List<Coordinate>>> multiPolygon)
    {
        writer.WriteStartArray();

        foreach (List<List<Coordinate>> polygon in multiPolygon)
        {
            writer.WriteStartArray();

            foreach (List<Coordinate> ring in polygon)
            {
                writer.WriteStartArray();

                foreach (Coordinate coordinate in ring)
                {
                    writer.WriteStartArray();
                    writer.WriteRawValue(Format(coordinate.Longitude), skipInputValidation: true);
                    writer.WriteRawValue(Format(coordinate.Latitude), skipInputValidation: true);
                    writer.WriteEndArray();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }

    private static string Format(double value)
    {
        value = Math.Round(value, 7, MidpointRounding.AwayFromZero);
        return value.ToString("0.#######", CultureInfo.InvariantCulture);
    }
}
