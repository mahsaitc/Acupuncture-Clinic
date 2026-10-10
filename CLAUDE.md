# Acupuncture Clinic: notes for Claude Code

ASP.NET Core 10 site (Persian first, English at `/en`). See README.md for running it.

## The point charts use rendered photos: do not go back to the drawings

All ten charts (`front`, `back`, `side`, `head-front`, `head-side`, `arm-inner`, `arm-outer`, `leg-inner`, `leg-outer`, `ear`)
are PNGs rendered from the free Z-Anatomy 3D model (CC BY-SA 4.0). These files make the photo charts work, so keep them and never
delete or overwrite them with drawings:

- `src/Clinic.Web/wwwroot/img/charts/*.png`: the rendered charts (the `*.svg` next to them are the old drawings, kept only for `--drawn`).
- `tools/charts/photo/silhouettes.json` and `tools/blender/charts.json`: the frames and outlines the generator needs.

How the library is built:

- `python3 tools/charts/generate.py` writes `AcupointLibrary.Data.g.cs`. When `photo/silhouettes.json` exists it moves every point of
  `points.py` onto the photo charts (`tools/charts/photo.py`, by anatomical landmarks) and marks those charts as `png`.
  `points.py` still holds positions measured on the old drawings: do not re-measure them on the photos, fix single points in
  `ADJUST` (`photo.py`) instead.
- If the silhouettes file is missing while the PNGs exist, the generator stops with an error instead of falling back silently.
  Use `--drawn` only to go back to the drawings deliberately (and then update `BodyMapTests.Charts_are_the_rendered_photos`).
- Never edit `AcupointLibrary.Data.g.cs` by hand; regenerate it. A new point goes in `points.py`; run `generate.py` and `tools/charts/preview.py sheet.html` to check it.
- To re-render the charts (needs Blender and the Z-Anatomy file): `docs/body-images-render-guide.fa.md`, then `python3 tools/charts/silhouettes.py`.
- The credit line under the chart (Z-Anatomy / BodyParts3D, CC BY-SA 4.0) in `Pages/Shared/_BodyMap.cshtml` must stay.

`tests/Clinic.Tests/BodyMapTests.cs` fails if any chart is not a photo again.

## Working on this project from more than one place

One branch is the source of truth: `phase-2-medical-records` on GitHub. Chat sessions do not share memory; the repository does.

- Start every session with `git pull` on that branch, and finish with `dotnet test` and `git push`. Do not work in two sessions at the same time.
- Never rewrite history or force-push this branch. Commits the owner pushed from the PC (other author e-mail) stay as they are.
- Use one session for all work (site features, charts, tests). Rendering the charts needs Blender and the Z-Anatomy `.blend` on the owner's
  Windows PC: tell the owner the exact commands (`docs/body-images-render-guide.fa.md`); the owner runs them and pushes the PNGs.
- The owner prefers short, one-command-per-block instructions in Persian, and checks results by sending screenshots.

### State of the photo charts (for whoever continues)

- Done: 10 rendered charts (the ear too: only the auricle parts, with the skin around it cut away and the edge softened in `charts.json`), 390 points moved onto them by landmarks, credit line, guards (`generate.py`, `BodyMapTests`).
- The model is a nude adult male (Z-Anatomy has no female model); groin objects are left out of the renders. The leg charts are cut
  below the groin, so `SP12` and `LR12` are on the front chart only.
- Point positions are a first placement for the owner (a doctor) to review. Corrections go into `ADJUST` in `tools/charts/photo.py`,
  keyed by `(chart, code)`, then run `python3 tools/charts/generate.py`.
- Known blemishes in the renders: a small dark patch at the shoulder cut of `arm-outer`, a grey cap at the top of `leg-inner`; the ear mesh of the model is low-polygon (smoothed in the render).
- `dotnet` is not available in the cloud session that built this: run `dotnet test` on the PC after pulling.
- Test admin login (local only, from `dotnet user-secrets`): see README; never commit secrets.
