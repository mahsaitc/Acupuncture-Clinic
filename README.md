# Acupuncture Clinic

Bilingual (Persian / English) website and clinic management system for an acupuncture clinic, built with ASP.NET Core 10.

## Run locally

```bash
dotnet run --project src/Clinic.Web
```

The app creates a local SQLite database (`clinic.db`) and applies migrations on startup. Persian is served at `/`, English at `/en`.

To create the first admin account, set its credentials with user secrets (never in `appsettings.json`):

```bash
cd src/Clinic.Web
dotnet user-secrets set "Seed:AdminEmail" "you@example.com"
dotnet user-secrets set "Seed:AdminPassword" "a-strong-password"
dotnet user-secrets set "Seed:AdminName" "Dr. ..."
```

The seed admin also gets the Doctor role. Open the management panel at `/Admin`, set working hours under "Working hours", then patients can book.

## Management panel

`/Admin` is open to staff. What each role sees:

| Section | Admin | Doctor | Receptionist |
| --- | --- | --- | --- |
| Dashboard, appointments, patients, messages | yes | yes | yes |
| Working hours | if also a doctor | yes | no |
| Blog and medical articles | yes | yes | no |
| Medical records, sessions, patient files | if also a doctor | yes | no |
| Home page and contact, users, services, access log | yes | no | no |

Blog posts and articles are written in Markdown. Raw HTML is not allowed and the output is sanitized.

## Medical records and patient files

Doctors open a patient's record from **Patients → patient → Open medical record**. Receptionists never see clinical data.

- **Record**: history, medications, allergies, first-visit measurements, pulse and tongue diagnosis, TCM pattern, ICD-10 diagnosis, plan and consent.
  The national code and the diagnosis are encrypted in the database.
- **Treatment sessions**: date, type (acupuncture, catgut embedding, electroacupuncture, cupping, auricular, consultation), weight, waist, pain score
  and the points used, marked on a front and back body diagram. Pick standard points from the list, load a protocol, copy the last session,
  or click the diagram for a point of your own. Diagram positions are approximate.
- **Files**: radiology, lab results and photos (JPG, PNG, WebP, PDF, DICOM, up to 50 MB). The doctor chooses which ones the patient can see;
  patients can send their own results from **My files**.
- **Access log** (admin): every opening and change of a record or file, with user, time and IP.

### Back up these folders with the database

| Setting | Default | Holds |
| --- | --- | --- |
| `DataProtection:KeysPath` | `keys/` | Encryption keys. **Without them the encrypted fields and login cookies cannot be read.** |
| `PrivateFiles:Root` | `private-files/` | Patient files. Never put this folder under `wwwroot` or `media`. |

Keep both outside the web root on the server and include them in every backup.

## Tests

```bash
dotnet test
```

## Structure

| Project | Contents |
| --- | --- |
| `src/Clinic.Domain` | Entities and role names |
| `src/Clinic.Application` | Slot calculation, Jalali dates, clinic time zone |
| `src/Clinic.Infrastructure` | EF Core, ASP.NET Identity, booking service, seeding |
| `src/Clinic.Web` | Razor Pages UI, localization, media uploads |
| `tests/Clinic.Tests` | Unit and web tests |

UI strings are written in English in the code and translated in `src/Clinic.Web/Resources/SharedResource.fa.resx`. A test fails if a string has no Persian translation.

Uploaded hero media is stored in `src/Clinic.Web/media` (configurable with `Media:Root`) and is public. Medical files must never be stored there.
