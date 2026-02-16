# Installation

## Requirements
- .NET 6.0 or higher

## Standalone Runtime

**Download from [Releases](https://github.com/jvnkoo/morphyn/releases/latest):**
- Runtime: `morphyn-windows-x64.exe` (Windows) / `morphyn-linux-x64` (Linux/macOS)
- Install script: `install.ps1` / `install.sh`

**Setup:**
1. Download both files for your platform
2. Run install script to add `morphyn` to PATH:
   - Windows: `.\install.ps1`
   - Linux/macOS: `./install.sh`

!!! note
    The install script only needs to be run once.

**Usage:**
```bash
# Create a file
echo 'entity Game { has score: 0 }' > game.morphyn

# Run it
morphyn game.morphyn
```

## Build from Source

Clone the repository:
```sh
git clone https://github.com/jvnkoo/morphyn.git
cd morphyn
```

Build the project:
```sh
dotnet build
```

The compiled binary will be in `bin/Debug/net6.0/` or `bin/Release/net6.0/`.

## File Extensions

Morphyn recognizes the following file extensions:
- `.morphyn` (recommended)
- `.morph`
- `.mrph`

## Development Branches

!!! note
    Development of ecosystem components is organized into branches:
    
    - **Unity Package:** Switch to the `unity-bridge` branch
    - **VS Code Extension:** Switch to the `vscode-extension` branch