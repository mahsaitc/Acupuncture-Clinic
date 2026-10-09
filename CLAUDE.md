# Acupuncture Clinic: notes for Claude Code

ASP.NET Core 10 site (Persian first, English at `/en`). See README.md for running it.

## The point charts use rendered photos: do not go back to the drawings

The body, head, arm and leg charts (`front`, `back`, `side`, `head-front`, `head-side`, `arm-inner`, `arm-outer`, `leg-inner`,
`leg-outer`) are PNGs rendered from the free Z-Anatomy 3D model (CC BY-SA 4.0). Only `ear` is still a drawing. These files
make the photo charts work, so keep them and never delete or overwrite them with drawings:

- `src/Clinic.Web/wwwroot/img/charts/*.png`: the rendered charts (the `*.svg` next to them are the old drawings, kept only for `--drawn`).
- `tools/charts/photo/silhouettes.json` and `tools/blender/charts.json`: the frames and outlines the generator needs.

How the library is built:

- `python3 tools/charts/generate.py` writes `AcupointLibrary.Data.g.cs`. When `photo/silhouettes.json` exists it moves every point of
  `points.py` onto the photo charts (`tools/charts/photo.py`, by anatomical landmarks) and marks those charts as `png`.
  `points.py` still holds positions measured on the old drawings: do not re-measure them on the photos, fix single points in
  `ADJUST` (`photo.py`) instead.
- If the silhouettes file is missing while the PNGs exist, the generator stops with an error instead of falling back silently.
  Use `--drawn` only to go back to the drawings deliberately (and then update `BodyMapTests.Charts_are_the_rendered_photos_except_the_ear`).
- Never edit `AcupointLibrary.Data.g.cs` by hand; regenerate it. A new point goes in `points.py`; run `generate.py` and `tools/charts/preview.py sheet.html` to check it.
- To re-render the charts (needs Blender and the Z-Anatomy file): `docs/body-images-render-guide.fa.md`, then `python3 tools/charts/silhouettes.py`.
- The credit line under the chart (Z-Anatomy / BodyParts3D, CC BY-SA 4.0) in `Pages/Shared/_BodyMap.cshtml` must stay.

`tests/Clinic.Tests/BodyMapTests.cs` fails if a chart other than the ear is not a photo again.
