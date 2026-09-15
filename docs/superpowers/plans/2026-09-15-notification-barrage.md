> 执行说明：本文保留初始计划。实际实现修正了 Windows API/包依赖，采用代码式 WPF 窗口和 MakeAppx 脚本；完成状态、实际文件和阻塞见 [验证记录](../../verification.md)。初稿中的英文提交示例已被用户要求的中文描述取代。

# Windows Notification Barrage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows desktop tray application that reads QQ/WeChat Windows notifications and renders them as non-interactive scrolling barrage messages over games.

**Architecture:** A .NET 8 WPF app hosts a tray controller, a WinRT `UserNotificationListener` adapter, pure filtering/queue services, and a transparent click-through overlay window. Settings are stored as JSON under `%AppData%\\NotificationBarrage`; the app is packaged with Windows identity and the `userNotificationListener` capability so Windows can grant notification access.

**Tech Stack:** .NET 8, WPF, Windows 10/11 WinRT APIs (`Microsoft.Windows.SDK.Contracts`), Windows Forms `NotifyIcon`, xUnit, MSIX packaging.

**Spec:** `notification-barrage/docs/superpowers/specs/2026-09-15-notification-barrage-design.md`

## Global Constraints

- Support Windows 10 1903 and later, plus Windows 11.
- Target .NET 8 WPF and keep the app in the new `notification-barrage/` directory.
- Read only Windows notifications; do not inspect QQ/WeChat chat windows, send replies, upload content, or log full message bodies.
- Default sources are QQ and WeChat; source matching must be configurable.
- Overlay is single-screen, transparent, topmost, non-activating, and mouse-through.
- Queue limit is 5 concurrent messages; receive limit is 3 messages per second.
- When paused, retain at most the latest 10 queued messages.
- Persist preferences at `%AppData%\\NotificationBarrage\\settings.json` and logs at `%AppData%\\NotificationBarrage\\logs\\`.

---

### Task 1: Scaffold the WPF solution and test project

**Files:**
- Create: `notification-barrage/NotificationBarrage.sln`
- Create: `notification-barrage/src/NotificationBarrage/NotificationBarrage.csproj`
- Create: `notification-barrage/src/NotificationBarrage/App.xaml`
- Create: `notification-barrage/src/NotificationBarrage/App.xaml.cs`
- Create: `notification-barrage/tests/NotificationBarrage.Tests/NotificationBarrage.Tests.csproj`
- Create: `notification-barrage/tests/NotificationBarrage.Tests/SmokeTests.cs`
- Create: `notification-barrage/README.md`

**Interfaces:**
- Produces a `net8.0-windows` WPF executable and an xUnit test project that can run without Windows UI startup.

- [ ] **Step 1: Create project files**

  Configure the app with `<UseWPF>true</UseWPF>`, `<UseWindowsForms>true</UseWindowsForms>`, `TargetFramework` `net8.0-windows10.0.19041.0`, nullable and implicit usings enabled. Add `Microsoft.Windows.SDK.Contracts` version `10.0.22621.755` and `Microsoft.Extensions.DependencyInjection` version `8.0.0`. Reference the app project from the test project and add xUnit packages.

- [ ] **Step 2: Add a smoke test**

  Add `Fact` test asserting `typeof(App).Assembly.GetName().Name == "NotificationBarrage"`; this proves the test project resolves the app assembly without constructing WPF windows.

- [ ] **Step 3: Run the first build and test**

  Run `dotnet test notification-barrage/tests/NotificationBarrage.Tests/NotificationBarrage.Tests.csproj`.
  Expected: PASS with one smoke test.

- [ ] **Step 4: Document local startup**

  README must include `dotnet run --project src/NotificationBarrage`, Windows target requirements, and the fact that notification access permission is required.

- [ ] **Step 5: Commit**

  `git add notification-barrage && git commit -m "build: scaffold notification barrage app"`

### Task 2: Add domain models, settings, and privacy-safe logging

