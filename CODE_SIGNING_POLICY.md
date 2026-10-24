# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

**Status:** application to SignPath Foundation submitted, pending. Until signing is active, releases are published unsigned; verify them with `SHA256SUMS.txt` attached to each release.

Windows releases of SupportLauncher (`SupportLauncher.exe` and `SupportLauncher-Setup.exe`) published on the [Releases](../../releases) page are signed. The signature names **SignPath Foundation** as publisher and confirms that the binary was built automatically from the source code in this repository.

## Team roles

| Role | Members | Responsibility |
|---|---|---|
| Authors | @Kammermanns | Trusted to modify the source code without additional review |
| Reviewers | @Kammermanns | Review all changes from other contributors (pull requests) before merge |
| Approvers | @Kammermanns | Approve every signing request manually |

All members use multi-factor authentication for GitHub and SignPath.

## Build and release process

- Every release is built by GitHub Actions from a `v*` tag of this repository.
- Build scripts and CI configuration are part of the repository and subject to the same review.
- Each signing request is approved manually by an Approver.
- Signing keys are kept in SignPath's HSM; the project never has access to the private key.

## What the software does

SupportLauncher downloads a file from the URL configured in `launcher.ini` and starts it only if its Authenticode signature is valid and the signer matches the configured publisher. It does not install drivers or services and does not change the system configuration. See [PRIVACY.md](PRIVACY.md).

Behaviour that touches Windows security features, stated openly:

- **Security warning for the started module.** After its own signature and publisher check has passed, the launcher removes the "downloaded from the internet" marker of the module and starts it without the "Open File – Security Warning" dialog. Files that fail the check are never started.
- **Administrator rights (optional, `RequestAdmin=1`).** The launcher asks for administrator rights through the normal Windows User Account Control prompt, once, and the started module inherits them. Nothing is elevated without that prompt.
- **Installed TeamViewer (optional, `StopInstalledTeamViewer=1`, default).** An installed TeamViewer blocks QuickSupport. The launcher stops the TeamViewer service and tray program for the session (after the User Account Control prompt) and starts the service again as soon as QuickSupport has been closed. The service configuration is not changed.
- **Run without administrator rights (optional, `RunWithoutAdmin=1`).** Starts the module with the user's normal rights only. This lowers privileges; it never raises them.

## Reporting

To report a suspicious binary claiming to be SupportLauncher, open a private report via [GitHub Security Advisories](../../security/advisories/new).
