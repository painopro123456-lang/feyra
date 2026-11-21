# FEYRA implementation status

Status is deliberately conservative: "implemented in code" does not mean independently QA-verified.

## Implemented in current source
- [x] Windows desktop WPF app scaffold targeting .NET 8 / Windows
- [x] WebView2 runtime initialization and real web navigation
- [x] Search-or-URL address bar, back, forward, reload
- [x] Multiple tabs, close buttons, duplicate, pin indicator, tab context menu
- [x] Open links/new windows in tabs
- [x] Keyboard shortcuts for tabs, address bar, history, downloads, bookmarks, find, zoom and navigation
- [x] Local settings, bookmarks, history and download metadata persistence
- [x] Searchable history, bookmark and download list windows
- [x] Configurable search provider, default zoom, startup session restore, vertical tabs toggle
- [x] Per-tab private WebView2 profile folders, private tabs excluded from saved history/session/bookmarks
- [x] Basic download progress/state metadata event handling
- [x] Find-in-page bar using window.find
- [x] Split-view prototype, picture-in-picture action, reader-style view action
- [x] Windows GitHub Actions build/publish workflow
- [x] Background WebView2 tab suspension/resume policy (reduces background activity; does not guarantee process memory is released)

## Partial / prototype — needs refinement and testing
- [ ] Tab pinning is currently a visual state only; pinned-tab ordering/close protection is not finished
- [ ] Tab groups are not implemented yet
- [ ] Tab mute action toggles media elements on the page, not WebView2's native audio mute state
- [ ] Split view is a prototype and needs lifecycle/layout testing
- [ ] Private profile cleanup is best-effort after tab close; crash cleanup and a dedicated private-window UX are not finished
- [ ] Downloads are tracked, but pause/resume/cancel/retry/open/show-in-folder and robust file destination UI are not finished
- [ ] Bookmarks lack folder editing, import/export and robust deletion UX
- [ ] History lacks date filters and granular clear-data controls
- [ ] Per-site permission management is not implemented; current setting is only a prompt policy placeholder
- [ ] Reader mode is a prototype DOM transformation, not a robust article extractor
- [ ] Picture-in-picture depends on the active site's video support
- [ ] Search suggestions, tab search, vertical tab polish, split-screen persistence and drag reorder need implementation
- [ ] Theme, compact mode and accessibility preferences need actual visual application
- [ ] Stronger tab sleeping that unloads inactive WebView2 instances is not implemented; current suspension retains page/process memory
- [ ] Memory/CPU diagnostics and measured RAM benchmarks need implementation
- [ ] Installer, update/install prompt, rollback and code signing are not implemented

## Validation
- [ ] Successful Windows CI build confirmed (latest changes have not yet been compiled/verified)
- [ ] Clean Windows 10 install and launch test
- [ ] Clean Windows 11 install and launch test
- [ ] Navigation, downloads, private profile isolation, session restore and crash recovery tested end-to-end

## Known release limitations
This is an actively developing preview, not a release-ready browser. Do not distribute it as a finished product until the validation checklist passes. The WebView2 runtime is required and must be included or installed by the eventual installer.
