# Demo

Ready-to-use example files for the universal setup.

| File | Purpose |
|---|---|
| `launcher.ini` | Demo settings (company "Demo AG", generic TeamViewer QuickSupport download) |
| `logo.png` | Demo logo shown in the launcher window |
| `app.ico` | Icon for the shortcut and the window |

## Try it

1. Download `SupportLauncher-Demo.zip` from the [latest build](../../../releases/tag/latest-build). It contains the setup together with these files.
2. Unzip it and run `SupportLauncher-Setup.exe`, or silently:
   ```bat
   SupportLauncher-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
   ```
3. Start the shortcut "Fernwartung Demo AG" on the desktop.

## Make it yours

Copy the three files, replace the logo and icon, and change at least `CompanyName`, `ShortcutName` and `DownloadUrl`. Put them next to the setup – no rebuild needed.
