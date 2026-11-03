# Desktop Support App (DSA) — Developer & Release Workflows

This document covers operational workflows for maintaining, extending, and releasing the Desktop Support App (DSA), including the application versioning block scheme and the end-to-end Box / SharePoint deployment pipeline. For an architectural overview of the system, see [`docs/ARCHITECTURE.md`](ARCHITECTURE.md). For JSON data dictionaries, see [`docs/SCHEMAS.md`](SCHEMAS.md).

---

## Table of Contents
1. [Application Versioning Logic & Block Scheme](#1-application-versioning-logic--block-scheme)
2. [Release & Deployment Workflow](#2-release--deployment-workflow)
3. [Updating Organizational JSON Feeds (`AdminView`)](#3-updating-organizational-json-feeds-adminview)
4. [Extending the Application](#4-extending-the-application)
   - [Adding a New View & ViewModel](#adding-a-new-view--viewmodel)
   - [Adding a CLI Command-Line Switch](#adding-a-cli-command-line-switch)
   - [Bumping a JSON Schema Version](#bumping-a-json-schema-version)

---

## 1. Application Versioning Logic & Block Scheme

Application versioning is controlled from [`DSAMVVM/DSAMVVM.csproj`](../DSAMVVM/DSAMVVM.csproj#L14-L16) via three MSBuild properties:

```xml
<AssemblyVersion>4.5.1</AssemblyVersion>
<FileVersion>4.5.1200</FileVersion>
<Version>4.5.1</Version>
```

### How Each Property Is Used
| Property | Runtime / Build Consumer | Purpose |
| :--- | :--- | :--- |
| **`<Version>`** | [`VersionDisplayHelper.GetSemVerDisplay()`](../DSAMVVM/Core/Utilities/VersionDisplayHelper.cs#L12) → [`Globals.g_AppVersion`](../DSAMVVM/MVVM/Model/globals.cs#L9) | User-facing Semantic Version (supports `-alphaN`, `-betaN`, `-rcN` suffixes). Evaluated by [`VersionChecker.IsNewerVersion`](../DSAMVVM/Core/Utilities/VersionChecker.cs#L128) against `Version.json`. |
| **`<FileVersion>`** | [`VersionDisplayHelper.GetFileVersionDisplay()`](../DSAMVVM/Core/Utilities/VersionDisplayHelper.cs#L30) → [`Globals.g_FileVersion`](../DSAMVVM/MVVM/Model/globals.cs#L10) & WiX `!(bind.fileVersion.DesktopSupportAppExe)` | Numeric 3-part version (`Major.Minor.BuildBlock`) bound directly into [`Package.wxs`](../DSA.Installer/Package.wxs#L6) and [`Bundle.wxs`](../DSA.Bootstrapper/Bundle.wxs#L5). Drives Windows Installer `MajorUpgrade` detection. |
| **`<AssemblyVersion>`** | .NET CLR Assembly Identity | Core `Major.Minor.Patch` numeric version (`4.5.1`). |

### `<FileVersion>` Block Scheme
Because Windows Installer (`MSI`) ignores pre-release strings and only compares the first three numeric fields (`Major.Minor.Build`) when performing a `<MajorUpgrade>`, `<FileVersion>` uses a deterministic **1000-series block scheme** in the 3rd segment so every sequential pre-release and GA patch strictly increases:

| Release Stage | `<Version>` Example | `<AssemblyVersion>` | `<FileVersion>` Pattern | `<FileVersion>` Example |
| :--- | :--- | :--- | :--- | :--- |
| **Alpha `N`** | `4.0.0-alpha2` | `4.0.0` | `Major.Minor.100N` | `4.0.1002` |
| **Beta `N`** | `4.0.0-beta1` | `4.0.0` | `Major.Minor.10N0` | `4.0.1010` |
| **RC `N`** | `4.0.0-rc1` | `4.0.0` | `Major.Minor.109N` | `4.0.1091` |
| **GA `.0`** | `4.0.0` | `4.0.0` | `Major.Minor.1100` | `4.0.1100` |
| **GA `.1`** | `4.0.1` | `4.0.1` | `Major.Minor.1200` | `4.0.1200` |
| **GA `.2`** | `4.0.2` | `4.0.2` | `Major.Minor.1300` | `4.0.1300` |

> [!NOTE]
> - **WiX Automatic Version Binding:** You do **not** need to manually edit version numbers in [`Package.wxs`](../DSA.Installer/Package.wxs) or [`Bundle.wxs`](../DSA.Bootstrapper/Bundle.wxs)—WiX automatically binds the installer version from the compiled executable via `Version="!(bind.fileVersion.DesktopSupportAppExe)"`.
> - **In-App Version Comparison:** [`VersionChecker.IsNewerVersion`](../DSAMVVM/Core/Utilities/VersionChecker.cs#L128-L190) parses `<Version>` (`Globals.g_AppVersion`) and enforces the hierarchy `alpha` < `beta` < `rc` < `stable` (supporting formats like `alpha-2`, `alpha2`, or `alpha.2`).

---

## 2. Release & Deployment Workflow

### Step 1: Bump Version Properties & Build Release Installers
1. Open [`DSAMVVM/DSAMVVM.csproj`](../DSAMVVM/DSAMVVM.csproj#L14-L16) and update `<AssemblyVersion>`, `<FileVersion>` (following the [Block Scheme](#fileversion-block-scheme) above), and `<Version>`.
2. Build the solution in `Release` configuration:
   ```powershell
   dotnet build DSAMVVM.sln -c Release
   ```
   - The custom MSBuild target `PublishAppForWix` in [`DSA.Installer.wixproj`](../DSA.Installer/DSA.Installer.wixproj#L27-L32) automatically publishes `DSAMVVM` before packaging and outputs:
     - `artifacts/Release/DSA_Installer.msi`
     - `artifacts/Release/DSA_Setup.exe`

### Step 2: Archive & Upload Installers to SharePoint
1. Compress `DSA_Installer.msi` and `DSA_Setup.exe` into a `.zip` archive named with the hyphenated version number:
   - **GA Example:** `4-5-1.zip`
   - **Pre-Release Example:** `4-6-0-b1.zip`
2. In the SharePoint installer repository:
   - Move the existing binaries from the **Current** (or **Pre-Release**) folder into the **Previous Releases** folder.
   - Upload the new `.zip` archive and/or installer binaries into the **Current** or **Pre-Release** folder as appropriate.

### Step 3: Rename Binaries to `.dat` & Upload to Box (`WNC/Apps/DSA/bin`)
To allow direct binary downloads via Box without browser/scanner executable blocking, rename the compiled outputs with a `.dat` extension using hyphenated version strings (`UpdaterService` automatically renames them back to `DSA_Installer.msi` and `setup.exe` when downloading to `%TEMP%\DSA_Update_<GUID>`):

- **GA Naming Convention:**
  - `DSA_Installer.msi` → `4-5-1.dat`
  - `DSA_Setup.exe` → `4-5-1-setup.dat`
- **Pre-Release Naming Convention:**
  - `DSA_Installer.msi` → `4-6-0-b1.dat`
  - `DSA_Setup.exe` → `4-6-0-b1-setup.dat`

1. Open the **WNC** Box share and navigate to `WNC/Apps/DSA/bin`.
2. Move the previous release's `.dat` binaries into the `WNC/Apps/DSA/bin/old` subfolder.
3. Upload the newly renamed `<version>.dat` and `<version>-setup.dat` files into `WNC/Apps/DSA/bin`.
4. For **each** uploaded `.dat` file:
   - Click **Share** and enable the **Shared Link**.
   - Change the link access permission to **People with the link** (Anyone with the link).
   - Open **Link Settings** and copy the **Direct Download Link** (`https://arizona.box.com/shared/static/...`).

### Step 4: Stage & Verify via `testversion.json` (`WNC/Apps/DSA/json`)
Before promoting the update to all users, verify the binary links and installer execution against the test manifest (`Globals.g_TestVersionJSON`):
1. Update your local copy of the test version manifest (`testversion.json`) with:
   - The new `version` string (e.g., `"4.5.1"` or `"4.6.0-beta1"`).
   - The new Box direct download links in `msiUrl` (`<version>.dat`) and `setupUrl` (`<version>-setup.dat`).
   - Updated `LastUpdatedUtc` timestamp and changelog/location metadata.
2. In Box under `WNC/Apps/DSA/json`, use **Upload New Version** on the test version JSON file so its static URL (`Globals.g_TestVersionJSON`) remains unchanged.
3. In DSA, enable **Internal Testing Sources** (and **Pre-Release Updates** if testing a pre-release) in Settings, run an update check, and verify that the `.dat` binaries download, install silently, and relaunch the app with `-updated`.

### Step 5: Promote `Version.json` to Production (`WNC/Apps/DSA/json`)
Once verified via `testversion.json`:
1. Copy the verified version info, direct `.dat` links (`msiUrl` and `setupUrl`), and patch notes/changelog URL into the production `Version.json` (under `Version.Current` for GA, or `Version.PreRelease` with `"exists": true` for a pre-release; update `Required.minVersion` if enforcing a mandatory upgrade).
2. In Box under `WNC/Apps/DSA/json`, use **Upload New Version** on `Version.json` to preserve its hardcoded static URL (`Globals.g_VersionJSON`).

### Step 6: Update SharePoint Pages & Announce in Teams
1. Update the SharePoint **Change Log** page ([`Globals.g_ChangeLogURL`](../DSAMVVM/MVVM/Model/globals.cs#L36)) and **Project Home** current build info ([`Globals.g_SharepointHome`](../DSAMVVM/MVVM/Model/globals.cs#L34)).
2. Post the release announcement and patch summary in the **General** Microsoft Teams channel.

---

## 3. Updating Organizational JSON Feeds (`AdminView`)

When departments, support teams, quick links, or ServiceMeow entries need to be updated (no application rebuild required):

1. **Unlock & Open Admin View:** Enable **JSON Admin** (`ToggleAdminModeCommand` / Settings) and select **Admin** in the sidebar.
2. **Load & Edit Dataset:** Select the target dataset (**Departments**, **Support Teams**, **Links**, or **ServiceMeow**) and add, edit, or delete entries.
3. **Export Schema-Validated JSON:** Export the modified file via `AdminView` ([`JsonExportService`](../DSAMVVM/Core/Services/Infrastructure/JsonExportService.cs) automatically stamps `Meta.LastUpdatedUtc` and the current `SchemaVersion` from [`Globals.cs`](../DSAMVVM/MVVM/Model/globals.cs#L61-L65)).
4. **Upload to Box:** In `WNC/Apps/DSA/json`, use **Upload New Version** on the corresponding `.json` file referenced in [`Globals.cs`](../DSAMVVM/MVVM/Model/globals.cs#L25-L31) so its static shared link is preserved.

---

## 4. Extending the Application

### Adding a New View & ViewModel
1. **Define the Enum Value:** Add a new entry to [`AppView`](../DSAMVVM/Core/Enums/AppView.cs).
2. **Create the ViewModel:**
   - Create `NewFeatureViewModel.cs` under [`DSAMVVM/MVVM/ViewModel/`](../DSAMVVM/MVVM/ViewModel/).
   - Inherit from [`ObservableObject`](../DSAMVVM/Core/Utilities/ObservableObject.cs) and inject required services through constructor parameters.
3. **Create the View:**
   - Create `NewFeatureView.xaml` under [`DSAMVVM/MVVM/View/`](../DSAMVVM/MVVM/View/).
4. **Register in DI:**
   - In [`ServiceRegistration.cs`](../DSAMVVM/ServiceRegistration.cs), register `services.AddTransient<NewFeatureViewModel>()` and its factory `services.AddTransient<Func<NewFeatureViewModel>>(...)`.
5. **Add to Navigation:**
   - In [`MainViewModel.cs`](../DSAMVVM/MVVM/ViewModel/MainViewModel.cs), add the navigation command, add a `NavItem` to `NavItems` / `PrimaryNavItems` / `FooterNavItems`, and handle the `AppView` switch case.
6. **Add DataTemplate Mapping:**
   - In [`App.xaml`](../DSAMVVM/App.xaml), add a `<DataTemplate>` mapping `NewFeatureViewModel` to `NewFeatureView`.

### Adding a CLI Command-Line Switch
To enable direct routing from Windows Taskbar JumpLists or external shortcuts:
1. Open [`MainViewModel.ProcessArgs`](../DSAMVVM/MVVM/ViewModel/MainViewModel.cs#L497).
2. Read the argument with `GetArgValue(args, "--your-flag")`.
3. Handle the view switch or parameter update.
4. If adding to the Windows Taskbar JumpList, add a `JumpTask` in [`App.ConfigureJumpList`](../DSAMVVM/App.xaml.cs#L425).

### Bumping a JSON Schema Version
When introducing breaking changes to a JSON feed or `settings.json`:
1. Increment the corresponding schema constant in [`Globals.cs`](../DSAMVVM/MVVM/Model/globals.cs#L61-L65) (e.g., `g_DepartmentJSONSchema`, `g_SettingsSchema`).
2. Update the C# model under [`DSAMVVM/MVVM/Model/`](../DSAMVVM/MVVM/Model/) and its corresponding `*Meta.cs` class under [`DSAMVVM/MVVM/Model/Schemas/`](../DSAMVVM/MVVM/Model/Schemas/).
3. Update [`docs/SCHEMAS.md`](SCHEMAS.md) to reflect any new or modified fields.
