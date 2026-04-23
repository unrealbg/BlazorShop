const authSessionEventKey = "blazorshop.auth-session-event";

let dotNetReference = null;
let storageHandler = null;

export function subscribe(reference) {
    unsubscribe();

    dotNetReference = reference;
    storageHandler = async (event) => {
        if (!dotNetReference || event.key !== authSessionEventKey || !event.newValue) {
            return;
        }

        try {
            const payload = JSON.parse(event.newValue);
            if (!payload || !payload.type) {
                return;
            }

            await dotNetReference.invokeMethodAsync("HandleAuthSessionEventAsync", payload.type);
        }
        catch {
            // Ignore malformed payloads or tabs that are already tearing down.
        }
    };

    window.addEventListener("storage", storageHandler);
}

export function publish(type) {
    if (!type) {
        console.warn("Auth session event type is required.");
        return;
    }

    const payload = {
        type,
        stamp: Date.now(),
        nonce: `${Date.now()}-${Math.random()}`,
    };

    window.localStorage.setItem(authSessionEventKey, JSON.stringify(payload));
}

export function unsubscribe() {
    if (storageHandler) {
        window.removeEventListener("storage", storageHandler);
        storageHandler = null;
    }

    dotNetReference = null;
}