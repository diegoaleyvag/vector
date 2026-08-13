// Small JS interop helpers for Vector, loaded as a classic script from index.html and invoked via
// IJSRuntime.InvokeAsync/InvokeVoidAsync by their plain (unprefixed) function names below.

/**
 * Triggers a client-side download of `text` as a file named `filename`, via a Blob and a
 * throwaway object URL. No server round-trip; the anchor is created, clicked, and discarded.
 */
function downloadFile(filename, text) {
    const blob = new Blob([text], { type: "text/markdown;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}

/**
 * Copies `text` to the clipboard, preferring the async Clipboard API and falling back to a
 * hidden textarea + execCommand for browsers/contexts where the Clipboard API is unavailable.
 */
async function copyToClipboard(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // Fall through to the legacy fallback below.
        }
    }

    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.select();
    let succeeded = false;
    try {
        succeeded = document.execCommand("copy");
    } catch {
        succeeded = false;
    }
    textarea.remove();
    return succeeded;
}

/** Returns true if the user has requested reduced motion at the OS/browser level. */
function prefersReducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}
