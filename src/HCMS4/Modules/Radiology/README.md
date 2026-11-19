# Radiology module (empty skeleton)

The radiology (imaging) department. No code yet. It is built in **Increments 4 and 5**.

## Requirements

| Code | Requirement | Increment |
|---|---|---|
| R1 | Create an imaging request for a patient | 4 |
| R2 | Manage imaging session appointments without overlaps | 4 |
| R4 | Manage request and imaging types (add, edit, delete) | 4 |
| R3 | Write the imaging report and send it to the doctor and the patient | 5 |
| R5 | Add a radiologist note | 5 |
| R6 | Daily radiology report | 5 |

Use cases, flows and acceptance tests (TC-…) are in `docs/REQUIREMENTS.md`. The diagrams are `docs/diagrams/UCD_R.puml` and `UC-R01`…`UC-R06`.

## Already prepared

- **Role:** `Radiologist`, seeded by `AdminSeeder`.
- **Permissions:** `Permissions.Radiology.ScheduleSession` and `WriteReport`, mapped in `Shared/Identity/Authorization/RolePermissions.cs`.
- **Imaging requests and sessions:** use `Shared/Workflow/StateMachine<TStatus>`. Each change records the time and the user.

## Planned

- **Migrations:**
  - `Inc4_R_RadiologyScheduling`: ImagingType, Device, ImagingRequest, ImagingSession, plus an index that prevents overlapping sessions on one device.
  - `Inc5_R_RadiologyReports`: ImagingReport, ImagingNote.
- **Folders:** `Controllers/`, `Services/`, `Models/`, `Data/`, `Views/`.
- **Registration:** `RadiologyModule.AddRadiologyModule()`.
