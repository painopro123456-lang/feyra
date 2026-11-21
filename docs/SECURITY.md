# FEYRA security baseline

Security is a release requirement. This document distinguishes protections that are already present in code from work that must be completed and tested before FEYRA is distributed as a finished browser.

## Current protection

- Website permission requests are denied by default. This prevents silent grants of camera, microphone, geolocation, notifications, and other WebView2 permissions.
- WebView2 password autosave and general autofill are disabled.
- Private tabs use separate profile directories and are excluded from normal history/session persistence.
- FEYRA uses the Microsoft Edge WebView2 runtime for web content; its Chromium security updates depend on the installed runtime being kept current.
- The updater is being integrated, but release authenticity, signing, and end-to-end update validation are not yet complete.

## Required before a stable release

1. Implement a per-site permission UI with explicit allow/block decisions and a settings page to review/revoke them. Until then, web permissions remain denied.
2. Add malicious URL/phishing protection backed by a maintained, reputable threat-intelligence service or list. Define update cadence, privacy behavior, and offline failure behavior.
3. Handle TLS/certificate errors safely. Never provide a silent certificate bypass; explain the risk and keep invalid-certificate navigation blocked by default.
4. Review all navigation and external protocol handling. Only permit expected web schemes in the browser surface; require explicit confirmation before launching external applications.
5. Improve downloads: choose/confirm destination, warn on executable/script/archive types, prevent silent launch, and show the source URL. Do not claim malware scanning unless a real scanner/reputation integration exists.
6. Keep WebView2 runtime and FEYRA updater current. Verify update signatures/integrity and test upgrade, interrupted download, rollback/recovery, and data preservation.
7. Add dependency vulnerability scanning, secret scanning, least-privilege CI permissions, and signed release artifacts.
8. Test private-profile cleanup, profile separation, permission denial, malicious URL warnings, certificate failures, download behavior, and update tampering/recovery.

## Honest product claims

- A built-in blocker is not equivalent to antivirus.
- HTTPS does not prove a website is trustworthy; it encrypts the connection to the named site.
- No browser can guarantee that every phishing page or malicious download is detected.
- FEYRA must disclose when protection data is unavailable or stale rather than silently implying full protection.
