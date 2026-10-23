#!/usr/bin/env bash
set -euo pipefail
export XDG_CURRENT_DESKTOP=GNOME
export GNOME_KEYRING_CONTROL="${XDG_RUNTIME_DIR}/keyring"
openbox >"${XDG_RUNTIME_DIR}/openbox.log" 2>&1 &
echo 'Starting isolated keyring'
printf '%s' 'muse-ci-keyring-password' | timeout 15 gnome-keyring-daemon --unlock --components=secrets
echo 'Testing desktop window'
timeout 45 npm run test:integration
session_directory="$(mktemp -d)"
for phase in write read abrupt read-abrupt logout read-logout corrupt; do
    echo "Testing session phase: ${phase}"
    timeout 30 ./node_modules/.bin/electron tests/session-process.cjs "$phase" "$session_directory"
done
