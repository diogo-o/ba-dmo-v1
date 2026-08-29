(function () {
  const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
  const $ = (selector, root = document) => root.querySelector(selector);
  const root = $(".jobon-canonical-root");
  if (!root) return;

  const views = {
    planning: $("#planningView"),
    sheet: $("#sheetView"),
    control: $("#controlView"),
    history: $("#historyView"),
    settings: $("#settingsView")
  };

  document.body.dataset.mode = "view";
  document.body.dataset.view = root.dataset.initialView || "planning";

  // Canonical Job On JS composition: CM + TP/CAL, BQ/AN + PU/ARR,
  // MF + CS/PI, and the article image + FO.
  const priorityGrid = $(".priority-grid");
  const secondaryGrid = $(".secondary-grid");
  if (priorityGrid && secondaryGrid) {
    const secondaryCard = code => $$(':scope > .tool-card', secondaryGrid)
      .find(card => $(".tool-code", card)?.textContent.trim() === code);
    const board = document.createElement("section");
    board.className = "operational-board";
    const makeColumn = (name, nodes) => {
      const column = document.createElement("div");
      column.className = `operational-column ${name}`;
      nodes.filter(Boolean).forEach(node => column.appendChild(node));
      return column;
    };
    board.append(
      makeColumn("column-cm", [priorityGrid.querySelector('[data-family="CM"]'), secondaryCard("TP"), secondaryCard("CAL")]),
      makeColumn("column-bq-pu", [priorityGrid.querySelector(".bq-stack"), priorityGrid.querySelector(".pu-stack")]),
      makeColumn("column-mf", [priorityGrid.querySelector('[data-family="MF"]'), secondaryCard("CS"), secondaryCard("PI")]),
      makeColumn("column-visual", [priorityGrid.querySelector(".priority-image"), secondaryCard("FO")])
    );
    priorityGrid.replaceWith(board);
    secondaryGrid.remove();
  }

  function openView(name) {
    Object.entries(views).forEach(([key, node]) => node?.classList.toggle("active", key === name));
    $$(".module-tabs [data-tab]").forEach(button => button.classList.toggle("active", button.dataset.tab === name));
    document.body.dataset.view = name;
    window.scrollTo({ top: 0, behavior: "instant" });
  }

  $$(".module-tabs [data-tab]").forEach(button => button.addEventListener("click", () => openView(button.dataset.tab)));
  $("#backPlanning")?.addEventListener("click", () => openView("planning"));
  $("#openReferenceHistory")?.addEventListener("click", () => openView("history"));

  const setMode = mode => {
    document.body.dataset.mode = mode;
    const label = $("#modeIndicator strong");
    if (label) label.textContent = mode === "edit" ? "Modo edição" : "Modo consulta";
  };
  $("#editSheet")?.addEventListener("click", () => setMode("edit"));
  $("#saveSheet")?.addEventListener("click", () => setMode("view"));
  $("#cancelEdit")?.addEventListener("click", () => setMode("view"));
  $$(".tool-change").forEach(button => button.addEventListener("click", () => {
    const title = $("#pickerTitle");
    if (title) title.textContent = `Alterar ${button.dataset.family} associado`;
    $("#inventoryPicker")?.scrollIntoView({ behavior: "smooth", block: "center" });
  }));

  let loadedRow = null;
  function loadPlanningRow(row) {
    loadedRow = row;
    $$(".job-row").forEach(item => item.classList.toggle("selected", item === row));
    const values = {
      "#loadedJobTitle": `${row.dataset.reference} · ${row.dataset.production}`,
      "#loadedJobReference": row.dataset.reference,
      "#loadedJobProduction": row.dataset.production,
      "#loadedJobMachine": row.dataset.line,
      "#loadedJobRevision": row.dataset.revision
    };
    Object.entries(values).forEach(([selector, value]) => { const node = $(selector); if (node) node.textContent = value; });
    $("#loadedJobContext")?.classList.remove("empty");
    const empty = $(".loaded-job-empty", $("#loadedJobContext"));
    const content = $(".loaded-job-content", $("#loadedJobContext"));
    if (empty) empty.hidden = true;
    if (content) content.hidden = false;
  }

  $$("[data-job-row]").forEach(row => {
    row.addEventListener("click", () => loadPlanningRow(row));
    row.addEventListener("dblclick", () => window.location.assign(row.dataset.openUrl));
    row.addEventListener("keydown", event => {
      if (event.key === "Enter" && event.ctrlKey) window.location.assign(row.dataset.openUrl);
      else if (event.key === "Enter") loadPlanningRow(row);
    });
  });
  $("#openLoadedSheet")?.addEventListener("click", () => { if (loadedRow) window.location.assign(loadedRow.dataset.openUrl); });
  $("#openLoadedControl")?.addEventListener("click", () => {
    if (!loadedRow) return;
    window.location.assign(`/controlo?jobOn=${encodeURIComponent(loadedRow.dataset.jobId)}&revision=${encodeURIComponent(loadedRow.dataset.revision)}`);
  });
  $("#openLoadedRepairs")?.addEventListener("click", () => {
    if (!loadedRow) return;
    window.location.assign(`/reparacao-interna?view=historico&jobOnId=${encodeURIComponent(loadedRow.dataset.jobId)}&production=${encodeURIComponent(loadedRow.dataset.production)}&line=${encodeURIComponent(loadedRow.dataset.line)}`);
  });

  const calendar = $("[data-dmo-calendar]");
  let dayNavigationTimer = null;
  calendar?.addEventListener("dmo:date-select", event => {
    const date = event.detail?.date;
    if (!date) return;
    clearTimeout(dayNavigationTimer);
    dayNavigationTimer = setTimeout(() => window.location.assign(`/jobon?date=${encodeURIComponent(date)}`), 260);
  });

  const productionSelect = $("#productionSelect");
  productionSelect?.addEventListener("change", () => {
    const option = productionSelect.selectedOptions[0];
    if (option?.dataset.jobId) window.location.assign(`/jobon?id=${encodeURIComponent(option.dataset.jobId)}`);
  });

  $$(".expand-note").forEach(button => button.addEventListener("click", event => {
    event.preventDefault();
    const field = button.closest(".notes-field");
    field?.classList.toggle("expanded");
    button.textContent = field?.classList.contains("expanded") ? "Recolher" : "Expandir";
  }));
  $("#goChecks")?.addEventListener("click", () => $("#checksSection")?.scrollIntoView({ behavior: "smooth", block: "start" }));

  function esc(value) {
    const map = { "&": "&" + "amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" };
    return String(value ?? "").replace(/[&<>"']/g, character => map[character]);
  }

  // Configurable labels remain escaped if the settings endpoint is connected.
  const renderCatalogLabel = label => `<strong>${esc(label)}</strong>`;
  void renderCatalogLabel;

  async function loadRail() {
    const panel = $("#linePanel");
    if (!panel) return;
    try {
      const response = await fetch("/api/boquilhas/production-context", { credentials: "same-origin" });
      if (!response.ok) throw new Error("production context unavailable");
      const cards = await response.json();
      const byLine = Object.fromEntries(cards.map(card => [card.line, card]));
      $$(".line-card", panel).forEach(button => {
        const card = byLine[button.dataset.line];
        if (!card?.hasActiveContext) {
          button.innerHTML = `<span class="line-code">${esc(button.dataset.line)}</span><span class="line-state idle">Sem produção</span><small>Sem Job On ativo</small>`;
          return;
        }
        button.dataset.jobId = card.jobOnId || "";
        button.innerHTML = `<span class="line-code">${esc(button.dataset.line)}</span><span class="line-state running">Ativo</span><strong>${esc(card.reference || "—")}</strong><small>Produção ${esc(card.productionCode || "—")}</small>`;
      });
    } catch {
      $$(".line-card", panel).forEach(button => {
        button.innerHTML = `<span class="line-code">${esc(button.dataset.line)}</span><span class="line-state idle">Indisponível</span><small>Contexto não carregado</small>`;
      });
    }
  }
  $$(".line-card").forEach(button => button.addEventListener("click", () => {
    $$(".line-card").forEach(item => item.classList.toggle("active", item === button));
    if (button.dataset.jobId) window.location.assign(`/jobon?id=${encodeURIComponent(button.dataset.jobId)}`);
  }));
  $("#railToggle")?.addEventListener("click", () => {
    const rail = $("#productionRail");
    rail?.classList.toggle("open");
    const open = rail?.classList.contains("open") === true;
    $("#railToggle").setAttribute("aria-expanded", String(open));
    $("#railToggle").textContent = open ? "Ocultar linhas" : "Ver linhas";
  });

  const image = $("#article-reference-image");
  const imageEmpty = $("#article-image-empty");
  const showImage = () => { if (image) image.hidden = false; if (imageEmpty) imageEmpty.hidden = true; };
  const showEmptyImage = () => { if (image) image.hidden = true; if (imageEmpty) imageEmpty.hidden = false; };
  image?.addEventListener("load", showImage);
  image?.addEventListener("error", showEmptyImage);
  if (image?.complete) image.naturalWidth > 0 ? showImage() : showEmptyImage();

  const imageDialog = $("#imageDialog");
  $("#articleImage")?.addEventListener("click", () => {
    if (!image || image.hidden || !imageDialog) return;
    $("#image-dialog-preview").src = image.currentSrc || image.src;
    imageDialog.showModal();
  });
  $(".dialog-close", imageDialog)?.addEventListener("click", () => imageDialog.close());
  imageDialog?.addEventListener("click", event => { if (event.target === imageDialog) imageDialog.close(); });

  const jobOnId = $("meta[name='jobon-id']")?.content;
  async function persistImageAction(action, imageAssetId) {
    if (!jobOnId) return false;
    const response = await fetch(`/api/jobon/${jobOnId}/image/${action}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: action === "remove" ? null : JSON.stringify({ imageAssetId })
    });
    return response.ok;
  }
  $("#job-image-input")?.addEventListener("change", async event => {
    const file = event.target.files?.[0];
    if (!file) return;
    if (await persistImageAction("replace", file.name)) {
      image.src = URL.createObjectURL(file);
      showImage();
    }
    event.target.value = "";
  });
  $("#remove-image-btn")?.addEventListener("click", async () => { if (await persistImageAction("remove", null)) showEmptyImage(); });

  $("[data-more-actions]")?.addEventListener("click", event => {
    event.stopPropagation();
    const trigger = event.currentTarget;
    const menu = trigger.parentElement?.querySelector(".tool-menu");
    const open = !menu?.classList.contains("open");
    menu?.classList.toggle("open", open);
    trigger.setAttribute("aria-expanded", String(open));
  });
  document.addEventListener("click", () => {
    $(".more-actions-menu .tool-menu")?.classList.remove("open");
    $("[data-more-actions]")?.setAttribute("aria-expanded", "false");
  });

  $("#printJobOn")?.addEventListener("click", async () => {
    if (!jobOnId) return;
    const button = $("#printJobOn");
    button.disabled = true;
    try {
      const response = await fetch(`/api/jobon/${jobOnId}/document`, { method: "POST", credentials: "same-origin" });
      if (!response.ok) throw new Error("document generation failed");
      const blobUrl = URL.createObjectURL(await response.blob());
      window.open(blobUrl, "_blank", "noopener");
      setTimeout(() => URL.revokeObjectURL(blobUrl), 60000);
    } finally {
      button.disabled = false;
    }
  });

  openView(root.dataset.initialView || "planning");
  if (innerWidth <= 980) $("#productionRail")?.classList.add("open");
  loadRail();
})();
