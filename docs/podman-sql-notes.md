You can start the same image with `podman run` and the settings `compose.yaml` uses. In PowerShell, from any folder:

**1. Set the password for this terminal**
```ps
$env:MSSQL_SA_PASSWORD = '<your-strong-password>'
```

**2. Create and start the container (first time only)**
```ps
podman run -d --name calendar-sql -e ACCEPT_EULA=Y -e MSSQL_PID=Express -e MSSQL_SA_PASSWORD=$env:MSSQL_SA_PASSWORD -p 127.0.0.1:1433:1433 -v calendar-sql-data:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718

# or

podman run -d --name calendar-sql --env-file .env -e ACCEPT_EULA=Y -e MSSQL_PID=Express -p 127.0.0.1:1433:1433 -v calendar-sql-data:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718
```
- `-d` runs it in the background, and `--name calendar-sql` names the container so later commands can refer to it.
- `MSSQL_PID=Express` makes it SQL Server Express, the edition the spec requires.
- `-p 127.0.0.1:1433:1433` makes it reachable at `localhost,1433` from this machine only, not from the local network.
- `-v calendar-sql-data:...` keeps the database in a named volume, so your data survives when the container is removed.
- The image is referenced by digest (the same one as `compose.yaml`), so dev always runs the same SQL Server build. The tag alone (`2022-latest`) moves over time.

**3. Check it's ready**
```ps
podman logs -f calendar-sql
```
Wait for "SQL Server is now ready for client connections", then press Ctrl+C.

Then set the `ConnectionStrings:Calendar` user secret (see `docs/local-setup.md`, step 2), pointing at `Server=localhost,1433`. When you run the API in Development, it starts this container if needed, creates the Calendar database and applies the migrations.

**Day to day**
```ps
podman stop calendar-sql
```
```ps
podman start calendar-sql
```
You only run `podman run` once. After that, use `start` and `stop`; the password is stored in the container and isn't needed again. You can also start and stop it from **Containers** in Podman Desktop.

**To start over** (for example, with a new password):
```ps
podman rm -f calendar-sql
```
The data stays in the `calendar-sql-data` volume. Remove that too with `podman volume rm calendar-sql-data` if you want a fresh database. A new password only takes effect with a fresh volume, because the existing data files keep the old `sa` password.

**Don't run both.** Use either this container or `podman compose up`, not both, since both want port 1433. Compose also creates its own volume, so the two don't share data.

#### When you want a newer SQL Server build, pull it, then replace the digest in `compose.yaml`, in the commands above, and in `tests/CalendarPlaybook.Api.IntegrationTests/Infrastructure/SqlServerFixture.cs` with the one from:

```ps
podman image inspect <_ID_> --format "{{json .RepoDigests}}"
```

---

## Troubleshooting: connecting from a SQL client (e.g. the VS Code extension)

**Port binding.** On some Podman setups on Windows, the port is forwarded only on `[::1]` (IPv6 loopback) even though the container was started with `127.0.0.1`. Check with `netstat -ano | findstr 1433`. The API still connects, but a client that resolves `localhost` to `127.0.0.1` gets refused.

**TLS certificate.** SQL Server in a container uses a self-signed certificate, and many clients reject it by default. This is the most common cause when the password is correct.

Try these in order in the connection dialog:

1. Set **Trust server certificate** to `True`. In the connection string this is `TrustServerCertificate=True`. Set **Encrypt** to `Optional` if it's offered.

2. If it still fails, change the server from `localhost,1433` to `[::1],1433`. Also try `127.0.0.1,1433` to confirm which address works.

3. In the connection string, use a comma between host and port (`localhost,1433`), not a colon.

If none of these works, note the exact error text (`ECONNREFUSED`, a certificate error, or `Login failed`); it shows which of these is the cause. For a login failure, check whether the container's SQL log shows the attempt:

```bash
podman logs --tail 30 calendar-sql
```
