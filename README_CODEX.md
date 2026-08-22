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

Requirements and design authority will be added in a separate, later commit.
They are **not** reconstructed from the excluded historical files. Do not
reconstruct requirements from excluded materials — treat this baseline as the
implementation state only, without design authority attached.

---

See `BASELINE_VERIFICATION.md` for the verification record of this baseline.