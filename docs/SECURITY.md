# FEYRA security baseline

Security is a release requirement. This document distinguishes protections that are already present in code from work that must be completed and tested before FEYRA is distributed as a finished browser.

## Current protection

- Website permission requests are denied by default. Camera, microphone, geolocation, notifications, and other WebView2 permissions remain unavailable until a real per-site permission manager exists.
- Top-level WebView2 navigation is restricted to HTTP and HTTPS, with `about:blank` as the only allowed `about:` page. Unsupported schemes are cancelled; pop-ups using unsupported schemes are blocked.
- WebView2 server-certificate errors are cancelled. FEYRA does not offer a certificate-error bypass.
- Downloads require an in-browser confirmation before continuing. Common executable and script extensions receive an additional warning, and the prompt explicitly states that FEYRA does not scan downloads for malware.
- WebView2 password autosave and general autofill are disabled.
- Private tabs use separate profile directories and are excluded from normal history/session persistence.
- FEYRA uses the Microsoft Edge WebView2 runtime for web content; its Chromium security updates depend on the installed runtime being kept current.
- The updater is being integrated, but release authenticity, signing, and end-to-end update validation are not yet complete.

## Required before a stable release

1. Implement a per-site permission UI with explicit allow/block decisions and a settings page to review/revoke them. Until then, web permissions remain denied.
2. Add malicious URL/phishing protection backed by a maintained, reputable threat-intelligence service or list. Define update cadence, privacy behavior, and offline failure behavior.
3. Add tests for the implemented TLS/certificate-error cancellation and maintain the no-bypass behavior.
4. Audit navigation and external protocol handling further. Current browser navigation is restricted to HTTP/HTTPS plus `about:blank`; external applications are not launched by FEYRA. Add a deliberate, explicit confirmation flow before supporting any external-app launch.
5. Improve downloads further: allow users to choose a destination, add archive-type warnings, and test that files are never silently launched. The current confirmation shows the source URL and warns on executable/script extensions, but does not scan files for malware.
6. Keep WebView2 runtime and FEYRA updater current. Verify update signatures/integrity and test upgrade, interrupted download, rollback/recovery, and data preservation.
7. Add dependency vulnerability scanning, secret scanning, least-privilege CI permissions, and signed release artifacts.
8. Test private-profile cleanup, profile separation, permission denial, malicious URL warnings, certificate failures, download behavior, and update tampering/recovery.

## Honest product claims

- A built-in blocker is not equivalent to antivirus.
- HTTPS does not prove a website is trustworthy; it encrypts the connection to the named site.
- No browser can guarantee that every phishing page or malicious download is detected.
- FEYRA must disclose when protection data is unavailable or stale rather than silently implying full protection.
