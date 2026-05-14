using System.Globalization;
using System.Text;
using System.Text.Json;
using Soenneker.TimeZones.Runner.Models;

namespace Soenneker.TimeZones.Runner.GeoJson;

public static class TimeZoneGeoJsonWriter
{
    public static async Task Write(string outputPath, IReadOnlyList<TimeZoneFeature> features, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string tempPath = outputPath + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 131_072, FileOptions.Asynchronous))
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

            await writer.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, outputPath, true);
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
