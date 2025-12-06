const charts = new Map();

function ensureChartJs() {
  if (typeof window === "undefined") {
    return null;
  }

  if (!window.Chart) {
    console.warn("Chart.js is not loaded.");
    return null;
  }

  return window.Chart;
}

export function downloadFile(fileName, content, contentType) {
  try {
    const safeContent = content ?? "";
    const blob = new Blob([safeContent], {
      type: contentType || "text/plain;charset=utf-8",
    });

    if (window.navigator && typeof window.navigator.msSaveOrOpenBlob === "function") {
      window.navigator.msSaveOrOpenBlob(blob, fileName ?? "download.txt");
      return;
    }

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName ?? "download.txt";
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  } catch (error) {
    console.error("downloadFile failed", error);
  }
}

export function renderLineChart(canvasId, labels, values, datasetOptions) {
  const ChartCtor = ensureChartJs();
  if (!ChartCtor) {
    return;
  }

  const canvas = document.getElementById(canvasId);
  if (!canvas) {
    console.warn(`Canvas with id '${canvasId}' was not found.`);
    return;
  }

  const ctx = canvas.getContext("2d");
  const defaults = {
    label: "Series",
    color: "#10b981",
    backgroundColor: "rgba(16,185,129,0.15)",
    tension: 0.3,
    fill: true,
    pointRadius: 2,
    borderWidth: 2,
  };

  const options = Object.assign({}, defaults, datasetOptions || {});
  disposeChart(canvasId);

  const chart = new ChartCtor(ctx, {
    type: options.chartType || "line",
    data: {
      labels: Array.isArray(labels) ? labels : [],
      datasets: [
        {
          label: options.label,
          data: Array.isArray(values) ? values : [],
          borderColor: options.color,
          backgroundColor: options.backgroundColor,
          tension: options.tension,
          fill: options.fill,
          pointRadius: options.pointRadius,
          borderWidth: options.borderWidth,
        },
      ],
    },
    options: {
      plugins: { legend: { display: false } },
      scales: { y: { beginAtZero: true } },
      responsive: true,
      maintainAspectRatio: false,
    },
  });

  charts.set(canvasId, chart);
}

export function disposeChart(canvasId) {
  if (!charts.has(canvasId)) {
    return;
  }

  const chart = charts.get(canvasId);
  if (chart) {
    chart.destroy();
  }

  charts.delete(canvasId);
}

export function disposeAllCharts() {
  charts.forEach((chart) => {
    if (chart) {
      chart.destroy();
    }
  });

  charts.clear();
}
