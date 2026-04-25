(function () {
  const cartCookieName = "my-cart";
  const buttonSelector = "[data-storefront-add-to-cart]";
  const badgeSelector = "[data-storefront-cart-badge]";
  const cartRemoveSelector = "[data-storefront-cart-remove]";
  const cartClearSelector = "[data-storefront-cart-clear]";
  const cartQuantitySelector = "[data-storefront-cart-quantity]";
  const toastRegionSelector = "[data-storefront-toast-region]";
  const toastTemplateSelector = "[data-storefront-toast-template]";
  const cartChangedEventName = "blazorshop:cart-changed";
  const pendingToastStorageKey = "blazorshop:storefront:pending-toast";
  const cartLifetimeDays = 30;
  const badgePollIntervalMs = 1500;
  const buttonResetDelayMs = 1600;
  const toastDurationMs = 5000;
  const buttonResetTimers = new WeakMap();
  let lastCartCookie = null;
  let badgePollHandle = 0;

  function readCookie(name) {
    const encodedName = `${encodeURIComponent(name)}=`;
    const cookies = document.cookie.split(";");

    for (const cookie of cookies) {
      const trimmedCookie = cookie.trim();
      if (trimmedCookie.startsWith(encodedName)) {
        return decodeURIComponent(trimmedCookie.substring(encodedName.length));
      }
    }

    return null;
  }

  function writeCookie(name, value, days, path) {
    const expires = new Date(Date.now() + days * 24 * 60 * 60 * 1000).toUTCString();
    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; expires=${expires}; path=${path}`;
  }

  function removeCookie(name, path) {
    document.cookie = `${encodeURIComponent(name)}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=${path}`;
  }

  function parseNumber(value) {
    const parsed = Number.parseFloat(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  function parseInteger(value, fallback = 0) {
    const parsed = Number.parseInt(value, 10);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  function getItemProductId(item) {
    return String(item?.ProductId ?? item?.productId ?? "").trim();
  }

  function getItemVariantId(item) {
    const value = item?.VariantId ?? item?.variantId;
    return value == null ? "" : String(value).trim();
  }

  function getItemQuantity(item) {
    return parseInteger(item?.Quantity ?? item?.quantity, 0);
  }

  function setItemQuantity(item, quantity) {
    if (Object.prototype.hasOwnProperty.call(item, "Quantity") || !Object.prototype.hasOwnProperty.call(item, "quantity")) {
      item.Quantity = quantity;
      return;
    }

    item.quantity = quantity;
  }

  function parseCart() {
    const rawCart = readCookie(cartCookieName);
    if (!rawCart) {
      return [];
    }

    try {
      const parsed = JSON.parse(rawCart);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  function getCartCount(items) {
    return items.reduce((total, item) => total + getItemQuantity(item), 0);
  }

  function updateBadges(items) {
    const cartItems = items ?? parseCart();
    const cartCount = getCartCount(cartItems);

    document.querySelectorAll(badgeSelector).forEach((badge) => {
      badge.textContent = cartCount > 99 ? "99+" : String(cartCount);
      badge.hidden = cartCount <= 0;
      badge.classList.toggle("hidden", cartCount <= 0);
    });
  }

  function persistCart(items) {
    const normalizedItems = Array.isArray(items)
      ? items.filter((item) => getItemProductId(item) && getItemQuantity(item) > 0)
      : [];

    if (normalizedItems.length === 0) {
      removeCookie(cartCookieName, "/");
      lastCartCookie = null;
      updateBadges([]);
      document.dispatchEvent(new CustomEvent(cartChangedEventName, { detail: { count: 0 } }));
      return;
    }

    writeCookie(cartCookieName, JSON.stringify(normalizedItems), cartLifetimeDays, "/");
    lastCartCookie = readCookie(cartCookieName);
    updateBadges(normalizedItems);
    document.dispatchEvent(new CustomEvent(cartChangedEventName, { detail: { count: getCartCount(normalizedItems) } }));
  }

  function setFeedback(button, message, isError) {
    const feedbackSelector = button.dataset.feedbackTarget;
    if (!feedbackSelector) {
      return;
    }

    const feedbackElement = document.querySelector(feedbackSelector);
    if (!(feedbackElement instanceof HTMLElement)) {
      return;
    }

    feedbackElement.textContent = message;
    feedbackElement.classList.remove("text-emerald-700", "text-red-700");
    feedbackElement.classList.add(isError ? "text-red-700" : "text-emerald-700");
  }

  function flashButton(button) {
    const defaultLabel = button.dataset.defaultLabel || button.textContent.trim();
    const successLabel = button.dataset.successLabel || "Added";
    button.dataset.defaultLabel = defaultLabel;
    button.textContent = successLabel;

    const existingTimer = buttonResetTimers.get(button);
    if (existingTimer) {
      window.clearTimeout(existingTimer);
    }

    const timer = window.setTimeout(() => {
      button.textContent = button.dataset.defaultLabel || defaultLabel;
      buttonResetTimers.delete(button);
    }, buttonResetDelayMs);

    buttonResetTimers.set(button, timer);
  }

  function resolveToastTheme(level) {
    switch ((level || "info").toLowerCase()) {
      case "success":
        return { background: "rgba(20, 83, 45, 0.96)", accentBackground: "rgba(187, 247, 208, 0.18)", accentColor: "#dcfce7" };
      case "warning":
        return { background: "rgba(180, 83, 9, 0.96)", accentBackground: "rgba(253, 230, 138, 0.18)", accentColor: "#fef3c7" };
      case "error":
        return { background: "rgba(153, 27, 27, 0.96)", accentBackground: "rgba(254, 202, 202, 0.18)", accentColor: "#fee2e2" };
      default:
        return { background: "rgba(3, 105, 161, 0.96)", accentBackground: "rgba(186, 230, 253, 0.18)", accentColor: "#e0f2fe" };
    }
  }

  function resolveToastIcon(level) {
    switch ((level || "info").toLowerCase()) {
      case "success":
        return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5"><path d="m5 13 4 4L19 7" /></svg>';
      case "warning":
        return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5"><path d="M12 9v4" /><path d="M12 17h.01" /><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z" /></svg>';
      case "error":
        return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5"><circle cx="12" cy="12" r="10" /><path d="m15 9-6 6" /><path d="m9 9 6 6" /></svg>';
      default:
        return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5"><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></svg>';
    }
  }

  function showToast(level, heading, message, duration = toastDurationMs) {
    const region = document.querySelector(toastRegionSelector);
    const template = document.querySelector(toastTemplateSelector);
    if (!(region instanceof HTMLElement) || !(template instanceof HTMLTemplateElement)) {
      return;
    }

    const fragment = template.content.cloneNode(true);
    const toast = fragment.querySelector("[data-storefront-toast]");
    const accent = fragment.querySelector("[data-storefront-toast-accent]");
    const headingElement = fragment.querySelector("[data-storefront-toast-heading]");
    const messageElement = fragment.querySelector("[data-storefront-toast-message]");
    const closeButton = fragment.querySelector("[data-storefront-toast-close]");

    if (!(toast instanceof HTMLElement) || !(accent instanceof HTMLElement) || !(headingElement instanceof HTMLElement) || !(messageElement instanceof HTMLElement)) {
      return;
    }

    const theme = resolveToastTheme(level);
    toast.style.backgroundColor = theme.background;
    accent.style.backgroundColor = theme.accentBackground;
    accent.style.color = theme.accentColor;
    accent.innerHTML = resolveToastIcon(level);
    headingElement.textContent = heading || "Info";
    messageElement.textContent = message || "An event occurred.";

    const dismiss = () => {
      if (toast.dataset.dismissed === "true") {
        return;
      }

      toast.dataset.dismissed = "true";
      toast.style.opacity = "0";
      toast.style.transform = "translateY(-8px)";
      window.setTimeout(() => toast.remove(), 180);
    };

    if (closeButton instanceof HTMLButtonElement) {
      closeButton.addEventListener("click", dismiss);
    }

    region.appendChild(fragment);
    window.requestAnimationFrame(() => {
      toast.style.opacity = "1";
      toast.style.transform = "translateY(0)";
    });

    window.setTimeout(dismiss, Math.max(1500, parseInteger(duration, toastDurationMs)));
  }

  function queueToastForNextLoad(level, heading, message, duration = toastDurationMs) {
    try {
      window.sessionStorage.setItem(pendingToastStorageKey, JSON.stringify({ level, heading, message, duration }));
    } catch {
      // Ignore storage restrictions; the cart mutation itself already succeeded.
    }
  }

  function flushQueuedToast() {
    try {
      const raw = window.sessionStorage.getItem(pendingToastStorageKey);
      if (!raw) {
        return;
      }

      window.sessionStorage.removeItem(pendingToastStorageKey);
      const toast = JSON.parse(raw);
      if (!toast || !toast.message) {
        return;
      }

      showToast(toast.level, toast.heading, toast.message, toast.duration);
    } catch {
      window.sessionStorage.removeItem(pendingToastStorageKey);
    }
  }

  function formatCartLabel(productName, sizeValue) {
    const resolvedName = (productName || "product").trim() || "product";
    const resolvedSize = (sizeValue || "").trim();
    return resolvedSize ? `${resolvedName} (size ${resolvedSize})` : resolvedName;
  }

  function buildCartPayload(button) {
    const productId = (button.dataset.productId || "").trim();
    const productName = (button.dataset.productName || "Product").trim() || "Product";

    if (!productId) {
      return { error: "This product cannot be added right now." };
    }

    const payload = {
      ProductId: productId,
      Quantity: 1,
      UnitPrice: parseNumber(button.dataset.unitPrice)
    };

    const variantSelectSelector = button.dataset.variantSelect;
    if (variantSelectSelector) {
      const select = document.querySelector(variantSelectSelector);
      if (!(select instanceof HTMLSelectElement)) {
        return { error: "This product variant selector is unavailable right now." };
      }

      const selectedOption = select.selectedOptions[0];
      if (!selectedOption || !selectedOption.value) {
        return { error: "Select a size before adding to cart." };
      }

      payload.VariantId = selectedOption.value.trim();
      payload.SizeValue = selectedOption.dataset.sizeValue || selectedOption.textContent.trim();
      payload.UnitPrice = parseNumber(selectedOption.dataset.unitPrice) || payload.UnitPrice;
    }

    return { payload, productName };
  }

  function addToCart(button) {
    const result = buildCartPayload(button);
    if (result.error) {
      setFeedback(button, result.error, true);
      showToast("error", "Cart", result.error);
      return;
    }

    const cartItems = parseCart();
    const { payload, productName } = result;
    const existingItem = cartItems.find((item) => getItemProductId(item) === payload.ProductId && (getItemVariantId(item) || null) === (payload.VariantId || null));

    let feedbackMessage = "";
    let toastLevel = "success";

    if (existingItem) {
      setItemQuantity(existingItem, getItemQuantity(existingItem) + payload.Quantity);
      feedbackMessage = `Increased quantity of ${formatCartLabel(productName, payload.SizeValue)}`;
      toastLevel = "info";
    } else {
      cartItems.push(payload);
      feedbackMessage = `Product ${formatCartLabel(productName, payload.SizeValue)} added to cart`;
    }

    persistCart(cartItems);
    setFeedback(button, feedbackMessage, false);
    showToast(toastLevel, "Cart", feedbackMessage);
    flashButton(button);
  }

  function findCartLine(items, productId, variantId) {
    return items.find((item) => getItemProductId(item) === productId && getItemVariantId(item) === (variantId || ""));
  }

  function removeCartLine(button) {
    const productId = (button.dataset.productId || "").trim();
    const variantId = (button.dataset.variantId || "").trim();
    if (!productId) {
      showToast("error", "Cart", "This cart item could not be removed.");
      return;
    }

    const cartItems = parseCart();
    const nextItems = cartItems.filter((item) => !(getItemProductId(item) === productId && getItemVariantId(item) === variantId));
    if (nextItems.length === cartItems.length) {
      showToast("error", "Cart", "This cart item could not be removed.");
      return;
    }

    const productName = formatCartLabel(button.dataset.productName, button.dataset.sizeValue);
    persistCart(nextItems);
    queueToastForNextLoad("warning", "Cart", `Removed ${productName} from cart.`);
    window.location.reload();
  }

  function clearCart() {
    const cartItems = parseCart();
    if (cartItems.length === 0) {
      showToast("info", "Cart", "Your cart is already empty.");
      return;
    }

    persistCart([]);
    queueToastForNextLoad("info", "Cart", "Cart cleared.");
    window.location.reload();
  }

  function updateCartQuantity(input) {
    const productId = (input.dataset.productId || "").trim();
    const variantId = (input.dataset.variantId || "").trim();
    const productName = formatCartLabel(input.dataset.productName, input.dataset.sizeValue);
    const nextQuantity = parseInteger(input.value, Number.NaN);
    const currentQuantity = parseInteger(input.getAttribute("value"), 1);

    if (!productId) {
      showToast("error", "Cart", "This cart item could not be updated.");
      return;
    }

    if (!Number.isFinite(nextQuantity) || nextQuantity < 0) {
      input.value = String(currentQuantity);
      showToast("error", "Cart", "Enter a valid quantity.");
      return;
    }

    const cartItems = parseCart();
    const cartLine = findCartLine(cartItems, productId, variantId);
    if (!cartLine) {
      showToast("error", "Cart", "This cart item could not be updated.");
      return;
    }

    if (nextQuantity === currentQuantity) {
      return;
    }

    if (nextQuantity === 0) {
      const nextItems = cartItems.filter((item) => !(getItemProductId(item) === productId && getItemVariantId(item) === variantId));
      persistCart(nextItems);
      queueToastForNextLoad("warning", "Cart", `Removed ${productName} from cart.`);
      window.location.reload();
      return;
    }

    setItemQuantity(cartLine, nextQuantity);
    persistCart(cartItems);
    queueToastForNextLoad("info", "Cart", `Updated quantity of ${productName}.`);
    window.location.reload();
  }

  function handleClick(event) {
    const clearButton = event.target.closest(cartClearSelector);
    if (clearButton instanceof HTMLButtonElement) {
      event.preventDefault();
      clearCart();
      return;
    }

    const removeButton = event.target.closest(cartRemoveSelector);
    if (removeButton instanceof HTMLButtonElement) {
      event.preventDefault();
      removeCartLine(removeButton);
      return;
    }

    const button = event.target.closest(buttonSelector);
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }

    event.preventDefault();
    addToCart(button);
  }

  function handleChange(event) {
    const quantityInput = event.target;
    if (!(quantityInput instanceof HTMLInputElement) || !quantityInput.matches(cartQuantitySelector)) {
      return;
    }

    updateCartQuantity(quantityInput);
  }

  function startBadgePolling() {
    if (badgePollHandle) {
      return;
    }

    lastCartCookie = readCookie(cartCookieName);
    badgePollHandle = window.setInterval(() => {
      const currentCartCookie = readCookie(cartCookieName);
      if (currentCartCookie !== lastCartCookie) {
        lastCartCookie = currentCartCookie;
        updateBadges();
      }
    }, badgePollIntervalMs);
  }

  function initialize() {
    flushQueuedToast();
    updateBadges();
    startBadgePolling();
    document.addEventListener("click", handleClick);
    document.addEventListener("change", handleChange);
    document.addEventListener(cartChangedEventName, () => updateBadges());
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initialize, { once: true });
  } else {
    initialize();
  }
})();