**Files:**
- Create: `notification-barrage/src/NotificationBarrage/Domain/IncomingNotification.cs`
- Create: `notification-barrage/src/NotificationBarrage/Domain/BarrageMessage.cs`
- Create: `notification-barrage/src/NotificationBarrage/Domain/AppSettings.cs`
- Create: `notification-barrage/src/NotificationBarrage/Services/SettingsStore.cs`
- Create: `notification-barrage/src/NotificationBarrage/Services/AppLogger.cs`
- Create: `notification-barrage/tests/NotificationBarrage.Tests/SettingsStoreTests.cs`

**Interfaces:**
- `IncomingNotification(Guid Id, string AppId, string AppName, string Title, string Body, DateTimeOffset ReceivedAt)`.
- `BarrageMessage(string Source, string Title, string Body, DateTimeOffset ReceivedAt, string DeduplicationKey)`.
- `AppSettings` properties: `EnableQQ`, `EnableWeChat`, `Position` (`Top|Center|Bottom`), `VerticalOffset`, `SpeedPixelsPerSecond`, `FontSize`, `Opacity`, `MaxBodyLength`, `StartWithWindows`.
- `SettingsStore.Load()` returns defaults on missing/corrupt JSON and backs up corrupt files; `Save(AppSettings)` atomically replaces the JSON file.
- `AppLogger.Info(string eventName, object? metadata = null)` and `Error(string eventName, Exception exception)` omit message body fields.

- [ ] **Step 1: Write failing settings tests**

  Test default values, round-trip JSON persistence under a temporary directory, and corrupt JSON fallback that creates a `.broken-*` backup.

- [ ] **Step 2: Implement immutable notification/message records and settings validation**

  Clamp speed to 50–2000, opacity to 0.2–1.0, font size to 12–72, max body length to 20–500, and vertical offset to -500–500.

- [ ] **Step 3: Implement atomic `SettingsStore`**

  Write JSON to `settings.json.tmp`, flush, then replace the destination. Use `%AppData%\\NotificationBarrage` by default but inject a root path in tests.

- [ ] **Step 4: Implement privacy-safe `AppLogger`**

  Write date-based files, cap each file at 2 MB, and serialize only event names and metadata explicitly passed by services.

- [ ] **Step 5: Run tests and commit**

  Run `dotnet test notification-barrage/tests/NotificationBarrage.Tests --filter FullyQualifiedName~SettingsStoreTests`.
  Expected: PASS.
  Commit with `git add notification-barrage && git commit -m "feat: add settings and privacy-safe logging"`.

### Task 3: Implement notification filtering and barrage queue

**Files:**
- Create: `notification-barrage/src/NotificationBarrage/Services/MessageFilter.cs`
- Create: `notification-barrage/src/NotificationBarrage/Services/BarrageQueue.cs`
- Create: `notification-barrage/tests/NotificationBarrage.Tests/MessageFilterTests.cs`
- Create: `notification-barrage/tests/NotificationBarrage.Tests/BarrageQueueTests.cs`

**Interfaces:**
- `MessageFilter.TryCreate(IncomingNotification notification, AppSettings settings, out BarrageMessage? message)`.
- `BarrageQueue.Enqueue(BarrageMessage message)`, `TryDequeue(out BarrageMessage? message)`, `SetPaused(bool paused)`, `Count`, `ActiveCount`, and `MarkActiveCompleted()`.

- [ ] **Step 1: Write filtering tests**

  Cover QQ/WeChat name and package matching, disabled sources, whitespace normalization, body truncation with `…`, empty title/body rejection, and deterministic deduplication keys.

- [ ] **Step 2: Write queue tests**

  Cover FIFO order, duplicate rejection, three-per-second receive throttling, five active-message cap, pause retention of latest ten, and recovery after `MarkActiveCompleted()`.

- [ ] **Step 3: Implement `MessageFilter`**

  Keep known QQ/WeChat display names and package fragments in one table. Normalize whitespace with a regular expression, prefer body over title for content, and hash source/title/body/time bucket when notification ID is unavailable.

- [ ] **Step 4: Implement thread-safe `BarrageQueue`**

  Use a lock-protected queue and `HashSet<string>` for dedupe. Maintain a sliding timestamp queue for the three-per-second limit; while paused, trim from the head until ten items remain.

