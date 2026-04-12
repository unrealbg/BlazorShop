const loader = document.querySelector(".loader_main");
const loaderText = loader?.querySelector(".loading-progress-text");
const errorUi = document.getElementById("blazor-error-ui");
let errorUiWired = false;

function setLoaderText(text) {
  if (!loaderText) {
    return;
  }

  const safeValue = typeof text === "string" ? text : "Loading";
  loaderText.style.setProperty("--blazor-load-percentage-text", `"${safeValue}"`);
}

function setLoaderProgress(value) {
  if (typeof value !== "number" || Number.isNaN(value)) {
    setLoaderText("Loading");
    return;
  }

  const bounded = Math.min(100, Math.max(0, Math.round(value)));
  setLoaderText(`${bounded}%`);
}

function showLoader() {
  loader?.classList.remove("hidden");
}

function hideLoader() {
  loader?.classList.add("hidden");
}

function wireErrorUi() {
  if (!errorUi || errorUiWired) {
    return;
  }

  const reloadLink = errorUi.querySelector(".reload");
  const dismissButton = errorUi.querySelector(".dismiss");

  reloadLink?.addEventListener("click", (event) => {
    event.preventDefault();
    window.location.reload();
  });

  dismissButton?.addEventListener("click", () => {
    errorUi.style.display = "none";
  });

  errorUiWired = true;
}

function init() {
  wireErrorUi();
  window.addEventListener("load", () => {
    setLoaderText("Ready");
    setTimeout(hideLoader, 150);
  });
}

window.blz = Object.freeze({
  showLoader,
  hideLoader,
  setLoaderText,
  setLoaderProgress,
  wireErrorUi,
});

init();
