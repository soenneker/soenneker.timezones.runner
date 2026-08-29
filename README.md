[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.TimeZones.Runner/build-and-test.yml?style=for-the-badge)](https://github.com/soenneker/Soenneker.TimeZones.Runner/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.TimeZones.Runner/daily-automatic-update.yml?style=for-the-badge&label=Daily%20Update)](https://github.com/soenneker/Soenneker.TimeZones.Runner/actions/workflows/daily-automatic-update.yml)

# Soenneker.TimeZones.Runner

Represents the clipper geometry.

> This is an automation runner, not a package intended for application consumption.

## What the runner does

- `ClipperGeometry.Scale` — The scale.
- `ClipperGeometry.BuildRelationGeometry(outerRings, innerRings)` — Builds relation geometry.
- `ClipperGeometry.Normalize(paths)` — Normalizes clipper Geometry.
- `ClipperGeometry.ToPaths(rings)` — Converts to paths.
- `ClipperGeometry.ToMultiPolygon(paths, minRingPoints)` — Converts to multi Polygon.

## What you get

- `ClipperGeometry` — Represents the clipper geometry.
- `Constants` — Represents the constants.
- `DefaultExtractManifest` — Represents the default extract manifest.
- `OsmTagUtil` — Represents the osm tag util.
- `RingStitcher` — Represents the ring stitcher.
- `RunnerOptionsParser` — Represents the runner options parser.
- `TimeZoneDatasetValidator` — Represents the time zone dataset validator.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `ClipperGeometry.Normalize(paths)` | Normalizes clipper Geometry. | The resulting paths. |
| `ClipperGeometry.ToPaths(rings)` | Converts to paths. | The resulting paths. |
| `ClipperGeometry.ToMultiPolygon(paths, minRingPoints)` | Converts to multi Polygon. | The resulting collection. |
| `Constants.ExtractChecksumManifestRelativePath` | The extract checksum manifest relative path. | The extract checksum manifest relative path. |
| `DefaultExtractManifest.World` | Gets world. | Gets world. |
| `DefaultExtractManifest.Continent` | Gets or sets continent. | Gets or sets continent. |
| `OsmTagUtil.TryGetValue(tags, key, value)` | Attempts to get value. | true if a matching value was found and assigned to the output parameter; otherwise, false. |
| `OsmTagUtil.IsTimezoneRelation(tags, includeAdminBoundaries)` | Determines whether the Osm Tag timezone Relation. | true if the tags identify a qualifying timezone relation; otherwise, false. |
| `TimeZoneDatasetValidator.Validate(features, minRingPoints)` | Validates the request Basic credentials against the configured username and password hash. | Returns no value; the requested change is complete when the method returns. |
| `TimeZoneGeoJsonWriter.Write(outputPath, features, fileUtil, directoryUtil, pathUtil, cancellationToken)` | Writes time Zone Geo JSON Writer. | A task that completes when the write operation is complete. |
| `BoundingBox.FromRings(rings)` | Creates from rings. | The same builder instance, so additional classes or variants can be chained. |
| `ConsoleHostedService.StartAsync(cancellationToken)` | Starts the Console Hosted Service and begins its background work. | A task that completes after the Console Hosted Service has started. |
| `ConsoleHostedService.StopAsync(cancellationToken)` | Stops the Console Hosted Service and waits for its background work to finish. | A task that completes after the Console Hosted Service has stopped. |
| `ExtractDefinition.Name` | Gets or sets name. | Gets or sets name. |
| `ExtractDefinition.Url` | Gets or sets url. | Gets or sets url. |
| `ExtractDefinition.CacheFileName` | Gets or sets cache file name. | Gets or sets cache file name. |
| `ExtractDefinition.Md5` | Gets or sets md5. | Gets or sets md5. |
| `ExtractDefinition.Enabled` | Gets or sets a value indicating whether enabled. | Gets or sets a value indicating whether enabled. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