- [ ] **Step 5: Run tests and commit**

  Run `dotnet test notification-barrage/tests/NotificationBarrage.Tests --filter FullyQualifiedName~MessageFilterTests|FullyQualifiedName~BarrageQueueTests`.
  Expected: PASS.
  Commit with `git add notification-barrage && git commit -m "feat: filter and queue notification messages"`.

### Task 4: Integrate Windows UserNotificationListener

**Files:**
- Create: `notification-barrage/src/NotificationBarrage/Services/INotificationSource.cs`
- Create: `notification-barrage/src/NotificationBarrage/Services/WindowsNotificationSource.cs`
- Create: `notification-barrage/src/NotificationBarrage/Services/NotificationPermissionService.cs`
- Create: `notification-barrage/tests/NotificationBarrage.Tests/NotificationMappingTests.cs`

**Interfaces:**
- `INotificationSource`: `event EventHandler<IncomingNotification>? NotificationReceived`, `Task<NotificationAccessStatus> GetAccessStatusAsync()`, `Task StartAsync(CancellationToken)`, `Task StopAsync()`.
- `NotificationPermissionService.OpenNotificationSettings()` opens `ms-settings:notifications` through `Process.Start` and never throws into the UI thread.

- [ ] **Step 1: Write pure mapping tests**

  Test conversion of a WinRT `UserNotification` fixture into `IncomingNotification`, including missing body/title and stable ID extraction. Keep fixtures as plain test records so tests run without a live notification center.

- [ ] **Step 2: Implement permission/status adapter**

  Wrap `UserNotificationListener.Current.RequestAccessAsync()` and `GetAccessStatus()`, map statuses to an internal enum, and expose a settings URI action.

- [ ] **Step 3: Implement event subscription and mapping**

  Subscribe to `UserNotificationListener.Current.NotificationChanged`; on `NotificationKinds.Added`, fetch the notification, read app display name, app user model ID, title and body, then raise `NotificationReceived`. Do not log body text.

- [ ] **Step 4: Add exponential retry**

  Retry initialization at 1s, 5s, 15s, then cap at 60s. Cancellation must stop retries and unsubscribe events.

- [ ] **Step 5: Run tests and commit**

  Run `dotnet test notification-barrage/tests/NotificationBarrage.Tests --filter FullyQualifiedName~NotificationMappingTests`.
  Expected: PASS.
  Commit with `git add notification-barrage && git commit -m "feat: read Windows user notifications"`.

### Task 5: Build the transparent barrage overlay

**Files:**
- Create: `notification-barrage/src/NotificationBarrage/UI/OverlayWindow.xaml`
- Create: `notification-barrage/src/NotificationBarrage/UI/OverlayWindow.xaml.cs`
- Create: `notification-barrage/src/NotificationBarrage/UI/BarrageItemControl.xaml`
- Create: `notification-barrage/src/NotificationBarrage/UI/BarrageItemControl.xaml.cs`
- Create: `notification-barrage/tests/NotificationBarrage.Tests/OverlayLayoutTests.cs`

**Interfaces:**
- `OverlayWindow.ShowMessage(BarrageMessage message, AppSettings settings)`.
- `OverlayWindow.ApplySettings(AppSettings settings)` and `OverlayWindow.ClearMessages()`.

- [ ] **Step 1: Write layout tests**

  Test position calculation for top/center/bottom and clamped vertical offsets using a pure `OverlayLayout.Calculate(Rect screen, AppSettings settings)` helper.

- [ ] **Step 2: Implement click-through window**

  Use `WindowStyle=None`, `AllowsTransparency=True`, transparent background, `Topmost=True`, `ShowActivated=False`, and Win32 `SetWindowLong` styles `WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`.

- [ ] **Step 3: Implement barrage animation**

  Render source color block, title and body in a rounded item. Animate from screen right to left with `TranslateTransform`, then fade opacity to zero and remove the item. Ensure completion decrements the queue active count.

- [ ] **Step 4: Run Windows build and manual test**

  Run `dotnet build notification-barrage/src/NotificationBarrage/NotificationBarrage.csproj -c Debug` on Windows and launch the app's test-message command once it is wired in Task 6. Confirm the overlay does not take focus or intercept mouse input.

- [ ] **Step 5: Commit**

  `git add notification-barrage && git commit -m "feat: add click-through barrage overlay"`

