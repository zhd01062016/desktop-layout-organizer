use pecofence_platform::{HWND, hotkey};
use std::path::PathBuf;

pub(super) const SCREENSHOT_ID: i32 = 71;
pub(super) const PIN_ID: i32 = 73;
const DEFAULT_CAPTURE_KEYS: [u32; 2] = [0x70, 0x71];

fn capture_key_file() -> Option<PathBuf> {
    Some(
        std::env::current_exe()
            .ok()?
            .parent()?
            .join("config")
            .join("capture-keys.json"),
    )
}

fn valid_capture_keys(keys: [u32; 2]) -> bool {
    keys[0] != keys[1]
        && keys.iter().all(|&k| {
            k >> 8 <= 7
                && matches!(k & 255, 0x30..=0x39 | 0x41..=0x5a | 0x70..=0x87)
                && (k >> 8 != 0 || k & 255 >= 0x70)
        })
}

pub(super) fn capture_keys() -> [u32; 2] {
    capture_key_file()
        .and_then(|p| std::fs::read(p).ok())
        .and_then(|data| serde_json::from_slice(&data).ok())
        .filter(|keys| valid_capture_keys(*keys))
        .unwrap_or(DEFAULT_CAPTURE_KEYS)
}

pub(super) fn bind_capture_keys(hwnd: HWND, keys: [u32; 2]) -> bool {
    let mut ok = true;
    for (id, key) in [SCREENSHOT_ID, PIN_ID].into_iter().zip(keys) {
        ok &= hotkey::register(hwnd, id, (key >> 8) | hotkey::mods::NOREPEAT, key & 255);
    }
    ok
}

pub(super) fn change_capture_keys(hwnd: HWND, keys: [u32; 2]) -> i32 {
    if !valid_capture_keys(keys) {
        return 0;
    }
    let Some(path) = capture_key_file() else {
        return 0;
    };
    let previous = capture_keys();
    for id in [SCREENSHOT_ID, PIN_ID] {
        hotkey::unregister(hwnd, id);
    }
    if bind_capture_keys(hwnd, keys) {
        let tmp = path.with_extension("tmp");
        if std::fs::write(&tmp, format!("[{},{}]", keys[0], keys[1])).is_ok()
            && std::fs::rename(tmp, path).is_ok()
        {
            return 1;
        }
    }
    for id in [SCREENSHOT_ID, PIN_ID] {
        hotkey::unregister(hwnd, id);
    }
    bind_capture_keys(hwnd, previous);
    0
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn default_keys_and_modifier_rules_match_the_settings_dialog() {
        assert!(valid_capture_keys(DEFAULT_CAPTURE_KEYS));
        assert!(!valid_capture_keys([0x70, 0x70]));
        assert!(!valid_capture_keys([0x41, 0x71]));
        assert!(valid_capture_keys([0x241, 0x71]));
        assert!(!valid_capture_keys([0x870, 0x71]));
    }
}
