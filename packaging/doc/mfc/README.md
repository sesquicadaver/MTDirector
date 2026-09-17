# MTDirector Controller — operator host notes (OPS-HOST-DOC-01 / W7-302)

This file is the packaging doc artifact referenced by the systemd unit:

```
Documentation=file:///usr/share/doc/mfc/README.md
```

Canonical source in the repo: `packaging/doc/mfc/README.md`.  
`scripts/release/package-controller.sh` also copies it into `$OUT_DIR/controller/README.md`.

## Install sketch (Linux)

After `package-controller.sh` (and OPS-HOST-SYSUSERS-01 bootstrap):

```bash
sudo rsync -a "$OUT_DIR/controller/" /opt/mfc/controller/
sudo install -D -m 0644 /opt/mfc/controller/README.md /usr/share/doc/mfc/README.md
# or from the repo tree:
# sudo install -D -m 0644 packaging/doc/mfc/README.md /usr/share/doc/mfc/README.md
sudo install -m 0644 packaging/systemd/mfc-controller.service /etc/systemd/system/mfc-controller.service
sudo systemctl daemon-reload
sudo systemctl enable --now mfc-controller.service
```

`systemctl status mfc-controller` / `systemctl help` will then resolve the unit `Documentation=` URI to this README.

## Related host templates

| Artifact | Role |
|----------|------|
| `mfc-controller.service` | systemd unit (OPS-HOST-SYSTEMD-01) |
| `mfc-controller.env.example` | EnvironmentFile sample (OPS-HOST-ENV-01) |
| `mfc-controller.sysusers` / `.tmpfiles` | mfc user + dirs (OPS-HOST-SYSUSERS-01) |
| `mfc-controller.winsw.xml` | Windows Service (OPS-HOST-WINSVC-01) |

See also: `docs/operations/installation.md`, `docs/howto/build-and-run.md`, `docs/release/packaging.md`.

## Out of scope

- Native MSI / AppImage (W7-22 lock).
- Secrets — never put production credentials in this README.
