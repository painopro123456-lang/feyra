# FEYRA Product & UX Specification

## Experience direction

FEYRA should feel calm, premium, fast, and deliberate. Use a deep graphite foundation, restrained accent color, crisp typography, subtle borders, carefully layered surfaces, and purposeful motion. Avoid excessive blur, neon overload, oversized empty areas, and visual noise.

The browser chrome must prioritize the webpage. Tabs and navigation should remain compact, readable, and easy to reach. Animations should be short and respect Windows reduced-motion preferences.

## Core layout

1. **Top chrome:** window controls, tab strip, new-tab action, and optional tab search.
2. **Navigation row:** back, forward, reload/stop, address/search field, bookmark action, downloads, profile/privacy entry, and main menu.
3. **Optional bookmarks row:** user-toggleable.
4. **Web content:** receives the majority of the window.
5. **Status surfaces:** lightweight download progress, permission prompts, find bar, and notifications that do not obscure content unnecessarily.

## Essential keyboard shortcuts

| Action | Shortcut |
|---|---|
| New tab | Ctrl+T |
| Close tab | Ctrl+W |
| Reopen closed tab | Ctrl+Shift+T |
| Focus address bar | Ctrl+L |
| Reload | Ctrl+R |
| Force reload | Ctrl+Shift+R |
| Find in page | Ctrl+F |
| Bookmark page | Ctrl+D |
| History | Ctrl+H |
| Downloads | Ctrl+J |
| New private window | Ctrl+Shift+N |
| Zoom in/out/reset | Ctrl+Plus / Ctrl+Minus / Ctrl+0 |

Shortcuts must not steal keystrokes intended for web content when the browser does not own the relevant command.

## Quality-of-life behavior details

- Closing the final tab should open a fresh new tab rather than leave a dead window.
- Reopening a closed tab should restore its URL and, where safe, its prior navigation state.
- Middle-click should open links and tabs in a new tab and close tabs where users expect.
- Ctrl+click should open a link in a background tab.
- Pasting a URL into the address bar should make navigation one action.
- Pressing Escape in the address bar should restore the current page URL.
- Downloads should show progress and explain failures; completed items should remain available until cleared.
- Permission prompts should identify the requesting site and offer remembered per-site choices.
- Restoring a session must not silently restore private windows or private session data.
- A crashed tab should offer reload and recovery without taking down the whole browser.
- Tab audio mute state should be available from the tab itself.
- Settings should be searchable and changes should apply immediately when safe.

## Suggested implementation milestones

### M0 — Foundation
- WinUI 3 application shell
- WebView2 initialization and runtime detection
- Navigation and address/search bar
- Basic tab lifecycle
- App settings persistence
- CI build and launch smoke test

### M1 — Daily-driver essentials
- Bookmarks, history, downloads
- Keyboard shortcuts
- New-tab page
- Restore closed tabs and session restore
- Permission UI and per-site settings

### M2 — Power-user QoL
- Tab search, pinning, groups, vertical tabs
- Split view
- Picture-in-picture and reader-friendly mode
- Per-site zoom and configurable shortcuts

### M3 — Polish and release readiness
- Accessibility and UI scaling audit
- Startup and memory profiling
- Crash recovery and diagnostics
- Update install flow, rollback, and release verification
- Installer and clean-machine testing

## Non-goals for the first release

- Writing a proprietary rendering engine
- Sync/account infrastructure before local browsing is reliable
- Built-in VPN claims or fake security guarantees
- Features that require invasive background services
