# FEYRA implementation status

This file tracks verified implementation. Roadmap ideas are not marked complete until implemented and tested.

## Foundation in repository
- [x] Windows WPF application project scaffold (.NET 8)
- [x] WebView2 package reference
- [x] Custom dark browser shell and address bar
- [x] Basic tab creation and closing
- [x] Basic navigation and search fallback
- [x] New-window requests open in a new tab
- [x] Local bookmark, history, and download-path persistence
- [x] Basic Ctrl+T, Ctrl+W, Ctrl+L, Ctrl+R, Ctrl+D, Ctrl+H, Ctrl+J shortcuts
- [x] Initial Windows CI build workflow

## Not yet implemented / not yet verified
- [ ] Run CI and fix all build/runtime issues
- [ ] Full download manager (progress, pause/resume, retry, open/show in folder)
- [ ] Full history and bookmark management (delete, folders, import/export)
- [ ] Private windows with isolated WebView2 profiles
- [ ] Settings UI and persisted preferences
- [ ] Session restore and recently closed tabs
- [ ] Tab pinning, groups, search, vertical tabs, split view
- [ ] Per-site permissions and zoom
- [ ] Find-in-page UI, reader mode, picture-in-picture
- [ ] Theme, density, accessibility, and shortcut customization
- [ ] Tab sleeping, diagnostics, crash recovery
- [ ] Installer, update flow, rollback, and signed releases

## Known preview limitations
- Private mode currently suppresses local history/bookmark persistence but does not yet create an isolated WebView2 profile. Do not treat it as a security boundary.
- The Settings entry is currently an explanatory placeholder.
- Download entries currently record paths only; this is not a complete download manager.
- History/bookmark views are basic searchable lists, not full management pages.
- The CI artifact is a published app folder, not yet a signed installer.
