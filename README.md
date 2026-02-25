# Storage Box SSH Key Installer

Local web application to generate SSH keys and install the public key on a Hetzner Storage Box sub account via SSH/SFTP port `23`.

## Disclaimer

This project is provided as-is, without warranty. Use it at your own risk.

## Architecture

- `StorageBoxKeyTool.Client`: Blazor WebAssembly UI (static frontend)
- `StorageBoxKeyTool.Api`: local ASP.NET Core API for operations browsers cannot do directly (SSH/SFTP and `ssh-keygen`)
- `StorageBoxKeyTool.Tests`: xUnit test project (domain/service validation-focused)

The API serves the Blazor static files, so you open one local URL.

## Security behavior

- Host and login are always derived from the form and restricted to Hetzner Storage Box format:
  - Login / username input: `<base-username>-sub<sub-id>`
  - Host: `<base-username>-sub<sub-id>.your-storagebox.de`
  - Port: `23`
- Passwords and passphrases are processed in memory only.
- UI clears password/passphrase fields after each finished run (except while waiting for overwrite confirmation).
- No password/passphrase values are written to logs.
- The API log level is set to `Warning`.
- Generated key files are created in a temporary directory only during generation and removed immediately after readback.

## Workflow supported

1. Fill in username (for example `u123456-sub12`) and password.
2. Choose either:
   - generate new key pair (`ed25519` / `ecdsa` / `rsa`), or
   - provide an existing public key.
3. Start process:
   - key generation (if selected)
   - remote check of `.ssh/authorized_keys`
   - if file differs: explicit overwrite confirmation required, then replace the file (delete + re-upload)
   - upload `authorized_keys` without changing permissions on files or folders
4. Success/error is shown and logged step-by-step in UI.
5. Reset button starts over from scratch.

## Run with Docker (recommended)

```bash
docker compose up --build
```

Open: `http://localhost:8080`

## Run with one container command

If the image is available as `ruepp/storagebox-privatekey`, run it directly:

```bash
docker run --rm -p 127.0.0.1:8080:8080 ruepp/storagebox-privatekey
```

```bash
podman run --rm -p 127.0.0.1:8080:8080 ruepp/storagebox-privatekey
```

Open: `http://localhost:8080`

If you need to build that exact image name locally first:

```bash
docker build -t ruepp/storagebox-privatekey .
```

```bash
podman build -t ruepp/storagebox-privatekey .
```

## Run with .NET SDK

```bash
dotnet run --project StorageBoxKeyTool.Api
```

Open URL shown in terminal (default: `http://localhost:5068`).

## Build check

```bash
dotnet build StorageBoxKeyTool.sln
```

## Tests

Run all tests:

```bash
dotnet test StorageBoxKeyTool.sln
```

Run the repository test project only:

```bash
dotnet test StorageBoxKeyTool.Tests/StorageBoxKeyTool.Tests.csproj
```

Run a single test:

```bash
dotnet test StorageBoxKeyTool.Tests/StorageBoxKeyTool.Tests.csproj --filter "FullyQualifiedName~StorageBoxKeyTool.Tests.Domain.StorageBoxInputValidatorTests.ValidateTarget_WithValidInput_ReturnsNormalizedTarget"
```

## License

This project is licensed under the MIT License. See `LICENSE` for details.