### Task 6: Add tray host, settings window, and service wiring

**Files:**
- Create: `notification-barrage/src/NotificationBarrage/AppHost.cs`
- Create: `notification-barrage/src/NotificationBarrage/UI/SettingsWindow.xaml`
- Create: `notification-barrage/src/NotificationBarrage/UI/SettingsWindow.xaml.cs`
- Modify: `notification-barrage/src/NotificationBarrage/App.xaml.cs`
- Create: `notification-barrage/src/NotificationBarrage/Services/StartupRegistration.cs`

**Interfaces:**
- Tray commands: `Pause/Resume`, `TestBarrage`, `OpenSettings`, `CheckPermission`, `Exit`.
- `StartupRegistration.SetEnabled(bool enabled)` creates/removes the per-user `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run` value for the packaged executable path.

- [ ] **Step 1: Wire dependency graph**

  In `AppHost`, construct `SettingsStore`, `AppLogger`, `WindowsNotificationSource`, `MessageFilter`, `BarrageQueue`, and `OverlayWindow`; subscribe source events to filter then queue, and run a dispatcher timer that drains queue into the overlay.

- [ ] **Step 2: Implement tray icon and commands**

  Use `NotifyIcon` with a context menu. Keep the app single-instance via a named mutex. Show one balloon tip for missing permission or corrupt settings, then remain quiet until state changes.

- [ ] **Step 3: Implement settings window**

  Bind controls to `AppSettings`; validate/clamp values on save; apply updated settings to queue and overlay; include an “Open Windows notification settings” button.

- [ ] **Step 4: Implement test barrage and pause behavior**

  Test command enqueues a synthetic QQ message. Pause stops draining while source events continue; resume trims to ten and drains recent messages.

- [ ] **Step 5: Run app smoke test and commit**

  Run `dotnet run --project notification-barrage/src/NotificationBarrage/NotificationBarrage.csproj`; verify tray menu, settings persistence, test barrage, pause/resume, and clean exit.
  Commit with `git add notification-barrage && git commit -m "feat: wire tray host and settings"`.

### Task 7: Package with notification access capability and document verification

**Files:**
- Create: `notification-barrage/packaging/NotificationBarrage.Package/Package.appxmanifest`
- Create: `notification-barrage/packaging/NotificationBarrage.Package/NotificationBarrage.Package.wapproj`
- Create: `notification-barrage/packaging/NotificationBarrage.Package/Images/` placeholder assets
- Modify: `notification-barrage/README.md`

- [ ] **Step 1: Declare Windows identity and capability**

  Add the desktop application entry point and `<rescap:Capability Name="userNotificationListener" />` to the MSIX manifest, targeting the minimum Windows version from the spec.

- [ ] **Step 2: Build package**

  Run `dotnet build notification-barrage/packaging/NotificationBarrage.Package/NotificationBarrage.Package.wapproj -c Debug`; expected: package build succeeds on a machine with Visual Studio/MSIX tooling.

- [ ] **Step 3: Update first-run instructions**

  Document installing the MSIX, granting notification access, starting the app, using “Test barrage”, and uninstalling. State that QQ/WeChat must be configured to show Windows notifications.

- [ ] **Step 4: Execute end-to-end checklist**

  Verify one QQ notification and one WeChat notification each render once, disabled sources are filtered, five messages animate concurrently, pause retains latest ten, malformed settings recover, and app remains usable when permission is denied.

- [ ] **Step 5: Commit**

  `git add notification-barrage && git commit -m "build: package notification barrage for Windows"`

## Self-review

- Spec coverage: Tasks 2–3 cover settings, privacy, filtering, dedupe, limits and pause semantics; Task 4 covers Windows notification access and retries; Task 5 covers the transparent overlay; Task 6 covers tray UX, testing and startup; Task 7 covers packaging, capability and manual acceptance.
- Placeholder scan: no TODO/TBD steps; each task names files, interfaces, commands and expected outcomes.
- Type consistency: `IncomingNotification`, `BarrageMessage`, `AppSettings`, `INotificationSource`, `MessageFilter`, `BarrageQueue`, and `OverlayWindow` signatures are defined before consumers.
