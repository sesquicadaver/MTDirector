# Living Spec matrix PR gate (QG-LIVESPEC-MATRIX-01)

**Queue:** W7-55 / QG-LIVESPEC-MATRIX-01  
**Canonical matrix:** [`ROADMAP.md`](../../ROADMAP.md) §5 + [`testing.md`](testing.md)

Operator checklist (Living Spec enforces durable invariants):

- [ ] ROADMAP §5 («Living Specification — матриця») present
- [ ] Every **DONE** `QG-*` row in [`plan-03-quality-gates.md`](../planning/plan-03-quality-gates.md) has a `## Living Specification — QG-…` section in `testing.md`
- [ ] ROADMAP §5 status-table **DONE** `QG-*` rows cite the same IDs in `testing.md`
- [ ] New quality-gate PRs update both ROADMAP §5 status table and `testing.md` in the same cycle

Automated gate: `QgLivespecMatrix01LivingSpecTests` (`dotnet test --filter "FullyQualifiedName~QgLivespecMatrix01"`).
