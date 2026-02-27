# AGENTS.md

Agent guidance for working in this repository.

## 1) Project Overview

- Repository type: .NET 9 solution with Blazor WebAssembly frontend + ASP.NET Core API backend.
- Main goal: provide two workflows for Hetzner Storage Box on SSH/SFTP port `23`: sFTP key management and backrest/restic compatibility setup.
- Connection input uses a single Storage Box login in `<base-username>-sub<sub-id>` format (for example `u123456-sub12`).
- Solution file: `StorageBoxKeyTool.sln`.
- Primary documentation: `@README.md`.

## 2) Repository Layout

- `StorageBoxKeyTool.Client/`: Blazor WebAssembly UI.
- `StorageBoxKeyTool.Api/`: ASP.NET Core host + API + SSH/SFTP + key generation logic.
- `StorageBoxKeyTool.Tests/`: xUnit test project for domain/service behavior.
- `Dockerfile`: multi-stage build for API + static frontend.
- `docker-compose.yml`: local container run binding to `127.0.0.1:8080`.
- `README.md`: runtime/security workflow and usage instructions.

## 3) Toolchain and Prerequisites

- .NET SDK: `9.x` required (`TargetFramework` is `net9.0`).
- Docker: optional but recommended for local deployment.
- For non-container local run, `ssh-keygen` must be available on PATH.
- OS assumptions: Linux/macOS/Windows with modern .NET + Docker support.

## 4) Build / Run / Lint / Test Commands

### Restore

- `dotnet restore StorageBoxKeyTool.sln`

### Build

- `dotnet build StorageBoxKeyTool.sln`
- `dotnet build StorageBoxKeyTool.sln -c Release`

### Run locally (no Docker)

- `dotnet run --project StorageBoxKeyTool.Api`
- API serves the SPA and static assets; open URL printed by Kestrel.

### Run with hot reload

- `dotnet watch --project StorageBoxKeyTool.Api run`

### Publish

- `dotnet publish StorageBoxKeyTool.Api/StorageBoxKeyTool.Api.csproj -c Release -o ./artifacts/publish`

### Docker

- `docker compose up --build`
- `docker build -t ruepp/storagebox-privatekey .`
- `docker run --rm -p 127.0.0.1:8080:8080 ruepp/storagebox-privatekey`
- `podman build -t ruepp/storagebox-privatekey .`
- `podman run --rm -p 127.0.0.1:8080:8080 ruepp/storagebox-privatekey`

### Lint / formatting

- `dotnet format StorageBoxKeyTool.sln --verify-no-changes`
- `dotnet format StorageBoxKeyTool.sln`

### Tests (current status + expected usage)

- Run all tests with:
- `dotnet test StorageBoxKeyTool.sln`
- Run tests in this repository's test project:
- `dotnet test StorageBoxKeyTool.Tests/StorageBoxKeyTool.Tests.csproj`
- Run a single test by fully-qualified name:
- `dotnet test StorageBoxKeyTool.Tests/StorageBoxKeyTool.Tests.csproj --filter "FullyQualifiedName~StorageBoxKeyTool.Tests.Domain.StorageBoxInputValidatorTests.ValidateTarget_WithValidInput_ReturnsNormalizedTarget"`
- Run tests from one class:
- `dotnet test StorageBoxKeyTool.Tests/StorageBoxKeyTool.Tests.csproj --filter "FullyQualifiedName~StorageBoxKeyTool.Tests.Domain.PublicKeyUtilityTests"`
- Run tests by partial method name:
- `dotnet test StorageBoxKeyTool.Tests/StorageBoxKeyTool.Tests.csproj --filter "Name~GenerateAsync_WithUnsupportedAlgorithm"`

## 5) C# and .NET Code Style

- Follow nullable reference types strictly (`<Nullable>enable</Nullable>` is on).
- Prefer file-scoped namespaces in non-top-level files.
- Keep one primary type per file.
- Use `record` / `record class` for DTO-like immutable payloads.
- Use `sealed` for classes not designed for inheritance.
- Use `internal` for non-public implementation details.
- Use `var` when type is obvious from the right-hand side; otherwise be explicit.
- Keep methods short and single-purpose.
- Use expression clarity over cleverness.

## 6) Imports and Ordering

- Place `using` directives at file top.
- Group order: `System.*` first, then third-party, then project namespaces.
- Remove unused imports.
- Keep usings alphabetically sorted within each group.

## 7) Naming Conventions

