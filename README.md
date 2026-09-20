# Meridian / INTERLIMEN

Meridian is the .NET application behind **INTERLIMEN**, an immigration analytics platform built around public FOIA data. It provides dashboards for immigration courts, judges, detention, and ICE enforcement activity, with organization accounts and trial access.

The project is not affiliated with ICE or EOIR. Source datasets are not included in this repository.

## Features

- Dashboard with arrest, removal, detention, and nationality summaries.
- Judge analytics, court backlog views, and case search.
- Detention facility profiles, population trends, detainers, and detention stays.
- Enforcement actions, removals by nationality, monthly trends, and an interactive arrests map.
- Organization registration, cookie authentication, email confirmation, password recovery, and team management.
- Trial and subscription expiry checks.
- Public landing, pricing, contact, terms, and privacy pages.

Alert configuration and Stripe billing management are currently placeholder pages.

## Technology and structure

The application targets .NET 8 and uses ASP.NET Core Blazor with interactive server rendering, MudBlazor 6.21, Entity Framework Core with PostgreSQL, ASP.NET Core Identity, MediatR, and FluentValidation. Charts use Blazor-ApexCharts; the arrests map uses Leaflet. Email is sent through Resend. The importer uses ClosedXML and CsvHelper.

| Project | Responsibility |
| --- | --- |
| `src/Meridian.Domain` | Entities, repository contracts, and shared domain types |
| `src/Meridian.Application` | Queries, handlers, validation, and logging behaviors |
| `src/Meridian.Infrastructure` | PostgreSQL persistence, migrations, repositories, and Identity users |
| `src/Meridian.Web` | Blazor pages, layouts, authentication endpoints, and email services |
| `src/Meridian.Importer` | Command-line data imports and trial account provisioning |
| `tests/Meridian.Tests.Unit` | Unit test project scaffold |
| `tests/Meridian.Tests.Integration` | Integration test project scaffold |

## Local development

### Prerequisites

- .NET 8 SDK.
- A running PostgreSQL server and a database/user for local development.
- The `dotnet-ef` 8.x tool for database migrations.
- A Resend API key and verified sender for email workflows.
- Source data files if you want populated analytics rather than an empty database.

Run the following commands from the repository root unless indicated otherwise.

### 1. Restore dependencies

```sh
dotnet restore Meridian.sln
```

### 2. Configure the web application

The web project already has a user-secrets ID. Set local values with:

```sh
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' \
  'Host=localhost;Port=5432;Database=meridian;Username=meridian;Password=YOUR_LOCAL_PASSWORD' \
  --project src/Meridian.Web
dotnet user-secrets set 'BaseUrl' 'http://localhost:5217' --project src/Meridian.Web
dotnet user-secrets set 'Resend:ApiKey' 'YOUR_RESEND_API_KEY' --project src/Meridian.Web
```

Replace the placeholders with your own development values. Keep credentials out of committed configuration files. An ignored `appsettings.Development.json` is another option; `appsettings.Local.json` is ignored by Git but is not explicitly loaded by the application.

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string; environment variable equivalent: `ConnectionStrings__DefaultConnection` |
| `DATABASE_URL` | PostgreSQL URL; takes precedence over the connection string and enables required SSL in the current parser |
| `BaseUrl` | Base URL used in account emails; set it to your local or deployed application URL |
| `Resend:ApiKey` | Resend API key; environment variable equivalent: `Resend__ApiKey` |
| `RESEND_API_KEY` | Fallback only when `Resend:ApiKey` is null; an empty configured value prevents this fallback |

For local PostgreSQL without TLS, use `ConnectionStrings:DefaultConnection` and ensure `DATABASE_URL` is unset. The runtime database provider is PostgreSQL, despite the web project's SQLite package reference.

Email sender addresses and notification recipients are currently specified in `src/Meridian.Web/Services/EmailServices.cs`. Adjust them for your environment before exercising email workflows.

### 3. Apply database migrations

Install the EF tool if needed:

```sh
dotnet tool install --global dotnet-ef --version '8.*'
```

Apply the checked-in migrations with the web application's development configuration:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project src/Meridian.Infrastructure \
  --startup-project src/Meridian.Web
```

Migrations live in `src/Meridian.Infrastructure/Migrations`. The web application does **not** automatically apply migrations at startup.

### 4. Start the application

```sh
dotnet run --project src/Meridian.Web --launch-profile Meridian
```

Open [localhost:5217](http://localhost:5217). Register at `/account/register`, then sign in at `/account/login` to access `/dashboard`. Analytics require imported data.

The checked-in launch profile includes `DOTNET_RUNTIME_ID=osx-arm64`. To run without that profile:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/Meridian.Web --no-launch-profile --urls http://localhost:5217
```

