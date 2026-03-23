# Review Policy

## Accepted Patterns

- **CI-gated auto-merge without human approval**: This repository restricts
  branch access to trusted contributors only. Auto-merging after all required
  CI checks pass is an intentional workflow choice, not a security gap.

- **Same-repository branches only**: All contributors work on branches within
  this repository. Fork-based PRs are not part of the workflow; PR lookup
  logic does not need to account for fork owners.

- **No unit tests for GitHub Actions scripts**: Workflow automation scripts
  in `.github/scripts/` are integration-heavy by nature. Pure helper
  functions are unit-tested; end-to-end flows are validated by the CI
  pipeline itself.
