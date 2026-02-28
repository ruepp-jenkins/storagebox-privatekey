# Hetzner StorageBox Helper

Local web application to manage SSH key setup and backrest/restic compatibility for a Hetzner Storage Box sub account via SSH/SFTP port `23`.

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
- UI shows a prominent warning banner when accessed from a non-local host (not `localhost`, loopback, or private network IP ranges).
- Warning banner can be suppressed intentionally with environment variable `STORAGEBOX_DISABLE_NON_LOCAL_WARNING=true`.
- Global password stays in the left navigation until changed manually; key passphrase fields are cleared after each finished run.
- No password/passphrase values are written to logs.
- The API log level is set to `Warning`.
- Generated key files are created in a temporary directory only during generation and removed immediately after readback.

## Automatic backup of `authorized_keys`

Before overwriting `/home/.ssh/authorized_keys`, both workflows automatically create a numbered backup on the Storage Box (e.g., `authorized_keys.backup.1`, `.backup.2`, etc.). This prevents data loss if a write goes wrong. If backup creation fails, the operation aborts.

## Workflow supported

The UI has a left navigation with two entries:

- `sFTP key`
- `backrest / restic`

Global connection credentials are configured once in the left navigation and reused for both workflows:

- Storage Box username (`<base>-sub<id>`, for example `u123456-sub12`)
- Storage Box password

### sFTP key

1. Choose key source:
   - generate a new key pair (`ed25519` / `ecdsa` / `rsa`), or
   - provide an existing OpenSSH public key.
2. Start process:
   - remote check of `/home/.ssh/authorized_keys`,
   - if key differs: explicit overwrite confirmation required,
   - upload/update only SSH key content (preserving unrelated lines where possible).

### backrest / restic

1. Pre-check runs first:
   - if no SSH key login exists in `/home/.ssh/authorized_keys`, the workflow errors immediately and points to the `sFTP key` tab.
2. If pre-check passes, apply compatibility setup:
   - ensure `/home/.config/rclone/rclone.conf` exists (created only if missing),
   - update `/home/.ssh/authorized_keys` to include:
     - SSH2 public-key block (`---- BEGIN SSH2 PUBLIC KEY ----` ...),
     - command line like `command="rclone serve restic --stdio /home" <algorithm> <base64> <login>@<host>`.

Success/error is shown and logged step-by-step in UI. Reset buttons start each workflow from scratch.

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

If you intentionally host this on a non-local/public endpoint and want to suppress the UI security warning banner:

```bash
docker run --rm -p 127.0.0.1:8080:8080 -e STORAGEBOX_DISABLE_NON_LOCAL_WARNING=true ruepp/storagebox-privatekey
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

# Screenshots

## sFTP key
<img width="1410" height="835" alt="image" src="https://github.com/user-attachments/assets/0fca6c17-563e-47d9-b104-5ff84c62d818" />

# backrest / restic
Add configuration and instruction to use backrest / restic with a storagebox using the restic http api server.
<img width="1418" height="1042" alt="image" src="https://github.com/user-attachments/assets/ccf5e59b-54cf-4ef7-a939-2c382642b7b6" />
