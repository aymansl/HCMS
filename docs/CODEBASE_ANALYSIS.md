# HCMS4 — Hospital Care Management System: Comprehensive Codebase Analysis

> **Purpose of this document.** It is a complete, AI-readable map of the HCMS4 repository: architecture, startup pipeline, data model, business rules, every controller action and service method, the Razor UI layer, the no-show ML microservice contract, the test suite, and a catalogue of known defects and risks. It is intended to let another engineer or AI agent (or a fresh chat session) understand the implementation without re-reading the source.
>
> **Verified against source** on 2026-10-21 using .NET SDK `10.0.103`. `dotnet build HCMS4.sln` → 0 errors. `dotnet test HCMS4.Tests/HCMS4.Tests.csproj` → **44 passed, 0 failed, 0 skipped**.

---

## Table of Contents

1. [Repository Layout](#1-repository-layout)
2. [Technology Stack](#2-technology-stack)
3. [Build, Run, and Test](#3-build-run-and-test)
4. [Configuration](#4-configuration)
5. [Startup Pipeline (`Program.cs`)](#5-startup-pipeline-programcs)
6. [Dependency Injection Registry](#6-dependency-injection-registry)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [Data Model](#8-data-model)
9. [Database Schema, Migrations &amp; Seed Data](#9-database-schema-migrations--seed-data)
10. [Business Rules Catalogue](#10-business-rules-catalogue)
11. [Service Layer](#11-service-layer)
12. [Controller Layer](#12-controller-layer)
13. [ViewModels](#13-viewmodels)
14. [Razor Views &amp; UI](#14-razor-views--ui)
15. [Front-end Assets](#15-front-end-assets)
16. [ML No-Show Prediction Microservice](#16-ml-no-show-prediction-microservice)
17. [Test Suite](#17-test-suite)
18. [Key End-to-End Workflows](#18-key-end-to-end-workflows)
19. [Known Issues, Defects &amp; Risks](#19-known-issues-defects--risks)
20. [Recommended Next Steps](#20-recommended-next-steps)
21. [Appendix A — Complete File Map](#appendix-a--complete-file-map)

---

## 1. Repository Layout

```
HCMS4_FINAL/
├── HCMS4.sln                       # Visual Studio solution — contains ONLY the web app
├── HCMS4/                          # ASP.NET Core MVC web application (net6.0)
│   ├── Program.cs                  # Top-level-statements startup
│   ├── HCMS4.csproj                # Main project file
│   ├── appsettings.json            # Connection string + NoShow API config
│   ├── appsettings.Development.json
│   ├── dotnet-tools.json           # Local dotnet-ef 6.0.35 tool manifest
│   ├── Areas/Identity/Pages/       # Scaffolded ASP.NET Identity Razor Pages
│   │   └── Account/                #   Login, Logout, Register, Manage/*, AccessDenied...
│   ├── Controllers/                # 7 controllers
│   ├── Data/
│   │   ├── ApplicationDbContext.cs # EF Core DbContext (360 lines)
│   │   └── Migrations/             # 24 migrations + 23 Designer files + model snapshot
│   ├── Models/                     # 36 entity/enum files (incl. Models/Common)
│   ├── Models/Common/              # BusinessRules, Pagination, ServiceResult, helpers
│   ├── Properties/                 # launchSettings.json, serviceDependencies*.json
│   ├── Services/                   # 18 files: 10 interfaces + 10 implementations + background service + seeder
│   ├── ViewModels/                 # 61 files (74 class declarations)
│   ├── Views/                      # 82 .cshtml (Admin, Doctor, Patient, Pharmacist, Account, Home, Shared)
│   └── wwwroot/                    # CSS/JS/vendor libs/images
├── HCMS4.Tests/                    # xUnit test project (net10.0) — NOT in the solution
└── medical-appointment-no-shows-main/
    └── medical-appointment-no-shows-main/   # Flask ML microservice (upstream GitHub project)
        ├── scripts/predict.py       # Flask app exposing POST /predict on :9696
        ├── scripts/train.py         # Model training pipeline
        ├── models/*.bin             # Pickled scaler+OneHotEncoder+classifier bundles
        ├── data/KaggleV2-May-2016.csv
        └── Dockerfile, Pipfile, README.md
```

**File counts (verified):** 82 views, 61 ViewModel files, 36 model files, 18 service files, 24 migrations (+ 23 Designer files + model snapshot), 7 controllers, 44 unit tests.

---

## 2. Technology Stack

### Web application (`HCMS4/HCMS4.csproj`)

| Item | Value |
|---|---|
| SDK | `Microsoft.NET.Sdk.Web` |
| Target framework | `net6.0` (EOL — see [Risks](#19-known-issues-defects--risks)) |
| Nullable reference types | enabled |
| Implicit usings | enabled |
| UserSecretsId | `aspnet-HCMS4-A260DD2B-EF83-4531-8B2B-7ADF582829DA` |
| ORM | EF Core 6.0.35 + `Microsoft.EntityFrameworkCore.SqlServer` 6.0.35 |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 6.0.35, `...Identity.UI` 6.0.35 |
| Diagnostics | `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore` 6.0.7 |
| Migrations tooling | `Microsoft.EntityFrameworkCore.Design` 6.0.35, `...Tools` 6.0.35 |
| Razor scaffolding | `Microsoft.VisualStudio.Web.CodeGeneration.Design` 6.0.18 |
| Client-side validation | `jQuery.Validation` 1.21.0, `Microsoft.jQuery.Unobtrusive.Ajax` 3.2.6, `Microsoft.jQuery.Unobtrusive.Validation` 4.0.0 |
| Front-end | Bootstrap 5 (vendored), Bootstrap Icons, Font Awesome Free, AOS, GLightbox, Swiper, PureCounter, jQuery 3.6/3.7 (CDN + local) |

### Test project (`HCMS4.Tests/HCMS4.Tests.csproj`)

| Item | Value |
|---|---|
| SDK | `Microsoft.NET.Sdk` |
| Target framework | `net10.0` (mismatch vs. the app's `net6.0`) |
| Test framework | xUnit 2.9.3 + `xunit.runner.visualstudio` 3.1.4 |
| Test SDK | `Microsoft.NET.Test.Sdk` 17.14.1 |
| DB provider | `Microsoft.EntityFrameworkCore.InMemory` 6.0.0 |
| Mocking | `Moq` 4.18.4 |
| Coverage | `coverlet.collector` 6.0.4 |
| Project reference | `..\HCMS4\HCMS4.csproj` |

### ML microservice

| Item | Value |
|---|---|
| Language | Python |
| Framework | Flask (`app = Flask('noshow')`) |
| Endpoint | `POST /predict` on `0.0.0.0:9696` |
| Model | LightGBM classifier (`LGBMClassifier_tranformers_final.bin`) + `StandardScaler` + `OneHotEncoder` |
| Data | Kaggle "Medical Appointment No Shows" dataset (110k+ rows) |

---

## 3. Build, Run, and Test

```bash
# Build the web app (solution contains only HCMS4)
dotnet build "C:\Users\Asus\Downloads\HCMS4_FINAL\HCMS4.sln"

# Build + run the test project directly (it is NOT in HCMS4.sln)
dotnet build "C:\Users\Asus\Downloads\HCMS4_FINAL\HCMS4.Tests\HCMS4.Tests.csproj"
dotnet test  "C:\Users\Asus\Downloads\HCMS4_FINAL\HCMS4.Tests\HCMS4.Tests.csproj"

# Run the web app (Development profile → https://localhost:7023 ; http://localhost:5023)
dotnet run --project "C:\Users\Asus\Downloads\HCMS4_FINAL\HCMS4\HCMS4.csproj"

# Run the ML service (from the nested repo root, so that ../models resolves)
cd medical-appointment-no-shows-main/medical-appointment-no-shows-main/scripts
python predict.py     # serves on :9696

# EF tooling (local tool manifest at HCMS4/dotnet-tools.json is pinned to 6.0.35, rollForward:false)
cd HCMS4
dotnet tool restore
dotnet ef migrations add <Name> --project HCMS4
```

> The tool manifest lives at `HCMS4/dotnet-tools.json` (not `.config/`), so `dotnet tool restore` must be run from inside the `HCMS4` folder.

**Launch profiles** (`HCMS4/Properties/launchSettings.json`)

| Profile | URL | Environment |
|---|---|---|
| `HCMS4` (Project) | `https://localhost:7023;http://localhost:5023` | `Development` |
| `IIS Express` | `http://localhost:6128` / ssl `44307` | `Development` |

**First-run behaviour:** on startup the app runs `dbContext.Database.Migrate()` and then `AdminSeeder.SeedAdminUser(...)`, so a fresh database is created and populated automatically.

---

## 4. Configuration

### `HCMS4/appsettings.json`

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=DESKTOP-VTA1SQ3\\SQLEXPRESS;Initial Catalog=aspnet-HCMS4T;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "NoShowAPI": {
    "BaseUrl": "http://localhost:9696",
    "ApiKey": ""
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

The file also contains a JSONC comment block instructing operators to configure the admin credentials through User Secrets:

```
dotnet user-secrets set "AdminSettings:Password" "YourSecurePassword"
dotnet user-secrets set "AdminSettings:Email" "admin@hospital.com"
dotnet user-secrets set "AdminSettings:UserName" "admin"
```

> **Security note.** The checked-in connection string points at a developer machine (`DESKTOP-VTA1SQ3\SQLEXPRESS`) and uses `Integrated Security`. It is a local-development artifact and must not be treated as a production credential. `NoShowAPI:ApiKey` is present in config but is **never read** by any code — the Flask service performs no authentication at all.

### `HCMS4/appsettings.Development.json`

Only log-level overrides (`Default: Information`, `Microsoft.AspNetCore: Warning`). No connection-string override, so Development uses the same database as Production.

### `HCMS4/Properties/serviceDependencies.json` / `.local.json`

Empty stubs (139 / 145 bytes) — no external service dependency is wired in.

---

## 5. Startup Pipeline (`Program.cs`)

`HCMS4/Program.cs` uses .NET 6 top-level statements. Execution order:

1. `WebApplication.CreateBuilder(args)`.
2. **EF Core** — `AddDbContext<ApplicationDbContext>` bound to `ConnectionStrings:DefaultConnection` with `UseSqlServer`.
3. **MVC + Razor Pages** — `AddControllersWithViews()` and `AddRazorPages()` (Identity UI needs the latter).
4. **Session** — 30-minute idle timeout; cookie `HttpOnly`, `IsEssential`, `SameSite=Strict`.
5. **Identity** — `AddIdentity<ApplicationUser, IdentityRole>`:
   - `Password.RequireDigit = true`, `RequiredLength = 6`, `RequireNonAlphanumeric = false`, `RequireUppercase = true`, `RequireLowercase = true`
   - `Lockout.DefaultLockoutTimeSpan = 5 min`, `MaxFailedAccessAttempts = 5`
   - `User.RequireUniqueEmail = true`
   - `AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders()`
6. **Application cookie** — `LoginPath = "/Account/Login"`, `AccessDeniedPath = "/Account/AccessDenied"`, `ExpireTimeSpan = 60 min`, `SlidingExpiration = true`.
7. **Authorization policies** — `AdminOnly`, `DoctorOnly`, `PatientOnly`, `AdminOrDoctor` (see [§7](#7-authentication--authorization) for the fact that none of them are consumed).
8. **Service registration** — see [§6](#6-dependency-injection-registry).
9. **Hosted service** — `AddHostedService<DailyReportBackgroundService>()`.
10. **Typed HTTP client** — `AddHttpClient("NoShowAPI")` with a 30 s timeout.
11. **Global culture** — `CultureInfo.DefaultThreadCurrentCulture` / `...UICulture` forced to `en-US` (dates, currency and number formatting are therefore US-style throughout the UI).
12. **Middleware pipeline**
    - Development → `UseDeveloperExceptionPage()`; otherwise `UseExceptionHandler("/Home/Error")` + `UseHsts()`
    - `UseHttpsRedirection()` → `UseStaticFiles()` → `UseRouting()` → **`UseSession()`** → `UseAuthentication()` → `UseAuthorization()`
13. **Routing** — default `{controller=Home}/{action=Index}/{id?}` + `MapRazorPages()`.
14. **Bootstrap block** — creates a DI scope, calls `dbContext.Database.Migrate()`, then `await AdminSeeder.SeedAdminUser(scope.ServiceProvider)`.
15. `app.Run()`.

> ⚠️ There is **no global authorization filter** (`options.FallbackPolicy` / `RequireAuthenticatedUser` on `AddControllersWithViews`). Anonymous access is therefore the default; protection relies entirely on `[Authorize]` attributes on controllers/actions.

---

## 6. Dependency Injection Registry

All in `Program.cs` lines 59–74.

| Service | Implementation | Lifetime |
|---|---|---|
| `IDailyReportService` | `DailyReportService` | Scoped |
| `IPatientService` | `PatientService` | Scoped |
| `IAppointmentService` | `AppointmentService` | Scoped |
| `IPrescriptionService` | `PrescriptionService` | Scoped |
| `IDrugService` | `DrugService` | Scoped |
| `IPharmacistService` | `PharmacistService` | Scoped |
| `ILeaveService` | `LeaveService` | Scoped |
| `INotificationService` | `NotificationService` | Scoped |
| `IActivityLogService` | `ActivityLogService` | Scoped |
| `INoShowRiskService` | `NoShowRiskService` | Scoped |
| `DailyReportBackgroundService` | `BackgroundService` | Hosted (singleton) |
| `HttpClient` | named client `"NoShowAPI"` (30 s timeout) | Factory-managed |

`AdminSeeder` is a static helper (not DI-registered) and is invoked directly during startup.

---

## 7. Authentication & Authorization

### Roles

`AdminSeeder` creates four `IdentityRole` rows if missing: **Admin, Doctor, Patient, Pharmacist**. Roles are assigned when accounts are created (self-registration → `Patient`; admin-created doctor/pharmacist → respective role; seeded admin → `Admin`).

### Authorization attributes actually used

| Controller | Class-level attribute | Per-action overrides |
|---|---|---|
| `HomeController` | *(none — public)* | — |
| `AccountController` | *(none — public)* | — |
| `AdminController` | `[Authorize(Roles = "Admin,Doctor")]` | `[Authorize(Roles = "Doctor")]` on `DailyVisitingPatientsReport`; `[Authorize(Roles = "Admin")]` on `Complaints`, `ComplaintDetails`, `UpdateComplaintStatus`, `Surveys`, `CreateSurvey` |
| `DoctorController` | `[Authorize(Roles = "Doctor")]` | — |
| `PatientController` | `[Authorize(Roles = "Patient")]` | (redundant per-action `[Authorize(Roles="Patient")]` on most actions) |
| `PharmacistController` | `[Authorize(Roles = "Pharmacist")]` | — |

> ⚠️ **Privilege-escalation risk.** Because `AdminController` is `[Authorize(Roles = "Admin,Doctor")]`, **any authenticated Doctor can reach most admin endpoints** — `/Admin/Patients`, `/Admin/EditPatient`, `/Admin/CreateDoctor`, `/Admin/ManageAppointments`, `/Admin/GenerateInvoice`, `/Admin/UpdatePaymentStatus`, `/Admin/PurchaseRequests`, `/Admin/ApprovePurchaseRequest`, `/Admin/Suppliers`, `/Admin/PriceList`, `/Admin/RegisterDoctorLeave`, `/Admin/DeleteUser`, etc. Only complaints/survey actions are Admin-only. This is the highest-impact authorization finding in the codebase.

### Named policies (registered but unused)

`AdminOnly`, `DoctorOnly`, `PatientOnly`, `AdminOrDoctor` are registered in `AddAuthorization` but **no controller or action references them** (verified by grep). They are dead configuration.

### Cookie configuration

- Login path: `/Account/Login` → `AccountController.Login` (custom MVC controller, `Views/Account/Login.cshtml`).
- Access-denied path: `/Account/AccessDenied` → **no such action exists** in `AccountController` and no `Views/Account/AccessDenied.cshtml` exists. The Identity scaffold page at `Areas/Identity/Pages/Account/AccessDenied.cshtml` is served at `/Identity/Account/AccessDenied`, not at the configured path. A role-denied request therefore yields a 404 rather than a friendly page.
- Identity scaffold also provides a separate login at `/Identity/Account/Login` (`Areas/Identity/Pages/Account/Login.cshtml`), so there are **two parallel login UIs**.
- **Lockout is configured but never armed** — both `AccountController.Login` and the scaffolded `Areas/Identity/Pages/Account/Login.cshtml.cs` call `PasswordSignInAsync(..., lockoutOnFailure: false)`, so the 5-attempt / 5-minute lockout policy never takes effect.

### `BaseController` (`Controllers/BaseController.cs`)

Abstract base exposing `protected readonly ApplicationDbContext _context` and `ILogger _logger`, plus:

| Helper | Behaviour |
|---|---|
| `GetCurrentPatientIdAsync()` | Reads `ClaimTypes.NameIdentifier` → looks up `Patients.UserId` → returns `Patient.Id` or `null` |
| `GetCurrentDoctorIdAsync()` | Same, against `Doctors` |
| `GetCurrentUserAsync()` | Looks up `Users` by `UserName` **or** `Email` |

A block of convenience helpers (`HandleError`, `HandleErrorWithView`, `HandleNotFound`, `HandleSuccess`, `AddErrors`, `AddError`) is present but **commented out**.

`AccountController` and `PharmacistController` do **not** derive from `BaseController`; they inject `ApplicationDbContext` directly.

---

## 8. Data Model

All entities live in namespace `HCMS4.Models` (folder `HCMS4/Models`). Shared primitives live in `HCMS4.Models.Common`.

### 8.1 Identity

**`ApplicationUser : IdentityUser`**
| Property | Type | Notes |
|---|---|---|
| `FirstName` | `string` | Required, max 50 |
| `LastName` | `string` | Required, max 50 |
| `FullName` | `string` | Computed → `$"{FirstName} {LastName}"` |
| `Doctor` / `Patient` / `Pharmacist` | nav? | 1:1 back-references to the profile tables |

### 8.2 Core entities

**`Patient`** — `Id`, `UserId` (FK → `Users`, required), `User`, `DateOfBirth` (nullable), `Address` (≤200), `EmergencyContact` (≤20), `ChronicConditions` (≤500), `Status` (`PatientStatus`: Active / Disabled, default Active), `NoShowCount` (int, default 0), `DisableReason`, `DisabledAt`.
Collections: `Appointments`, `Prescriptions`, `Invoices`.

**`Doctor`** — `Id`, `UserId` (FK → `Users`, required), `User`, `SpecializationId?` (FK → `Specialization`, `SetNull` on delete), `Specialization`, `SpecializationName` (computed → "Not specified"), `Qualifications` (≤500), `ContactInfo` (≤50, phone), `IsAvailable` (bool, default `true`), `AverageRating` (`decimal(3,2)`, 0–5), `RatingCount` (int), `CanPublishArticles` (bool, default `true`).
Collections: `Appointments`, `Prescriptions`, `DoctorLeaves`, `MedicalArticles`. Computed `FullName`.

**`Pharmacist`** — `Id`, `UserId` (FK → `Users`), `User`, `Qualifications` (≤500), `ContactInfo` (≤20), `CreatedAt`, `LastLoginAt`, `IsActive` (default `true`), `Shift` (string). Computed `FullName`.

**`Appointment`**
| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `PatientId` | int | Required FK |
| `DoctorId` | int | Required FK |
| `AppointmentDateTime` | `DateTime` | Required; validated by nested `FutureDateAttribute` (must be > `DateTime.Now`) |
| `Status` | `AppointmentStatus` | Scheduled / Completed / Canceled (default Scheduled) |
| `CancellationReason` | `string?` | ≤500 |
| `CreatedAt` / `UpdatedAt` | `DateTime?` | Default SQL `GETUTCDATE()` on `CreatedAt` |
| `ConsultationFee` | `decimal` | 0–10 000, currency |
| `WasNoShow` | `bool` | default `false` — no-show flag |
| `NoShowEvaluatedAt` | `DateTime?` | |
| `NoShowRiskScore` | `double?` | 0–1 — cached ML risk score |

Nested `Appointment.FutureDateAttribute : ValidationAttribute` rejects dates `<= DateTime.Now` with "Appointment must be in the future".

**`Prescription`** — `Id`, `PatientId`, `DoctorId` (both required), `AppointmentId?` (FK, `SetNull` on delete), `PrescriptionDate` (default `GETUTCDATE()`), `Notes` (required, ≤1000), `Status` (`PrescriptionStatus`: Pending / Completed / Canceled), `PrescriptionItems` collection, `DispensedDate?`, `DispensedBy?` (≤100), `TotalCost` (computed → Σ `Quantity × Drug.Price`), `MedicationTotal?` (0–10 000), `CreatedAt`, `UpdatedAt`, `DispensingNotes?` (≤500).

**`PrescriptionItem`** — `Id`, `PrescriptionId`, `DrugId?` (nullable despite `[Required]`), `DrugName` (≤100), `Dosage` (≤50), `Duration` (≤50), `Frequency` (≤50), `Quantity` (1–1000), `Instructions` (≤500), `Prescription`, `Drug`.

**`Drug`** — `Id`, `Name` (≤100), `Supplier?` (≤100), `Price` (0.01–10 000, currency), `Quantity` (0–10 000), `ExpiryDate` (required; nested `FutureDateAttribute`), `Description?` (≤500), `CreatedAt`, `UpdatedAt`, `PrescriptionItems` collection.
Computed `ExpiryStatus`: `"Expired"` (≤0 d), `"Expiring Soon"` (≤30 d), `"Expiring Shortly"` (≤60 d), else `"Valid"` — computed against `DateTime.Now`.

**`ClinicalNote`** — `Id`, `PatientId`, `DoctorId`, `Date` (default `GETUTCDATE()`), `NoteType` (≤50), `Content` (required, ≤2000), `Diagnosis?` (≤500), `Patient`, `Doctor`.

**`Invoice`** — `Id`, `PatientId` (required), `AppointmentId?`, `PrescriptionId?` (both `SetNull` on delete), `InvoiceDate` (default `GETUTCDATE()`), `ConsultationFee` (0–10 000), `MedicationTotal` (0–10 000), `TotalAmount` (0.01–20 000), `PaymentStatus` (`PaymentStatus`: Pending / Paid), `PaymentDate?`, `AmountPaid?`, `Notes?` (≤500), `UpdatedAt`, `PharmacistId?` (FK → `Pharmacist`, "Dispensed By"), `DispensedAt?`.

### 8.3 Pharmacy / procurement

**`Specialization`** — `Id`, `Name` (≤100), `Description?` (≤500), `ConsultationFee` (0–10 000), `IsActive` (default `true`), `Doctors` collection. **Seeded with 6 rows** (see §9).

**`Supplier`** — `Id`, `Name` (≤100), `Address?` (≤200), `Phone?` (≤20), `Email?` (≤100), `ContactPerson?` (≤100), `CreatedAt`, `IsActive` (default `true`), `PurchaseRequests` collection.

**`PurchaseRequest`** — `Id`, `RequestNumber` (required), `PharmacistId` (required FK), `SupplierId?` (FK), `RequestDate` (default UTC now), `Status` (`PurchaseRequestStatus`: Pending / Approved / Rejected / Ordered / Received / Cancelled), `TotalAmount`, `Notes?` (≤500), `ApprovedDate?`, `ApprovedBy?` (≤100), `RejectionReason?` (≤500), `ReceivedDate?`, `CreatedAt`, `UpdatedAt`, `Items` collection.

**`PurchaseRequestItem`** — `Id`, `PurchaseRequestId`, `DrugId`, `DrugName` (≤100), `Quantity` (1–10 000), `UnitPrice` (0.01–10 000), `Subtotal` (computed), `Notes?` (≤500).

### 8.4 Scheduling, leave, feedback

**`DoctorLeave`** — `Id`, `DoctorId`, `Doctor`, `StartDate`, `EndDate`, `LeaveType` (`LeaveType`: Sick / Annual / Emergency / Other), `Notes?` (≤500), `Status` (`LeaveStatus`: Pending / Approved / Rejected), `CreatedAt?`, `CreatedBy?`.

**`LeaveRequest`** — `Id`, `DoctorId`, `Doctor`, `StartDate`, `EndDate`, `LeaveType`, `Reason?` (≤500), `Status` (default Pending), `CreatedAt?`.
> Two parallel leave concepts exist: a doctor-submitted `LeaveRequest` and an admin-registered `DoctorLeave`. Approving a `LeaveRequest` materialises a corresponding `DoctorLeave`.

**`VisitRating`** — `Id`, `PatientId`, `DoctorId`, `AppointmentId`, `Rating` (1–5), `Comment?` (≤1000), `DoctorCooperative?`, `WaitingTimeReasonable?`, `CreatedAt` (default `GETUTCDATE()`).

**`Complaint`** — `Id`, `PatientId`, `Title` (≤200), `Type` (`ComplaintType`: Service / Doctor / Appointments / Billing / Other), `Description` (required, ≤2000), `AssociatedVisitDate?`, `AttachmentPath?` (≤500), `TrackingNumber`, `SubmissionDate` (default `GETUTCDATE()`), `Status` (`ComplaintStatus`: Submitted / UnderReview / Resolved / Closed), `ResolvedAt?`, `ResolutionNotes?` (≤1000).

**`PrescriptionReissueRequest`** — `Id`, `PatientId`, `PrescriptionId`, `DoctorId`, `RequestDate` (default `GETUTCDATE()`), `Status` (`ReissueRequestStatus`: Pending / Approved / Rejected / Expired), `DoctorResponse?` (≤500), `ReviewedAt?`.

**`DoctorReviewRequest`** — `Id`, `PharmacistId`, `PrescriptionId`, `DoctorId`, `ReasonForReview` (`ReviewReason`: UnclearDosage / DrugInteraction / MedicationUnavailable / AlternativeMedication / Other), `AdditionalComments?` (≤1000), `SuggestedAlternative?` (≤500), `RequestDate` (default `GETUTCDATE()`), `Status` (`ReviewRequestStatus`: Pending / Responded / Closed), `DoctorResponse?` (≤1000), `ReviewedAt?`.

**`PharmacistPrescriptionNote`** — `Id`, `PrescriptionId`, `PharmacistId`, `NoteText` (required, ≤2000), `NotifyDoctor` (bool, default false), `CreatedAt` (default `GETUTCDATE()`).

**`MedicalArticle`** — `Id`, `DoctorId`, `Title` (≤200), `Summary?` (≤500), `Content` (required), `Category?` (≤100), `Status` (`ArticleStatus`: Draft / Published, default Draft), `CreatedAt`, `UpdatedAt`, `PublishedAt?`, `Doctor`.

### 8.5 Surveys

**`Survey`** — `Id`, `Title` (≤200), `Description?` (≤1000), `StartDate`, `EndDate?`, `TargetAudience` (`SurveyTargetAudience`: AllPatients / SpecificCategory / SpecificPatients), `TargetCriteria?` (≤300), `Status` (`SurveyStatus`: Draft / Active / Closed), `CreatedByUserId` (FK → `Users`, `Restrict`), `CreatedAt`, `UpdatedAt`, `SentAt?`, `Questions`, `Assignments`.

**`SurveyQuestion`** — `Id`, `SurveyId`, `QuestionText` (≤500), `QuestionType` (`SurveyQuestionType`: MultipleChoice / Rating / YesNo / OpenText), `IsRequired`, `DisplayOrder`, `Options`, `Answers`.

**`SurveyQuestionOption`** — `Id`, `SurveyQuestionId`, `OptionText` (≤200), `DisplayOrder`.

**`SurveyAssignment`** — `Id`, `SurveyId`, `PatientId`, `Status` (`SurveyAssignmentStatus`: Pending / Completed), `AssignedAt`, `CompletedAt?`, `Answers`. **Unique index** on `(SurveyId, PatientId)`.

**`SurveyAnswer`** — `Id`, `SurveyAssignmentId`, `SurveyQuestionId`, `AnswerText?` (≤2000), `NumericValue?`, `BooleanValue?`.

### 8.6 Platform entities

**`UserNotification`** — `Id`, `UserId` (FK → `Users`), `Title` (≤200), `Message` (≤1000), `Type` (`NotificationType`: General / AppointmentReminder / Complaint / PrescriptionReissue / DoctorReview / PrescriptionNote / Survey), `LinkUrl?` (≤500), `RelatedEntityType?` (≤100), `RelatedEntityId?`, `IsRead`, `CreatedAt` (default `GETUTCDATE()`). **Index** on `(UserId, IsRead)`.

**`SystemActivityLog`** — `Id`, `UserId?` (≤450), `UserName?` (≤256), `Action` (mapped to column `ActivityType`, ≤100), `EntityType` (≤100), `EntityId?` (mapped to column `RelatedEntityId`, ≤100), `Description` (≤2000), `CreatedAt` (default `GETUTCDATE()`).

**`DailyReport`** — `Id`, `ReportDate`, `TotalInvoices`, `TotalAmount`, `GeneratedAt`, `ReportData?` (JSON blob).

### 8.7 `HCMS4.Models.Common`

| Type | Purpose |
|---|---|
| `BusinessRules` (static) | Centralised constants — see [§10](#10-business-rules-catalogue) |
| `ConcurrencyEntity` (abstract) | `[Timestamp] byte[] RowVersion` — optimistic concurrency base class. **Currently unused**: no entity derives from it, and no `RowVersion` column exists on the domain tables. |
| `PaginatedResult<T>` | `Items`, `TotalCount`, `PageNumber`, `PageSize`, computed `TotalPages`, `HasPreviousPage`, `HasNextPage` |
| `PaginationParams` | `PageNumber` (default 1), `PageSize` (default 20, hard-capped at 100) |
| `ServiceResult` | `Success`, `Message`, `Errors` (List<string>); static `Ok(message)` / `Fail(message, errors)` |
| `ServiceResult<T>` : `ServiceResult` | adds `Data`; static `Ok(data, message)` / `Fail(message, errors)` |
| `RiskLevelHelper` (static) | `GetLevel(score)` → `"high"` / `"medium"` / `"low"`; `Matches(selectedRiskLevel, score)` — `"all"`/blank matches everything |

### 8.8 Relationship map

```
ApplicationUser (Identity) 1──1 Doctor / Patient / Pharmacist
      │
      ├──1:N── Appointment ──N:1── Doctor
      │                 └──N:1── Patient
      ├──1:N── Prescription ──N:1── Doctor / Patient
      │                   └──0..1── Appointment
      │                   └──1:N── PrescriptionItem ──N:1── Drug
      ├──1:N── ClinicalNote ──N:1── Doctor + Patient
      ├──1:N── Invoice ──N:1── Patient
      │                ──0..1── Appointment / Prescription
      │                ──0..1── Pharmacist
      ├──1:N── DoctorLeave ──N:1── Doctor
      ├──1:N── LeaveRequest ──N:1── Doctor
      ├──1:N── VisitRating ──N:1── Patient / Doctor / Appointment
      ├──1:N── PrescriptionReissueRequest ── Patient / Prescription / Doctor
      ├──1:N── DoctorReviewRequest ── Pharmacist / Prescription / Doctor
      ├──1:N── PharmacistPrescriptionNote ── Prescription / Pharmacist
      ├──1:N── MedicalArticle ──N:1── Doctor
      ├──1:N── UserNotification
      └──1:N── Survey (as CreatedByUserId)

Specialization 1──N Doctor
Supplier 1──N PurchaseRequest ──1:N PurchaseRequestItem ──N:1 Drug
Survey 1──N SurveyQuestion ──1:N SurveyQuestionOption
Survey 1──N SurveyAssignment ──N:1 Patient
SurveyAssignment 1──N SurveyAnswer ──N:1 SurveyQuestion
```

**Delete behaviours** — `Patient` → appointments/prescriptions/invoices = **Cascade**; `Doctor` → appointments/prescriptions = **Restrict**; `PrescriptionItem` → `Drug` = **Restrict** (prevents deleting a drug referenced by a prescription); `PrescriptionItem` → `Prescription` = **Cascade**; `Appointment`/`Prescription` → `Invoice` = **SetNull**; `Doctor` → `Specialization` = **SetNull**.

---

## 9. Database Schema, Migrations & Seed Data

`HCMS4/Data/ApplicationDbContext.cs` (360 lines) defines **29 `DbSet`s** and the full fluent configuration. It contains **no** `SaveChanges` override, **no** global query filters, and **no** value converters.

### 9.1 Migrations (24 migrations + model snapshot)

`HCMS4/Data/Migrations/` holds 48 `.cs` files: **24 migration classes**, 23 `.Designer.cs` snapshot files and 1 `ApplicationDbContextModelSnapshot.cs`. The `<identity>` migration is hand-written and has no `.Designer.cs` companion.

| # | Migration | Purpose |
|---|---|---|
| 1 | `00000000000000_CreateIdentitySchema` | ASP.NET Identity tables |
| 2 | `20251204090228_Init` | Core domain schema |
| 3 | `20251208073926_AddIsAvailableToDoctor` | `Doctor.IsAvailable` |
| 4 | `20251210133141_AddingFieldsToPresc` | Prescription fields |
| 5 | `20251210172704_AddConsultationFeeAndMedicationTotal` | Invoice money split |
| 6 | `20251211062230_AddingFieldsToInvoice` | Invoice extras |
| 7 | `20251218121627_AddingFieldsToPatient` | Patient extras |
| 8 | `20251221145636_FixClinicalNoteValidation` | ClinicalNote constraint fixes |
| 9 | `20260319065424_AddPharmacistTable` | Pharmacist profile table |
| 10 | `20260319065919_EditedPharmacistTable` | Pharmacist tweaks |
| 11 | `20260321094049_EditingInvoiceAndPrescriptionModels` | Model tweaks |
| 12 | `20260321162610_PhInvoice` | `Invoice.PharmacistId` / `DispensedAt` |
| 13 | `20260321163637_DailyReportTable` | `DailyReports` table |
| 14 | `20260322170159_DrugPurchase` | Purchase request tables |
| 15 | `20260407192731_Specialization` | `Specializations` table |
| 16 | `20260416140527_AddIncrement5Features` | Surveys, notifications, activity logs, `NoShowRiskScore`/`WasNoShow` |
| 17 | `20260422110016_AddRejectionReasonToPurchaseRequest` | `PurchaseRequest.RejectionReason` |
| 18 | `20260505114205_Increment6` | Feature bundle |
| 19 | `20260505120000_AddMissingTables` | Feature bundle |
| 20 | `20260506120000_AddDoctorMissingColumns` | Doctor columns |
| 21 | `20260506153000_AddNotificationMetadataColumns` | `UserNotification` metadata |
| 22 | `20260506154500_AddActivityLogMetadataColumns` | `SystemActivityLog` metadata |
| 23 | `20260506170000_AddSurveyMetadataColumns` | `Survey` metadata |
| 24 | `20260506183000_AddSurveyAnswerAnsweredAt` | `SurveyAnswer.AnsweredAt` |
| — | *(snapshot)* `ApplicationDbContextModelSnapshot.cs` | Current model snapshot (61 KB) — not a migration |

### 9.2 Seed data

`modelBuilder.Entity<Specialization>().HasData(...)` seeds 6 specializations:

| Id | Name | ConsultationFee | Description |
|---|---|---|---|
| 1 | Cardiology | 150 |قلبية |
| 2 | Dermatology | 120 | جلدية |
| 3 | Orthopedics | 130 | عظمية |
| 4 | Neurology | 180 | عصبية |
| 5 | Pediatrics | 110 | أطفال |
| 6 | General Medicine | 100 | طب عام |

### 9.3 SQL Server defaults

`GETUTCDATE()` is the default for `Appointment.CreatedAt`, `Drug.CreatedAt`, `Prescription.PrescriptionDate`, `ClinicalNote.Date`, `Invoice.InvoiceDate`, `VisitRating.CreatedAt`, `PrescriptionReissueRequest.RequestDate`, `DoctorReviewRequest.RequestDate`, `PharmacistPrescriptionNote.CreatedAt`, `MedicalArticle.CreatedAt`, `Survey.CreatedAt`, `UserNotification.CreatedAt`, `SystemActivityLog.CreatedAt`.

### 9.4 Admin seeding (`Services/AdminSeeder.cs`)

1. Ensures the roles **Admin, Doctor, Patient, Pharmacist** exist.
2. Reads `AdminSettings:Email` / `AdminSettings:UserName` / `AdminSettings:Password` from configuration (User Secrets).
3. **If any are missing → logs a warning and falls back to hard-coded defaults**: `admin@hospital.com` / `admin` / `Admin@123`. This is a real production risk — see [§19](#19-known-issues-defects--risks).
4. Creates the admin user if the email is not taken and adds it to the `Admin` role.

---

## 10. Business Rules Catalogue

`HCMS4/Models/Common/BusinessRules.cs`:

| Constant | Value | Meaning |
|---|---|---|
| `AppointmentCancellationWindowHours` | 24 | Minimum notice before an appointment can be cancelled |
| `AppointmentSlotBufferMinutes` | 30 | Buffer around a slot that blocks overlapping bookings |
| `UpcomingAppointmentsWindowDays` | 30 | Look-ahead window for "upcoming" lists |
| `MaxUpcomingAppointments` | 50 | Cap on upcoming-appointment results |
| `DrugExpiryWarningDays` | 30 | "Expiring Soon" threshold |
| `DrugExpiryShortWarningDays` | 60 | "Expiring Shortly" threshold |
| `LowStockThreshold` | 10 | Quantity below which a drug is "low stock" |
| `HighRiskThreshold` | 0.7 | No-show score ≥ 0.7 → high risk |
| `MediumRiskThreshold` | 0.4 | No-show score ≥ 0.4 → medium risk |
| `PrescriptionReissueValidityDays` | 30 | Days after which a prescription cannot be re-issued |
| `ComplaintAttachmentMaxBytes` | 5 MiB | Max complaint attachment size |
| `DefaultPageSize` | 20 | Default pagination page size |
| `MaxPageSize` | 100 | Hard cap on page size |

---

## 11. Service Layer

`HCMS4/Services/` — 18 files. They contain **10 service interfaces**, **10 service implementations**, one `BackgroundService` and one static seeder helper. Six interfaces live in their own `I*.cs` files (`IAppointmentService`, `IDailyReportService`, `IDrugService`, `IPatientService`, `IPharmacistService`, `IPrescriptionService`); the other four (`ILeaveService`, `INotificationService`, `IActivityLogService`, `INoShowRiskService`) are declared in the same file as their implementation.

### 11.1 `IAppointmentService` / `AppointmentService` (`AppointmentService.cs`, 312 lines)

| Method | Behaviour |
|---|---|
| `GetByIdAsync(int id)` | Appointment + Patient.User + Doctor.User (`AsNoTracking`) |
| `GetByIdWithDetailsAsync(int id)` | Adds `Doctor.Specialization` |
| `GetAllAsync(PaginationParams, string status = "all")` | Paged list ordered by date desc; `status` parsed with `Enum.TryParse<AppointmentStatus>` |
| `GetByDoctorIdAsync(int)` / `GetByPatientIdAsync(int)` | Filtered lists, date desc |
| `GetTodayAppointmentsAsync(int? doctorId, int? patientId)` | `AppointmentDateTime.Date == today` **and** status `Scheduled` |
| `GetUpcomingAppointmentsAsync(int? doctorId, int days = 30)` | Date > today, status `Scheduled`, capped at 50 |
| `GetCanceledAppointmentsAsync(int? doctorId)` | Status `Canceled` and date ≥ today, capped at 20 |
| `CreateAsync(Appointment)` | Checks `IsTimeSlotAvailableAsync`; stamps `CreatedAt`/`UpdatedAt` = UTC now; forces status `Scheduled`; **does not validate future date** |
| `RescheduleAsync(int id, DateTime newDateTime)` | Requires status `Scheduled`; checks slot availability excluding self; **preserves the original `ConsultationFee`** |
| `CancelAsync(int id, string reason)` | Requires status `Scheduled`; rejects if `AppointmentDateTime <= UtcNow + 24 h`; requires a non-empty reason; sets status `Canceled` + `CancellationReason` |
| `UpdatePastScheduledToCompletedAsync()` | Bulk-flips past `Scheduled` appointments to `Completed` (never called from `Program.cs` — see risks) |
| `IsTimeSlotAvailableAsync(int doctorId, DateTime dateTime, int? excludeAppointmentId = null)` | Rejects the slot if any non-canceled appointment for the doctor falls within ±30 min |

### 11.2 `IPrescriptionService` / `PrescriptionService` (`PrescriptionService.cs`, 266 lines)

| Method | Behaviour |
|---|---|
| `GetByIdAsync(int)` / `GetByIdWithDetailsAsync(int)` | Includes items + drugs, appointment |
| `GetPendingAsync()` | Status `Pending`, oldest first |
| `GetByPatientIdAsync(int)` / `GetByDoctorIdAsync(int)` | Filtered lists |
| `CreateAsync(Prescription, List<PrescriptionItem>)` | Batch-loads drugs (avoids N+1); validates every item — drug must exist, `drug.Quantity >= item.Quantity`, `drug.ExpiryDate > now`; **collects all errors** then returns `ServiceResult.Fail("Validation failed.", errors)`; on success runs inside an explicit `BeginTransactionAsync` — sets `MedicationTotal = Σ Quantity × Price`, status `Pending`, inserts prescription + items, and **decrements drug stock** |
| `MarkAsCompletedAsync(int id)` | Only from `Pending`; catches `DbUpdateConcurrencyException` with a friendly message |
| `CancelAsync(int id)` | Restores drug stock for every item inside a transaction; sets status `Canceled` |
| `HasDrugStockAsync(int drugId, int quantity)` | `drug.Quantity >= quantity && drug.ExpiryDate > UtcNow` |

### 11.3 `IPatientService` / `PatientService` (`PatientService.cs`, 219 lines)

`GetByIdAsync`, `GetByIdWithDetailsAsync`, `GetAllAsync(PaginationParams)`, `UpdateAsync(int, Patient)`, `DeleteAsync(int)` (blocked when related records exist), `HasRelatedRecordsAsync(int)` (appointments OR prescriptions OR invoices), `GetCountAsync()`, `DisablePatientAsync(int, string reason)` (sets `Status = Disabled`, `DisableReason`, `DisabledAt`), `EnablePatientAsync(int)` (clears the disable fields).

### 11.4 `IDrugService` / `DrugService` (`DrugService.cs`, 210 lines)

| Method | Behaviour |
|---|---|
| `GetByIdAsync(int)` | No tracking |
| `GetAllAsync(string? searchTerm, string? expiryFilter)` | `searchTerm` matches Name/Supplier/Description; `expiryFilter` ∈ `expired` / `expiring-soon` / `valid`; ordered by `ExpiryDate` asc |
| `CreateAsync(Drug)` | Stamps UTC timestamps |
| `UpdateAsync(Drug)` | Copies fields onto the tracked entity; catches `DbUpdateConcurrencyException` |
| `DeleteAsync(int)` | **Refuses** to delete a drug that has `PrescriptionItems` |
| `UpdateStockAsync(int id, int newQuantity)` | Sets absolute quantity; returns a warning message when `0 < qty < LowStockThreshold` and an alert when `qty == 0` |
| `GetExpiringDrugsAsync(int daysThreshold = 30)` | Expiry in `(now, now + days]` |
| `GetLowStockDrugsAsync(int threshold = 10)` | `Quantity < threshold`, ascending |
| `GetTotalInventoryValueAsync()` | `Σ Price × Quantity` |

### 11.5 `IPharmacistService` / `PharmacistService` (`PharmacistService.cs`, 160 lines)

`GetByIdAsync(int)`, `GetByUserIdAsync(string)`, `UpdateProfileAsync(int id, string qualifications, string? contactInfo, string? shift)`, `MarkPrescriptionCompletedAsync(int prescriptionId, string pharmacistUserId)`, `ProcessPrescriptionAsync(int, string)`.
`MarkPrescriptionCompletedAsync` runs in a transaction: verifies every drug exists and has sufficient stock, deducts quantities, sets status `Completed`, `DispensedDate = UtcNow`, `DispensedBy = pharmacist.User.FullName ?? "Unknown"`.
`ProcessPrescriptionAsync` is a **thin alias** that simply calls `MarkPrescriptionCompletedAsync` (the source comment says it is kept separate for future differentiation).

### 11.6 `ILeaveService` / `LeaveService` (`LeaveService.cs`, 192 lines)

| Method | Behaviour |
|---|---|
| `CreateLeaveRequestAsync(LeaveRequest)` | Stamps `CreatedAt`, forces status `Pending` |
| `GetPendingRequestsForDoctorAsync(int doctorId)` | Pending only, newest first |
| `GetDoctorLeavesAsync(int doctorId)` | All leaves for the doctor, newest start date first |
| `ApproveLeaveRequestAsync(int requestId, string notes)` | Creates a matching `DoctorLeave` (status Approved) **and** sets the request to Approved |
| `RejectLeaveRequestAsync(int requestId, string reason)` | Sets status `Rejected`; **the reason is discarded** (not persisted — the entity has no rejection-reason field) |
| `RegisterDoctorLeaveAsync(DoctorLeave leave)` | Validates `EndDate >= StartDate`; rejects overlaps with existing approved leaves; forces status `Approved` |
| `IsDoctorOnLeaveAsync(int doctorId, DateTime date)` | Any approved leave covering `date` |
| `NotifyManagerAsync(int doctorId, string message)` | **Stub** — only writes to the logger, no notification is created |

### 11.7 `IDailyReportService` / `DailyReportService` (`DailyReportService.cs`, 225 lines)

| Method | Behaviour |
|---|---|
| `GetDailyReportAsync(DateTime date)` | Pharmacy invoices only (`PrescriptionId != null`) whose `InvoiceDate` falls in the day; maps to `DailyInvoiceItem`; totals computed in memory |
| `GetFilteredReportAsync(DateTime? startDate, DateTime? endDate, PaymentStatus? status, string? searchTerm)` | Same base query plus date range (end date inclusive), payment-status filter, and a free-text filter over patient name / invoice id / pharmacist name |
| `GenerateDailyReportAsync(DateTime date)` | Idempotent — skips if a report for that date already exists; otherwise persists a `DailyReport` row with a JSON `ReportData` blob of the invoices |
| `GetAvailableReportDatesAsync()` | Distinct invoice dates, newest first, capped at 30 |

> Note: "pharmacy invoice" is defined as *any invoice with a `PrescriptionId`*. Consultation-only invoices are excluded from the daily pharmacy report.

### 11.8 `INotificationService` / `NotificationService` (`NotificationService.cs`)

- `CreateForUserAsync(string userId, string title, string message, NotificationType type, string? linkUrl, string? relatedEntityType, int? relatedEntityId)` — no-ops on a blank `userId`; `relatedEntityId` is stored as a string.
- `CreateForRoleAsync(string roleName, ...)` — resolves the role's user ids via `UserRoles ⋈ Roles`, then creates one notification per user.

### 11.9 `IActivityLogService` / `ActivityLogService` (`ActivityLogService.cs`)

`LogAsync(string action, string entityType, string description, string? entityId, string? userId, string? userName)` — inserts a `SystemActivityLog` row and saves immediately (one `SaveChangesAsync` per call).

### 11.10 `INoShowRiskService` / `NoShowRiskService` (`NoShowRiskService.cs`, 302 lines)

See [§16](#16-ml-no-show-prediction-microservice) for the full contract. Public surface:

```csharp
Task<double> CalculateRiskScoreAsync(int patientId);
Task<Dictionary<int, double>> CalculateRiskScoresAsync(IEnumerable<int> appointmentIds);
bool IsServiceAvailable { get; }
bool IsUsingAI { get; }
string? LastErrorMessage { get; }
```

Key behaviours:
- `CalculateRiskScoresAsync` reuses any score already persisted on `Appointment.NoShowRiskScore`; otherwise calls the ML API; on failure falls back to the local heuristic.
- `CalculateRiskScoreAsync` (local heuristic): `noShowRate = NoShowCount / totalAppointments`; `recentFactor = min(recentNoShows(3) × 0.15, 0.45)`; `baseRisk = min(noShowRate × 0.5 + recentFactor, 1.0)`; `distanceFactor = 0.05` when the patient has an address; final score `min(baseRisk + distanceFactor, 1.0)`, rounded to 2 dp. Returns `0.3` when the patient is unknown or has no appointments.
- DTOs: `NoShowAppointmentRequest` (request body) and `NoShowPredictionResponse` (response body) with explicit `[JsonPropertyName]` mappings.

### 11.11 `DailyReportBackgroundService` (`DailyReportBackgroundService.cs`)

`BackgroundService` that waits until midnight, generates the **previous day's** pharmacy report, then delays 24 h and repeats. Exceptions are logged and swallowed so the loop survives.

### 11.12 `AdminSeeder` (`AdminSeeder.cs`)

Static `SeedAdminUser(IServiceProvider)` — see [§9.4](#94-admin-seeding-servicesadminseedercs).

---

## 12. Controller Layer

`HCMS4/Controllers/` — 7 controllers.

### 12.1 `HomeController` (`HomeController.cs`) — public

| Action | Type | Description |
|---|---|---|
| `Index()` | GET | Loads the 6 most recent **published** `MedicalArticle`s with their doctor + user; renders `Views/Home/Index.cshtml` |
| `About()` | GET | Static view |
| `Contact()` | GET | Static view |
| `Error()` | GET | `[ResponseCache(Duration = 0, Location = None, NoStore = true)]`; returns `Views/Shared/Error.cshtml` with `ErrorViewModel` |

### 12.2 `AccountController` (`AccountController.cs`) — public, `UserManager` + `SignInManager` + `ApplicationDbContext`

| Action | Type | Description |
|---|---|---|
| `Login(string? returnUrl)` | GET | Renders `Views/Account/Login.cshtml` |
| `Login(LoginViewModel, string? returnUrl)` | POST | Looks the user up by **email** (`FindByEmailAsync`) and calls `PasswordSignInAsync(user.UserName, Password, RememberMe, lockoutOnFailure: **false**)`. On success it resolves the user's roles and redirects → `Admin/Dashboard`, `Doctor/Dashboard`, `Patient/Dashboard`, `Pharmacist/Dashboard`, or `Home/Index`; `returnUrl` (when supplied) takes priority and is emitted via `LocalRedirect`. `IsLockedOut` and generic failures both redirect/return with the message "Invalid login attempt." |
| `Register()` | GET | Renders `Views/Account/Register.cshtml` |
| `Register(RegisterViewModel)` | POST | Creates the `ApplicationUser` with `UserName = Email`, adds the **Patient** role, creates the `Patient` profile row (`DateOfBirth`, `ChronicConditions`), signs the user in (`isPersistent: false`), redirects to `/Patient/Dashboard` |
| `Logout()` | POST | Signs out, redirects to `Home/Index` |
| `Profile()` | GET | Loads the profile + role-aware extras (doctor/patient profile info) into `ProfileViewModel` |
| `EditProfile()` | GET | Renders `Views/Account/EditProfile.cshtml` with `EditProfileViewModel` |
| `EditProfile(EditProfileViewModel)` | POST | Updates `ApplicationUser` fields and, if a doctor, the `Doctor` profile |
| `ChangePassword(ChangePasswordViewModel)` | POST | `ChangePasswordAsync`; requires the current password |

All POST actions carry `[ValidateAntiForgeryToken]`.

> `AccountController` has **no `AccessDenied` action**, even though the cookie is configured to redirect there.

### 12.3 `AdminController` (`AdminController.cs`, 103 KB — the largest file) — `: BaseController`, `[Authorize(Roles = "Admin,Doctor")]`

Constructor injects `UserManager`, `SignInManager`, `ApplicationDbContext`, `ILogger`, `IDailyReportService`, `IPatientService`, `IAppointmentService`, `IPrescriptionService`, `INoShowRiskService`, `ILeaveService`, `INotificationService`, `IActivityLogService`.

| Action | Type | Description |
|---|---|---|
| `Dashboard()` | GET | Aggregate counters (appointments, patients, doctors, revenue, pending prescriptions, complaints, surveys) |
| `CreateDoctor()` | GET | `DoctorRegisterViewModel` + specialization list |
| `CreateDoctor(DoctorRegisterViewModel)` | POST | Creates the `ApplicationUser` + `Doctor` profile, assigns the `Doctor` role |
| `Doctors()` | GET | Lists doctors with specialization, rating, availability |
| `DeleteUser(string id)` | POST | Deletes the account (and cascades profile rows) |
| `Patients(int page = 1, int pageSize = 20)` | GET | Paged patient list with search/filter |
| `EditPatient(int? id)` | GET | Edit form |
| `EditPatient(int id, Patient patient)` | POST | Persists patient fields |
| `DeletePatient(int id)` | POST | Deletes when no related records exist |
| `PatientDetails(int? id)` | GET | Patient detail view |
| `ManageAppointments(string status = "all", string riskLevel = "all")` | GET | Builds `ManageAppointmentsViewModel`; calls `INoShowRiskService.CalculateRiskScoresAsync`; surfaces `AnalyticsServiceAvailable` / `IsUsingAI`; filters by `RiskLevelHelper.Matches` |
| `BookAppointment(int? patientId = null)` | GET | Booking form |
| `BookAppointment(BookAppointmentViewModel)` | POST | Creates the appointment via `IAppointmentService`, assigns the consultation fee from the doctor's specialization |
| `GetDoctorConsultationFee(int doctorId)` | GET | JSON endpoint returning the doctor's fee (used by the booking AJAX) |
| `RescheduleAppointment(int? id)` | GET | Reschedule form |
| `RescheduleAppointment(RescheduleAppointmentViewModel)` | POST | Delegates to `IAppointmentService.RescheduleAsync` |
| `CancelAppointment(int id, string cancellationReason)` | POST | Delegates to `IAppointmentService.CancelAsync` |
| `SendReminder(int id)` | POST | Creates an `AppointmentReminder` notification for the patient |
| `DoctorSchedule(int id, string status = "all")` | GET | `DoctorScheduleViewModel` — today/upcoming/completed/canceled buckets + counts |
| `PendingPrescriptions()` | GET | Pending prescription queue |
| `MarkPrescriptionCompleted(int id)` | POST | Marks a prescription completed |
| `GenerateInvoice(int patientId)` | GET | Invoice builder with recent appointments/prescriptions and existing invoices |
| `GenerateInvoice(GenerateInvoiceViewModel)` | POST | Creates the `Invoice` (consultation fee + medication total) |
| `ViewInvoice(int id)` | GET | Invoice preview |
| `InvoiceDetails(int id)` | GET | Full invoice detail |
| `UpdatePaymentStatus(int id)` | GET | Payment update form |
| `UpdatePaymentStatusPost(int id)` | POST (`[ActionName("UpdatePaymentStatus")]`) | Applies the payment status/amount/date |
| `CreatePharmacist()` | GET | Registration form |
| `CreatePharmacist(PharmacistRegisterViewModel)` | POST | Creates the account + `Pharmacist` profile + role |
| `Pharmacists()` | GET | Lists pharmacists |
| `EditPharmacist(int? id)` | GET | Edit form |
| `EditPharmacist(int id, PharmacistEditViewModel)` | POST | Persists the profile |
| `DeletePharmacist(int id)` | POST | Deletes the pharmacist |
| `DailyPharmacyReport(DateTime? date = null)` | GET | Pharmacy daily report |
| `FilteredPharmacyReport(DailyReportFilterViewModel filters)` | GET | Filtered report |
| `ExportPharmacyReport(DateTime? date)` | GET | CSV-style export of the daily report |
| `Suppliers()` | GET | Supplier list + stats |
| `CreateSupplier()` | GET | Supplier form |
| `CreateSupplier(Supplier supplier)` | POST | Creates; rejects duplicate names |
| `EditSupplier(int? id)` | GET | Edit form |
| `EditSupplier(int id, Supplier supplier)` | POST | Persists |
| `DeleteSupplier(int id)` | POST | **Soft delete** (deactivate) when purchase requests exist, otherwise hard delete |
| `PriceList()` | GET | Specializations + consultation fees |
| `UpdateSpecializationFee(int id, decimal consultationFee)` | POST | Updates the fee |
| `DailyVisitingPatientsReport(DateTime? date = null)` | GET | `[Authorize(Roles = "Doctor")]` — daily visiting-patients report (`DailyVisitingPatientsReportViewModel`) |
| `DisablePatient(int id)` | GET | Disable form |
| `DisablePatient(DisablePatientViewModel)` | POST | Soft-disables the patient |
| `EnablePatient(int id)` | POST | Re-enables the patient |
| `RegisterDoctorLeave(int id)` | GET | Leave registration form |
| `RegisterDoctorLeave(RegisterDoctorLeaveViewModel)` | POST | Registers an approved `DoctorLeave` |
| `ViewDoctorLeaves(int id)` | GET | Doctor leave list |
| `ApproveLeaveRequest(int requestId, string notes)` | POST | Approves a `LeaveRequest` |
| `RejectLeaveRequest(int requestId, string reason)` | POST | Rejects a `LeaveRequest` |
| `PurchaseRequests(string status = "all")` | GET | Purchase-request list with counts per status |
| `PurchaseRequestDetails(int id)` | GET | Purchase-request detail |
| `ApprovePurchaseRequest(int id)` | POST | Status → `Approved`; **increments drug stock** for every line item (goods received); stamps `ApprovedDate` / `ApprovedBy` |
| `RejectPurchaseRequest(int id, string rejectionReason)` | POST | Requires a non-empty reason; status → `Rejected`; stores `RejectionReason` |
| `Complaints()` | GET | `[Authorize(Roles = "Admin")]` — complaint list |
| `ComplaintDetails(int id)` | GET | `[Authorize(Roles = "Admin")]` |
| `UpdateComplaintStatus(int id, ComplaintStatus status, string? resolutionNotes)` | POST | `[Authorize(Roles = "Admin")]` |
| `Surveys()` | GET | `[Authorize(Roles = "Admin")]` — survey list |
| `CreateSurvey()` | GET | `[Authorize(Roles = "Admin")]` — survey builder form |
| `CreateSurvey(SurveyEditorViewModel)` | POST | `[Authorize(Roles = "Admin")]` — persists the survey, its questions/options, and creates `SurveyAssignment` rows for the target audience |

Private helpers: `PatientExists(int)`, `AppointmentExists(int)`, `DrugExists(int)`, `SupplierExists(int)`, `ReloadInvoiceDropdowns(GenerateInvoiceViewModel)`, `DetermineVisitType(int patientId, int currentAppointmentId)`, `GetDiagnosis(Prescription?)`, `GetSurveyTargetPatientsAsync(SurveyEditorViewModel)`.

### 12.4 `DoctorController` (`DoctorController.cs`) — `: BaseController`, `[Authorize(Roles = "Doctor")]`

| Action | Type | Description |
|---|---|---|
| `Dashboard()` | GET | `DoctorDashboardViewModel` — today's count, pending reissue requests, pending review requests, article counts, average rating, recent ratings, recent notifications |
| `ViewAppointments(string viewMode = "today", DateTime? selectedDate, ...)` | GET | `DoctorAppointmentsViewModel`; modes `today` / `date` / `range`; attaches `AppointmentRiskScores` from `INoShowRiskService` plus `AnalyticsServiceAvailable` / `IsUsingAI`; filters by `SelectedRiskLevel` |
| `SendReminder(int id)` | POST | Sends an appointment reminder notification to the patient |
| `ReissueRequests()` | GET | Lists pending `PrescriptionReissueRequest`s for this doctor |
| `ReviewReissueRequest(int id, bool approve, string? doctorResponse)` | POST | Approves/rejects a reissue request; sets `ReviewedAt` |
| `DoctorReviewRequests()` | GET | Lists `DoctorReviewRequest`s raised by pharmacists for this doctor |
| `RespondToReviewRequest(int id, string doctorResponse)` | POST | Responds; status → `Responded` |
| `MedicalArticles()` | GET | The doctor's articles (published + drafts) |
| `PublishMedicalArticle(int id)` | POST | Draft → Published, stamps `PublishedAt` |
| `CreateMedicalArticle()` | GET | Editor form |
| `CreateMedicalArticle(MedicalArticleEditorViewModel, string submitAction)` | POST | `submitAction` distinguishes "save draft" vs "publish" |
| `PatientRecord(int patientId)` | GET | Full medical record: appointment history, prescriptions, clinical notes, invoices |
| `CreateClinicalNote(int patientId)` | GET | Clinical-note form |
| `CreateClinicalNote(CreateClinicalNoteViewModel)` | POST | Persists the clinical note |
| `CreatePrescription(int patientId, int? appointmentId = null)` | GET | Prescription builder with the available drug list |
| `CreatePrescription(CreatePrescriptionViewModel)` | POST | Delegates to `IPrescriptionService.CreateAsync` (stock validation + decrement) |
| `Profile()` | GET | Doctor profile |
| `RequestLeave()` | GET | Leave request form |
| `RequestLeave(LeaveRequestViewModel)` | POST | Creates a `LeaveRequest` via `ILeaveService` |
| `ViewReports()` | GET | Report entry point for the doctor |

### 12.5 `PatientController` (`PatientController.cs`) — `: BaseController`, `[Authorize(Roles = "Patient")]`

| Action | Type | Description |
|---|---|---|
| `Dashboard()` | GET | `PatientDashboardViewModel` — upcoming appointments, completed visits, active surveys, open complaints, published articles, recent notifications |
| `BookAppointment()` | GET | Booking form with available doctors |
| `BookAppointment(BookAppointmentViewModel)` | POST | Books the appointment; on failure calls `ReloadViewModel` |
| `GetDoctorConsultationFee(int doctorId)` | GET | JSON fee lookup (used by the booking AJAX) |
| `ViewAppointmentHistory()` | GET | Paginated history with `CanRateVisit` / `HasVisitRating` flags |
| `InvoiceHistory()` | GET | The patient's invoices |
| `InvoiceDetails(int id)` | GET | A single invoice (ownership-checked against the current patient) |
| `ProcessPayment(int invoiceId, decimal amount, string paymentMethod, string? notes)` | POST | Ownership-checked against the current patient; validates `0 < amount <= TotalAmount`; **supports partial payments** — `AmountPaid` accumulates, status flips to `Paid` once `AmountPaid >= TotalAmount`, otherwise stays `Pending`; appends a payment note to `Invoice.Notes` |
| `Prescriptions()` | GET | The patient's prescriptions (grouped per prescription with drug names) |
| `PrescriptionDetails(int id)` | GET | Prescription detail incl. pharmacist notes and reissue eligibility |
| `RateVisit(int appointmentId)` | GET | Rating form |
| `RateVisit(VisitRatingFormViewModel)` | POST | Requires the appointment to belong to the current patient **and** be `Completed`; rejects duplicate ratings for the same appointment; persists the `VisitRating`; recomputes `Doctor.RatingCount` and `Doctor.AverageRating` (rounded to 2 dp); writes an activity-log entry |
| `RequestPrescriptionReissue(int prescriptionId)` | POST | Creates a `PrescriptionReissueRequest` (guarded by `IsPrescriptionEligibleForReissue`) |
| `Complaints()` | GET | The patient's complaints |
| `CreateComplaint()` | GET | Complaint form |
| `CreateComplaint(PatientComplaintFormViewModel)` | POST | Optional attachment upload — extension allowlist `.jpg .jpeg .png .pdf .doc .docx`, size ≤ `BusinessRules.ComplaintAttachmentMaxBytes` (5 MiB), stored under `wwwroot/uploads/complaints/{guid}{ext}` with the public path stored on `Complaint.AttachmentPath`. Generates a tracking number `CMP-{yyyyMMdd}-{6 hex chars}`, saves the complaint with status `Submitted`, notifies the **Admin** role, and writes an activity-log entry |
| `Surveys()` | GET | Surveys assigned to the patient |
| `TakeSurvey(int assignmentId)` | GET | Survey renderer |
| `TakeSurvey(SurveyResponseViewModel)` | POST | Saves `SurveyAnswer` rows and marks the assignment `Completed` |

Private helpers: `ReloadViewModel(BookAppointmentViewModel, int patientId)`, `IsPrescriptionEligibleForReissue(Prescription)`, `BuildSurveyResponseViewModelAsync(int assignmentId, int patientId, ...)`.

### 12.6 `PharmacistController` (`PharmacistController.cs`) — `: Controller` (not `BaseController`), `[Authorize(Roles = "Pharmacist")]`

| Action | Type | Description |
|---|---|---|
| `Dashboard()` | GET | `PharmacistDashboardViewModel` — drug counts, expiry/low-stock counts, inventory value, pending/completed prescriptions, alerts |
| `Drugs(string searchTerm, string expiryFilter)` | GET | Inventory list with filtering |
| `CreateDrug()` | GET | Drug form |
| `CreateDrug(Drug drug)` | POST | Creates via `IDrugService` |
| `EditDrug(int? id)` | GET | Drug edit form |
| `EditDrug(int id, Drug drug)` | POST | Persists |
| `DeleteDrug(int id)` | POST | Deletes (blocked when the drug has prescription items) |
| `UpdateStock(int id, int newQuantity)` | POST | Sets absolute stock |
| `Prescriptions(string status = "pending")` | GET | Prescription queue |
| `MarkPrescriptionCompleted(int id)` | POST | Completes the prescription and decrements stock |
| `Profile()` | GET | Pharmacist profile |
| `EditProfile()` | GET | Edit form |
| `EditProfile(PharmacistEditProfileViewModel)` | POST | Persists |
| `ProcessPrescription(int id)` | GET | Dispensing screen (line items, stock, alternatives) |
| `DispensePrescription(int id, List<int> selectedItemIds, string dispensingNotes)` | POST | Partial dispensing: decrements stock for the selected items, records `DispensingNotes` |
| `PrescriptionDetails(int id)` | GET | Full prescription detail |
| `RequestDoctorReview(int prescriptionId)` | GET | Review-request form |
| `RequestDoctorReview(PrescriptionReviewRequestFormViewModel)` | POST | Creates a `DoctorReviewRequest` and notifies the doctor |
| `AddPrescriptionNote(int prescriptionId)` | GET | Note form |
| `AddPrescriptionNote(PrescriptionNoteFormViewModel)` | POST | Creates a `PharmacistPrescriptionNote`; optionally notifies the doctor |
| `PurchaseRequests(string status = "all")` | GET | The pharmacist's purchase requests |
| `CreatePurchaseRequest()` | GET | Builder form with suppliers + drugs |
| `CreatePurchaseRequest(CreatePurchaseRequestViewModel)` | POST | Creates the request + items, generates a request number, computes the total |
| `PurchaseRequestDetails(int id)` | GET | Detail view |
| `AddItemToRequest(int drugId, int quantity, decimal unitPrice, string? notes)` | POST | AJAX endpoint that appends a line item to the in-progress request |

---

## 13. ViewModels

`HCMS4/ViewModels/` — 61 files, 74 class declarations. All are plain POCOs with DataAnnotations; there is no FluentValidation.

### Authentication / profile
`LoginViewModel` (Email, Password, RememberMe), `RegisterViewModel` (FirstName, LastName, Email, Password, ConfirmPassword, PhoneNumber, DateOfBirth, ChronicConditions, PreferredTimeOfDay + two read-only suggestion lists), `ChangePasswordViewModel`, `ProfileViewModel` (roles, doctor/patient profile flags, specialisation, address, emergency contact), `EditProfileViewModel`, `UserViewModel`.

### Doctor
`DoctorRegisterViewModel`, `DoctorDashboardViewModel`, `DoctorAppointmentsViewModel` (today/upcoming/past buckets, `ViewMode`, `SelectedRiskLevel`, `AnalyticsServiceAvailable`, `IsUsingAI`, `AppointmentRiskScores`), `DoctorScheduleViewModel`, `DoctorSelectDto`, `MedicalArticleEditorViewModel`.

### Patient
`PatientDashboardViewModel`, `PatientBookAppointmentViewModel` (also carries `RecommendedDoctors` / `IsUrgent`), `BookAppointmentViewModel` (shared by patient + admin booking; `AvailableDoctors` / `AvailablePatients` / `Symptoms`), `AppointmentHistoryViewModel`, `RescheduleAppointmentViewModel`, `PatientMedicalRecordViewModel`, `PatientPrescriptionDetailViewModel`, `PatientPrescriptionsListViewModel`, `PatientPrescriptionsViewModel`, `PatientViewModel`, `PatientSelectDto`, `PatientStatsViewModel`, `PatientComplaintFormViewModel`, `VisitRatingFormViewModel`, `SurveyResponseViewModel` (+ `SurveyResponseQuestionViewModel`).

### Clinical
`ClinicalNoteViewModel`, `CreateClinicalNoteViewModel`, `CreatePrescriptionViewModel`, `PrescriptionItemCreateViewModel` (`SelectedDrugId`, `Price`, `AvailableStock`, `ExpiryStatus`), `PrescriptionItemViewModel`, `PrescriptionItemDetailViewModel`, `PrescriptionViewModel`, `PendingPrescriptionViewModel`, `PrescriptionNoteFormViewModel`, `PrescriptionNoteDisplayViewModel`, `PrescriptionReviewRequestFormViewModel`.

### Pharmacy / finance
`DrugSelectionViewModel`, `DrugAlternativeViewModel`, `ExpiryAlertsViewModel`, `InvoiceViewModel`, `GenerateInvoiceViewModel` (+ nested `AppointmentSelectDto`, `PrescriptionSelectDto`), `DailyInvoiceItem`, `DailyPharmacyReportViewModel`, `DailyReportFilterViewModel`, `DailyVisitingPatientsReportViewModel` (+ `VisitingPatientItem`), `AdminPurchaseRequestViewModel.cs` (contains `AdminPurchaseRequestListViewModel` and `AdminPurchaseRequestDetailViewModel`), `PurchaseRequestListViewModel`, `PurchaseRequestDetailViewModel`, `PurchaseRequestViewModel.cs` (contains `CreatePurchaseRequestViewModel` + `SupplierSelectDto`, `DrugSelectDto`, `PurchaseRequestItemDto`).

### Pharmacist
`PharmacistRegisterViewModel`, `PharmacistEditViewModel`, `PharmacistEditProfileViewModel`, `PharmacistProfileViewModel`, `PharmacistViewModel`, `PharmacistDashboardViewModel`.

### Surveys / misc
`SurveyEditorViewModel` (+ `SurveyQuestionEditorViewModel`), `LeaveRequestViewModel.cs` (contains `LeaveRequestViewModel`, `DoctorLeaveViewModel`, `DoctorLeaveItem`, `RegisterDoctorLeaveViewModel`), `DisablePatientViewModel`.

---

## 14. Razor Views & UI

`HCMS4/Views/` — 82 `.cshtml` files.

### Layout & shared
| File | Purpose |
|---|---|
| `_ViewStart.cshtml` | Chooses the layout at runtime: `_LayoutAuth` when `User.Identity.IsAuthenticated`, otherwise `_Layout` |
| `_ViewImports.cshtml` | `@using HCMS4`, `@using HCMS4.Models`, tag helpers |
| `Shared/_Layout.cshtml` | Guest layout (Bootstrap, AOS, GLightbox, Swiper, PureCounter, `main.css`) |
| `Shared/_LayoutAuth.cshtml` | Authenticated layout (adds `admintemp.css` and the role-aware sidebar/nav) |
| `Shared/_Layout.cshtml.css` | Scoped CSS for the guest layout |
| `Shared/_ValidationScriptsPartial.cshtml` | jQuery validation bundle |
| `Shared/_AppointmentStatusBadge.cshtml` | Status badge partial |
| `Shared/Error.cshtml` | Error page (`ErrorViewModel`) |

### Per-area view inventory
- **Account (4):** `Login`, `Register`, `Profile`, `EditProfile`
- **Home (4):** `Index`, `About`, `Contact`, `Privacy`
- **Admin (33):** `Dashboard`, `Doctors`, `CreateDoctor`, `Patients`, `EditPatient`, `PatientDetails`, `DisablePatient`, `ManageAppointments`, `BookAppointment`, `RescheduleAppointment`, `DoctorSchedule`, `RegisterDoctorLeave`, `ViewDoctorLeaves`, `PendingPrescriptions`, `GenerateInvoice`, `ViewInvoice`, `InvoiceDetails`, `UpdatePaymentStatus`, `DailyPharmacyReport`, `DailyVisitingPatientsReport`, `PriceList`, `Suppliers`, `CreateSupplier`, `EditSupplier`, `Pharmacists`, `CreatePharmacist`, `EditPharmacist`, `PurchaseRequests`, `PurchaseRequestDetails`, `Complaints`, `ComplaintDetails`, `Surveys`, `CreateSurvey`
- **Doctor (11):** `Dashboard`, `ViewAppointments`, `PatientRecord`, `CreateClinicalNote`, `CreatePrescription`, `Profile`, `RequestLeave`, `ReissueRequests`, `DoctorReviewRequests`, `MedicalArticles`, `CreateMedicalArticle`
- **Patient (11):** `Dashboard`, `BookAppointment`, `ViewAppointmentHistory`, `InvoiceHistory`, `Prescriptions`, `PrescriptionDetails`, `RateVisit`, `Complaints`, `CreateComplaint`, `Surveys`, `TakeSurvey`
- **Pharmacist (12):** `Dashboard`, `Drugs`, `CreateDrug`, `EditDrug`, `Prescriptions`, `ProcessPrescription`, `PrescriptionDetails`, `RequestDoctorReview`, `AddPrescriptionNote`, `Profile`, `PurchaseRequests`, `CreatePurchaseRequest`, `PurchaseRequestDetails`
- **Shared (5 `.cshtml` + 1 `.cshtml.css`):** `_Layout`, `_LayoutAuth`, `_ValidationScriptsPartial`, `_AppointmentStatusBadge`, `Error` (+ `_Layout.cshtml.css`)
- **Identity scaffold (`Areas/Identity/Pages/`):** full `Account/` set — `Login`, `Logout`, `Register`, `AccessDenied`, `ConfirmEmail`, `ExternalLogin`, `ForgotPassword`, `Lockout`, `ResetPassword`, `LoginWith2fa`, `LoginWithRecoveryCode`, plus the complete `Account/Manage/` area (change/delete email, password, 2FA, personal data, external logins, recovery codes).

### Notable client-side behaviours
- `Views/Patient/BookAppointment.cshtml` contains an "AI assistant" block that calls `/Patient/GetAppointmentSuggestions?patientId=…`. **No such action exists** on `PatientController` → the request always 404s.
- The same file contains a **corrupted JavaScript fragment** (duplicated lines around 229–235) that leaves an unbalanced block and an empty `if (patientId) { … }` body inside `initializeAssistant()`. The script block as written is not syntactically valid.
- `Views/Shared/_LayoutAuth.cshtml` loads jQuery **twice** (`code.jquery.com/jquery-3.6.0.min.js` at the top and `jquery-3.7.1.min.js` at the bottom) plus two copies of the validation libraries (CDN 1.19.5 / 4.0.0 at the top, and the local `~/lib/...` copies at the bottom).

---

## 15. Front-end Assets

`HCMS4/wwwroot/`

| Path | Contents |
|---|---|
| `assets/css/main.css` | 37 KB main theme (referenced by both layouts) |
| `assets/css/admintemp.css` | 14 KB admin dashboard theme (referenced only by `_LayoutAuth.cshtml`) |
| `assets/css/dashboard.css` | 1.9 KB — **orphaned**, not referenced by any view |
| `assets/js/main.js` | 5 KB shared client script (loaded by both layouts) |
| `assets/scss/` | SCSS sources for the theme |
| `assets/img/` | images, plus `doctors/`, `gallery/`, `testimonials/` |
| `assets/vendor/bootstrap/` | Bootstrap 5 CSS + JS (full, min, rtl, grid, reboot, utilities, esm) |
| `assets/vendor/bootstrap-icons/` | icon font + CSS + JSON manifest |
| `assets/vendor/fontawesome-free/` | Font Awesome 6 (solid/regular/brands, webfonts, svg) |
| `assets/vendor/aos/` | AOS scroll animations (js/css/esm/cjs) |
| `assets/vendor/glightbox/` | lightbox |
| `assets/vendor/swiper/` | Swiper slider |
| `assets/vendor/purecounter/` | counter animation |
| `assets/vendor/php-email-form/validate.js` | unused contact-form validator |
| `lib/jquery/dist/jquery.min.js` | local jQuery copy (also loaded from CDN) |
| `lib/jquery-validation/dist/jquery.validate.min.js` | local validation |
| `lib/jquery-validation-unobtrusiv/jquery.validate.unobtrusive.min.js` | local unobtrusive validation |

---

## 16. ML No-Show Prediction Microservice

### 16.1 The Flask service

`medical-appointment-no-shows-main/medical-appointment-no-shows-main/scripts/predict.py`

- Loads `../models/LGBMClassifier_tranformers_final.bin` — a pickle containing `(scaler, ohe, model)`.
- `prepare_data(df, test)`: renames `Hipertension` → `Hypertension`, parses `ScheduledDay`/`AppointmentDay` as datetimes, lower-cases column names and replaces `-` with `_`, lower-cases categorical string values and replaces spaces with `_`.
- `feature_engineering(df)`: for `scheduledday` and `appointmentday` it derives `Year, Month, Week, Day, Dayofweek, Dayofyear, Is_month_end, Is_month_start, Is_quarter_end, Is_quarter_start, Hour, Minute, Second` and drops the original columns.
- Feature order:
  - **Numerical (10):** `patientid, age, scheduleddayWeek, scheduleddayDay, scheduleddayDayofyear, scheduleddayHour, scheduleddayMinute, scheduleddaySecond, appointmentdayDay, appointmentdayDayofyear`
  - **Categorical (20):** `gender, scholarship, hypertension, diabetes, alcoholism, handcap, sms_received, scheduleddayYear, scheduleddayMonth, scheduleddayDayofweek, scheduleddayIs_month_end, scheduleddayIs_month_start, scheduleddayIs_quarter_end, scheduleddayIs_quarter_start, appointmentdayMonth, appointmentdayWeek, appointmentdayDayofweek, appointmentdayIs_month_end, appointmentdayIs_month_start, neighbourhood`
- `POST /predict` → `model.predict_proba(X_test)[0,1]`; `no_show = y_pred >= 0.5`.
- Response body: `{ "no_show_probability": <float>, "no_show": <bool> }`.
- Served via `app.run(debug=True, host='0.0.0.0', port=9696)` — **`debug=True` on a network-facing host is unsafe.**

### 16.2 The .NET client

`HCMS4/Services/NoShowRiskService.cs`

**Request** → `POST {NoShowAPI:BaseUrl}/predict`, JSON body (`NoShowAppointmentRequest`):

| JSON key | C# type | Value sent by HCMS4 |
|---|---|---|
| `PatientId` | `double` | `appointment.PatientId` |
| `AppointmentID` | `int` | `appointment.Id` |
| `Gender` | `string` | hard-coded `"m"` |
| `ScheduledDay` | `string` | `appointment.CreatedAt ?? UtcNow` formatted `yyyy-MM-dd HH:mm:ss` |
| `AppointmentDay` | `string` | `appointment.AppointmentDateTime` formatted `yyyy-MM-dd 00:00:00` |
| `Age` | `int` | computed from `Patient.DateOfBirth` (default 30, clamped 0–120) |
| `Neighbourhood` | `string` | hard-coded `"clinic"` |
| `Scholarship` | `bool` | hard-coded `false` |
| `Hipertension` | `bool` | hard-coded `false` |
| `Diabetes` | `bool` | hard-coded `false` |
| `Alcoholism` | `bool` | hard-coded `false` |
| `Handcap` | `int` | hard-coded `0` |
| `SMS_received` | `bool` | hard-coded `false` |

> The clinical/socio-economic features are **all constants** — the model therefore sees no real patient data beyond age and the two dates. The `Hipertension` (sic) key matches the Flask side's rename of `Hipertension` → `Hypertension`, and `sms_received` matches the lower-cased column name.

**Response** → `NoShowPredictionResponse`: `no_show_probability` (double), `no_show` (bool).

**Transport details**
- Named `HttpClient` `"NoShowAPI"` (30 s timeout) created via `IHttpClientFactory`; `NoShowRiskService` additionally overrides the timeout to **10 s** per call.
- `NoShowAPI:ApiKey` is **never sent** — the endpoint is unauthenticated.
- On any failure (`HttpRequestException`, non-success status, deserialization failure) the service logs, sets `_serviceAvailable = false` / `_lastErrorMessage`, and falls back to the local heuristic (`CalculateRiskScoresLocallyAsync`).
- `IsUsingAI` is `true` only when at least one live API prediction was obtained.

**Where it is consumed**
- `AdminController.ManageAppointments` (line ~401) → fills `ManageAppointmentsViewModel.AppointmentRiskScores` and the `AnalyticsServiceAvailable` / `IsUsingAI` flags.
- `DoctorController.ViewAppointments` (line ~208) → fills `DoctorAppointmentsViewModel.AppointmentRiskScores`.

**Risk levels** — `RiskLevelHelper.GetLevel(score)` → `"high"` (≥ 0.7), `"medium"` (≥ 0.4), else `"low"`; `Matches(selected, score)` treats `"all"`/blank as match-all.

### 16.3 Other ML assets
`models/` also contains `DecisionTreeClassifier.bin`, `LGBMClassifier.bin`, `LogisticRegression.bin`, `RandomForestClassifier.bin`, `XGBClassifier.bin`, `XGBClassifier_final.bin`, `LGBMClassifier_tranformers_final.bin` (the one in use) and `XGBClassifier_tranformers_final.bin`. `scripts/train.py` reproduces the training pipeline; `data/KaggleV2-May-2016.csv` is the source dataset.

---

## 17. Test Suite

`HCMS4.Tests/` — xUnit + EF Core InMemory. **44 tests, all passing.**

| File | Tests | Coverage |
|---|---|---|
| `TestDbContextFactory.cs` | — | Helper that builds an InMemory `ApplicationDbContext` with a seeded `Specialization` |
| `AppointmentServiceTests.cs` | 9 | create (success + duplicate slot), cancel (success, <24 h rejected, already-canceled rejected), reschedule, today-list, slot availability (free / conflicting) |
| `PrescriptionServiceTests.cs` | 7 | create (success, insufficient stock, expired drug), mark completed (success, already completed), cancel restores stock, pending list |
| `DrugServiceTests.cs` | 8 | create, update, delete blocked by prescription items, expiry-status detection, `ExpiryStatus` computation, low-stock identification, negative-quantity validation, past-expiry validation |
| `PatientServiceTests.cs` | 6 | get by id (found / not found), paginated list, update, delete (no relations / with appointments) |
| `DailyReportServiceTests.cs` | 5 | daily report with prescription invoices, empty report, filter by payment status, generate new report, idempotent regeneration |
| `AdminServiceTests.cs` | 9 | supplier create / duplicate-name rejection / soft delete with purchase requests, specialization fee update (+ zero-fee rejection), daily-report totals, invoice payment status update, already-paid invoice immutability, purchase-request total calculation |

### Coverage gaps
No tests exist for:
- `NotificationService`, `LeaveService`, `PharmacistService`, `NoShowRiskService`, `ActivityLogService`, `DailyReportBackgroundService`
- `AdminSeeder`
- All controllers (no integration tests at all)
- The `Views` layer
- The Flask microservice

### Running
```bash
dotnet test "C:\Users\Asus\Downloads\HCMS4_FINAL\HCMS4.Tests\HCMS4.Tests.csproj"
# → Passed! - Failed: 0, Passed: 44, Skipped: 0, Total: 44
```
`dotnet test HCMS4.sln` executes **zero** tests because `HCMS4.Tests` is not registered in the solution.

---

## 18. Key End-to-End Workflows

### 18.1 Patient books an appointment
1. `PatientController.BookAppointment()` (GET) loads available doctors (`Doctor.IsAvailable`) into `BookAppointmentViewModel.AvailableDoctors`.
2. The view calls `GET /Patient/GetDoctorConsultationFee?doctorId=` to display the fee.
3. `BookAppointment(BookAppointmentViewModel)` (POST) → `IAppointmentService.CreateAsync` → `IsTimeSlotAvailableAsync` (±30 min conflict check, canceled appointments ignored) → insert with `Status = Scheduled` and `ConsultationFee` from the doctor's specialization.
4. A notification can be raised; the appointment appears in the patient, doctor and admin dashboards.

### 18.2 Doctor issues a prescription
1. `DoctorController.CreatePrescription(patientId, appointmentId?)` (GET) loads the drug catalogue into `CreatePrescriptionViewModel.AvailableDrugs`.
2. `CreatePrescription(CreatePrescriptionViewModel)` (POST) builds `Prescription` + `PrescriptionItem`s and calls `IPrescriptionService.CreateAsync`.
3. `CreateAsync` validates every line (drug exists, stock sufficient, not expired), returns **all** validation errors at once, then in a transaction inserts the prescription (status `Pending`, `MedicationTotal` computed) and **decrements the drug stock**.
4. The prescription appears in the pharmacist's pending queue.

### 18.3 Pharmacist dispenses
1. `PharmacistController.ProcessPrescription(id)` (GET) shows line items, current stock and alternatives.
2. `DispensePrescription(id, selectedItemIds, dispensingNotes)` (POST) decrements stock for the selected lines and records `DispensingNotes`.
3. `MarkPrescriptionCompleted(id)` (POST) → `IPharmacistService.MarkPrescriptionCompletedAsync` → transactional stock deduction, status `Completed`, `DispensedDate`, `DispensedBy`.
4. A `PharmacistPrescriptionNote` can be attached; if `NotifyDoctor` is set, a `DoctorReviewRequest`/notification is raised for the prescribing doctor.

### 18.4 Billing
1. `AdminController.GenerateInvoice(patientId)` (GET) loads recent appointments, recent prescriptions and existing invoices.
2. `GenerateInvoice(GenerateInvoiceViewModel)` (POST) creates the `Invoice` with `ConsultationFee` + `MedicationTotal` = `TotalAmount`, `PaymentStatus = Pending`.
3. `UpdatePaymentStatusPost(id)` (POST) sets `PaymentStatus = Paid`, `AmountPaid`, `PaymentDate`.
4. `PatientController.ProcessPayment(invoiceId, amount, method, notes)` lets the patient settle their own invoice, including partial payments.
5. `DailyReportService` aggregates pharmacy invoices (`PrescriptionId != null`) per day; `DailyReportBackgroundService` persists a `DailyReport` row for yesterday every night.

### 18.5 Doctor leave
1. `DoctorController.RequestLeave(LeaveRequestViewModel)` (POST) → `ILeaveService.CreateLeaveRequestAsync` → `LeaveRequest` (Pending).
2. `AdminController.ApproveLeaveRequest(requestId, notes)` (POST) → `ILeaveService.ApproveLeaveRequestAsync` → creates an approved `DoctorLeave` **and** approves the request.
3. `AdminController.RegisterDoctorLeave(RegisterDoctorLeaveViewModel)` (POST) → `ILeaveService.RegisterDoctorLeaveAsync` → validated against overlapping approved leaves.
4. `ILeaveService.IsDoctorOnLeaveAsync(doctorId, date)` reports whether a doctor is on leave.

### 18.6 No-show risk scoring
1. `AdminController.ManageAppointments` / `DoctorController.ViewAppointments` call `INoShowRiskService.CalculateRiskScoresAsync(ids)`.
2. Cached `Appointment.NoShowRiskScore` values are reused; otherwise `POST /predict` is called per appointment.
3. Failures fall back to the local heuristic; the UI shows a "local estimate" badge via `AnalyticsServiceAvailable` / `IsUsingAI`.
4. `RiskLevelHelper` buckets each score and the views filter by the selected level.

### 18.7 Survey lifecycle
`AdminController.CreateSurvey(SurveyEditorViewModel)` → persists the survey + questions/options → `GetSurveyTargetPatientsAsync` resolves the audience → creates `SurveyAssignment` rows → `PatientController.TakeSurvey(id)` (GET) renders the questions → `TakeSurvey(SurveyResponseViewModel)` (POST) writes `SurveyAnswer` rows and flips the assignment to `Completed`.

---

## 19. Known Issues, Defects & Risks

### Critical

1. **`AdminController` is reachable by any Doctor** — the class attribute is `[Authorize(Roles = "Admin,Doctor")]`, so doctors can list/edit patients, create doctors, approve purchase requests, change specialization fees, generate invoices and change payment status. Only complaints and surveys are Admin-only. Fix: change the class attribute to `[Authorize(Roles = "Admin")]` and re-apply `[Authorize(Roles = "Doctor")]` (or move `DailyVisitingPatientsReport` into `DoctorController`).
2. **Hard-coded fallback admin credentials** — `AdminSeeder` falls back to `admin@hospital.com` / `Admin@123` when User Secrets are not configured. Any deployment that forgets `dotnet user-secrets set` ships a publicly known admin password.
3. **SQL Server connection string committed** in `appsettings.json`, pointing at a specific developer machine.
4. **`app.run(debug=True, host='0.0.0.0')`** in the Flask service exposes the Werkzeug debugger on all interfaces.
5. **`NoShowAPI:ApiKey` is configured but never used**; the ML endpoint is completely unauthenticated.

### High

6. **Account lockout never triggers.** `Program.cs` configures Identity lockout (5 attempts / 5-minute window), but **both** login implementations pass `lockoutOnFailure: false`:
   - `Controllers/AccountController.cs` → `PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false)`
   - `Areas/Identity/Pages/Account/Login.cshtml.cs` → `PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false)` (with the scaffold comment "To enable password failures to trigger account lockout, set lockoutOnFailure: true")

   Brute-force protection is therefore effectively disabled on both login screens.
7. **Corrupted JavaScript in `Views/Patient/BookAppointment.cshtml`** — an orphaned fragment (duplicate `this.classList.remove(...)` / `showToast(...)` block at lines ~229–235) sits outside any function and leaves the braces unbalanced. Extracting the `@section Scripts` block and running `node --check` fails with `SyntaxError: missing ) after argument list`, so the **entire** script block fails to parse and no booking-page JS runs at all. `initializeAssistant()` (line 276) additionally has an empty `if (patientId) { ... }` body.
8. **`/Patient/GetAppointmentSuggestions` does not exist** — the assistant UI always 404s.
9. **`AccessDeniedPath = "/Account/AccessDenied"`** points to a non-existent action; there is no `Views/Account/AccessDenied.cshtml`. Role-denied users get a 404.
10. **Two competing login UIs** — `/Account/Login` (MVC) and `/Identity/Account/Login` (scaffolded Razor Page), with different look-ups and behaviours.
11. **Test project is not in `HCMS4.sln`** — `dotnet test HCMS4.sln` silently runs zero tests in CI.
12. **`net6.0` is out of support** (build emits `NETSDK1138`); the test project is on `net10.0` while the app is on `net6.0`.
13. **44 NuGet advisories** on the test project, including a **critical** `System.Drawing.Common 4.7.0` vulnerability (`GHSA-rxg9-xrhp-64gj`).

### Medium

14. **`Drug` mixes UTC and local time** — `CreatedAt`/`UpdatedAt` use `DateTime.UtcNow` while `ExpiryStatus` compares `ExpiryDate` to `DateTime.Now`. `DrugService.GetAllAsync` and `GetExpiringDrugsAsync` also compare `ExpiryDate` against `DateTime.UtcNow`. `PrescriptionService` uses `DateTime.UtcNow` for expiry checks. The same inconsistency exists in `AppointmentService.CancelAsync`, which compares a local appointment time against `DateTime.UtcNow`, and in `PatientController.ProcessPayment`, which stamps `PaymentDate = DateTime.Now`.
15. **Past appointments only auto-complete when an admin opens the appointments page.** `AppointmentService.UpdatePastScheduledToCompletedAsync()` has exactly one call site — `AdminController.ManageAppointments` (`AdminController.cs:358`). `DailyReportBackgroundService` does **not** call it, so `Scheduled` appointments stay `Scheduled` for doctors/patients until an administrator loads `/Admin/ManageAppointments`.
16. **`LeaveService.NotifyManagerAsync` is a stub** — `LeaveService.cs:186` only calls `_logger.LogInformation(...)` and returns `Task.CompletedTask`; no `UserNotification` is created.
17. **`LeaveService.RejectLeaveRequestAsync` discards the rejection reason** — the `reason` parameter (`LeaveService.cs:107`) is never assigned to the entity, and `LeaveRequest` has no reason column. The rejection is logged only.
18. **`AppointmentService.CreateAsync` does not validate that the appointment is in the future** (the model attribute only fires on model binding in MVC paths that bind the entity, not on direct service calls).
19. **`PharmacistService.ProcessPrescriptionAsync` is a thin alias** — `PharmacistService.cs:153` simply `return await MarkPrescriptionCompletedAsync(...)` with the comment "ProcessPrescription is essentially the same as MarkPrescriptionCompletedAsync / but we keep it separate for potential future differentiation". Dead API surface.
20. **`ConcurrencyEntity` is defined but unused** — no entity has a `RowVersion`, so the `DbUpdateConcurrencyException` handlers in `DrugService`/`PrescriptionService` can never trigger.
21. **`wwwroot/assets/css/dashboard.css` is orphaned** (no view references it).
22. **jQuery and the validation libraries are loaded twice** in `_LayoutAuth.cshtml` (CDN 3.6.0 + CDN 3.7.1 + local copies).
23. **`Drug.ExpiryStatus` / `DrugService` expiry thresholds are inconsistent**: the model uses 30/60-day buckets, `DrugService.GetAllAsync("valid")` uses only the 30-day threshold, and `expiring-soon` means 30 days while `DrugExpiryShortWarningDays` (60) is never used by the service.
24. **`DoctorLeave` and `LeaveRequest` are two parallel leave concepts** — `RegisterDoctorLeaveAsync` manages `DoctorLeave` while `RejectLeaveRequestAsync`/`ApproveLeaveRequestAsync` manage `LeaveRequest`, and the two are never reconciled. Both are surfaced in the admin UI. `BusinessRules` is applied correctly elsewhere: `IsPrescriptionEligibleForReissue` (`PatientController.cs:1118`) reads `BusinessRules.PrescriptionReissueValidityDays`, and `PatientController.CreateComplaint` reads `BusinessRules.ComplaintAttachmentMaxBytes`.
25. **`DailyReportService.GetFilteredReportAsync` reports `ReportDate = DateTime.UtcNow`** rather than the requested range.
26. **`NoShowRiskService` sends constant clinical features** (`Scholarship`, `Hipertension`, `Diabetes`, `Alcoholism`, `Handcap`, `SMS_received`, `Gender`, `Neighbourhood` are all hard-coded) — the ML model is effectively scoring on age + dates only.
27. **`INoShowRiskService.IsServiceAvailable` / `IsUsingAI` / `LastErrorMessage` are mutable instance state on a scoped service** — they reset per request and are not thread-safe across concurrent calls within one request scope.
28. **`AdminSeeder` creates a scope inside a scope** (`Program.cs` already creates one) — harmless but redundant.
29. **Complaint attachments are written to `wwwroot/uploads/complaints`** — the directory is created at runtime and is served as static content, so uploaded files are publicly retrievable by URL.
30. **`PatientController.ProcessPayment` allows overpayment.** The guard is `amount <= 0 || amount > invoice.TotalAmount` — it compares against the **total**, not the **remaining balance** (`TotalAmount - AmountPaid`). Paying an already-fully-paid invoice again pushes `AmountPaid` above `TotalAmount` (e.g. total 100, paid 100, pay 100 again → `AmountPaid = 200`). The `else if` branch then leaves `PaymentStatus` untouched, so the invoice reports `Paid` while the recorded payment exceeds the amount due.
31. **The payment method is never persisted.** `Invoice` has no payment-method column. `ProcessPayment` only embeds `paymentMethod` inside the free-text `Invoice.Notes` string — and only when `notes` is non-empty. A payment submitted without notes loses the method entirely.

### Low / cosmetic

32. `IMPLEMENTATION_STATUS.md`, `IMPLEMENTATION_SUMMARY.md` and `TESTING_GUIDE.md` (all under `HCMS4/`) are **UTF-8 without a BOM**. The Arabic content is valid UTF-8, but BOM-less files are mis-detected as ANSI/Windows-1252 by some Windows editors and legacy tools, which renders the Arabic as mojibake. Saving them with a BOM (or as UTF-16LE) removes the ambiguity.
33. `TESTING_GUIDE.md` documents admin credentials (`admin@gmail.com` / `SecureP@ssw0rd123`) that do not match the seeder's defaults — stale documentation.
34. `PrescriptionItem.DrugId` is declared `[Required]` but typed `int?`; `PrescriptionItem.Drug` is non-nullable while the FK is nullable.
35. `PatientController.GetDoctorConsultationFee` and `AdminController.GetDoctorConsultationFee` are separate implementations of the same JSON endpoint.
36. `BaseController`'s error-handling helpers are commented out; controllers duplicate their own try/catch blocks.
37. `HomeController` has no `[Authorize]`, so `Home/Index` (which queries published articles) is public — intentional, but worth noting.
38. `PharmacistController` and `AccountController` do not derive from `BaseController`, so they duplicate the current-patient/doctor/user lookup logic.

---

## 20. Recommended Next Steps

1. **Fix the authorization model** — restrict `AdminController` to `Admin`; move doctor-only actions to `DoctorController`.
2. **Remove the default admin credentials** — fail fast if `AdminSettings:*` is missing instead of falling back to `Admin@123`.
3. **Enable account lockout** — pass `lockoutOnFailure: true` in both `AccountController.Login` and `Areas/Identity/Pages/Account/Login.cshtml.cs`.
4. **Repair `Views/Patient/BookAppointment.cshtml`** — remove the duplicated fragment, implement or remove `initializeAssistant()`, and either add `PatientController.GetAppointmentSuggestions` or delete the call.
5. **Add `AccountController.AccessDenied`** + `Views/Account/AccessDenied.cshtml` (or point `AccessDeniedPath` at the Identity page).
6. **Add `HCMS4.Tests` to `HCMS4.sln`** and align the target frameworks (`net8.0`+ for both).
7. **Standardise on UTC** — replace `DateTime.Now` in `Drug.ExpiryStatus`, `DrugService` expiry filters, `AppointmentService.CancelAsync` and `PatientController.ProcessPayment` with a single, explicit convention.
8. **Call `UpdatePastScheduledToCompletedAsync()`** from `DailyReportBackgroundService` (or a dedicated scheduler) so appointments auto-complete.
9. **Implement `LeaveService.NotifyManagerAsync`** and persist the leave rejection reason.
10. **Remove the duplicate jQuery/validation includes** in `_LayoutAuth.cshtml`; delete or wire up `dashboard.css`.
11. **Serve complaint attachments outside `wwwroot`** (or behind an authorization check) so uploaded files are not publicly reachable.
12. **Protect the Flask service** — disable `debug`, bind to `127.0.0.1` behind a reverse proxy, and add API-key validation that the .NET client actually sends.
13. **Add controller-level tests** (WebApplicationFactory) and tests for the untested services.
14. **Move the connection string to User Secrets / environment variables**, and add a UTF-8 BOM to the three Arabic Markdown docs so Windows editors render them correctly.

---

## Appendix A — Complete File Map

### A.1 `HCMS4/` (web application)

```
Program.cs                                  Startup / DI / middleware / migration+seed
HCMS4.csproj                                net6.0 web project
appsettings.json                            ConnectionStrings, NoShowAPI, Logging
appsettings.Development.json                LogLevel overrides
dotnet-tools.json                           dotnet-ef 6.0.35 (rollForward: false)
IMPLEMENTATION_STATUS.md                    Requirements vs. implementation report (UTF-8, no BOM)
IMPLEMENTATION_SUMMARY.md                   Feature-completion summary (UTF-8, no BOM)
TESTING_GUIDE.md                            Manual test guide (UTF-8, no BOM)

Properties/launchSettings.json              Project + IIS Express profiles
Properties/serviceDependencies.json         Empty stub
Properties/serviceDependencies.local.json   Empty stub

Data/ApplicationDbContext.cs                29 DbSets, fluent config, seed data (360 lines)
Data/Migrations/                            24 migrations + 23 Designer files + ModelSnapshot
Data/Migrations/ApplicationDbContextModelSnapshot.cs

Models/ApplicationUser.cs                   IdentityUser + FirstName/LastName/FullName
Models/Patient.cs                           Patient + PatientStatus
Models/Doctor.cs                            Doctor (Specialization, ratings, availability)
Models/Pharmacist.cs                        Pharmacist + shift/active flag
Models/Appointment.cs                       Appointment + AppointmentStatus + FutureDateAttribute
Models/Prescription.cs                      Prescription + PrescriptionStatus
Models/PrescriptionItem.cs                  PrescriptionItem
Models/Drug.cs                              Drug + nested FutureDateAttribute + ExpiryStatus
Models/ClinicalNote.cs                      ClinicalNote
Models/Invoice.cs                           Invoice + PaymentStatus
Models/Specialization.cs                    Specialization (seeded)
Models/Supplier.cs                          Supplier
Models/PurchaseRequest.cs                   PurchaseRequest + PurchaseRequestStatus
Models/PurchaseRequestItem.cs               PurchaseRequestItem
Models/DoctorLeave.cs                       DoctorLeave + LeaveType + LeaveStatus
Models/LeaveRequest.cs                      LeaveRequest
Models/VisitRating.cs                       VisitRating
Models/Complaint.cs                         Complaint + ComplaintType + ComplaintStatus
Models/PrescriptionReissueRequest.cs        PrescriptionReissueRequest + ReissueRequestStatus
Models/DoctorReviewRequest.cs               DoctorReviewRequest + ReviewReason + ReviewRequestStatus
Models/PharmacistPrescriptionNote.cs        PharmacistPrescriptionNote
Models/MedicalArticle.cs                    MedicalArticle + ArticleStatus
Models/Survey.cs                            Survey + SurveyTargetAudience + SurveyStatus
Models/SurveyQuestion.cs                    SurveyQuestion + SurveyQuestionType
Models/SurveyQuestionOption.cs              SurveyQuestionOption
Models/SurveyAssignment.cs                  SurveyAssignment + SurveyAssignmentStatus
Models/SurveyAnswer.cs                      SurveyAnswer
Models/UserNotification.cs                  UserNotification + NotificationType
Models/SystemActivityLog.cs                 SystemActivityLog
Models/DailyReport.cs                       DailyReport
Models/ErrorViewModel.cs                    ErrorViewModel
Models/Common/BusinessRules.cs              Centralised constants
Models/Common/ConcurrencyEntity.cs          Unused optimistic-concurrency base
Models/Common/Pagination.cs                 PaginatedResult<T> + PaginationParams
Models/Common/RiskLevelHelper.cs            Risk bucketing helpers
Models/Common/ServiceResult.cs              ServiceResult + ServiceResult<T>

Services/IDailyReportService.cs             Interface
Services/DailyReportService.cs              Daily pharmacy report generation + filtering
Services/DailyReportBackgroundService.cs    Midnight hosted service
Services/IPatientService.cs                 Interface
Services/PatientService.cs                  Patient CRUD, pagination, disable/enable
Services/IAppointmentService.cs             Interface
Services/AppointmentService.cs              Booking, reschedule, cancel, slot checks
Services/IPrescriptionService.cs            Interface
Services/PrescriptionService.cs             Prescription lifecycle + transactional stock
Services/IDrugService.cs                    Interface
Services/DrugService.cs                     Inventory CRUD, stock, expiry, low stock
Services/IPharmacistService.cs              Interface
Services/PharmacistService.cs               Dispensing + pharmacist profile
Services/LeaveService.cs                    ILeaveService + LeaveService (requests & leaves)
Services/NotificationService.cs             INotificationService + NotificationService
Services/ActivityLogService.cs              IActivityLogService + ActivityLogService
Services/NoShowRiskService.cs               INoShowRiskService + DTOs (ML client)
Services/AdminSeeder.cs                     Role + admin-user seeding

Controllers/BaseController.cs               _context/_logger + current patient/doctor/user helpers
Controllers/HomeController.cs               Public landing pages
Controllers/AccountController.cs            Login/Register/Logout/Profile/EditProfile/ChangePassword
Controllers/AdminController.cs              Admin back-office (103 KB, 63 action methods)
Controllers/DoctorController.cs             Doctor workspace
Controllers/PatientController.cs            Patient portal
Controllers/PharmacistController.cs         Pharmacy workspace

ViewModels/  (61 files, 74 classes)         See §13

Views/_ViewImports.cshtml                   Usings + tag helpers
Views/_ViewStart.cshtml                     Runtime layout selection
Views/Shared/_Layout.cshtml                 Guest layout
Views/Shared/_LayoutAuth.cshtml             Authenticated layout
Views/Shared/_Layout.cshtml.css             Scoped guest CSS
Views/Shared/_ValidationScriptsPartial.cshtml
Views/Shared/_AppointmentStatusBadge.cshtml
Views/Shared/Error.cshtml
Views/Home/     (4 views)                   Index, About, Contact, Privacy
Views/Account/  (4 views)                   Login, Register, Profile, EditProfile
Views/Admin/    (33 views)                  See §14
Views/Doctor/   (11 views)
Views/Patient/  (11 views)
Views/Pharmacist/ (12 views)
Views/Shared/   (5 views)                   _Layout, _LayoutAuth, _Layout.cshtml.css,
                                            _ValidationScriptsPartial, _AppointmentStatusBadge, Error
Areas/Identity/Pages/Account/...            Scaffolded Identity UI (Account + Manage)

wwwroot/assets/{css,js,scss,img,vendor}/    Theme + vendored libraries
wwwroot/lib/{jquery,jquery-validation,jquery-validation-unobtrusiv}/
```

### A.2 `HCMS4.Tests/`

```
HCMS4.Tests.csproj        net10.0, xUnit 2.9.3, EF InMemory, Moq, coverlet
TestDbContextFactory.cs   InMemory ApplicationDbContext helper
AppointmentServiceTests.cs   9 tests
PrescriptionServiceTests.cs  7 tests
DrugServiceTests.cs          8 tests
PatientServiceTests.cs       6 tests
DailyReportServiceTests.cs   5 tests
AdminServiceTests.cs         9 tests
```

### A.3 `medical-appointment-no-shows-main/medical-appointment-no-shows-main/`

```
scripts/predict.py            Flask /predict endpoint (port 9696)
scripts/train.py              Training pipeline
scripts/constants.py          Shared constants
scripts/predict-test.py       Offline test harness
scripts/predict-test-aws.py   EC2/AWS variant
scripts/../models/*.bin       Pickled (scaler, ohe, model) bundles
data/KaggleV2-May-2016.csv    Source dataset (110k rows)
data/no-show-patients.jpg     Dataset image
Dockerfile, Pipfile, Pipfile.lock, README.md, LICENSE
docs/setting-up-ec2-eb.md     EC2/Elastic Beanstalk deployment notes
notebooks/notebook.ipynb      Exploratory analysis
opt/environment.yml           Conda environment
opt/optional_requirement.txt
```
