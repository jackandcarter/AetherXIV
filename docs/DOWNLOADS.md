# Downloads and integrity

AetherXIV 2.1.2 is build **22044**. Open
[Releases](https://github.com/jackandcarter/AetherXIV/releases) while signed in
with repository access. This download repository is currently private; GitHub
can show a 404 when it is unavailable to the signed-in account.

## Choose a package

| Package | Contents | Use it when |
|---|---|---|
| Full, with no package suffix | Core, server hosts, database, Launcher, Umbra, and platform runtime | Running a local server and client together |
| `-Core` | Core GUI, server hosts, database package and server data | Operating a server |
| `-Launcher` | Launcher, Umbra, launch helpers, and platform runtime | Connecting to an existing server |

macOS/Linux/SteamOS Launcher packages include the AetherXIV compatibility
runtime. Windows launches the game natively and does not include Wine.
Factory is optional and is not included in these application downloads.

| Platform | Filename prefix | Extension |
|---|---|---|
| Windows x64 | `AetherXIV-2.1.2.22044-Windows-x64` | `.zip` |
| macOS Apple silicon | `AetherXIV-2.1.2.22044-MacOS-arm64` | `.zip` |
| Linux x64 | `AetherXIV-2.1.2.22044-Linux-x64` | `.tar.gz` |
| SteamOS x64 | `AetherXIV-2.1.2.22044-SteamOS-x64` | `.tar.gz` |

Append `-Core` or `-Launcher` before the extension for those variants. Extract
the complete archive; macOS archives may also contain normal Apple metadata.

## Verify a download

Download `SHA256SUMS.txt` beside your chosen archive. Compare its matching line
with the hash produced locally:

```powershell
Get-FileHash .\AetherXIV-2.1.2.22044-Windows-x64.zip -Algorithm SHA256
```

```sh
# macOS
shasum -a 256 AetherXIV-2.1.2.22044-MacOS-arm64.zip

# Linux; use the matching SteamOS filename on Steam Deck
sha256sum AetherXIV-2.1.2.22044-Linux-x64.tar.gz
```

The checksum file covers the twelve application archives and
`resolved-dependencies.lock.json`. The lock records the exact reviewed Umbra
and Wine producer versions and archive hashes. It is provenance metadata,
not an installer or a credential file.

## GitHub source archive links

GitHub automatically adds **Source code (zip)** and **Source code (tar.gz)**
links for a release tag. They contain the documentation repository at that
tag, not the application implementation or the platform binaries. Choose the
named AetherXIV platform archive under **Assets** to install the application.

The v2.1.2 tag remains an immutable snapshot. Guides on the default branch can
receive corrections after the tag was created; use the current documentation
index for the latest installation guidance.