The environment-variable syntax in this README is for POSIX shells such as Bash or Zsh; use the equivalent syntax in PowerShell when developing on Windows.

## Importing data

The importer loads `appsettings.json` from its working directory, so run it from `src/Meridian.Importer`. It uses its own configuration; the web project's user secrets do not configure it.

```sh
cd src/Meridian.Importer
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=meridian;Username=meridian;Password=YOUR_LOCAL_PASSWORD'
export ImportSettings__DataPath='/absolute/path/to/source-data'
export ImportSettings__CsvPath='/absolute/path/to/source-data/csv'
dotnet run -- removals
```

`ImportSettings:DataPath` is required. `ImportSettings:CsvPath` defaults to a `csv` subdirectory of that path when absent. `DATABASE_URL` also takes precedence in the importer.

**Every importer invocation applies database migrations before dispatching the command, including help and account provisioning.** Check the target database before running it.

| Command | Input / action |
| --- | --- |
| `removals` | Imports `ice_removals_*.csv` from the CSV directory |
| `detention` | Imports `detention-facility-daily-population*.xlsx` from the data directory |
| `ddp-arrests` | Imports arrest workbooks from configured release subdirectories |
| `ddp-removals` | Imports removal workbooks |
| `ddp-detentions` | Imports detention workbooks |
| `ddp-detainers` | Imports detainer workbooks |
| `eoir` | Imports EOIR lookup and proceeding CSV files |
| `provision` | Creates an organization and an email-confirmed owner with a 14-day Pro trial |

The DDP commands expect specific release folder names and workbook layouts defined in `src/Meridian.Importer/Program.cs`. The EOIR command currently uses a hard-coded path under `~/MeridianData/imports/EOIR_April2026/EOIR Case Data 2026-0501`, rather than `ImportSettings:DataPath`. Review the relevant import function before preparing a new release.

For a local account without the registration email flow, run from the importer directory:

```sh
dotnet run -- provision 'Example Firm' 'Test' 'User' 'test@example.com' 'LOCAL_TEST_PASSWORD'
```

Use a development-only password satisfying Identity's policy: at least eight characters, including a digit and an uppercase letter. The command accepts the password as a process argument.

Raw spreadsheets and CSV files are ignored by Git. Importing records is separate from schema migration; applying migrations alone does not populate the analytics.

## Build and tests

```sh
dotnet build Meridian.sln
dotnet test Meridian.sln
```

Both test projects currently contain only empty placeholder tests. A passing test run does not establish functional coverage. For UI changes, also check sign-in, dashboard loading, navigation at desktop and mobile widths, charts, and map interactions against a configured development database.

## Deployment

The root `Dockerfile` builds and publishes the web project using .NET 8 and exposes port 8080. It does not package an importer workflow or a PostgreSQL server.

```sh
docker build -t meridian .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection \
  -e Resend__ApiKey \
  -e BaseUrl \
  meridian
```

Export these environment variables in your shell before running the container, using a PostgreSQL host reachable from the container. Alternatively, pass `DATABASE_URL` for a hosted database. Apply migrations separately before starting the deployed application.

`railway.toml` configures Railway to use the Dockerfile and start `Meridian.Web.dll`, with up to three retries on failure. Configure the same database, email, and base URL settings in the deployment environment. Blazor's interactive server UI requires a working persistent SignalR connection through the hosting proxy.

## Development notes

- Authenticated routes render interactively so layout controls, including the mobile navigation drawer, can process clicks. Public routes retain static rendering with interactive components where specified.
- MudBlazor 6.21's theme provider includes a popover provider. Keep one theme provider per active layout and avoid adding a second popover provider.
- EF Core contexts must not be used by overlapping database operations. The layout uses a separate service scope for account and subscription lookups so it does not share the page's context during initialization.
- Authentication cookies are issued and cleared by the `/auth/login` and `/auth/logout` HTTP POST endpoints.
- Fonts, Leaflet assets, and map tiles depend on external services. The arrests map uses OpenStreetMap's standard tiles without an API key; its URL and required attribution are defined in `src/Meridian.Web/wwwroot/js/arrests-map.js`. The community tile service is best-effort and subject to the [tile usage policy](https://operations.osmfoundation.org/policies/tiles/), including no bulk downloads or offline prefetching. For heavy traffic or guaranteed availability, use a dedicated provider or self-hosted tiles.
