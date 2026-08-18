# Deploying ADA to an Ubuntu VPS

Two ASP.NET processes behind Apache, plus SQL Server on the same box. The split
is deliberate: only `ADA-MKII-Server` holds the database credentials and provider
keys, and the web head reaches it over HTTP like any other client.

```
              Apache (TLS, :443)
                     │
        ┌────────────┴────────────┐
        │                         │
   /api, /health                  /
        │                         │
  ADA-MKII-Server           ADA-MKII-Web
   127.0.0.1:5100           127.0.0.1:5200
        │
   SQL Server 127.0.0.1:1433
```

Both apps bind to loopback only. Apache is the sole ingress.

Files here: `ada-server.service`, `ada-web.service`, `ada-apache.conf`,
`ada.env.example`.

---

## 1. SQL Server

Microsoft's `mssql-server` packages track specific Ubuntu releases and lag new
ones. **Check whether your Ubuntu version is supported before using the apt
route** — if it is not, Docker is the reliable path and is what these
instructions assume.

```bash
sudo apt install -y docker.io
sudo mkdir -p /var/opt/ada-sql
```

```bash
sudo docker run -d --name ada-sql --restart unless-stopped -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='<strong-password>' -p 127.0.0.1:1433:1433 -v /var/opt/ada-sql:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest
```

`-p 127.0.0.1:1433:1433` is the important part. Publishing without the address
would expose SQL Server to the internet, and Docker writes its own iptables rules
that bypass ufw — a firewall alone will not save you here.

Verify it is not reachable from outside:

```bash
ss -ltnp | grep 1433
```

The address must read `127.0.0.1:1433`, not `0.0.0.0:1433` or `*:1433`.

---

## 2. Publish

Build on your workstation, not the VPS. Publishing self-contained avoids
installing the .NET runtime on the server, which matters while .NET 10 is new
enough that distro packages lag.

```bash
dotnet publish ADA-MKII-Server -c Release -r linux-x64 --self-contained -o publish/server
```

```bash
dotnet publish ADA-MKII-Web -c Release -r linux-x64 --self-contained -o publish/web
```

Copy both to the server:

```bash
rsync -az --delete publish/server/ ada@your-vps:/opt/ada/server/
```

```bash
rsync -az --delete publish/web/ ada@your-vps:/opt/ada/web/
```

Then on the VPS:

```bash
sudo useradd --system --home /opt/ada --shell /usr/sbin/nologin ada
```

```bash
sudo chown -R ada:ada /opt/ada && sudo chmod +x /opt/ada/server/ADA-MKII-Server /opt/ada/web/ADA-MKII-Web
```

---

## 3. Secrets

```bash
sudo mkdir -p /etc/ada
```

Copy `ada.env.example` to `/etc/ada/server.env`, fill in the SQL password and the
OpenAI key, then lock it down:

```bash
sudo chown root:root /etc/ada/server.env && sudo chmod 0600 /etc/ada/server.env
```

The web head needs only the API address:

```bash
printf 'Ada__Client__BaseAddress=http://127.0.0.1:5100\n' | sudo tee /etc/ada/web.env >/dev/null && sudo chmod 0600 /etc/ada/web.env
```

The web head talks to the API over loopback, so that stays plain HTTP. Nothing
leaves the machine unencrypted.

---

## 4. Database schema

**Never auto-migrate on startup.** Generate a bundle on your workstation and run
it as a deliberate step:

```bash
dotnet ef migrations bundle --project ADA-MKII-Data --startup-project ADA-MKII-Server -r linux-x64 -o publish/migrate
```

Copy it up and run it with the connection string in the environment:

```bash
ConnectionStrings__Ada='Server=127.0.0.1,1433;Database=Ada;User Id=sa;Password=<password>;TrustServerCertificate=True' ./migrate
```

Run this **before** starting the services for the first time, and before each
deploy that includes a new migration.

