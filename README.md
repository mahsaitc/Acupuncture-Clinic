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
| Home page and contact, users, services | yes | no | no |

Blog posts and articles are written in Markdown. Raw HTML is not allowed and the output is sanitized.

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
