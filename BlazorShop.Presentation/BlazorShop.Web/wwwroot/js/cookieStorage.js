export function setCookie(name, value, days = 0, path = "/") {
    if (!name || !value) {
        console.warn("Both name and value are required to set a cookie.");
        return;
    }

    let expires = "";
    if (days > 0) {
        const date = new Date();
        date.setTime(date.getTime() + days * 24 * 60 * 60 * 1000);
        expires = "; expires=" + date.toUTCString();
    }

    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}${expires}; path=${path}`;
}

export function getCookie(name) {
    if (!name) {
        console.warn("Cookie name is required to get a cookie.");
        return null;
    }

    const nameEQ = encodeURIComponent(name) + "=";
    const cookies = document.cookie.split(';');

    for (const cookie of cookies) {
        const trimmedCookie = cookie.trim();
        if (trimmedCookie.startsWith(nameEQ)) {
            return decodeURIComponent(trimmedCookie.substring(nameEQ.length));
        }
    }

    return null;
}

export function removeCookie(name, path = "/") {
    if (!name) {
        console.warn("Cookie name is required to remove a cookie.");
        return;
    }

    document.cookie = `${encodeURIComponent(name)}=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; path=${path}`;
}