[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.TimeZones.Runner/build-and-test.yml?style=for-the-badge)](https://github.com/soenneker/Soenneker.TimeZones.Runner/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.TimeZones.Runner/daily-automatic-update.yml?style=for-the-badge&label=Daily%20Update)](https://github.com/soenneker/Soenneker.TimeZones.Runner/actions/workflows/daily-automatic-update.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.TimeZones.Runner
### Automatically updates the Soenneker.TimeZones.Data package

This runner executes a GitHub action that updates another project. It's not meant for consumption.

The default world run downloads the OpenStreetMap planet PBF, uses pyosmium to prefilter timezone relations and their referenced members, then builds the GeoJSON from that filtered OSM extract. The runner uses `Soenneker.Python.Util` to locate or install Python, creates a local virtual environment under `artifacts/tools`, and installs the `osmium` Python package there. Pass `--disable-python-auto-install` to require an existing Python install, `--python-version` to choose the Python version, or `--disable-pyosmium-prefilter` to use the slower managed fallback.
