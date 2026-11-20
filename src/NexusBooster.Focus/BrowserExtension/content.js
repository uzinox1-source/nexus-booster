(() => {
  "use strict";

  const youtube = location.hostname === "youtube.com" || location.hostname.endsWith(".youtube.com");
  const instagram = location.hostname === "instagram.com" || location.hostname.endsWith(".instagram.com");
  const facebook = location.hostname === "facebook.com" || location.hostname.endsWith(".facebook.com");
  const scrollTimes = [];
  let lastNudge = 0;

  function removeYouTubeFeeds() {
    const path = location.pathname;
    const onHome = path === "/" || path === "/feed/home";

    if (onHome) {
      document.querySelectorAll("ytd-rich-grid-renderer, ytd-reel-shelf-renderer, ytd-rich-section-renderer")
        .forEach((element) => element.remove());
    }

    if (path === "/watch") {
      document.querySelectorAll(
        "#related, ytd-watch-next-secondary-results-renderer, ytd-reel-shelf-renderer"
      ).forEach((element) => element.remove());
    }

    document.querySelectorAll(
      "ytd-reel-shelf-renderer, ytd-reel-item-renderer, ytd-shorts, ytd-rich-shelf-renderer[is-shorts]"
    ).forEach((element) => element.remove());
  }

  function removeSocialFeedLinks() {
    if (instagram) {
      document.querySelectorAll('a[href^="/reels/"], a[href^="/explore/"]')
        .forEach((link) => link.closest("article, div[role='presentation']")?.remove());
    }

    if (facebook) {
      document.querySelectorAll('a[href*="/reel/"]')
        .forEach((link) => link.closest('[role="article"]')?.remove());
    }
  }

  const clean = () => {
    if (youtube) {
      removeYouTubeFeeds();
    } else {
      removeSocialFeedLinks();
    }
  };

  function showScrollNudge() {
    if (document.getElementById("__nexus-focus-scroll-nudge")) {
      return;
    }

    const host = document.createElement("div");
    host.id = "__nexus-focus-scroll-nudge";
    const shadow = host.attachShadow({ mode: "closed" });
    const style = document.createElement("style");
    style.textContent = `
      :host { all: initial; }
      aside {
        position: fixed; z-index: 2147483647; right: 18px; bottom: 18px; max-width: 310px;
        padding: 14px 16px; border: 1px solid #485164; border-radius: 12px;
        background: #171c27; color: #f3f4f8; box-shadow: 0 8px 32px #0009;
        font: 13px/1.45 system-ui, sans-serif;
      }
      button {
        margin-top: 10px; padding: 6px 10px; border: 0; border-radius: 7px;
        background: #43e7c3; color: #10131b; font: 600 12px system-ui, sans-serif; cursor: pointer;
      }`;
    const notice = document.createElement("aside");
    const message = document.createElement("div");
    message.textContent = "Rapid scrolling detected. Pause for a moment, or dismiss this reminder.";
    const dismiss = document.createElement("button");
    dismiss.type = "button";
    dismiss.textContent = "Dismiss";
    dismiss.addEventListener("click", () => host.remove());
    notice.append(message, dismiss);
    shadow.append(style, notice);
    document.documentElement.append(host);
    window.setTimeout(() => host.remove(), 15000);
  }

  function detectScrollLoop(event) {
    if (event.type === "wheel" && Math.abs(event.deltaY) < 20) {
      return;
    }

    const now = Date.now();
    while (scrollTimes.length > 0 && now - scrollTimes[0] > 10000) {
      scrollTimes.shift();
    }

    scrollTimes.push(now);
    if (scrollTimes.length >= 18 && now - lastNudge > 30000) {
      lastNudge = now;
      showScrollNudge();
      scrollTimes.length = 0;
    }
  }

  clean();
  new MutationObserver(clean).observe(document.documentElement, { childList: true, subtree: true });

  document.addEventListener("yt-navigate-finish", clean);
  window.addEventListener("popstate", clean);
  document.addEventListener("wheel", detectScrollLoop, { passive: true });
  document.addEventListener("touchmove", detectScrollLoop, { passive: true });
})();
