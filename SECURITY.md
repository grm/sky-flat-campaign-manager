# Security Policy

## Supported versions

Security fixes are applied to the latest released version of Sky Flat Campaign Manager.

## Reporting a vulnerability

Please open a private GitHub security advisory on the repository, or contact the maintainers.
Do not file public issues for undisclosed vulnerabilities.

## Supply chain

- Dependabot is enabled for NuGet and GitHub Actions.
- Workflows use pinned major action versions and minimal permissions.
- Do not commit secrets, webhook URLs, or campaign state files.
- Optional future HTTP webhooks must never log the URL.
