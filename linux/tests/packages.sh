#!/usr/bin/env bash
set -euo pipefail
export XDG_CURRENT_DESKTOP=GNOME
openbox >"${XDG_RUNTIME_DIR}/openbox-packages.log" 2>&1 &
printf '%s' 'muse-ci-keyring-password' | gnome-keyring-daemon --unlock --components=secrets

check_launch() {
    local executable="$1"
    local label="$2"
    export XDG_CONFIG_HOME
    XDG_CONFIG_HOME="$(mktemp -d)"
    APPIMAGE_EXTRACT_AND_RUN=1 "$executable" >"${XDG_RUNTIME_DIR}/${label}.log" 2>&1 &
    local application_pid=$!
    local window_id=""
    for attempt in {1..40}; do
        window_id="$(xdotool search --onlyvisible --name 'Muse Desktop' 2>/dev/null | head -n 1 || true)"
        if [[ -n "$window_id" ]]; then break; fi
        if ! kill -0 "$application_pid" 2>/dev/null; then
            cat "${XDG_RUNTIME_DIR}/${label}.log"
            return 1
        fi
        sleep 0.5
    done
    if [[ -z "$window_id" ]]; then
        cat "${XDG_RUNTIME_DIR}/${label}.log"
        echo "FAIL ${label}: no application window"
        return 1
    fi
    local browser_pid
    browser_pid="$(xdotool getwindowpid "$window_id")"
    APPIMAGE_EXTRACT_AND_RUN=1 timeout 15 "$executable"
    test "$(xdotool search --onlyvisible --pid "$browser_pid" | wc -l)" -eq 1
    xdotool windowactivate --sync "$window_id"
    xdotool key alt+F4
    for attempt in {1..40}; do
        if ! kill -0 "$application_pid" 2>/dev/null; then break; fi
        sleep 0.25
    done
    if kill -0 "$application_pid" 2>/dev/null; then
        kill "$application_pid"
        echo "FAIL ${label}: closing without a tray host did not exit"
        return 1
    fi
    wait "$application_pid"
    test -d "${XDG_CONFIG_HOME}/MuseDesktop"
    test -s "${XDG_CONFIG_HOME}/MuseDesktop/window_state.json"
    echo "PASS ${label} launch, single instance, close, fixed user data path"
}

check_launch /opt/Muse\ Desktop/muse-desktop deb
appimage_path="$(realpath ../release/linux/*.AppImage)"
chmod +x "$appimage_path"
check_launch "$appimage_path" appimage
