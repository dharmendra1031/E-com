(() => {
    "use strict";
    const root = document.querySelector("[data-store-search]");
    if (!root) return;
    const results = root.querySelector("[data-search-results]");
    const message = document.getElementById("searchPageMessage");
    const ar = document.documentElement.lang === "ar";
    let request;
    async function load(url, push = true) {
        request?.abort();
        const active = new AbortController();
        request = active;
        const target = new URL(url, location.origin);
        target.searchParams.delete("partial");
        const endpoint = new URL(target);
        endpoint.searchParams.set("partial", "true");
        results.setAttribute("aria-busy", "true");
        message.textContent = ar ? "جارٍ تحميل المنتجات…" : "Loading products…";
        try {
            const response = await fetch(endpoint, { signal: active.signal, headers: { Accept: "text/html" } });
            if (!response.ok || response.redirected) throw new Error();
            const html = await response.text();
            if (active.signal.aborted) return;
            results.innerHTML = html;
            if (push && target.href !== location.href) history.pushState(null, "", target);
            message.textContent = "";
        } catch {
            if (!active.signal.aborted) message.textContent = ar ? "تعذر تحميل المنتجات. حاول مرة أخرى." : "Unable to load products. Try again.";
        } finally {
            if (request === active) results.removeAttribute("aria-busy");
        }
    }
    results.addEventListener("submit", event => {
        const form = event.target;
        if (!form.matches("[data-search-filter]")) return;
        event.preventDefault();
        const url = new URL(form.action);
        for (const [key, value] of new FormData(form)) url.searchParams.set(key, value);
        load(url);
    });
    results.addEventListener("click", event => {
        const link = event.target.closest("[data-search-page]");
        if (!link || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
        event.preventDefault();
        load(link.href);
    });
    window.addEventListener("popstate", () => load(location.href, false));
})();
