# Desktop Support App (DSA)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D6?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![UI Framework](https://img.shields.io/badge/UI-WPF%20(MVVM)-1E5288)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Installer](https://img.shields.io/badge/Installer-WiX%20Toolset%20SDK-orange)](https://wixtoolset.org/)
[![Organization](https://img.shields.io/badge/Institution-University%20of%20Arizona-0C234B)](https://www.arizona.edu/)

**Desktop Support App (DSA)** is a Windows utility designed specifically for **University of Arizona (UITS Desktop Support)** technicians and IT staff. It unifies quick information lookup, department escalation routing, curated IT links, in-app bug reporting via Microsoft Graph, and team utilities into a single, resilient desktop client.

---

## Table of Contents
- [Key Features](#key-features)
- [Architecture & Tech Stack](#architecture--tech-stack)
- [Documentation Links](#documentation-links)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Clone & Build](#clone--build)
  - [Run from CLI](#run-from-cli)
- [Building Installers (WiX Toolset SDK)](#building-installers-wix-toolset-sdk)
- [Project & Runtime Overview](#project--runtime-overview)
- [Contributing & Code Conventions](#contributing--code-conventions)

---

## Key Features

### 🔍 Active Directory & Department Lookups
- **User Lookups:** Query NetID (`sAMAccountName`) or Employee ID to view display name, account enabled/locked status, 4-digit department number & name, primary affiliation (`eduPersonPrimaryAffiliation`), parsed M365 license tier (`extensionAttribute15`), MIM workstation/division rollup membership (`UA-MIM-Wrkst-AllDivUsers`, `MIM-DivisionRollup`), and Adobe Acrobat Pro / Creative Cloud licensing entitlements.
- **Computer Lookups:** Query hostname to inspect Active Directory OU path, description, operating system, enabled state, hybrid join group membership (`UA-MEMHybridDevices`), and formatted `lastLogonTimestamp`.
- **Group & Department Lookups:** Supports four lookup modes:
  1. **User MIM (`mim`):** Lists a user's direct and nested `MIM` groups.
  2. **AD Group Members (`members`):** Enumerates all members of a security or distribution group (with ranged retrieval for large groups).
  3. **Department (`dept`):** Looks up a 4-digit department number to display assigned IT support team, manager contact, notes, and file repository path.
  4. **Division (`div`):** Looks up a 4-character division code to find the supporting IT team and its supported divisions.
- **Formatted Card Export:** Copy clean, standardized text summaries of User, Computer, or Group result cards to the clipboard for support tickets in one click.

### 💬 In-App Bug Reports & Requests (Microsoft Graph API)
- **Direct Teams Channel Dispatch:** Technicians can report bugs, submit feedback, or request features directly within the app without leaving their workflow.
- **Interactive SSO & Graph Delivery:** Uses MSAL with Windows Web Account Manager (WAM) SSO to authenticate the user and transmit rich, formatted HTML cards directly into designated Microsoft Teams channels via Microsoft Graph (`GraphServiceClient`).

### ⚡ Global QuickSearch Overlay
- **Double-Tap Modifier Trigger:** Double-tap a configurable modifier key (default: double-tap `Left Ctrl` within a configurable millisecond threshold) anywhere in Windows to invoke a floating search overlay powered by low-level hooks (`SharpHook`).
- **Automatic Selection Capture & Inline Results:** Simulates `Ctrl+C` (`EventSimulator`) to capture currently highlighted text from any window, pre-populates the overlay search bar, and lets you target **User**, **Computer**, or **Group** (`1–4` mode menu) to render the full result card directly inside the floating overlay.

### 🌐 Network & Domain Awareness
- **Real-Time Network Detection:** Detects whether the workstation is connected via Cisco Secure Client VPN, Campus Wired (`arizona.edu`), Campus Wi-Fi (`UAWiFi`), or Off-Campus (including restricted networks like `eduroam` and `UAGuest`).
- **Domain Controller Health:** Probes `bluecat.arizona.edu` via DNS resolution, TCP port 389 (LDAP) handshake latency, and `RootDSE` fast-bind verification.

### 📁 Department Support & IT Resources Directory
- **Resilient Offline Cache:** Synchronizes departmental support tiers, contact notes, and curated campus IT tool links from cloud JSON feeds with an automatic local fallback cache.
- **Built-In Admin Editor:** Includes an administrative editor (`AdminView`) for maintaining and exporting schema-validated JSON feeds for Departments, Support Teams, Links, and ServiceMeow.

### 🐾 ServiceMeow (Team Pets)
- **Community Feature:** A built-in departmental pet gallery displaying team members' pets, photos, breeds, and fun blurbs.

### 🔄 Auto-Update Engine
- **Silent Background Checks & Runtime Detection:** Periodically checks for updates (including `PreRelease` and `Required` minimum version enforcement), verifies whether a newer .NET Desktop Runtime is needed, and runs quiet MSI/bootstrapper upgrades via a PowerShell watchdog with automatic application relaunch (`-updated`).

### 🎨 Modern Windows Design
- **UArizona Branding:** Clean, high-contrast interface designed using official University of Arizona Navy (`#0C234B`) and Cardinal Red (`#AB0520`).
- **System Integration:** Windows 11 backdrop effects (Mica / Acrylic), Windows Jump Lists, and optional system tray minimization.

---

## Architecture & Tech Stack

Desktop Support App is built on modern .NET desktop standards:

| Component | Technology | Description |
| :--- | :--- | :--- |
| **Framework** | **.NET 10.0** (`net10.0-windows10.0.26100.0`) | Modern C# 14 runtime targeted for Windows 10/11 x64. |
| **UI Framework** | **WPF (XAML)** | Hardware-accelerated presentation layer with strict MVVM. |
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection` | Centralized IoC container configured in `ServiceRegistration.cs`. |
| **Directory Services** | `System.DirectoryServices` / `AccountManagement` | High-speed LDAP `DirectorySearcher` queries against `bluecat.arizona.edu`. |
| **Authentication & Graph** | `Microsoft.Identity.Client`, `Microsoft.Graph` | MSAL + WAM Broker SSO; Graph API for submitting bug reports and requests to Teams channels. |
| **Global Hooks** | `SharpHook` | Low-level keyboard hook & event simulator for double-tap QuickSearch overlay. |
| **Tray Integration** | `H.NotifyIcon.Wpf` | System tray notification icon and context menu. |
| **JSON Serialization** | `Newtonsoft.Json` | Resilient JSON cache reading, writing, and schema versioning. |
| **Packaging** | **WiX Toolset (`WixToolset.Sdk`)** | Automated MSBuild MSI installer and executable bootstrapper bundle. |

---

## Documentation Links

For deeper technical documentation, please consult:

- 📖 [**Architecture Deep Dive (`docs/ARCHITECTURE.md`)**](docs/ARCHITECTURE.md) — Informational technical overview of subsystems, external dependencies, MVVM layers, IPC named pipes, DI service lifecycles, and Graph API messaging.
- 🛠️ [**Developer & Release Workflows (`docs/WORKFLOWS.md`)**](docs/WORKFLOWS.md) — Step-by-step guides for releasing updates, publishing JSON feeds via `AdminView`, and extending views/services.
- 📋 [**JSON Schemas & Data Reference (`docs/SCHEMAS.md`)**](docs/SCHEMAS.md) — Data dictionary and annotated JSON templates for `Version.json`, `departments.json`, `links.json`, `servicemeow.json`, and `settings.json`.

---

## Getting Started

### Prerequisites
1. **Windows 10 (Build 19041+) or Windows 11 (x64)**
2. **.NET 10.0 SDK** (or later)
3. **IDE:** [JetBrains Rider](https://www.jetbrains.com/rider/) or [Visual Studio 2022 / 2026](https://visualstudio.microsoft.com/) with .NET desktop development workload

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/ibroomallaz/Desktop-Support.git
cd Desktop-Support

# Restore dependencies and build the solution (including MSI & Setup.exe)
dotnet build DSAMVVM.sln
```

### Run from CLI
```powershell
# Run the main application
dotnet run --project DSAMVVM
```

---

## Building Installers (WiX Toolset SDK)

The solution includes two packaging projects powered by `WixToolset.Sdk` (restored automatically via NuGet during build):
- **`DSA.Installer`**: Produces the per-user MSI package (`artifacts/Release/DSA_Installer.msi`).
- **`DSA.Bootstrapper`**: Produces the chained executable setup wizard (`artifacts/Release/DSA_Setup.exe`).

To build installers:
```powershell
# Build in Release configuration
dotnet build DSAMVVM.sln -c Release

# Output packages will be generated in:
# - artifacts/Release/DSA_Installer.msi
# - artifacts/Release/DSA_Setup.exe
```

---

## Project & Runtime Overview

- **Solution Layout:**
  - [`DSAMVVM/`](DSAMVVM/) — Main WPF application (`Core/` domain services & utilities, `MVVM/` views, viewmodels & models, `Theme/` XAML styles).
  - [`DSA.Installer/`](DSA.Installer/) & [`DSA.Bootstrapper/`](DSA.Bootstrapper/) — WiX Toolset MSI and Setup bundle projects.
  - [`docs/`](docs/) — Technical architecture ([`ARCHITECTURE.md`](docs/ARCHITECTURE.md)), developer/release workflows ([`WORKFLOWS.md`](docs/WORKFLOWS.md)), and JSON data schemas ([`SCHEMAS.md`](docs/SCHEMAS.md)).
- **Per-User Runtime & Install Path (`%LOCALAPPDATA%\UArizona\DesktopSupportApp\`):**
  - `App\` (per-user MSI installation binaries), `settings.json` (user preferences), `data\` (offline JSON cache), `img\sm\` (cached ServiceMeow photos), and `logs\` (rolling daily logs).
  - *(See [Section 2](docs/ARCHITECTURE.md#2-solution-organization--project-structure) and [Section 13](docs/ARCHITECTURE.md#13-storage-layout-logging--diagnostics) of `docs/ARCHITECTURE.md` for full directory trees.)*

---

## Contributing & Code Conventions

1. **Architecture First:** Follow the established MVVM separation. Views do not query services directly; all logic resides in ViewModels and injected services.
2. **Dependency Injection:** Register all new services in [`ServiceRegistration.cs`](DSAMVVM/ServiceRegistration.cs). Prefer constructor injection with interfaces.
3. **Asynchronous Operations:** All network, LDAP, and disk operations must run asynchronously using `async`/`await` and pass cancellation tokens where applicable.
4. **Structured Logging:** Use the static `Log` helper (`Log.Debug`, `Log.Info`, `Log.Warn`, `Log.Error`) with clear category tags.
5. **UI Scaling & Styling:** Utilize styles from the `Theme/` directory and ensure typography binds through `CardTypography` to support user font size preferences.

---

## Author & Maintainers

- **Author:** Isaac Broomall ([@ibroomallaz](https://github.com/ibroomallaz))
- **Organization:** University of Arizona — UITS Desktop Support
- **Repository:** [https://github.com/ibroomallaz/Desktop-Support](https://github.com/ibroomallaz/Desktop-Support)
