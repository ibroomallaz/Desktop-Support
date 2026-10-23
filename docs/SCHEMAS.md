# Desktop Support App (DSA) — JSON Schemas & Data Reference

This document serves as the data dictionary and schema reference for all remote Box CDN feeds and local JSON configuration files used by the Desktop Support App.

---

## Table of Contents
1. [Schema Versioning & Migration Architecture](#1-schema-versioning--migration-architecture)
2. [Version Manifest Schema (`Version.json`)](#2-version-manifest-schema-versionjson)
3. [Departments & Support Teams Schema (`departments.json`)](#3-departments--support-teams-schema-departmentsjson)
4. [Quick Links Schema (`links.json`)](#4-quick-links-schema-linksjson)
5. [ServiceMeow Schema (`servicemeow.json`)](#5-servicemeow-schema-servicemeowjson)
6. [Local User Settings Schema (`settings.json`)](#6-local-user-settings-schema-settingsjson)

---

## 1. Schema Versioning & Migration Architecture

All JSON datasets in DSA use a standardized metadata contract defined in [`JSONMetaBase.cs`](../DSAMVVM/MVVM/Model/Schemas/JSONMetaBase.cs):

```csharp
public interface IJsonMeta
{
    int SchemaVersion { get; set; }
    DateTime? LastUpdatedUtc { get; set; }
}
```

### How Schema Upgrades Work
1. **Source of Truth Constants:** Active schema integer constants are defined centrally in [`Globals.cs`](../DSAMVVM/MVVM/Model/globals.cs#L58-L62):
   - `Globals.g_VersionSchema`
   - `Globals.g_DepartmentJSONSchema`
   - `Globals.g_LinkJSONSchema`
   - `Globals.g_ServiceMeowJSONSchema`
   - `Globals.g_SettingsSchema`
2. **In-Memory Auto-Upgrades (`[OnDeserialized]`):** Each schema metadata class ([`DepartmentMeta`](../DSAMVVM/MVVM/Model/Schemas/DepartmentMeta.cs), [`LinksMeta`](../DSAMVVM/MVVM/Model/Schemas/LinksMeta.cs), [`ServiceMeowMeta`](../DSAMVVM/MVVM/Model/Schemas/ServiceMeowMeta.cs), [`SettingsMeta`](../DSAMVVM/MVVM/Model/Schemas/SettingsMeta.cs), [`VersionManifest`](../DSAMVVM/MVVM/Model/Schemas/VersionSchema.cs)) includes an `[OnDeserialized]` hook that automatically upgrades older `SchemaVersion` numbers in memory to the current constant when deserialized.
3. **Export Stamping:** When exporting modified datasets via [`AdminView`](../DSAMVVM/MVVM/View/AdminView.xaml) / [`JsonExportService`](../DSAMVVM/Core/Services/Infrastructure/JsonExportService.cs), `Meta.Normalize()` stamps the current UTC timestamp (`LastUpdatedUtc`) and current `SchemaVersion`.

---

## 2. Version Manifest Schema (`Version.json`)

- **C# Model:** [`VersionManifest`](../DSAMVVM/MVVM/Model/Schemas/VersionSchema.cs)
- **Remote URL Constants:** `Globals.g_VersionJSON` (Production) / `Globals.g_TestVersionJSON` (Staging)
- **Consumed By:** [`VersionChecker`](../DSAMVVM/Core/Utilities/VersionChecker.cs), [`VersionCheckerUI`](../DSAMVVM/MVVM/Services/Updates/VersionCheckerUI.cs), [`UpdaterService`](../DSAMVVM/Core/Services/Updates/UpdaterService.cs)

> [!IMPORTANT]
> Unlike `departments.json`, `links.json`, and `servicemeow.json`, the **Version Manifest** is not edited via `AdminView`. Maintainers update this file on Box when publishing a new release.

### Annotated JSON Example
```json
{
  "SchemaVersion": 3,
  "LastUpdatedUtc": "2026-10-23T18:00:00Z",
  "Version": {
    "Current": {
      "version": "4.5.1",
      "location": "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx",
      "msiUrl": "https://arizona.box.com/shared/static/example_msi_link.msi",
      "setupUrl": "https://arizona.box.com/shared/static/example_setup_link.exe",
      "requiredDotNetVersion": "10.0.0",
      "changelog": "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/Change-Log.aspx"
    },
    "PreRelease": {
      "exists": false,
      "version": "4.6.0-beta1",
      "location": "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx",
      "msiUrl": "https://arizona.box.com/shared/static/example_beta_msi.msi",
      "setupUrl": "https://arizona.box.com/shared/static/example_beta_setup.exe",
      "requiredDotNetVersion": "10.0.0",
      "changelog": "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/Change-Log.aspx"
    }
  },
  "Required": {
    "minVersion": "4.4.0",
    "message": "This update includes required security and directory changes."
  }
}
```

### Field Reference (`Current` / `PreRelease` & `Required`)
| Field Path | Type | Required | Description |
| :--- | :--- | :--- | :--- |
| `SchemaVersion` | `int` | Yes | Root schema version matching `Globals.g_VersionSchema` (Note: `VersionManifest` inherits `JsonMetaBase` directly at the root level). |
| `Version.Current.version` | `string` | Yes | Latest production semantic version (compared against `AssemblyVersion`). |
| `Version.Current.msiUrl` | `string` | Yes | Direct Box static download URL for `DSA_Installer.msi`. |
| `Version.Current.setupUrl` | `string` | Optional | Direct Box static download URL for `DSA_Setup.exe` (used when `.NET` runtime installation is needed). |
| `Version.Current.requiredDotNetVersion` | `string` | Optional | Minimum `Microsoft.WindowsDesktop.App` runtime version required (e.g., `"10.0.0"`). |
| `Version.Current.location` | `string` | Optional | Fallback web URL opened if automated download/installation fails. |
| `Version.Current.changelog` | `string` | Optional | URL to the release notes / changelog page. |
| `Version.PreRelease.exists` | `bool` | Yes | Set to `true` when an active beta/pre-release build is available for opt-in testers. |
| `Required.minVersion` | `string` | Optional | Any running client with an `AssemblyVersion` **below** this version is forced to update at startup (`EnforceRequiredAsync`). |
| `Required.message` | `string` | Optional | Custom explanation displayed in the mandatory update dialog. |

---

## 3. Departments & Support Teams Schema (`departments.json`)

- **C# Model:** [`DepartmentListWrapper`](../DSAMVVM/MVVM/Model/Data/DepartmentData.cs) (`DepartmentMeta`, `Department`, `SupportTeam`, `SupportedDivs`)
- **Remote URL Constants:** `Globals.g_DepartmentJSONURL` (Production) / `Globals.g_DepartmentTestJSONURL` (Staging)
- **Local Cache Path:** `%LOCALAPPDATA%\UArizona\DesktopSupportApp\data\departments.json`
- **Editable in App:** Yes (`AdminView` -> **Department** and **Support Team** sections)

### Annotated JSON Example
```json
{
  "Meta": {
    "SchemaVersion": 3,
    "LastUpdatedUtc": "2026-10-23T18:00:00Z"
  },
  "DepartmentList": [
    {
      "Number": "0401",
      "SupportKnown": true,
      "Team": "UITS Desktop Support",
      "Notes": "Main administration building; VIP escalation protocols apply.",
      "FileRepoPath": "\\\\fileserver.arizona.edu\\dept$\\0401"
    }
  ],
  "SupportTeams": [
    {
      "SupportTeamName": "UITS Desktop Support",
      "ManagerName": "Jane Doe",
      "ManagerNetID": "janedoe",
      "PhoneNumber": "520-621-0000",
      "SupportedDivs": [
        {
          "DivAbbrev": "UITS",
          "DivFullName": "University Information Technology Services"
        }
      ]
    }
  ]
}
```

### Field Reference
| Object | Property | Type | Description |
| :--- | :--- | :--- | :--- |
| **`DepartmentList[]`** | `Number` | `string` | 4-digit UArizona department number (primary lookup key matched against AD user/group queries). |
| | `SupportKnown` | `bool` | Whether IT support ownership is known for this department. |
| | `Team` | `string?` | Name of the assigned support team (links to `SupportTeams[].SupportTeamName`). |
| | `Notes` | `string?` | Special handling notes displayed on User and Group lookup cards. |
| | `FileRepoPath` | `string?` | Optional UNC or network repository path associated with the department. |
| **`SupportTeams[]`** | `SupportTeamName` | `string` | Unique display name of the IT support team. |
| | `ManagerName` | `string` | Full name of the support team manager/lead. |
| | `ManagerNetID` | `string` | NetID of the support team manager (enables clickable deep-link lookup in DSA). |
| | `PhoneNumber` | `string?` | Contact phone number for the team. |
| | `SupportedDivs` | `array` | List of `{ "DivAbbrev", "DivFullName" }` pairs (note JSON property name is `SupportedDivs`, mapped to C# `SupportedDivisions`). |

---

## 4. Quick Links Schema (`links.json`)

- **C# Model:** [`LinksData`](../DSAMVVM/MVVM/Model/Data/LinksData.cs) (`LinksMeta`, `Link`, `TeamLinkGroup`)
- **Remote URL Constant:** `Globals.g_LinksJSON`
- **Local Cache Path:** `%LOCALAPPDATA%\UArizona\DesktopSupportApp\data\links.json`
- **Editable in App:** Yes (`AdminView` -> **Links** section)

### Annotated JSON Example
```json
{
  "Meta": {
    "SchemaVersion": 2,
    "LastUpdatedUtc": "2026-10-23T18:00:00Z"
  },
  "CommonLinks": [
    {
      "Name": "UAccess Employee",
      "Description": "University HR and employee portal",
      "URL": "https://uaccess.arizona.edu"
    }
  ],
  "TeamLinks": [
    {
      "Team": "UITS Desktop Support",
      "Links": [
        {
          "Name": "Desktop Support SharePoint",
          "Description": "Internal documentation and SOPs",
          "URL": "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp"
        }
      ]
    }
  ]
}
```

### Field Reference
| Object | Property | Type | Description |
| :--- | :--- | :--- | :--- |
| **`CommonLinks[]`** | `Name` | `string` | Display title of the link shown to all users. |
| | `Description` | `string` | Subtitle / description used in display and search filtering. |
| | `URL` | `string` | Target `https://` URL opened when clicked. |
| **`TeamLinks[]`** | `Team` | `string` | Support team name used to filter team-specific links in `LinksView`. |
| | `Links[]` | `Link[]` | Array of `{ "Name", "Description", "URL" }` objects belonging to that team. |

---

## 5. ServiceMeow Schema (`servicemeow.json`)

- **C# Model:** [`ServiceMeowData`](../DSAMVVM/MVVM/Model/Data/ServiceMeowData.cs) (`ServiceMeowMeta`, `ServiceMeowOwner`, `ServiceMeowPet`)
- **Remote URL Constant:** `Globals.g_ServiceMeowJSON`
- **Local Cache Path:** `%LOCALAPPDATA%\UArizona\DesktopSupportApp\data\servicemeow.json` (Images cached in `img\sm\`)
- **Editable in App:** Yes (`AdminView` -> **ServiceMeow** section)

### Annotated JSON Example
```json
{
  "SchemaVersion": 1,
  "Meta": {
    "SchemaVersion": 1,
    "LastUpdatedUtc": "2026-10-23T18:00:00Z"
  },
  "Owners": [
    {
      "NetId": "ibroomall",
      "Name": "Isaac Broomall",
      "Team": "Desktop Support",
      "Pets": [
        {
          "Id": "a1b2c3d4e5f64789a1b2c3d4e5f64789",
          "Name": "Mochi",
          "Title": "Chief Nap Officer",
          "Breed": "Domestic Shorthair",
          "Species": "Cat",
          "Blurb": "Supervises all keyboard activity.",
          "Images": [
            "https://arizona.box.com/shared/static/example_pet_photo.jpg"
          ]
        }
      ]
    }
  ]
}
```

> [!NOTE]
> `ServiceMeowPet.ValidImages` automatically filters out placeholder strings such as `"TBD"`, `"TDB"`, or `"NONE"`, and [`ImageCacheService`](../DSAMVVM/Core/Services/UI/ImageCacheService.cs) downloads and caches valid image URLs locally into `%LOCALAPPDATA%\UArizona\DesktopSupportApp\img\sm\`.

---

## 6. Local User Settings Schema (`settings.json`)

- **C# Model:** [`AppSettings`](../DSAMVVM/MVVM/Model/Config/AppSettings.cs)
- **Local Path:** `%LOCALAPPDATA%\UArizona\DesktopSupportApp\settings.json`
- **Backup Directory:** `%LOCALAPPDATA%\UArizona\DesktopSupportApp\settings-legacy\`
- **Managed By:** [`SettingsService`](../DSAMVVM/Core/Services/Infrastructure/SettingsService.cs)

### Top-Level Structure Overview
| Section Property | C# Class | Purpose |
| :--- | :--- | :--- |
| **`Meta`** | [`SettingsMeta`](../DSAMVVM/MVVM/Model/Schemas/SettingsMeta.cs) | Tracks `SchemaVersion` (`Globals.g_SettingsSchema`) and `LastUpdatedUtc`. |
| **`Paths`** | [`Paths`](../DSAMVVM/MVVM/Model/Config/Paths/Paths.cs) | Custom file/directory overrides normalized via `Paths.NormalizeAll()`. |
| **`Ui`** | [`Ui`](../DSAMVVM/MVVM/Model/Config/UI/UI.cs) | Contains `Font` ([`FontSettings`](../DSAMVVM/MVVM/Model/Config/UI/FontSettings.cs)), `HomeShortcuts` ([`HomeShortcutsSettings`](../DSAMVVM/MVVM/Model/Config/UI/HomeShortcutsSettings.cs)), `Links` ([`LinksSettings`](../DSAMVVM/MVVM/Model/Config/UI/LinksSettings.cs)), `Search` ([`SearchSettings`](../DSAMVVM/MVVM/Model/Config/UI/SearchSettings.cs)), `ServiceMeow` ([`ServiceMeowUiSettings`](../DSAMVVM/MVVM/Model/Config/UI/ServiceMeowUiSettings.cs)), and `Tray` ([`TraySettings`](../DSAMVVM/MVVM/Model/Config/UI/TraySettings.cs)). |
| **`Logging`** | [`LoggingSettings`](../DSAMVVM/MVVM/Model/Config/LoggingSettings.cs) | Minimum log level, file retention count, and debug logging toggles. |
| **`Updates`** | [`UpdateSettings`](../DSAMVVM/MVVM/Model/Config/UpdateSettings.cs) | Pre-release opt-in preferences and update check intervals. |
| **`QuickSearch`** | [`QuickSearchSettings`](../DSAMVVM/MVVM/Model/Config/QuickSearchSettings.cs) | Global double-tap modifier key (`ModifierKeyCode`, default `VcLeftControl`), tap threshold (`DoubleTapThresholdMs`, default `350`ms, clamped `100`–`1000`ms), and `Enabled` state. |
