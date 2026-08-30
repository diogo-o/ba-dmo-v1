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

  // =============================================================
  // REAL CREATE FLOW — "Criar Job On" (POST /api/jobon).
  // The dialog collects the minimum real production context and submits
  // server-side; the service validates + atomically persists the header AND
  // the initial revision, then the client opens the newly created folha.
  // A user without jobon.edit never sees the button (server-rendered) and the
  // route-level policy + service gate fail closed server-side regardless.
  // =============================================================
  const newJobButton = $("#newJob");
  const newJobDialog = $("#newJobDialog");
  if (newJobButton && newJobDialog && typeof newJobDialog.showModal === "function") {
    newJobButton.addEventListener("click", () => {
      const errorEl = $("#newJobError");
      if (errorEl) { errorEl.textContent = ""; errorEl.classList.remove("visible"); }
      newJobDialog.showModal();
    });
    $("#newJobCancel")?.addEventListener("click", () => newJobDialog.close());
    newJobDialog.addEventListener("click", event => { if (event.target === newJobDialog) newJobDialog.close(); });
    $("#newJobForm")?.addEventListener("submit", async event => {
      event.preventDefault();
      const form = event.currentTarget;
      const errorEl = $("#newJobError");
      const submit = $("#newJobSubmit");
      const showError = message => {
        if (errorEl) { errorEl.textContent = message; errorEl.classList.add("visible"); }
      };
      const values = {
        productionCode: form.elements.productionCode.value.trim(),
        reference: form.elements.reference.value.trim(),
        machineCode: form.elements.machineCode.value,
        plannedStartAt: form.elements.plannedStartAt.value || null,
        plannedEndAt: form.elements.plannedEndAt.value || null
      };
      if (!values.productionCode || !values.reference || !values.machineCode) {
        showError("Produção, Referência e Máquina são obrigatórias.");
        return;
      }
      submit.disabled = true;
      try {
        const response = await fetch("/api/jobon", {
          method: "POST",
          credentials: "same-origin",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(values)
        });
        if (response.ok) {
          const payload = await response.json();
          // Open the newly created Folha Job On (the redirect/landing target).
          window.location.assign(`/jobon?id=${encodeURIComponent(payload.jobOnId)}`);
          return;
        }
        let message = "Não foi possível criar o Job On. Verifique os dados e tente novamente.";
        try {
          const body = await response.json();
          if (body && body.message) message = body.message;
        } catch { /* keep the default message */ }
        showError(message);
      } catch {
        showError("Não foi possível criar o Job On. Verifique a ligação e tente novamente.");
      } finally {
        submit.disabled = false;
      }
    });
  }

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

  // Row contract (click selects / double click opens the folha / Enter +
  // Ctrl+Enter opens). Extracted so the planning fetch can re-attach it to
  // client-rendered rows (PHASE 4 planning isolation).
  function bindRow(row) {
    row.addEventListener("click", () => loadPlanningRow(row));
    row.addEventListener("dblclick", () => window.location.assign(row.dataset.openUrl));
    row.addEventListener("keydown", event => {
      if (event.key === "Enter" && event.ctrlKey) window.location.assign(row.dataset.openUrl);
      else if (event.key === "Enter") loadPlanningRow(row);
    });
  }
  $$("[data-job-row]").forEach(bindRow);
  $("#openLoadedSheet")?.addEventListener("click", () => { if (loadedRow) window.location.assign(loadedRow.dataset.openUrl); });
  $("#openLoadedControl")?.addEventListener("click", () => {
    if (!loadedRow) return;
    window.location.assign(`/controlo?jobOn=${encodeURIComponent(loadedRow.dataset.jobId)}&revision=${encodeURIComponent(loadedRow.dataset.revision)}`);
  });
  $("#openLoadedRepairs")?.addEventListener("click", () => {
    if (!loadedRow) return;
    window.location.assign(`/reparacao-interna?view=historico&jobOnId=${encodeURIComponent(loadedRow.dataset.jobId)}&production=${encodeURIComponent(loadedRow.dataset.production)}&line=${encodeURIComponent(loadedRow.dataset.line)}`);
  });

  // =============================================================
  // PHASE 4 — JOB ON PLANNING ISOLATION.
  //
  // Date/month selection updates ONLY the planning area via
  // GET /api/jobon/planning (the planning-only endpoint, same
  // projection as the landing page). It never:
  //   - reloads the document (no window.location.assign),
  //   - recreates the shell / header / navigation,
  //   - re-fetches the shared production rail endpoint — the rail keeps
  //     its single initial load owned by production-rail.js,
  //   - alters the Current Production Context.
  // =============================================================
  const planningView = views.planning;
  const calendar = $("[data-dmo-calendar]");
  const jobsCard = planningView ? $(".jobs-card", planningView) : null;
  const jobList = $("#jobList");
  const jobsHeading = jobsCard ? $(".section-heading h2", jobsCard) : null;
  const jobsFooter = jobsCard ? $(".list-footer > span", jobsCard) : null;

  // Server-rendered initial selection (the same value the page used to build
  // the calendar + list) — the initial client planning state.
  let planningDate = planningView?.dataset.selectedDate || null;
  let planningSeq = 0;
  let dayNavigationTimer = null;

  const PLANNING_EMPTY_HTML =
    `<div class="loaded-job-context empty"><div class="loaded-job-empty"><strong>Nenhum Job On planeado</strong><span>Não existem produções para o dia selecionado.</span></div></div>`;
  // Contained server-error state: only the planning list area (canonical
  // .dmo-error-state treatment; the page, rail and shell stay untouched).
  const PLANNING_ERROR_HTML =
    `<div class="dmo-error-state" role="alert"><strong>Não foi possível carregar o planeamento</strong><span>Verifique a ligação e selecione novamente um dia do calendário.</span></div>`;

  // Row markup mirrors the server-rendered row (index.planning projection) 1:1.
  function planningRowHtml(item) {
    const lineKey = item.lineColorKey || "";
    return `<article class="job-row" tabindex="0" data-job-row data-dmo-row data-id="${esc(item.jobOnId)}" data-job-id="${esc(item.jobOnId)}" data-production="${esc(item.production)}" data-reference="${esc(item.reference)}" data-line="${esc(item.machine)}" data-line-key="${esc(lineKey)}" data-revision="${esc(item.revisionNumber)}" data-date="${esc(item.dateIso)}" data-full-date="${esc(item.date)}" data-open-url="/jobon?id=${encodeURIComponent(item.jobOnId)}"><div class="date-block"><strong>${esc(item.dayLabel)}</strong><span>${esc(item.timeRange)}</span></div><div><span class="job-line ${lineKey ? "line-" + esc(lineKey) : ""}">${esc(item.machine)}</span><strong>${esc(item.reference)}</strong><small>Produção ${esc(item.production)}</small></div><div><span>Preparação</span><strong>${esc(item.preparationDisplay)}</strong><small>Rev. ${esc(item.revisionNumber)}</small></div><span class="status ${esc(item.lifecyclePillClass)}">${esc(item.lifecycleDisplay)}</span></article>`;
  }

  // Restore the initial (server-rendered) empty loaded-context state — the
  // same reset a page load performs; the context follows the planning date.
  function resetLoadedContext() {
    loadedRow = null;
    $$(".job-row", jobList).forEach(item => item.classList.remove("selected"));
    const context = $("#loadedJobContext");
    if (!context) return;
    context.classList.add("empty");
    const empty = $(".loaded-job-empty", context);
    const content = $(".loaded-job-content", context);
    if (empty) empty.hidden = false;
    if (content) content.hidden = true;
  }

  // Calendar data refresh (in place, no reload): update the canonical
  // calendar attributes and let dmo-calendar.js re-render the grid. The
  // selected day is preserved when it belongs to the rendered month
  // (same rule as month navigation).
  function refreshCalendarData(data) {
    if (!calendar) return;
    calendar.setAttribute("data-month", data.month);
    calendar.setAttribute("data-record-dates", data.recordDatesCsv || "");
    calendar.setAttribute("data-record-lines", JSON.stringify(data.recordLines || {}));
    calendar.dispatchEvent(new CustomEvent("dmo:calendar-data", {
      bubbles: false,
      detail: { selectedDate: planningDate }
    }));
  }

  function applyPlanning(data) {
    if (!jobList) return;
    // The server-resolved date is the single source of truth for the client
    // planning state (covers the missing/invalid -> today rule server-side).
    planningDate = data.selectedDateValue || planningDate;
    refreshCalendarData(data);
    if (jobsHeading) jobsHeading.textContent = `Job Ons de ${data.selectedDateDisplay}`;
    const items = Array.isArray(data.items) ? data.items : [];
    jobList.innerHTML = items.length ? items.map(planningRowHtml).join("") : PLANNING_EMPTY_HTML;
    // Re-attach the canonical list contract + the Job On row behavior to the
    // client-rendered rows (both binders are idempotent per row).
    if (window.dmoBindList) window.dmoBindList(jobList);
    $$(".job-row", jobList).forEach(bindRow);
    if (jobsFooter) jobsFooter.textContent = `${items.length} Job Ons · Página 1 de 1`;
    resetLoadedContext();
  }

  async function loadPlanning(options = {}) {
    const { date = planningDate, month = null, pushUrl = false } = options;
    if (!calendar || !jobList) return;
    const seq = ++planningSeq;
    // Contained loading state: only the list area pulses (the calendar, the
    // rail and the shell never enter a planning loading state).
    jobList.innerHTML = `<div class="dmo-skeleton" aria-hidden="true"></div>`;
    let payload = null;
    try {
      const params = new URLSearchParams();
      if (date) params.set("date", date);
      if (month) params.set("month", month);
      const response = await fetch(`/api/jobon/planning?${params.toString()}`, { credentials: "same-origin" });
      if (!response.ok) throw new Error(`planning ${response.status}`);
      payload = await response.json();
    } catch {
      payload = null;
    }
    if (seq !== planningSeq) return; // a newer selection superseded this load
    if (!payload) {
      jobList.innerHTML = PLANNING_ERROR_HTML;
      return;
    }
    applyPlanning(payload);
    if (pushUrl && date) {
      // Browser state: the selected date lives in the URL query, updated
      // WITHOUT navigation (history entry for Back/Forward support).
      const url = new URL(window.location.href);
      url.searchParams.set("date", date);
      window.history.pushState({ planningDate: date }, "", url);
    }
  }

  // Calendar day selection -> planning-only fetch + URL update (no reload).
  calendar?.addEventListener("dmo:date-select", event => {
    const date = event.detail?.date;
    if (!date) return;
    clearTimeout(dayNavigationTimer);
    dayNavigationTimer = setTimeout(() => loadPlanning({ date, pushUrl: true }), 260);
  });

  // Calendar month navigation (prev/next) -> planning-only fetch for the new
  // month; the selected date is preserved (selected-date rule).
  calendar?.addEventListener("dmo:month-change", event => {
    const month = event.detail?.month;
    if (!month) return;
    loadPlanning({ month });
  });

  // Browser Back/Forward over the pushed planning dates -> reload ONLY the
  // planning section; the document, shell and rail are never reloaded.
  window.addEventListener("popstate", () => {
    const date = new URLSearchParams(window.location.search).get("date");
    loadPlanning({ date: date ?? null, pushUrl: false });
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
