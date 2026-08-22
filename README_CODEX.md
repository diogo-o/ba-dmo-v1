# BA DMO — Clean Implementation Baseline

This repository is the **clean implementation baseline** of the current BA DMO
application.

## What this repository is

- The current application source, copied faithfully and byte-for-byte from the
  source workspace, preserved exactly as it exists now.
- A clean, independent, version-controlled starting point from which every
  later change can be measured.

## What was intentionally excluded

Historical recovery material was deliberately excluded from this baseline:

- historical audits and audit conclusions
- recovery reports and remediation plans
- old design packages / design mockups / visual-refinement folders
- AI-context packages and agent-tooling state
- build artifacts, logs, temporary output, coverage, publish output
- the prior Git history (including the source `.git`)

None of this excluded material is needed to restore, build, test, or run the
current application.

## Current implementation

The current implementation is preserved here **before** any pre-design repair
work. No design implementation has occurred in this baseline commit. Known
application defects are intentionally **not** corrected in this baseline.

## Requirements / design authority

The authoritative design and implementation context for this repository is now
contained in the `AI-CONTEXT/` and `reports/` folders of this same repository.

### AUTHORITY ORDER

1. **AI-CONTEXT/docs/FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md**
   = functional/business authority

2. **AI-CONTEXT/design-coder**
   = current final design/presentation/interactions authority

3. **current application source**
   = implementation evidence, not requirements authority

4. **reports/DESIGN_PLAN_DATABASE_SUPPORT_AUDIT.md**
   and
   **reports/DAPPER_REPOSITORY_COMPLETENESS_AUDIT.md**
   = verified implementation-readiness evidence

Historical recovery materials were intentionally excluded. Do not search outside
this repository to reconstruct requirements.

The baseline application commit intentionally contains known pre-design defects
that will be fixed in later commits. They are not described here in detail.

---

See `BASELINE_VERIFICATION.md` for the verification record of this baseline.