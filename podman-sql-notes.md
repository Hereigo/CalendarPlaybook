You can start the same image with `podman run` and the settings `compose.yaml` uses. In PowerShell, from any folder:

**1. Set the password for this terminal**
```powershell
$env:MSSQL_SA_PASSWORD = 'Your_Str0ng!Pass'
```

**2. Create and start the container (first time only)**
```powershell
podman run -d --name calendar-sql -e ACCEPT_EULA=Y -e MSSQL_PID=Express -e MSSQL_SA_PASSWORD=$env:MSSQL_SA_PASSWORD -p 1433:1433 -v calendar-sql-data:/var/opt/mssql mcr.microsoft.com/mssql/server@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718
```
- `-d` runs it in the background, and `--name calendar-sql` names the container so later commands can refer to it.
- `MSSQL_PID=Express` makes it SQL Server Express, the edition the spec requires.
- `-p 1433:1433` makes it reachable at `localhost,1433`.
- `-v calendar-sql-data:...` keeps the database in a named volume, so your data survives when the container is removed.
- The image is referenced by digest, so this uses the image you already have. You could also write `mcr.microsoft.com/mssql/server:2022-latest` or just `7fb213934171`.

**3. Check it's ready**
```powershell
podman logs -f calendar-sql
```
Wait for "SQL Server is now ready for client connections", then press Ctrl+C.

**Day to day**
```powershell
podman stop calendar-sql
```
```powershell
podman start calendar-sql
```
You only run `podman run` once. After that, use `start` and `stop`; the password is stored in the container and isn't needed again. You can also start and stop it from **Containers** in Podman Desktop.

**To start over** (for example, with a new password):
```powershell
podman rm -f calendar-sql
```
The data stays in the `calendar-sql-data` volume. Remove that too with `podman volume rm calendar-sql-data` if you want a fresh database. A new password only takes effect with a fresh volume, because the existing data files keep the old `sa` password.

**Don't run both.** Use either this container or `podman compose up`, not both, since both want port 1433. Compose also creates its own volume, so the two don't share data.

The API connection string is the same as before: `ConnectionStrings:Calendar` in user secrets, pointing at `Server=localhost,1433`.

Look for “SQL Server is now ready for client connections.” After that, set the ConnectionStrings:Calendar user secret from my earlier message. When you run the API in Development, it creates the Calendar database and applies the migration on startup.


#### When you want a newer SQL Server build, pull it, then replace the digest in compose.yaml with the one from:

```ps
podman image inspect <_ID_> --format "{{json .RepoDigests}}"
```