# Changelog

## [3.1.0] - 2026-04-06

### Added

- **Restart LAMISPlus functionality**
  - New "🔄 Restart LAMISPlus" button integrated into the toolbar between "Update Selected" and "Clear Logs"
  - Automatically opens LAMISPlus in the default browser after successful restart
  - Positioned strategically for easy access after module installations/updates
  - Calls the `/restart` endpoint to gracefully restart the LAMISPlus server

- **Enhanced install/update reliability**
  - Pre-loads already-installed modules from server before Install All operation
  - Prevents unnecessary reinstallation attempts that could cause failures
  - Added 60-second registration wait after successful module installation to ensure server fully registers modules
  - Skips already-installed modules intelligently during Install All workflow

### Fixed

- **Critical JSON deserialization bug**
  - Changed `Permission.Id` from `string?` to `int?` to match LAMISPlus server response format
  - Fixed "The JSON value could not be converted to System.String" error during module upload
  - Resolved cascade failures where Patient, ADR, Backup, DQR and other modules failed to install
  - This was causing 14+ modules to be skipped due to missing dependencies

- **Dependency tracking improvements**
  - Better logging showing exactly which dependencies are missing when modules are skipped
  - Clear messages differentiating between already-installed modules and skipped modules
  - Improved HashSet tracking for installed modules to prevent false negatives

### Improved

- **User experience**
  - Restart button automatically launches LAMISPlus login page in browser (matches native restart behavior)
  - More informative log messages during Install All showing pre-installed modules
  - Better visual consistency with restart button matching other toolbar buttons

- **Code consistency**
  - Aligned GUI `InstallModuleAsync` behavior with console app `Program.cs` by adding registration wait
  - Improved error messages and logging throughout the installation process

### Summary

Version **3.1.0** is a **critical stability and usability release** that fixes a major JSON deserialization bug preventing module installations, adds convenient LAMISPlus restart functionality, and significantly improves installation reliability by pre-checking installed modules and waiting for proper registration. This release resolves the cascade failure issue where Patient module failure caused 14+ dependent modules to skip installation.

---

## [3.0.0] - 2026-04-02

### Added

- **Professional application footer with branding**
  - Copyright notice: "© 2025-2026 Brilliant Smart. All rights reserved."
  - Contact information with internationally formatted phone number: +234 803 462 5258
  - "Powered by Brilliant Smart" branding with version display
  - Professional layout with clean, modern design

- **DPI awareness support**
  - Added application manifest with PerMonitorV2 DPI awareness mode
  - Fixes blurry UI on high-DPI displays (4K monitors, high-resolution laptops)
  - Ensures crisp text and controls on all display configurations

- **Auto-scroll to installing module**
  - DataGrid automatically scrolls to and highlights the currently installing module
  - Improves user experience during batch installations by keeping the active module visible

### Improved

- **Build quality**
  - Resolved all compiler warnings (previously 15 warnings, now 0)
  - Fixed nullable reference warnings in ModuleViewModel and MainViewModel
  - Removed System.Windows.Forms reference conflicts
  - Clean build output ensures production-ready quality

- **Code quality**
  - Properly initialized all nullable fields with default values
  - Enhanced JSON deserialization with proper attribute mapping for ModuleInstallResponse

### Technical

- **Version**: Updated from 2.3.2 to 3.0.0
- **Target Framework**: .NET 8.0 Windows
- **Build**: Zero warnings, zero errors

### Summary

Version **3.0.0** is a **quality and branding release** that enhances the professional appearance of the application while improving technical quality.  
The addition of **Brilliant Smart branding**, **DPI awareness**, and **zero-warning builds** makes this the most polished and production-ready version yet.  
This release demonstrates commitment to **quality, professionalism, and user experience**.

---

## [1.0.0] - 2025-10-05

### Added

- Login with password field
- Module loading from selected folder
- Dependency-based installation order
- Installation progress bar with percent
- Folder picker to change modules directory

## [1.1.0] - 2025-10-05

### Changed

- Buttons get smooth modern look with hover effect.
- GroupBoxes have subtle shadow + rounded corners.
- DataGrid headers are elegant with light gray background.
- Entire app has uniform margins, spacing, and padding.

## [1.2.0] - 2025-10-07

### Improved

- Layout now keeps **buttons, progress bar, and logs always visible**, regardless of window resize.
- **DataGrid scrolls independently**, ensuring large module lists don’t push other UI elements out of view.
- **Buttons remain fixed above logs**, preventing UI jumping or overlap.
- All sections are visible immediately on launch — **no manual resizing required**.

### Added

- **Automatic folder initialization**:
  - Checks if module folder exists.
  - Prompts the user to create it if missing.
  - Creates the folder automatically upon confirmation.
  - Calls `LoadLocalModules()` immediately afterward to ensure modules are loaded every time.

### Summary

A usability-focused release that stabilizes layout, improves scroll behavior, and automates module directory setup without changing core installer logic.

## [1.2.1] - 2025-10-17

### Fixed

- **Module auto-loading works again on startup.**
  - If the modules folder already exists, modules are now loaded immediately without requiring manual re-selection.
  - If the folder is missing or deleted, the app now prompts to create it and automatically loads modules after confirmation.

### Improved

