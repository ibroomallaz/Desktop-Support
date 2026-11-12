# .NET 10 vs. .NET 11 Load-Time & Responsiveness Benchmark Report

> [!IMPORTANT]
> **Executive Recommendation: Casual, Planned Upgrade Over Time (Stay on .NET 10 LTS for Now)**
> Across **25 fresh `dotnet test` process launches** and **over 8,000 timed warm iterations** covering 12 startup and UI responsiveness scenarios, **.NET 10 (`10.0.11` LTS)** matches or outperforms **.NET 11 (`11.0.0`)** on the metrics that matter most to a WPF desktop technician app—specifically **WPF XAML view instantiation (`14–23%` faster on .NET 10)** and **shell tab switching (`15.1 ms/tab` on .NET 10 vs. `18.4 ms/tab` on .NET 11)**. Upgrading Microsoft NuGet packages (`Microsoft.Extensions.DependencyInjection 11.0.0`, `System.DirectoryServices.AccountManagement 11.0.0`, `Microsoft.Graph 6.10.0`) also yielded zero measurable startup or DI speedups over `10.0.14`.

---

## 1. Test Architecture & Methodology

To isolate runtime, JIT/Dynamic PGO, BCL, and WPF layout differences from external network/Active Directory jitter, a dedicated multi-targeted xUnit benchmark project ([`DSA.Tests.csproj`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/DSA.Tests.csproj)) was created targeting both `net10.0-windows10.0.26100.0` and `net11.0-windows10.0.26100.0`.

### Key Harness Files
* **Project Configuration:** [`DSA.Tests.csproj`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/DSA.Tests.csproj)
* **STA Dispatcher & Statistical Runner:** [`LoadBenchmarkRunner.cs`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Infrastructure/LoadBenchmarkRunner.cs)
* **Deterministic Test Harness & Fakes:** [`TestAppHarness.cs`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Infrastructure/TestAppHarness.cs)
* **Deterministic Test Ordering (`T01–T06`, `R01–R06`):** [`AlphabeticalOrderer.cs`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Infrastructure/AlphabeticalOrderer.cs)
* **Startup / Load-Time Suite:** [`StartupLoadTimeTests.cs`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/LoadTime/StartupLoadTimeTests.cs)
* **UI Responsiveness Suite:** [`UiResponsivenessTests.cs`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Responsiveness/UiResponsivenessTests.cs)

---

## 2. Part I: Startup & Load-Time Benchmarks (`T01` – `T06`)

Each configuration was executed across **5 fresh process launches** in `Release` mode (`40` warmup iterations + `150–400` timed warm iterations per scenario per configuration):
1. **`.NET 10.0.11` Baseline** (`Microsoft.Extensions.DependencyInjection 10.0.14`, `System.DirectoryServices.AccountManagement 10.0.14`, `Microsoft.Graph 6.9.0`)
2. **`.NET 11.0.0` (Same `10.0.14` Packages)**
3. **`.NET 11.0.0` + Updated `11.0.0` MS Packages** (`Microsoft.Extensions.DependencyInjection 11.0.0`, `System.DirectoryServices.AccountManagement 11.0.0`, `Microsoft.Graph 6.10.0`)

### A. Cold Start Latency (Median & Mean ± SD across 5 Fresh Processes)

