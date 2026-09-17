[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.TimeZones.Runner/build-and-test.yml?style=for-the-badge)](https://github.com/soenneker/Soenneker.TimeZones.Runner/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.TimeZones.Runner/daily-automatic-update.yml?style=for-the-badge&label=Daily%20Update)](https://github.com/soenneker/Soenneker.TimeZones.Runner/actions/workflows/daily-automatic-update.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.TimeZones.Runner/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/Soenneker.TimeZones.Runner/actions/workflows/codeql.yml)

# Soenneker.TimeZones.Runner

Automation runner that generates and publishes the geographic dataset used by `Soenneker.TimeZones.Data`.

The daily job generates and packs with `--prepare-publication artifacts/publication.json`, then logs in to NuGet and runs `--publish-publication artifacts/publication.json`. This keeps the one-hour NuGet credential out of the long download and generation phase. An unchanged source checksum skips login and publishing.

The state file contains the prepared package version, paths, and checksum commit flag, without credentials. Both commands must run on the same machine: preparation retains its temporary data checkout for publication. Failed publication leaves the state and package available for retry with a fresh credential; the daily job also uploads the package as a failure artifact. Temporary checkouts are reclaimed when the hosted job ends. Running without either option still generates and publishes in one invocation.