- Types, methods, properties, constants: `PascalCase`.
- Local variables and parameters: `camelCase`.
- Private fields in components/classes: `_camelCase`.
- Async methods must end with `Async`.
- Boolean members should read naturally (`IsBusy`, `RequiresOverwrite`, etc.).

## 8) API and Backend Conventions

- Keep API endpoints under `/api` route group.
- Return structured error payloads (`ApiErrorResponse`) for failures.
- Catch specific exceptions first, broader exceptions last.
- Do not return stack traces or raw sensitive internals in HTTP responses.
- Keep workflows separated:
  - sFTP key flow: validation -> check -> optional overwrite -> upload.
  - backrest/restic flow: pre-check for existing SSH key login -> apply compatibility changes.
- Do not modify permissions on `.ssh`, `.ssh/authorized_keys`, `.config/rclone`, or `.config/rclone/rclone.conf`; only create/update file content.
- Keep host derivation constrained to Hetzner Storage Box format.
- Keep fixed port `23` behavior unless explicit product change is requested.

## 9) Validation and Error Handling

- Prefer fail-fast validation near boundaries (request DTOs, validators).
- Use `ValidationException` for input validation paths.
- Use domain-specific exceptions for workflow conflicts (e.g., overwrite required).
- Keep user-facing errors actionable and concise.
- Never include secrets in exception messages.

## 10) Security and Privacy Rules (Critical)

- Never log or persist Storage Box passwords.
- Never log or persist key passphrases.
- Never log private key material.
- Keep temporary key files ephemeral and delete in `finally` blocks.
- Do not add telemetry that could capture credential-like payloads.
- Do not loosen hostname/port restrictions without explicit requirement.
- Keep API logging level conservative (`Warning` in appsettings).
- Keep non-local hosting warning enabled by default; only allow suppression via `STORAGEBOX_DISABLE_NON_LOCAL_WARNING=true` (intentional deployments).

## 11) Blazor / Frontend Conventions

- Keep form validation explicit and user-visible.
- Preserve reset/start-over UX after each run.
- Keep progress log updates deterministic and step-based.
- Use strongly typed models for form state and API payloads.
- Keep components responsive (desktop + mobile layouts).
- Use scoped CSS (`.razor.css`) for component-specific styles.
- Keep the left navigation with two entries (`sFTP key`, `backrest / restic`).
- Keep global connection inputs (username/password) in the left navigation and reuse them across workflows.
- Keep workflow-specific UI isolated per navigation entry (do not mix actions between tabs/pages).
- In backrest/restic flow, pre-check must fail fast when no SSH key login is configured and provide a clear path to the `sFTP key` entry.
- Support root directory input with `/home` default; custom path must remain under `/home`.
- Show a prominent warning banner when the app is accessed from non-local/non-private hosts.

## 12) Files to Avoid Editing Directly

- Do not edit generated build outputs under `bin/` or `obj/`.
- Do not hand-edit bundled vendor assets under `wwwroot/lib/` unless required.
- Prefer editing source files in `Pages/`, `Services/`, `Domain/`, `Contracts/`, `Models/`.

## 13) Documentation and Change Hygiene

- Update `README.md` when behavior or run instructions change.
- Always review and update `AGENTS.md` when project structure, commands, conventions, security rules, or workflows change.
- After code changes, run `dotnet build StorageBoxKeyTool.sln` and relevant `dotnet test` commands before finishing.
- When behavior, validation, or workflows change, review whether existing tests must be updated and whether new tests must be added.
- Keep command examples copy/paste friendly.
- Ensure new instructions are local-first and security-aware.

## 14) Cursor / Copilot Rule Files

- Checked paths:
- `.cursor/rules/`
- `.cursorrules`
- `.github/copilot-instructions.md`
- Current status: none of these files exist in this repository.
- If such files are added later, treat them as higher-priority agent instructions.

## 15) Definition of Done for Agent Changes

- Code compiles with `dotnet build StorageBoxKeyTool.sln`.
- Relevant automated tests pass (`dotnet test StorageBoxKeyTool.sln` or targeted test filters when appropriate).
- Formatting passes (`dotnet format ... --verify-no-changes`) when practical.
- Any changed workflow is documented in `README.md`.
- `AGENTS.md` has been reviewed and updated when needed.
- Test impact has been reviewed; tests were adjusted or added when needed.
- Security constraints around secrets are preserved.
- No unrelated refactors or drive-by changes.
