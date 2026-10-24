# SupportLauncher

A tiny Windows launcher that always starts the **current** version of a remote support module (for example a customized TeamViewer QuickSupport).

Instead of copying a support EXE to customer PCs, where it goes stale, the customer gets one desktop shortcut. Each click:

1. shows a small branded window (logo, company name, status),
2. downloads the support module from a configured HTTPS link,
3. checks its Authenticode signature and publisher; anything else is **never started**,
4. starts it and closes.

If the PC is offline, the last successfully verified copy is started instead.

No admin rights are needed to run it. The EXE targets .NET Framework 4.8, which is part of Windows 10 and 11, so there is nothing else to install.

> SupportLauncher is an independent project and is not affiliated with or endorsed by TeamViewer. TeamViewer is a trademark of TeamViewer Germany GmbH. This project does not contain or redistribute any TeamViewer software; it only downloads the file from the link you configure.

## Configuration

Everything organisation-specific lives in [`branding/`](branding):

| File | Purpose |
|---|---|
| `launcher.ini` | Company name, window texts, download URL, allowed publisher(s) |
| `logo.png` | Optional logo shown in the window (about 380 × 80 px) |
| `app.ico` | Icon of the EXE, installer and shortcut (built-in default) |

Key settings in `launcher.ini`:

```ini
DownloadUrl=https://get.teamviewer.com/YOUR-ID
AllowedPublishers=TeamViewer Germany GmbH
```

`AllowedPublishers` must match the signer's `CN` or `O` exactly. Check it on a downloaded file with
`Get-AuthenticodeSignature .\QuickSupport.exe | Select -Expand SignerCertificate`.

## Installation

Download `SupportLauncher-Setup.exe` from the [Releases](../../releases) page.

**The setup is universal.** You do not need to rebuild it for your company: put your files next to the setup and it installs them instead of the built-in defaults.

```
SupportLauncher-Setup.exe
launcher.ini      settings: download link, company name, shortcut name, ...
logo.png          optional: logo in the launcher window (about 380 x 80 px)
app.ico           optional: icon for the shortcut and the window
```

Use [`branding/launcher.ini`](branding/launcher.ini) as a template, or start from the working [demo](demo): `SupportLauncher-Demo.zip` on the [latest build](../../releases/tag/latest-build) contains the setup with demo files and runs out of the box. The signed setup and EXE are not modified, so their signatures stay valid. Without files next to the setup, an existing configuration from an earlier installation is kept.

```bat
:: interactive
SupportLauncher-Setup.exe

:: silent, all users (admin)
SupportLauncher-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART

:: silent, current user only (no admin)
SupportLauncher-Setup.exe /VERYSILENT /CURRENTUSER

:: silent with overrides instead of a launcher.ini
SupportLauncher-Setup.exe /VERYSILENT /URL="https://get.teamviewer.com/xyz" /COMPANY="Example Ltd" /NAME="Remote Support"

:: silent uninstall
"%ProgramFiles%\SupportLauncher\unins000.exe" /VERYSILENT
```

The installer creates a desktop and start menu shortcut (`ShortcutName`, default "Fernwartung"). Downloaded modules are cached in `%LOCALAPPDATA%\SupportLauncher`.

Note: the shortcut name is read with the Windows INI API, so keep `ShortcutName` free of umlauts or pass it with `/NAME=`. All other values may contain any characters (UTF-8 or ANSI).

## Building

Releases are built by GitHub Actions ([`build.yml`](.github/workflows/build.yml)) on every `v*` tag:

1. build `SupportLauncher.exe`
2. sign it via SignPath and verify the result
3. build the Inno Setup installer **from the signed EXE**
4. sign the installer and verify it
5. create a draft release with SHA-256 checksums

Local build on Windows: `dotnet publish src/SupportLauncher -c Release -o publish`, then `iscc installer\SupportLauncher.iss`.

### SignPath setup (maintainers)

Repository secret: `SIGNPATH_API_TOKEN`
Repository variables: `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_POLICY_SLUG`, `SIGNPATH_LAUNCHER_CONFIG_SLUG`, `SIGNPATH_SETUP_CONFIG_SLUG`

The artifact configurations are in [`.signpath/`](.signpath). Without `SIGNPATH_PROJECT_SLUG` the pipeline still builds, unsigned.

## Code signing

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

See the [code signing policy](CODE_SIGNING_POLICY.md) and the [privacy policy](PRIVACY.md).

## License

[MIT](LICENSE)
