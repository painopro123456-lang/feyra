# FEYRA

**A fast, thoughtful Windows browser.**

FEYRA is a from-scratch browser project for Windows 10 and Windows 11. The goal is a genuinely useful daily browser with a distinctive, premium interface and practical quality-of-life features—not a mockup full of non-functional controls.

## Product principles

- **Fast by default:** low idle overhead, quick startup, efficient tab handling.
- **Useful details:** sensible defaults, keyboard shortcuts, clear feedback, and easy recovery from mistakes.
- **Honest UI:** every visible control must work or be clearly marked as planned.
- **Native Windows feel:** polished window behavior, accessibility, scaling, and system integration.
- **Privacy-respecting:** transparent controls; no hidden telemetry or surprise data collection.
- **Reliable releases:** automated builds, smoke tests, versioned installers, and verified update packages.

## Planned platform and foundation

- Windows 10 and Windows 11
- C# / .NET 8 with WPF for the native Windows application shell
- Microsoft Edge WebView2 for standards-based web rendering
- WebView2 runtime availability and installation handled explicitly by the installer

FEYRA is building its own browser application and experience from scratch. It is not attempting to write a new HTML/CSS/JavaScript rendering engine.

## Quality-of-life roadmap

### Everyday browsing
- Tabs: create, close, duplicate, pin, mute, restore closed tab, and drag to reorder
- Tab search and a clear visual indicator for audio-playing tabs
- Address/search bar with search suggestions, URL detection, and paste-and-go
- Back, forward, reload, stop, home, and keyboard navigation
- Configurable default search engine
- Bookmarks bar, folders, quick add, and bookmark search
- Browsing history with search, date grouping, and selective deletion
- Download manager with progress, pause/resume where supported, open, show in folder, retry, and clear completed
- New-tab page with configurable shortcuts and a clean search field

### Productivity
- Split view / side-by-side tabs
- Vertical tabs option
- Tab groups with names and colors
- Recently closed tabs and session restore
- Startup behavior: new tab, previous session, or selected pages
- Reader-friendly page mode where supported
- Picture-in-picture for supported video
- Find in page
- Print and save page
- Web-app shortcuts / installable sites where supported

### Comfort and accessibility
- Dark, light, and system themes
- Adjustable UI density and text scaling
- Optional compact mode
- Customizable keyboard shortcuts
- Clear focus states and full keyboard navigation
- Reduced-motion option
- Screen-reader labels and high-contrast support
- Per-site zoom and permissions

### Personalization and built-in media

- Full browser-chrome color customization: accent, surface, background, text, and tab colors
- Preset themes (dark, light, system) plus saved custom themes
- Theme preview and one-click reset; keep contrast and accessibility readable
- Built-in FEYRA Music panel with song search, artwork, queue, playback controls, shuffle, and repeat
- Stream music without requiring users to download song files
- Music playback must use a legitimate licensed streaming provider or a clearly labeled legal free-music catalog; do not scrape or bypass another service's ads or access controls
- Provider availability, account requirements, regional support, and ad-free playback depend on the selected provider

### Ad and tracker blocking

- User-toggleable built-in ad blocker, enabled by default
- Easy global on/off control and per-site allowlist
- Filter-list updates with clear last-updated status
- Blocking statistics and troubleshooting controls
- Block common network ads and trackers where technically possible; explain that first-party and server-delivered ads cannot always be separated from site content

### Privacy and control
- Private windows with separate session state
- Clear browsing data by time range and data type
- Per-site camera, microphone, location, notifications, and pop-up permissions
- Cookie and tracking controls exposed with clear explanations
- Do Not Track setting where supported
- Password-manager integration only with explicit user consent; never store credentials in plain text
- No account requirement for basic browsing

### Performance and reliability
- Lazy background tabs and configurable tab sleeping
- Resource-conscious startup and idle behavior
- Hardware acceleration controls with safe fallback
- Crash recovery and restore previous session
- Clear diagnostics page and copyable error details
- Safe handling of failed downloads and navigation errors
- Update checks with an explicit **Install update** action
- Rollback-safe update process and verified installer signatures when signing is configured

## Release quality gates

A feature is not complete until it has:
1. A working implementation—not just a visual control.
2. A clear empty, loading, success, and error state where applicable.
3. Keyboard and mouse support where appropriate.
4. Tests for important behavior.
5. A Windows build that launches and passes smoke checks.

## Development status

The repository is at the product-definition stage. No feature should be described as implemented until its code is present and tested.
