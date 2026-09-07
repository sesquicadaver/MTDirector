# Weekly documentation smoke (QG-DOCS-01)

**Queue:** W7-53 / QG-DOCS-01  
**Canonical NEXT source:** [`ROADMAP.md`](../../ROADMAP.md) §3.C (`§3.C NEXT = …`)

Operator checklist (recurring; Living Spec enforces the durable invariants):

- [ ] Root [`README.md`](../../README.md) **Queue (§3.C)** line matches ROADMAP `§3.C NEXT`
- [ ] [`docs/README.md`](../README.md) **Next delivery (§3)** line matches ROADMAP `§3.C NEXT`
- [ ] Docs index links in `docs/README.md` resolve to existing files
- [ ] Service surface present: `ROADMAP.md`, `ISSUES.md`, `CHANGELOG.md`, `Directory.Build.props`, `docs/development/testing.md`, `docs/release/known-limitations.md`
- [ ] PLAN-03 quality-gate plan still lists this smoke: [`plan-03-quality-gates.md`](../planning/plan-03-quality-gates.md)

Automated gate: `QgDocs01WeeklyDocsSmokeLivingSpecTests` (`dotnet test --filter "FullyQualifiedName~QgDocs01WeeklyDocsSmoke"`).