- **DataGrid now respects module install hierarchy.**  
  Modules are sorted and displayed in the same dependency order, in an order to be used during the installation, making the UI reflect real execution flow.

- **Installed version detection is now accurate.**
  - The app now queries LAMISPlus to match module names (`Patient` ↔ `PatientModule`) and fetch actual versions of the modules currently installed on the server.
  - Installed modules show their real version (e.g. `2.1.1`) instead of `(unknown)`.
  - Modules not found on the server are clearly labeled as **Not Installed** instead of ambiguous defaults (unknown).

### UX Summary

A more intelligent and reliable modules' grid. Modules are now sorted based on dependency order (in an order in which they will be installed), and modules that are installed or currently on the server (lamisplus) are displayed with their versions correctly, not (unknown).

---

## [2.0.0] - 2025-10-19

### Added

- **Install Selected Command** — Users can now install a single module manually instead of always using “Install All”.
- **Dependency validation before install** — If a module depends on others (e.g. ENCOUNTER → PATIENT), the installer checks server-installed modules first and blocks installation if dependencies are missing.

### Improved

- **Cleaner module name matching** — Normalizes variations like `Patient`, `patient-module`, or `01_PatientModule` to detect dependencies reliably.
- **Installation feedback** — Status messages now clearly differentiate success, failure, and dependency errors.

### Summary

This version introduces **manual control over installation** with single-module deployment and **intelligent dependency checks**, laying the groundwork for upcoming multi-select support in **v2.1.0**. Which users can select multiple modules and install them in bulk.

---

## [2.2.0] - 2025-10-22

### Added

- **Multi-module selection and installation** — Users can now select and install multiple modules in one batch.
- **Automatic dependency resolution** — If selected modules depend on others also selected, they are automatically installed first.
- **Progress tracking for multiple modules** — Real-time installation percentage and logs show progress across all selected modules.

### Improved

- **Dependency enforcement** — The installer now strictly validates dependencies before each install. Missing prerequisites outside the selection stop the operation with a clear alert.
- **Normalized dependency handling** — Consistent name matching ensures even modules with naming variations (e.g. `HIV-Module`, `hivmodule`, `HIV`) are correctly identified.
- **Robust logging** — Detailed feedback for every module: dependency checks, upload, install status, and completion summaries.

### Summary

Version **2.2.0** brings full **multi-select installation** powered by a dependency-aware engine.  
It intelligently installs in the correct hierarchy, tracks progress across all modules, and halts gracefully on missing prerequisites.  
This release solidifies the installer as a reliable tool for managing complex LAMISPlus module deployments.

---

## [2.3.1] - 2025-10-30

### Added

- **Post-installation summary report** — Displays total number of modules processed, with counts of ✅ successful, ❌ failed, and ⏭️ skipped installs.
- **Improved dependency validation logic** — Modules that fail due to missing dependencies now display the exact missing modules.
- **Server message relay** — Backend errors (e.g., rollback-only transactions or unsatisfied requirements) are now surfaced directly in the log.
- **Update All and Update Selected commands** — Users can now update all or specific modules automatically by clicking the appropriate button **Its implementation will come in the next update**.
  - Only the buttons are added so far

### Improved

- **Error resilience** — Even when one module fails, others continue installing or updating; missing dependencies are skipped cleanly instead of aborting the process.
- **Logging clarity** — Each module action (check, upload, install/update, verify) is timestamped and includes precise status with emojis for easy tracking.
- **Installation and update verification** — The installer now automatically re-checks the server after ambiguous responses to confirm whether a module was successfully installed or updated.
- **Final status transparency** — At the end of each batch installation or update, a structured summary block is printed in the logs for audit and debugging.

### Summary

Version **2.3.1** unifies **installation and update operations** into a single intelligent engine.  
Users can now install, update, and verify modules with complete transparency — seeing exactly what succeeded, failed, or was skipped.  
This release brings the most comprehensive and user-friendly experience yet, closing the loop between **local module management and server state**.

---

## [2.3.2] - 2025-12-31

### Added

- **Fully implemented Update All and Update Selected commands**

  - The update buttons introduced in v2.3.1 are now fully functional.
  - The installer detects installed modules on the LAMISPlus server and compares versions before updating.
  - Modules not found on the server are treated as fresh installations automatically.

- **Batch update summary report**
  - Displays total modules processed.
  - Clear breakdown of:
    - ✅ Successfully updated / installed
    - ❌ Failed updates
    - ⏭️ Skipped (already up-to-date)

### Improved

- **Unified update engine**

  - Installation and update now share the same robust execution pipeline.
  - Errors in one module no longer stop the entire update process.

- **Version comparison logic**

  - Local and server versions are compared safely, even when version data is missing or inconsistent.
  - Modules already up to date are skipped cleanly with a clear log message.

- **Logging clarity and transparency**
  - Step-by-step logs for:
    - Server module discovery
    - Update vs install decision
    - Per-module execution status
  - Final structured summary block printed at the end of every update operation.

### Summary

Version **2.3.2** completes the update feature introduced in v2.3.1.  
Users can now **update all modules or selected modules confidently**, with clear feedback, safe failure handling, and a transparent audit trail.  
This release focuses on **reliability, recoverability, and operational clarity** rather than UI changes.
