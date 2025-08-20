let handler = null;
let lastCart = null;

function readCookie(name) {
  const nameEQ = encodeURIComponent(name) + '=';
  const ca = document.cookie.split(';');
  for (let i = 0; i < ca.length; i++) {
    let c = ca[i].trim();
    if (c.indexOf(nameEQ) === 0) return decodeURIComponent(c.substring(nameEQ.length));
  }
  return null;
}

export function getCartCookie() {
  return readCookie('my-cart');
}

export function subscribeCartChanges(dotnet) {
  if (handler) return;
  lastCart = readCookie('my-cart');
  handler = setInterval(() => {
    const cur = readCookie('my-cart');
    if (cur !== lastCart) {
      lastCart = cur;
      try { dotnet.invokeMethodAsync('RefreshCartCount'); } catch {}
    }
  }, 800);
}

export function unsubscribeCartChanges() {
  if (handler) {
    clearInterval(handler);
    handler = null;
  }
}

export function closeDetails(detailsEl) {
  try {
    if (detailsEl && detailsEl.removeAttribute) {
      detailsEl.removeAttribute('open');
    }
  } catch {}
}