---

## 5. systemd

```bash
sudo cp deploy/ada-server.service deploy/ada-web.service /etc/systemd/system/
```

```bash
sudo systemctl daemon-reload && sudo systemctl enable --now ada-server ada-web
```

```bash
systemctl status ada-server ada-web --no-pager
```

Logs go to journald:

```bash
journalctl -u ada-server -f
```

---

## 6. Apache

```bash
sudo a2enmod proxy proxy_http proxy_wstunnel headers rewrite ssl
```

`proxy_wstunnel` is not optional — Blazor Server runs the UI over a WebSocket at
`/_blazor`, and without it the page loads and then sits failing to reconnect.

```bash
sudo cp deploy/ada-apache.conf /etc/apache2/sites-available/ada.conf
```

Edit `ServerName` to your domain, then:

```bash
sudo a2ensite ada && sudo apache2ctl configtest && sudo systemctl reload apache2
```

Certificates:

```bash
sudo apt install -y certbot python3-certbot-apache && sudo certbot --apache -d ada.example.com
```

Browser microphone access requires a secure context, so HTTPS is required for
voice — not merely advisable.

---

## 7. The first account

There is no registration endpoint. Accounts are created only by
`ADA-MKII-DataManager`, which is Windows-only and talks to the database
directly — so run it from your workstation over an SSH tunnel rather than
exposing SQL Server:

```bash
ssh -L 1433:127.0.0.1:1433 ada@your-vps
```

With that open, point DataManager at the tunnel:

```bash
dotnet user-secrets set "ConnectionStrings:Ada" "Server=127.0.0.1,1433;Database=Ada;User Id=sa;Password=<password>;TrustServerCertificate=True" --project ADA-MKII-DataManager
```

Create your account there, then sign in from the web UI or the phone.

---

## 8. Verify

```bash
curl -s https://ada.example.com/health
```

Then, in order:

1. `/health` returns `{"status":"ok"}` — Apache, the API and the unit are all up.
2. Signing in through the browser works — the database is migrated and the
   account exists.
3. A chat reply arrives **word by word, not in one lump** — SSE is not being
   buffered. If it arrives all at once, check the `no-gzip` block in the vhost.
4. The page does not show "Rejoining the server" — the WebSocket is proxied.
5. From a phone on mobile data, set the server address on the login screen to
   `https://ada.example.com` and sign in.

With no `Ada__OpenAI__ApiKey` set, the server uses `EchoLlmProvider` and replies
`[echo provider] You said: ...`. That is a useful first smoke test, because it
proves everything except the model call.

---

## 9. Backups

The database holds a verbatim transcript of everything ever said to ADA. Treat it
accordingly: encrypt the volume, and **test a restore** rather than assuming the
backup works.

```bash
sudo docker exec ada-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<password>' -C -Q "BACKUP DATABASE Ada TO DISK='/var/opt/mssql/ada.bak' WITH INIT, COMPRESSION"
```

`/var/opt/ada-sql` on the host is the mounted volume, so the backup lands there
for you to copy off the machine on a schedule.

---

## Known gotchas

- **Docker bypasses ufw.** Publishing a port without an explicit `127.0.0.1:`
  prefix exposes it regardless of firewall rules.
- **Forwarded headers matter.** The API reads `X-Forwarded-For` so the per-IP
  rate limiters work; without it every request looks like `127.0.0.1` and the
  login limiter would lock out everyone after ten attempts from anyone. Only
  loopback proxies are trusted, so the header cannot be forged from outside.
- **SSE and compression do not mix.** Any gzip filter over `/api/chat` turns a
  stream into a single delayed response.
- **A page reload signs you out of the web UI.** Deliberate: the token lives in
  the Blazor circuit's memory, never in browser storage.
- **`dotnet ef database update` is not a deploy step.** Use the bundle; the
  server has no EF tooling and should not have database DDL rights at runtime.
