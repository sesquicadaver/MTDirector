# Branch protection status (M0-01)

Repository settings applied:

- Squash merge **enabled**
- Merge commit **disabled**
- Rebase merge **disabled**
- Delete branch on merge **enabled**
- Default branch: `main`

Classic branch protection / rulesets for a **private** repository require GitHub Pro (or a public repository). Until that is available, enforce the following by process:

1. No direct commits to `main` — open a PR.
2. No force-push to `main`.
3. Squash-merge only.
4. Resolve conversations before merge.
5. Require CODEOWNERS review for paths listed in `.github/CODEOWNERS`.

When Pro is enabled or the repo is public, apply the protection payload documented in `Repository Bootstrap Plan v0.1` §11.4 via:

```bash
gh api repos/{owner}/{repo}/branches/main/protection -X PUT --input branch-protection.json
```
