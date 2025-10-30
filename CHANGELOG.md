# Changelog

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
