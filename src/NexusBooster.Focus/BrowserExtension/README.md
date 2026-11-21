# Nexus Focus Feed Blocker

This optional Manifest V3 extension is a user-installed, local browser helper. It works in Chromium-based browsers that support Manifest V3 (for example, Chrome, Edge, and Brave); it does not install into every browser or intercept traffic outside the browser.

## Install

1. Review `manifest.json`, `rules.json`, and `content.js`.
2. Open the browser's extensions page, enable its developer mode, and choose **Load unpacked**.
3. Select this `BrowserExtension` folder.
4. To remove it, disable or remove the extension from that same browser page.

The extension asks for access only to YouTube, Instagram, Facebook, and Reddit. Its network rules block selected `/shorts`, `/reels`, and `/explore` pages on those hosts; regular YouTube `/watch` videos and search remain available. On YouTube it removes the home feed and watch-page recommendations. A high-rate scroll pattern shows a brief, dismissible reminder; it does not minimize, lock, or block the browser. Review or edit the host list before loading it if any of those sites are important to your work.

## Limits

- Rules apply only to the listed sites in browsers where this extension is installed. There is no system-wide proxy, certificate installation, HTTPS interception, hosts-file edit, process termination, browser minimization, or timed browser lock.
- Instagram/Facebook feed removal depends on their changing page markup. The extension does not block their entire domains.
- It does not collect browsing history, transmit page data, contact a server, or report Shorts attempts back to Nexus Safe Tweaks.
- Remove the extension in the browser's extension manager to reverse its changes.
