export function setItem(key, value) {
    if (!key || !value) {
        console.warn("Both key and value are required to write session storage.");
        return;
    }

    window.sessionStorage.setItem(key, value);
}

export function getItem(key) {
    if (!key) {
        console.warn("Storage key is required to read session storage.");
        return null;
    }

    return window.sessionStorage.getItem(key);
}

export function removeItem(key) {
    if (!key) {
        console.warn("Storage key is required to remove session storage.");
        return;
    }

    window.sessionStorage.removeItem(key);
}