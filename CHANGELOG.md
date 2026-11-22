# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Attributors

- Bert Berrevoets (<bert@berrevoets.net>) — Berrevoets Systems
- dependabot[bot]
- Claude Fable 5.2 — co-authors on the commits listed below

## [Unreleased]

### Added

Author: *Bert Berrevoets*

- AI agent setup: [AGENTS.md](AGENTS.md) as the single instruction file, a [CLAUDE.md](CLAUDE.md) stub that imports it,
  markdownlint (180 characters), Git LFS rules and this changelog.

### Changed

Author: *Bert Berrevoets*

- The default branch is renamed from `master` to `main`, protected by the ruleset "Protect main"; merged branches
  are deleted automatically.
- `README.md` now describes the project, how it works and how to run it.

## [2026-11-22]

### Security

Author: *Bert Berrevoets*

- Upgraded `Aspire.AppHost.Sdk` and `Aspire.Hosting.AppHost` from 9.5.0 to 13.6.5: fixes the Aspire elevation of
  privilege advisory CVE-2026-120006 ([GHSA-f7vh-h5g3-7jrh](https://github.com/advisories/GHSA-f7vh-h5g3-7jrh)).
- Upgraded `Microsoft.AspNetCore.OpenApi` from 10.0.0-rc.1 to 10.0.15. The release candidate pulled in
  `Microsoft.OpenApi` 2.0.0, which has a known vulnerability
  ([GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)).

## [2026-04-30]

### Changed

Author: *dependabot[bot]*

- Bumped `OpenTelemetry.Exporter.OpenTelemetryProtocol` to 1.15.3 (merged on 2026-08-30).

## [2025-10-12]

### Changed

Author: *Bert Berrevoets*

- Updated the Aspire SDK and package versions to 9.5.0.

## [2025-09-15]

### Added

Author: *Bert Berrevoets*

- SignalR: the dashboard updates live, without reloading the page.
- Simulated printers for testing without a real printer (`DiscoveryTest` settings).

## [2025-09-14]

### Added

Author: *Bert Berrevoets*

- The solution: Aspire AppHost, API service, Razor Pages web front end and service defaults.
- Printer cards on the home page, with a detail card that opens on a click.
- A background service with automatic refresh.
- mDNS discovery of Ultimaker printers and support for more than one nozzle.
- Print job properties, job state and the camera stream.
- `.gitattributes`, `.gitignore`, `README.md` and `LICENSE.txt`.

### Changed

Author: *Bert Berrevoets*

- Renamed the projects from UltimakeMonitor to UltimakerMonitor.
