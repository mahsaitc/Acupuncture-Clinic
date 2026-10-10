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

### Site owner (branding)

Each installation has one site owner, above the admin. Only the owner sees "Logo, name and colours" (`/Admin/Branding`):
logo, clinic name, the lines beside the logo, the home page label and title (fa and en) and the theme colours (olive is the default).
The owner role cannot be given from any page; the server's configuration names the account, and at every start-up that
account becomes the owner (and an admin) and anyone else loses the role. The admin cannot change the owner's roles,
deactivate it or edit its doctor file.

```bash
dotnet user-secrets set "Owner:Email" "owner@example.com"
```

If no account has that email yet, also set `Owner:Password` (and optionally `Owner:Name`) and it is created at start-up.
On a server use environment variables instead (`Owner__Email`, `Owner__Password`).

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

Receptionists register patients from **Patients → New patient**: name and mobile are required; personal details (national code,
birth date, marital status, father's name, job, education, phones, address, referrer, insurance, emergency contact, front desk notes)
are optional and editable later. A patient registered at the front desk has no password, and online sign-up with the same mobile asks
them to contact the clinic instead of creating a second account.

Doctors open a patient's record from **Patients → patient → Open medical record**. Receptionists never see clinical data.

- **Record**: follows the clinic's paper intake form, section by section: reason for the visit and goals, conditions checklist,
  medications, surgeries and allergies, general state (sleep, appetite, cravings, digestion, energy, cycle, mood), measurements with
  live BMI and BMR, TCM diagnosis (pulse, tongue, imbalance), treatment plan, consent and the doctor's notes.
  **Print form** lays the record out as the A4 form with the session table, ready to print or save as PDF.
  The national code and the diagnosis are encrypted in the database.
- **Treatment sessions**: date, type (acupuncture, catgut embedding, electroacupuncture, cupping, auricular, consultation), weight, waist, pain score
  and the points used. Type a point's code or name (about 190 points, ear points included) and it is marked on every chart it appears on:
  whole body (front, back, side), face and head, inner and outer arm, inner and outer leg, and ear. Points are saved by code and side.
  You can also load a protocol, copy the last session, or click a chart for a point of your own. Chart positions are approximate.
- **Files**: radiology, lab results and photos (JPG, PNG, WebP, PDF, DICOM, up to 50 MB). The doctor chooses which ones the patient can see;
  patients can send their own results from **My files**.
- **Access log** (admin): every opening and change of a record or file, with user, time and IP.

### Back up these folders with the database

| Setting | Default | Holds |
| --- | --- | --- |
| `DataProtection:KeysPath` | `keys/` | Encryption keys. **Without them the encrypted fields and login cookies cannot be read.** |
| `PrivateFiles:Root` | `private-files/` | Patient files. Never put this folder under `wwwroot` or `media`. |

Keep both outside the web root on the server and include them in every backup.

### Point charts

The chart drawings and the point library are generated from `tools/charts/` (Python 3, no packages):
`figures.py` draws the charts, `points.py` lists every point with its place on each chart, and

```bash
python3 tools/charts/generate.py
```

writes `src/Clinic.Web/wwwroot/img/charts/*.svg` and `src/Clinic.Application/Acupuncture/AcupointLibrary.Data.g.cs`.
`python3 tools/charts/preview.py sheet.html` draws all charts on one page for checking.

#### Photo-realistic charts

The charts can be rendered from the free Z-Anatomy 3D model instead of drawn (`tools/blender/`, see
`docs/body-images-render-guide.fa.md`): `blender -b Z-Anatomy.blend --python tools/blender/render_views.py -- --charts ...`
writes one PNG per chart and `charts-silhouettes.json`. Copy the PNGs to `src/Clinic.Web/wwwroot/img/charts/` and the JSON to
`tools/charts/photo/silhouettes.json`, then run `python3 tools/charts/generate.py`: the points of `points.py` are moved onto the
photos by landmarks (`tools/charts/photo.py`) and the app serves the PNGs. The ear chart is rendered too. Without
`silhouettes.json` the generator keeps the drawings. Positions are a first placement: check them with
`python3 tools/charts/preview.py sheet.html` and correct single points in `ADJUST` (`photo.py`).
The model is CC BY-SA 4.0 (Z-Anatomy, from BodyParts3D): credit it in the app.


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
