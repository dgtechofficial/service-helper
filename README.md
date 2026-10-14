# PostgreSQL & Redis Manager

This Windows Forms utility manages PostgreSQL and Redis services and launches configured Node.js and PHP applications.

## Configuration defaults
- PostgreSQL `pg_ctl.exe`: `D:\postgreSQL\bin\pg_ctl.exe`
- PostgreSQL data directory: `D:\postgreSQL\data`
- PostgreSQL port: `5432`
- Redis Windows service: `Redis`
- Redis executables: `D:\Software\Redis\redis-server.exe` and `D:\Software\Redis\redis-cli.exe`
- Redis port: `6379`

An existing `appsettings.json` beside the application takes precedence over these defaults. Use each service's Settings button to update its paths, host, port, or service name.

## Project layout
```text
Models/     Configuration models
Services/   Configuration persistence, TCP checks, PostgreSQL process execution, Redis service control, app processes
UI/         Services, Node apps, PHP apps and Settings tabs; app launchers and settings subtabs
```

## Run
Install the .NET 10 SDK for Windows, open PowerShell in this folder, then run:
```powershell
dotnet restore
dotnet run
```

## Publish a standalone Windows app
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
Published files will be in `bin\Release\net10.0-windows\win-x64\publish\`.

## Important notes
- The Services tab controls PostgreSQL and Redis. The Node apps and PHP apps tabs run each configured command in its project directory and stream process output into the app. Settings contains separate PostgreSQL, Redis, Node apps and PHP apps subtabs.
- Add Node app directories in Settings. Each directory must exist and contain `package.json`; the configured package manager must be installed and available through the Windows `PATH` inherited by the manager.
- Add PHP app directories in Settings. Each directory must exist and contain a `spark` file. The default command is `php spark serve`; PHP must be available through the Windows `PATH` inherited by the manager. The command can be customized per app.
- Each Node and PHP app has independent Start and Stop controls, so several apps can run concurrently and be stopped or restarted individually.
- Node and PHP app processes are stopped with their child process tree when stopped from the UI. When closing the manager with apps running, choose whether to stop them and close or keep the manager open.
- PostgreSQL is started/stopped with `pg_ctl.exe` and the configured data directory. The app checks the TCP endpoint and verifies the executable and data directory before starting.
- Redis is controlled as a Windows service. If Windows denies access, the app reports the underlying service error and recommends running as Administrator when appropriate.
- Start both and stop both actions are available. Stopping services can disconnect dependent applications; stop actions require confirmation.
- Settings are saved to `appsettings.json` beside the application.
- A reachable TCP port proves only that a TCP connection was accepted; it does not authenticate or execute a database command.

## Validation
The project targets `net10.0-windows` and builds with `dotnet build` on Windows with the .NET 10 SDK. Service and Node app start/stop actions were not exercised as part of the source build because they change local machine state.

The project build was checked with `dotnet build PostgresRedisManager.csproj --no-restore`. App/service start and stop actions were not exercised as part of that source build because they change local machine state.

Verified against the current workspace files on 2026-10-14. Git branch/commit information was unavailable in this workspace.

## creation

run : dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:SatelliteResourceLanguages=en