# UltimakerMonitor

A web dashboard that finds the Ultimaker 3D printers on your local network and shows their state live. Every printer
is a card with its status; a click opens a large card with temperatures, the running print job and the camera stream.

A hobby project by Bert Berrevoets, built with .NET 10 and .NET Aspire. It runs on a development PC and is not
deployed anywhere.

## How it works

```text
printers on the LAN → ApiService → Web → your browser
  (mDNS + REST API)    (/api/printers)  (Razor Pages + SignalR)
```

- **UltimakerMonitor.ApiService** searches for printers with mDNS and asks each one for its state every 10 seconds.
- **UltimakerMonitor.Web** shows the cards and pushes changes to the browser with SignalR, without a page reload.
- **UltimakerMonitor.AppHost** starts both and opens the Aspire dashboard (logs, traces, metrics).
- **UltimakerMonitor.ServiceDefaults** holds the shared Aspire settings.

## Prerequisites

- .NET 10 SDK
- A trusted development certificate: `dotnet dev-certs https --trust`

No Docker and no Aspire workload are needed.

## Running

```sh
dotnet run --project UltimakerMonitor.AppHost
```

The console prints the link to the Aspire dashboard; the web front end is listed there.

No Ultimaker on your network? Set `DiscoveryTest:EnableSimulation` to `true` in
`UltimakerMonitor.ApiService/appsettings.json` to get simulated printers.

## License

See [LICENSE.txt](LICENSE.txt). The history is in [CHANGELOG.md](CHANGELOG.md).

## Conventions

- **Branching:** GitHub Flow. `main` is protected and accepts changes only through pull requests.
- **Markdown:** linted with `markdownlint-cli2`, 180 character limit.

  ```sh
  npx markdownlint-cli2 "**/*.md"
  ```

- **Binaries:** tracked with Git LFS; the covered types and any deliberate exceptions are listed in
  [AGENTS.md](AGENTS.md). Run `git lfs install` once per machine before cloning; in a clone made without it, run
  `git lfs install` and then `git lfs pull` to download the files.
- **Agent instructions:** in [AGENTS.md](AGENTS.md), read by Claude Code, Codex and any other coding agent.
  `CLAUDE.md` only imports it, so edit `AGENTS.md`.

## Author

Bert Berrevoets — Berrevoets Systems — <bert@berrevoets.net>
