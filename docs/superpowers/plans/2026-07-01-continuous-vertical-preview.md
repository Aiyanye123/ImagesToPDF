# Continuous Vertical Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make vertical PDF preview scroll continuously like a browser while keeping horizontal preview page-based.

**Architecture:** Reuse `VirtualGalleryView` and its custom scrollbars. The vertical scrollbar stores a pixel offset over width-fitted page slots; painting calculates the visible page range and draws only those cached previews. Horizontal mode keeps the existing page-index behavior.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms/GDI+.

---

### Task 1: Separate vertical pixel scrolling from horizontal page switching

**Files:**
- Modify: `ImgsToPDF/GalleryForm.cs`

- [x] Set the vertical scrollbar range from the total stacked page height and its `LargeChange` from the viewport height.
- [x] Make the mouse wheel update the vertical pixel offset while horizontal mode continues to update the page index.
- [x] Draw only vertical page slots intersecting the viewport; fit each page to the available width with 12px side margins and 12px page gaps.
- [x] Keep horizontal black-area navigation and right-to-left behavior unchanged.

### Task 2: Verify the focused change

**Files:**
- Modify: `ImgsToPDF/ImgsToPDF.cs`
- Modify: `docs/superpowers/specs/2026-07-01-professional-page-organizer-design.md`

- [x] Refresh the main-window preview from the selected cover, or the first selected page when no cover is set, after the organizer returns `DialogResult.OK`.
- [x] Run `git diff --check -- . ':(exclude)ImgsToPDF/obj/**' ':(exclude)ImgsToPDFCore/obj/**'` and expect no whitespace errors.
- [x] Run MSBuild `ImgsToPDF.sln /t:Build /p:Configuration=Debug /p:Platform=x64` and expect zero compile errors; if the running app locks the output EXE, verify `CoreCompile` separately and report the lock.
- [x] Run `ImgsToPDFCore.exe --self-check-page-manifest` and expect `Page manifest self-check passed.`
