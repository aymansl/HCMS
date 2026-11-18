# HCMS — Health Centers Management System

Graduation project (Software Engineering, Syrian Private University).
An ASP.NET Core MVC system for small and medium health centers, with a separate Python service for no-show prediction.

- **Graduation 1 (current code):** appointments, medical records, prescriptions, pharmacy and inventory, invoices, complaints and surveys, for the roles Admin, Doctor, Patient and Pharmacist.
- **Graduation 2 (in progress):** Laboratory, Radiology and Emergency departments, shared services and assistive AI. See [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md).

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0.x (latest patch recommended) |
| SQL Server | SQL Server Express (or any SQL Server instance) |
| Visual Studio | 2022 17.x or later (optional) |
| Python | 3.11 with `pipenv` (only for the AI service) |

## Setup

All commands are for **PowerShell**, run from the repository root.

### 1. Restore tools and packages

```powershell
dotnet tool restore
dotnet restore HCMS4.sln
```

### 2. Configure secrets

Secrets are never stored in tracked files. In development they live in .NET **User Secrets** (outside the repository).
`src/HCMS4/appsettings.Development.example.json` shows every key that is expected.

```powershell
# Database connection (replace the server and database names)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=.\SQLEXPRESS;Initial Catalog=HCMS;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/HCMS4

# Initial admin account (required: the app refuses to start without Email and Password).
# If the admin already exists, use its email; its password is not changed.
dotnet user-secrets set "AdminSettings:Email" "<admin email>" --project src/HCMS4
dotnet user-secrets set "AdminSettings:UserName" "<admin user name>" --project src/HCMS4
dotnet user-secrets set "AdminSettings:Password" "<strong password>" --project src/HCMS4

# AI service key (must equal HCMS_AI_API_KEY on the AI service; without it the app uses the local estimate)
dotnet user-secrets set "AiService:ApiKey" "<api key>" --project src/HCMS4

# Check which keys are set (prints values — do not share the output)
dotnet user-secrets list --project src/HCMS4
```

Outside development, use environment variables with `__` as the separator, for example `ConnectionStrings__DefaultConnection`.

### 3. Database

The app applies pending EF Core migrations and seeds the roles and the admin account automatically on start.
To manage migrations manually:

```powershell
dotnet ef migrations list --project src/HCMS4
dotnet ef database update --project src/HCMS4
```

### 4. Run the web app

```powershell
dotnet run --project src/HCMS4
```

Then open `https://localhost:7023`. In Visual Studio, open `HCMS4.sln` and run the `HCMS4` profile.

### 5. Run the AI service (optional)

The web app keeps working without it and falls back to a local estimate.
The service refuses to start without `HCMS_AI_API_KEY` and rejects requests without a matching `X-API-Key` header.

```powershell
cd ai-service
pipenv install
$env:HCMS_AI_API_KEY = "<api key>"
cd scripts
pipenv run python predict.py
```

The service listens on `http://127.0.0.1:9696` (`AiService:BaseUrl`), with debug mode off.

## Time zone

All dates and times are stored and shown in **Damascus time (UTC+3)**. Code reads the time from
`ClinicTime` / `IClinicClock` (`src/HCMS4/Models/Common/ClinicTime.cs`), never from `DateTime.Now` or `DateTime.UtcNow`,
and SQL column defaults use the same zone.

## Tests

```powershell
dotnet build HCMS4.sln
dotnet test HCMS4.sln
```

With coverage:

```powershell
dotnet test HCMS4.sln --collect:"XPlat Code Coverage"
```

## Branch and commit rules

- One branch: `main`. Every commit must build with 0 errors and pass all tests.
- Commit messages: `<type>(<area>): <description> (<CODE>)`, for example `feat(lab): receive lab request from doctor (L3)`.
  Types: `feat`, `fix`, `refactor`, `test`, `chore`, `docs`, `build`.
- The last commit of each increment is tagged `v2.N`.

## Project structure

```
HCMS4_FINAL/
├── HCMS4.sln
├── Directory.Build.props      shared build settings
├── .editorconfig              code style
├── .config/dotnet-tools.json  local tools (dotnet-ef)
├── src/HCMS4/                 ASP.NET Core MVC web app
├── tests/HCMS4.Tests/         xUnit tests
├── ai-service/                Python no-show prediction service
└── docs/                      requirements, diagrams, analysis and guides
```
