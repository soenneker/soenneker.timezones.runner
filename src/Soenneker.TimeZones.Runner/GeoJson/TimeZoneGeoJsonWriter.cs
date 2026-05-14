using System.Globalization;
using System.Text.Json;
using Soenneker.TimeZones.Runner.Models;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Path.Abstract;

namespace Soenneker.TimeZones.Runner.GeoJson;

public static class TimeZoneGeoJsonWriter
{
    public static async Task Write(string outputPath, IReadOnlyList<TimeZoneFeature> features, IFileUtil fileUtil, IDirectoryUtil directoryUtil,
        IPathUtil pathUtil, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory))
            await directoryUtil.Create(directory, cancellationToken: cancellationToken);

        string tempPath = !string.IsNullOrWhiteSpace(directory)
            ? await pathUtil.GetRandomUniqueFilePath(directory, ".tmp", cancellationToken)
            : await pathUtil.GetRandomTempFilePath(".tmp", cancellationToken);

        await using (FileStream stream = fileUtil.OpenWrite(tempPath))
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

        await fileUtil.DeleteIfExists(outputPath, cancellationToken: cancellationToken);
        await fileUtil.Move(tempPath, outputPath, cancellationToken: cancellationToken);
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
