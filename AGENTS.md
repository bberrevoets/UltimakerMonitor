# AGENTS.md

Instructions for AI coding agents (Claude Code, Codex and others) working in this repository. `CLAUDE.md` only imports
this file, so edit this one. [README.md](README.md) describes the project for people; this file lists what the code
alone does not tell you.

<!-- repo-init:start -->
## Repository Info

- **Company:** Berrevoets Systems
- **Author:** Bert Berrevoets
- **Contact:** <bert@berrevoets.net>
- **Remote:** <https://github.com/bberrevoets/UltimakerMonitor> (public)

## Workflow Rules

- **GitHub Flow:** `main` is the default and only long-lived branch. It is protected by the ruleset "Protect main":
  no direct pushes, no deletion, changes only through a pull request, and all PR conversations must be resolved
  before merging. Merged head branches are deleted automatically.
- After cloning, run `git config pull.rebase true` once, so `git pull` rebases instead of creating merge commits.
- Work on a branch per topic, push it, and open a PR to `main` only when Bert asks for it, assigned to him
  (`gh pr create --assignee bberrevoets`). PRs are squash-merged. After a merge: switch to `main` and pull, then delete the
  local branch with `git branch -D <branch>` (`-d` refuses after a squash merge) once
  `gh pr view <branch> --json state,headRefOid` shows `MERGED` and the same commit as `git rev-parse <branch>`.
- **"Update the documentation"** means `AGENTS.md`, the project's own `README.md` files (not vendored third-party ones)
  and `CHANGELOG.md`; then commit and push.
- [CHANGELOG.md](CHANGELOG.md) follows Keep a Changelog: the Attributors list at the top, and one `Author: *Name*`
  line under each section heading (`### Added`, `### Changed`, ...).
- Markdown must pass `markdownlint-cli2 "**/*.md"` with [.markdownlint.json](.markdownlint.json) (180-character
  lines) before every commit.
- Binaries go through Git LFS: every `filter=lfs` pattern in `.gitattributes` — the standard set (PDF, ZIP, PNG,
  JPEG, STEP/STP, STL, 3MF, F3D/F3Z) plus any pattern this repository adds. Add a pattern there before committing
  another binary type (GIF, BMP, ...). The patterns are lowercase and match `X.PNG` only where `core.ignorecase` is
  true (Windows, macOS); on a case-sensitive system check a new binary with `git check-attr filter -- <file>` before
  committing it.
- Commit messages use the conventional style (`feat:`, `fix:`, `docs:`, `chore:`).
<!-- repo-init:end -->

## Project Overview

Bert's own hobby project (September 2025): a web dashboard that finds the Ultimaker 3D printers on the home LAN and
shows their state live. Each printer is a card; a click opens a large card with temperatures, the print job and the
camera stream. It runs only on a development PC through the Aspire AppHost; nothing is deployed.

State: working, and quiet since October 2025 apart from security upgrades (Aspire 13.6.5 on 2026-11-22).

[bberrevoets/UltimakerManager](https://github.com/bberrevoets/UltimakerManager) is an older experiment for the same
printers (worker services with RabbitMQ). The two repositories share no code.

### How it fits together

```text
mDNS + Ultimaker REST API (LAN) → ApiService (keeps the printers in memory) → /api/printers
                                                                                  ↓ every 3 s
                                              browser ← SignalR /hubs/printers ← Web
```

- **ApiService** searches with mDNS every 5 minutes and asks every known printer for its state every 10 seconds
  (`http://{ip}/api/v1/system`, `/print_job`, `/printer`).
- **Web** (Razor Pages) reads `/api/printers` every 3 seconds, compares with the last result and sends only the
  differences to the browsers (`PrinterAdded`, `PrinterChanged`, `PrinterRemoved`; a new browser gets `PrintersSnapshot`).
- **AppHost** starts both and gives Web the address of ApiService (`http://apiservice`, service discovery).

## Commands

Run from the repository root. Needs the .NET 10 SDK; Aspire 13.6.5 comes from NuGet, no workload and no Docker.

- Build: `dotnet build UltimakerMonitor.slnx`
- Run everything (both services plus the Aspire dashboard): `dotnet run --project UltimakerMonitor.AppHost`
- Known vulnerable packages: `dotnet list UltimakerMonitor.slnx package --vulnerable --include-transitive`
- Markdown: `npx markdownlint-cli2 "**/*.md"`

There is no test project and no CI. A quick check after a package upgrade, with the AppHost running (Development):

| What | URL | Expected |
| --- | --- | --- |
| API health | `https://localhost:7274/health` | `Healthy` |
| Web health | `https://localhost:7138/health` | `Healthy` |
| OpenAPI document | `https://localhost:7274/openapi/v1.json` | three `/api/printers` paths |

Both services redirect HTTP to HTTPS, so the HTTP ports (5009, 5069) answer with status 307.

## Repository Layout

- `UltimakerMonitor.slnx` — the solution (XML format) with all four projects
- [UltimakerMonitor.AppHost/](UltimakerMonitor.AppHost/) — Aspire orchestrator; `AppHost.cs` is the whole composition
- [UltimakerMonitor.ApiService/](UltimakerMonitor.ApiService/) — minimal API; `Services/PrinterDiscoveryService.cs`
  (mDNS, polling, simulated printers) and `Services/UltimakerApiClient.cs` (the printer's REST API)
- [UltimakerMonitor.Web/](UltimakerMonitor.Web/) — Razor Pages front end; `Hubs/PrinterHub.cs`,
  `Services/PrinterUpdateService.cs`, and the camera proxy in `Program.cs`
- [UltimakerMonitor.ServiceDefaults/](UltimakerMonitor.ServiceDefaults/) — shared Aspire defaults: OpenTelemetry,
  health checks, service discovery, HttpClient resilience
- `UltimakerMonitor.Web/wwwroot/lib/` — third-party client libraries (Bootstrap, jQuery, SignalR), not our code
- `PromptText.txt` — Bert's original prompt and feature notes for this project

## Conventions

- **CSS per page:** styles for one page live in its own file next to it (`Index.cshtml` → `Index.cshtml.css`), like a
  code-behind file. Only site-wide styles go in `wwwroot/css/site.css`.
- **Two `Printer` classes:** `ApiService/Models/Printer.cs` and `Web/Models/Printer.cs` are separate copies of the JSON
  contract, and the Web copy has fewer members. A field added on the API side reaches the page only after it is added
  to the Web copy too.
- **Nothing is stored:** the printers live in memory in `PrinterDiscoveryService`. After a restart the list is empty
  until the next mDNS search finds them again.
- **Simulated printers:** set `DiscoveryTest:EnableSimulation` to `true` in the ApiService settings to get dummy
  printers that appear and expire (`AddEverySeconds`, `LifetimeSeconds`). Useful without a real printer on the LAN.
- **Camera proxy:** `/api/camera/stream?host=<ip>[:port]` in Web forwards a printer's MJPEG stream. It accepts only
  private and loopback addresses. That check stops the endpoint from being used to reach other hosts (SSRF); keep it.
- **Aspire version in two places:** `Aspire.AppHost.Sdk` and `Aspire.Hosting.AppHost` in the AppHost project file
  must have the same version. A Dependabot alert names only the package.
- **Security:** Dependabot alerts and security updates are enabled on GitHub. Its alerts cover direct package
  references; the `dotnet list ... --vulnerable --include-transitive` command above also finds indirect ones.
- `wwwroot/lib/` holds vendored files: do not edit them.
