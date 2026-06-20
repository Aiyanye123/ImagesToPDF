# Repository Guidelines

## Project Structure & Module Organization

This directory contains the `ImgsToPDF` Windows Forms application targeting .NET Framework 4.8. Main UI code lives in `ImgsToPDF.cs` and `GalleryForm.cs`; designer-generated files use the matching `*.Designer.cs` pattern and should normally be changed through Visual Studio designer tools. Localized strings and form resources live in `*.resx`, `*.zh.resx`, and `Lang/`. Static images are in `Resources/`. `Core/` contains bundled runtime files copied to the output directory by the project post-build step. Build outputs in `bin/` and `obj/` are generated artifacts.

## Build, Test, and Development Commands

From this directory:

```powershell
msbuild ImgsToPDF.csproj /p:Configuration=Debug /p:Platform=x64
msbuild ImgsToPDF.csproj /p:Configuration=Release /p:Platform=x64
.\bin\x64\Debug\ImgsToPDF.exe
```

Use the Debug build for local development and the Release build for distributable binaries. The parent directory also contains `ImgsToPDF.sln` for Visual Studio users.

## Coding Style & Naming Conventions

Use C# 7.3-compatible syntax. Keep four-space indentation and the existing brace style (`namespace ImgsToPDF {`). Public types and methods use PascalCase; locals and private fields use camelCase unless existing nearby code differs. Keep generated designer and resource files minimal and avoid hand-editing generated sections. Code comments should be brief and useful; follow the surrounding language when touching existing comments.

## Testing Guidelines

There is no dedicated automated test project in this tree. For logic changes, add the smallest practical check near the changed code or document the manual verification performed. For UI changes, manually run `.\bin\x64\Debug\ImgsToPDF.exe` and verify the affected form, language resources, image loading, and PDF generation path.

## Commit & Pull Request Guidelines

No Git history is available in this checkout, so prefer concise imperative commit messages such as `Fix gallery image ordering` or `Update Chinese resources`. Pull requests should describe the user-visible change, list manual test steps, mention affected resource files, and include screenshots for visible UI changes.

## Security & Configuration Tips

Do not commit secrets, local paths, or machine-specific settings. Treat files in `Core/` as shipped dependencies: replace them deliberately, keep versions traceable in the PR, and verify the post-build copy still produces a runnable `bin/x64/*` output.
