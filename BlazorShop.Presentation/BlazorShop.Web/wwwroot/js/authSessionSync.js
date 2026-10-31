const eventKey = "blazorshop.auth-session-event";
const stateKey = "blazorshop.auth-session-state";
const epochKey = "blazorshop.auth-session-epoch";
export const lockName = "blazorshop.authentication";
let reference;
let storageHandler;

function state() {
    const value = localStorage.getItem(stateKey);
    return value ? JSON.parse(value) : { epoch: "initial", status: "unknown" };
}
export function currentEpoch() { return state().epoch; }
export function canRefresh() { return !["signed-out", "authenticating"].includes(state().status); }
export function isCurrent(epoch) { return state().epoch === epoch; }
export function isSignedIn(epoch) { return isCurrent(epoch) && state().status === "signed-in"; }
export function hasCurrentEpoch() { return sessionStorage.getItem(epochKey) === currentEpoch(); }
export function beginRefresh(epoch) {
    const current = state();
    if (!isCurrent(epoch) || !canRefresh() || current.pending) return false;
    localStorage.setItem(stateKey, JSON.stringify({ ...current, pending: true }));
    return true;
}
export function commitToken(epoch, token) {
    const current = state();
    if (!isCurrent(epoch) || current.status === "signed-out" || !token) return false;
    // Avoid same-document async gaps between the epoch check and token storage.
    // Completed cross-tab credential operations are serialized by the Web Lock;
    // intent notifications are advisory and never business authorization.
    sessionStorage.setItem('token', token);
    sessionStorage.setItem(epochKey, epoch);
    localStorage.setItem(stateKey, JSON.stringify({ epoch, status: current.status }));
    return true;
}
export function invalidateCurrent(epoch) {
    if (!isCurrent(epoch)) return false;
    publish('signed-out');
    return true;
}
export function publishSignedIn(epoch) {
    if (!isCurrent(epoch) || state().status === 'signed-out') return false;
    publish('signed-in');
    return true;
}

export function beginIntent(operation) {
    if (operation === "refresh") return currentEpoch();
    const next = { epoch: crypto.randomUUID(), status: operation === "logout" ? "signed-out" : "authenticating" };
    sessionStorage.removeItem('token');
    localStorage.setItem(stateKey, JSON.stringify(next));
    notify(operation === "logout" ? "signed-out" : "session-changing", next.epoch);
    return next.epoch;
}

// The callback promise holds the Web Lock through HTTP completion, cookie processing,
// and local state publication. A timeout only cancels a queued acquisition, never an
// already-running credential POST. No localStorage/BroadcastChannel mutex fallback.
export async function runExclusive(dotnet, operationId, operation, intentEpoch, timeoutMs = 15000) {
    if (!navigator.locks?.request) return "unavailable";
    let acquiredEpoch;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
        return await navigator.locks.request(lockName, { mode: "exclusive", signal: controller.signal }, async () => {
            clearTimeout(timer);
            if (operation !== "refresh" && !isCurrent(intentEpoch)) return "superseded";
            if (operation === "refresh" && state().pending) {
                // A prior document died while a credential POST was unresolved. Its
                // cookie may already be consumed: never attempt automatic recovery.
                publish('signed-out');
                return 'superseded';
            }
            if (operation === "refresh" && !canRefresh()) return "superseded";
            acquiredEpoch = currentEpoch();
            await dotnet.invokeMethodAsync("ExecuteUnderLockAsync", operationId, acquiredEpoch);
            return "completed";
        });
    } catch {
        if (operation === "refresh" && acquiredEpoch && isCurrent(acquiredEpoch)) publish("signed-out");
        return "unavailable";
    }
    finally { clearTimeout(timer); }
}

function notify(type, epoch) {
    // Notifications carry no JWT, refresh credential, cookie, or other secret.
    localStorage.setItem(eventKey, JSON.stringify({ type, epoch, nonce: crypto.randomUUID() }));
}
export function publish(type) {
    const current = state();
    if (type === "signed-out" && current.status !== "signed-out") {
        beginIntent("logout");
        return;
    }
    if (type === "signed-in") {
        localStorage.setItem(stateKey, JSON.stringify({ epoch: current.epoch, status: "signed-in" }));
    }
    notify(type, current.epoch);
}

export function subscribe(dotnet) {
    unsubscribe();
    reference = dotnet;
    storageHandler = async event => {
        if (event.key !== eventKey || !event.newValue || event.newValue !== localStorage.getItem(eventKey)) return;
        try {
            const payload = JSON.parse(event.newValue);
            if (!payload || payload.epoch !== currentEpoch()) return;
            await reference?.invokeMethodAsync("HandleAuthSessionEventAsync", payload.type);
        } catch { /* Ignore malformed notifications and disposed tabs. */ }
    };
    window.addEventListener("storage", storageHandler);
}
export function unsubscribe() {
    if (storageHandler) window.removeEventListener("storage", storageHandler);
    reference = storageHandler = null;
}
