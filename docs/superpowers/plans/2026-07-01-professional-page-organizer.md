# Professional Page Organizer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the existing folder gallery into a non-destructive professional page organizer with preserved filtering, page reordering, rotation, cropping, cover selection, reading direction, and final PDF layout preview.

**Architecture:** A shared `PageEdit` manifest carries ordered per-page operations from WinForms to Core. The gallery edits only metadata and transformed thumbnails; Core streams source bitmaps, applies rotation/crop, groups output pages, and disposes each bitmap after writing. Existing directory and archive export paths remain unchanged.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms, System.Drawing, iTextSharp, existing Lua configuration.

---

### Task 1: Shared page manifest

**Files:**
- Create: `ImgsToPDFCore/PageManifest.cs`
- Modify: `ImgsToPDFCore/ImgsToPDFCore.csproj`
- Modify: `ImgsToPDF/ImgsToPDF.csproj`

- [ ] Define `PageEdit` with `Path`, `OriginalIndex`, `Rotation`, normalized `RectangleF Crop`, and `IsCover`.
- [ ] Define versioned line serialization using Base64 UTF-8 paths and invariant-culture numeric fields:

```text
ImagesToPDF.PageManifest/1
<base64 path>|<rotation>|<left>|<top>|<width>|<height>|<cover 0/1>
```

- [ ] Reject missing files, unsupported rotations, non-finite values, zero-sized crops, and crop rectangles outside 0–1.
- [ ] Link the same source file into the UI project so both executables use identical parsing rules.
- [ ] Add a DEBUG round-trip self-check covering a Chinese path, rotation, crop, cover, and ordering.

### Task 2: Core ordered-page export

**Files:**
- Modify: `ImgsToPDFCore/Program.cs`
- Modify: `ImgsToPDFCore/PDFWrapper.cs`
- Modify: `ImgsToPDFCore/config.lua`
- Modify: `ImgsToPDF/Core/config.lua`

- [ ] Add `--page-manifest` and allow it as a valid input only when `--dir-path` names the source folder.
- [ ] Pass the manifest as the fifth `Config:PreProcess` argument; create the normal output name from the source directory and call `PDFWrapper.ImagesToPDFManifest`.
- [ ] Apply edits in this order:

```csharp
bitmap.RotateFlip(ToRotateFlipType(page.Rotation));
bitmap = CropNormalized(bitmap, page.Crop);
```

- [ ] Preserve manifest line order without Lua or Core sorting.
- [ ] In duplex modes, emit cover pages and landscape pages alone; combine only adjacent portrait pages in the selected reading direction.
- [ ] Reuse the same edited dimensions for uniform-width calculation and dispose all intermediate bitmaps.

### Task 3: Organizer state and main-form handoff

**Files:**
- Modify: `ImgsToPDF/ImgsToPDF.cs`
- Modify: `ImgsToPDF/ImgsToPDF.resx`
- Modify: `ImgsToPDF/ImgsToPDF.zh.resx`
- Modify: `ImgsToPDF/GalleryForm.resx`
- Modify: `ImgsToPDF/GalleryForm.zh.resx`

- [ ] Rename the main button and gallery title to `专业整理` / `Professional Organizer`.
- [ ] Replace the selected-path-only handoff with ordered `PageEdit` values and the chosen layout index.
- [ ] Write selected pages to a temporary manifest and invoke Core with both `--dir-path` and `--page-manifest`; delete the manifest in `finally`.
- [ ] Reopening the organizer receives the current edits; cancelling leaves main-form state unchanged.

### Task 4: Existing gallery preservation and drag ordering

**Files:**
- Modify: `ImgsToPDF/GalleryForm.cs`

- [ ] Extend `GalleryItem` with original index, rotation, crop, and cover metadata while preserving extension filtering, selection, statistics, zoom, and external viewer behavior.
- [ ] Add left-button drag/drop between cards in page view. Moving a visible item inserts it before the target in the full list while hidden items retain relative order.
- [ ] Keep checkbox clicks as the only include/exclude control.
- [ ] Add a visible `一键全部重置` / `Reset All` button to the existing selection summary card. Confirmation clears rotation/crop/cover and restores natural order while preserving selection, filters, zoom, and reading direction.

### Task 5: Context operations and crop dialog

**Files:**
- Create: `ImgsToPDF/CropForm.cs`
- Modify: `ImgsToPDF/ImgsToPDF.csproj`
- Modify: `ImgsToPDF/GalleryForm.cs`

- [ ] Show `Rotate Left`, `Rotate Right`, `Crop…`, `Set as Cover`, and `Restore This Page` when right-clicking one card.
- [ ] Transform an existing normalized crop when rotating so the same visible content remains selected:

```csharp
// clockwise
new RectangleF(1f - crop.Bottom, crop.Left, crop.Height, crop.Width);
// counter-clockwise
new RectangleF(crop.Top, 1f - crop.Right, crop.Height, crop.Width);
```

- [ ] Implement a modal crop control with aspect-fit image painting, dark outside mask, four edge and four corner handles, a 2% minimum crop, and Reset/Cancel/Apply buttons.
- [ ] Restore one page to rotation 0, full crop, no cover, and its natural-order position without changing its checkbox.
- [ ] Draw `[封面]`, rotation, and crop badges plus an orange status dot; do not mark reordered pages.

### Task 6: Output preview and bounded thumbnails

**Files:**
- Modify: `ImgsToPDF/GalleryForm.cs`

- [ ] Add an output card in the left sidebar containing Page/PDF Preview toggles and Single/LTR/RTL reading-direction choices.
- [ ] Page mode remains an interactive thumbnail grid; PDF mode is a dark, read-only vertical reader that renders large finished pages with the exact same cover/landscape/duplex rules as Core.
- [ ] Rebuild preview immediately after selection, order, edit, cover, or reading-direction changes.
- [ ] Render previews from existing thumbnails and cap retained thumbnails at 200 using last-access order; never cache full-size images outside the active crop dialog.

### Task 7: Verification

**Files:**
- No additional production files.

- [ ] Run `git diff --check`; expected: no errors.
- [ ] After explicit test authorization, run `msbuild ImgsToPDF.sln /p:Configuration=Debug /p:Platform=x64`; expected: build succeeds.
- [ ] Manually verify existing extension filtering, select-all/clear-visible, counts, zoom/reset, and double-click viewer.
- [ ] Manually verify drag order, all context actions, per-page restore, reset-all preservation rules, layout synchronization, badges, and PDF preview grouping.
- [ ] Export Single/LTR/RTL PDFs and compare page order, rotation, crop, cover isolation, landscape handling, Unicode paths, and memory stability with the preview.
