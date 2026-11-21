# Lab module

The laboratory department, built in **Increments 2 and 3** (`L1`–`L11`). Use cases, flows and acceptance
tests (TC-…) are in `docs/REQUIREMENTS.md`; diagrams are `docs/diagrams/UC-L01`…`UC-L11`.

## Contents

| Folder | What is in it |
|---|---|
| `Models` | `LabTechnician`, `LabTest`, `LabRequest` (+ `LabRequestStatus`), `LabRequestItem`, `LabResult` (+ `LabResultRules`), `LabNote`, `LabAppointment` (+ `LabSchedule`) |
| `Services` | `LabTechnicianService`, `LabTestService`, `LabRequestService`, `LabRequestWorkflow`, `LabAppointmentService`, `LabResultService`, `LabHistoryService` |
| `Controllers` | Lab pages (`/Lab/...`), the doctor's lab pages (`/Doctor/...`), the patient's lab pages (`/Patient/...`), admin technician accounts (`/Admin/...`) |
| `Components` | `CriticalLabResultsViewComponent`, shown on the doctor dashboard |

## Rules

- **Lab requests use `Shared/Workflow/StateMachine<TStatus>`** (`LabRequestWorkflow`).
  - Allowed transitions only.
  - Each change records the time and the user.
- **Permissions:** `Permissions.Lab.*`, mapped in `Shared/Identity/Authorization/RolePermissions.cs`.
- **Time:** all times are Damascus time, read through `IClinicClock`.
- **Other modules:** the module reaches them only through their interfaces (`IMedicalRecordService`, `IPatientService`, `INotificationService`).
