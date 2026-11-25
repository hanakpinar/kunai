// PublicProfile moves between its tabs on ArrowLeft, ArrowRight, Home and End. Blazor cannot cancel the browser's own
// action for some keys only, so this listener does: otherwise Home and End also scroll the page to its top or bottom,
// away from the tabs. Keys with a modifier (Alt+ArrowLeft goes back) are left to the browser, as PublicProfile does.
const tabKeys = new Set(["ArrowLeft", "ArrowRight", "Home", "End"]);
const guarded = new WeakSet();

export function preventTabKeyScrolling(tablist) {
    if (!tablist || guarded.has(tablist)) {
        return;
    }

    guarded.add(tablist);
    // The listener goes with the element: a new profile, or leaving the page, renders a new tab list.
    tablist.addEventListener("keydown", event => {
        if (tabKeys.has(event.key) && !event.altKey && !event.ctrlKey && !event.metaKey && !event.shiftKey) {
            event.preventDefault();
        }
    });
}
