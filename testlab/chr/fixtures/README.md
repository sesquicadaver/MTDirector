# Synthetic RouterOS fixtures

Place only **synthetic**, minimal `.rsc.example` scripts here.

Rules:

- No real public IPs, customer prefixes, or production hostnames.
- No passwords, certificates, or license payloads.
- Prefer documentation-range addresses (`10.255.0.0/16` lab space used by topologies).

Executable `.rsc` applied on CHR stays in gitignored `private/run-*` after credential injection.