| Scenario | `.NET 10.0.11` (`10.0.14` pkgs) | `.NET 11.0.0` (`10.0.14` pkgs) | `.NET 11.0.0` (`11.0.0` pkgs) | Cold Start Winner & Takeaways |
| :--- | ---: | ---: | ---: | :--- |
| **T01.** [`T01_DiContainer_And_MainViewModelGraph_LoadTime`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/LoadTime/StartupLoadTimeTests.cs#L23-L53) | **`48.26 ms`**<br>(`49.96 ± 3.69 ms`) | **`46.36 ms`**<br>(`49.74 ± 7.33 ms`) | **`50.23 ms`**<br>(`50.86 ± 2.81 ms`) | **Tie** (`~48–50 ms` across all three). |
| **T02.** [`T02_SettingsService_LoadAndClamp_LoadTime`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/LoadTime/StartupLoadTimeTests.cs#L56-L90) | **`249.87 ms`**<br>(`229.4 ± 72.3 ms`) | **`30.11 ms`**<br>(`129.6 ± 221.6 ms`) | **`30.96 ms`**<br>(`34.56 ± 8.39 ms`) | **`.NET 11` wins (`-88%`)**: faster first-time `Newtonsoft.Json` reflection/IL generation. |
| **T03.** [`T03_DepartmentService_PreCacheAndLookup_LoadTime`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/LoadTime/StartupLoadTimeTests.cs#L93-L134) | **`498.97 ms`**<br>(`473.1 ± 85.9 ms`) | **`398.34 ms`**<br>(`376.7 ± 45.6 ms`) | **`427.43 ms`**<br>(`430.3 ± 58.3 ms`) | **`.NET 11` wins (`-14%` to `-20%`)**: saves `~70–100 ms` on cold 500-dept parse + index. |
| **T04.** [`T04_LinksAndServiceMeow_Warmup_LoadTime`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/LoadTime/StartupLoadTimeTests.cs#L137-L185) | **`29.55 ms`**<br>(`28.21 ± 2.40 ms`) | **`30.85 ms`**<br>(`30.97 ± 3.72 ms`) | **`31.10 ms`**<br>(`30.02 ± 1.78 ms`) | **Tie** (`~30 ms` across all three). |
| **T05.** [`T05_RemoteWithBackUpLoader_CacheSynchronization_LoadTime`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/LoadTime/StartupLoadTimeTests.cs#L188-L235) | **`19.27 ms`**<br>(`19.40 ± 3.39 ms`) | **`15.57 ms`**<br>(`17.59 ± 3.40 ms`) | **`18.20 ms`**<br>(`17.73 ± 2.86 ms`) | **Tie** (`~16–19 ms` across all three). |
| **T06.** [`T06_WpfThemeAndViews_InstantiationAndLayout_LoadTime`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/LoadTime/StartupLoadTimeTests.cs#L238-L274) | **`396.33 ms`**<br>(`403.6 ± 71.2 ms`) | **`430.05 ms`**<br>(`503.7 ± 124.5 ms`) | **`540.84 ms`**<br>(`549.9 ± 122.9 ms`) | **`.NET 10` wins (`-8.5%` to `-26.7%`)**: fastest cold WPF XAML instantiation & layout pass. |

---

### B. Warm Steady-State Load Latency (`150` to `400` Timed Iterations per TFM)

| Scenario | `.NET 10.0.11` Warm Median (Mean ± SD) | `.NET 11.0.0` (`10.x` pkgs) Warm Median (Mean ± SD) | `.NET 11.0.0` (`11.0` pkgs) Warm Median (Mean ± SD) | Memory Allocated (`KB/op`) (10 vs 11 vs 11+pkgs) |
| :--- | ---: | ---: | ---: | :--- |
| **T01. DI + `MainViewModel` (`N=400`)** | **`2.97 ms`**<br>(`2.99 ± 0.06 ms`) | **`2.99 ms`**<br>(`3.03 ± 0.19 ms`) | **`3.09 ms`**<br>(`3.24 ± 0.32 ms`) | `152.6 KB` / `151.9 KB` / `152.9 KB` |
| **T02. `SettingsService.LoadAsync` (`N=400`)** | **`2.26 ms`**<br>(`2.33 ± 0.33 ms`) | **`1.78 ms`**<br>(`1.85 ± 0.21 ms`) | **`1.88 ms`**<br>(`1.91 ± 0.21 ms`) | `.NET 11` is **`~18–21%` faster** (`-0.4 ms`). |
| **T03. `DepartmentService` (`N=300`)** | **`5.60 ms`**<br>(`5.99 ± 1.17 ms`) | **`7.57 ms`**<br>(`6.78 ± 1.65 ms`) | **`8.90 ms`**<br>(`8.49 ± 1.43 ms`) | `861.3 KB` / `868.9 KB` / `859.0 KB` |
| **T04. `LinksVM` + `ServiceMeow` (`N=300`)** | **`7.53 ms`**<br>(`7.50 ± 0.66 ms`) | **`6.98 ms`**<br>(`7.79 ± 1.55 ms`) | **`9.42 ms`**<br>(`8.64 ± 1.46 ms`) | `696.7 KB` / `695.2 KB` / `695.4 KB` |
| **T05. `RemoteWithBackUpLoader` (`N=300`)** | **`4.01 ms`**<br>(`4.59 ± 1.14 ms`) | **`4.05 ms`**<br>(`4.93 ± 1.99 ms`) | **`6.80 ms`**<br>(`6.03 ± 1.97 ms`) | `554.7 KB` / `554.8 KB` / `554.5 KB` |
| **T06. WPF 8 Views XAML + Layout (`N=150`)** | **`343.74 ms`**<br>(`348.8 ± 24.3 ms`) | **`390.35 ms`**<br>(`397.5 ± 36.8 ms`) | **`450.83 ms`**<br>(`445.2 ± 25.5 ms`) | `21,375 KB` / `21,411 KB` / `21,390 KB` |
| **Total Load Suite Duration (5 Passes)** | **Mean: `20.0 s`** (`18–22 s`) | **Mean: `21.6 s`** (`20–25 s`) | **Mean: `25.0 s`** (`23–27 s`) | — |

---

## 3. Part II: UI Responsiveness Benchmarks (`R01` – `R06`)

Executed across **5 interleaved process launches per framework** (`2,200` total timed interactive operations per framework):

### A. Warm Interactive Responsiveness (`N = 300` to `500` ops per TFM)

| Scenario | `.NET 10.0.11` Warm Median (Mean ± SD) | `.NET 10` Per-Action Latency | `.NET 11.0.0` Warm Median (Mean ± SD) | `.NET 11` Per-Action Latency | Delta (`.NET 11` vs `.NET 10`) | Allocated (`KB/op`) (`.NET 10` vs `.NET 11`) |
| :--- | ---: | ---: | ---: | ---: | :--- | :--- |
| **R01.** [`R01_ShellNavigation_And_ContentHostRebind_Responsiveness`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Responsiveness/UiResponsivenessTests.cs#L28-L78)<br>*(6-Tab Cycle + Layout)* | **`90.69 ms`**<br>(`89.74 ± 4.78 ms`) | **`15.1 ms` / tab** | **`110.32 ms`**<br>(`108.99 ± 5.69 ms`) | **`18.4 ms` / tab** | **+21.6% slower on .NET 11** (`+3.3 ms` per tab switch) | `3,526.1 KB` vs `3,530.0 KB` |
| **R02.** [`R02_GroupCard_LiveKeystrokeFiltering_Responsiveness`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Responsiveness/UiResponsivenessTests.cs#L81-L138)<br>*(16 Keystrokes: 200 MIM + 500 Members)* | **`0.481 ms`**<br>(`0.478 ± 0.009 ms`) | **`30.1 µs` / key** | **`0.505 ms`**<br>(`0.558 ± 0.178 ms`) | **`31.5 µs` / key** | **Tie (`+0.024 ms`)**; `.NET 10` has `20x` lower SD | `312.14 KB` vs `312.02 KB` |
| **R03.** [`R03_LinksViewModel_TeamSwitching_Responsiveness`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Responsiveness/UiResponsivenessTests.cs#L141-L190)<br>*(25 Support Teams x 12 Links)* | **`0.140 ms`**<br>(`0.137 ± 0.012 ms`) | **`5.6 µs` / switch** | **`0.148 ms`**<br>(`0.150 ± 0.013 ms`) | **`5.9 µs` / switch** | **Tie (`+0.008 ms`)**; `.NET 11` allocates `-0.8 KB` less | `45.32 KB` vs `44.54 KB` |
| **R04.** [`R04_SearchCardFeed_RapidLookupAndCapRotation_Responsiveness`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Responsiveness/UiResponsivenessTests.cs#L193-L258)<br>*(15 Lookups + History Cap)* | **`0.197 ms`**<br>(`0.202 ± 0.058 ms`) | **`13.1 µs` / card** | **`0.321 ms`**<br>(`0.280 ± 0.063 ms`) | **`21.4 µs` / card** | **+62.9% slower on .NET 11** (`+0.12 ms`, both sub-ms) | `160.9 KB` vs `153.2 KB` |
| **R05.** [`R05_Typography_FontZoomBroadcast_And_Relayout_Responsiveness`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Responsiveness/UiResponsivenessTests.cs#L261-L312)<br>*(10 Font Zoom Steps + Relayout)* | **`0.139 ms`**<br>(`0.164 ± 0.061 ms`) | **`13.9 µs` / step** | **`0.269 ms`**<br>(`0.271 ± 0.028 ms`) | **`26.9 µs` / step** | **+93.9% slower on .NET 11** (`+0.13 ms`, both sub-ms) | `140.6 KB` vs `138.3 KB` |
| **R06.** [`R06_Dispatcher_InputAndRenderQueue_Latency_UnderContention`](file:///C:/Users/ibroomall/RiderProjects/Desktop-Support/DSA.Tests/Responsiveness/UiResponsivenessTests.cs#L315-L353)<br>*(50 UI Priority Frames Under Load)* | **`7.650 ms`**<br>(`7.835 ± 1.91 ms`) | **`153 µs` / frame** | **`6.877 ms`**<br>(`7.410 ± 1.43 ms`) | **`138 µs` / frame** | **-10.1% faster on .NET 11** (`-0.77 ms` across 50 frames) | `745.1 KB` vs `745.3 KB` |
| **Total Responsiveness Suite Duration** | **Mean: `9.2 s`** (`9–10 s`) | — | **Mean: `10.4 s`** (`10–11 s`) | — | **+13.0% (`+1.2 s` slower on .NET 11)** | — |

### B. Cold First-Interaction Latency (5 Fresh Processes per TFM)

| Scenario | `.NET 10.0.11` Cold Median (Mean ± SD) | `.NET 11.0.0` Cold Median (Mean ± SD) | Cold Delta |
| :--- | ---: | ---: | :--- |
| **R01. First 6-Tab Navigation Cycle** | **`294.06 ms`** (`304.9 ± 20.9 ms`) | **`305.08 ms`** (`300.0 ± 16.6 ms`) | **Tie** (`~300 ms`) |
| **R02. First 16-Keystroke Filter** | **`4.12 ms`** (`4.03 ± 0.57 ms`) | **`3.12 ms`** (`3.70 ± 1.32 ms`) | **`-1.0 ms`** faster on `.NET 11` |
| **R03. First 25-Team Switch Sweep** | **`0.72 ms`** (`0.71 ± 0.10 ms`) | **`0.77 ms`** (`0.74 ± 0.16 ms`) | **Tie** (`< 0.05 ms`) |
| **R04. First 15-Card Search Feed Batch** | **`26.68 ms`** (`27.28 ± 7.16 ms`) | **`26.21 ms`** (`28.93 ± 9.60 ms`) | **Tie** (`~26.5 ms`) |
| **R05. First 10-Step Font Zoom + Relayout** | **`3.78 ms`** (`3.55 ± 0.74 ms`) | **`4.01 ms`** (`3.85 ± 0.42 ms`) | **Tie** (`+0.23 ms`) |
| **R06. First 50 Dispatcher Frames Under Load** | **`122.93 ms`** (`128.6 ± 10.9 ms`) | **`120.69 ms`** (`123.6 ± 6.07 ms`) | **`-2.2 ms`** faster on `.NET 11` |

---

## 4. Key Insights & Decision Matrix

### 1. Why `.NET 10` Is Optimal for Production Right Now
* **60Hz Frame-Budget Compliance on Tab Switching (`R01`):** A single 60Hz display frame is **`16.67 ms`**. On `.NET 10.0.11`, switching tabs in `MainViewModel` and completing the full WPF `Measure`/`Arrange`/`UpdateLayout` pass averages **`15.1 ms` per tab** (within a single 60Hz frame). On `.NET 11.0.0`, the same tab switch averages **`18.4 ms` per tab** (crossing the 1-frame threshold).
* **Mature LTS Servicing vs. Early STS Release:** `.NET 10.0.11` has 11 servicing updates of JIT and WPF refinements and Long-Term Support (LTS), whereas `.NET 11.0.0` is an initial `.0` Standard-Term Support (STS) build.
* **No Deployment Churn Required:** Upgrading `DSAMVVM` in production would also require updating `DSA.Installer` and `DSA.Bootstrapper` prerequisite checks to push the `.NET 11` WindowsDesktop runtime to technician machines for no net UX improvement.

### 2. Where `.NET 11` Shows Promise for the Future
* **Dynamic Code Gen / Reflection Speed (`T02` & `T03` Cold):** `.NET 11` compiles `Newtonsoft.Json` dynamic serializers **`70–200 ms` faster** on the very first cold hit in a new process.
* **Dispatcher Priority Contention (`R06`) & Memory Efficiency (`R03–R05`):** Under heavy background thread-pool contention, `.NET 11` services `DispatcherPriority.Input`/`Render` callbacks **`~10%` faster** (`6.88 ms` vs `7.65 ms` for 50 frames) and reduces per-operation allocations by `1–5%` on several ViewModel operations.

---

## 5. How to Re-Run the Benchmarks Anytime

You can run the full suite or individual categories in Rider's Unit Test Explorer or from PowerShell:

```powershell
# Run all 12 benchmarks (Load-Time + Responsiveness) on .NET 10
dotnet test DSA.Tests/DSA.Tests.csproj -c Release -f net10.0-windows10.0.26100.0

# Run all 12 benchmarks (Load-Time + Responsiveness) on .NET 11
dotnet test DSA.Tests/DSA.Tests.csproj -c Release -f net11.0-windows10.0.26100.0

# Run only Load-Time tests or only Responsiveness tests
dotnet test DSA.Tests/DSA.Tests.csproj -c Release --filter "FullyQualifiedName~StartupLoadTimeTests"
dotnet test DSA.Tests/DSA.Tests.csproj -c Release --filter "FullyQualifiedName~UiResponsivenessTests"
```
