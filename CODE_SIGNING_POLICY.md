# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

Windows releases of SupportLauncher (`SupportLauncher.exe` and `SupportLauncher-Setup.exe`) published on the [Releases](../../releases) page are signed. The signature names **SignPath Foundation** as publisher and confirms that the binary was built automatically from the source code in this repository.

## Team roles

| Role | Members | Responsibility |
|---|---|---|
| Committers and reviewers | @Kammermanns | May change the source code; external contributions are reviewed before merge |
| Approvers | @Kammermanns | Approve every signing request manually |

All members use multi-factor authentication for GitHub and SignPath.

## Build and release process

- Every release is built by GitHub Actions from a `v*` tag of this repository.
- Build scripts and CI configuration are part of the repository and subject to the same review.
- Each signing request is approved manually by an Approver.
- Signing keys are kept in SignPath's HSM; the project never has access to the private key.

## What the software does

SupportLauncher downloads a file from the URL configured in `launcher.ini` and starts it only if its Authenticode signature is valid and the signer matches the configured publisher. It does not install drivers or services and needs no admin rights at runtime. See [PRIVACY.md](PRIVACY.md).

## Reporting

To report a suspicious binary claiming to be SupportLauncher, open a private report via [GitHub Security Advisories](../../security/advisories/new).
