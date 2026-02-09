#!/bin/bash
SOURCE_BINARY="morphyn-linux-x64"
DEST_NAME="morphyn"
INSTALL_DIR="/usr/local/bin"

if [ ! -f "$SOURCE_BINARY" ]; then
    echo "Error: $SOURCE_BINARY not found in current directory."
    ls -F
    exit 1
fi

chmod +x "$SOURCE_BINARY"
sudo cp "$SOURCE_BINARY" "$INSTALL_DIR/$DEST_NAME"
echo "Success: Installed as '$DEST_NAME'. Try running 'morphyn' now."