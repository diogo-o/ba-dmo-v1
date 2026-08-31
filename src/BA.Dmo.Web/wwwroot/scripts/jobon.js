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

  // =============================================================
  // REAL DUPLICATE FLOW — "Duplicar" (POST /api/jobon/{id}/duplicate).
  // The dialog collects only the NEW production/date context; the reference
  // and tool setup are reused from the source revision. The service validates
  // and atomically persists the new header + the copied initial revision +
  // the audit event, then the client opens the newly created folha. The source
  // Job On is never modified. The button is server-rendered only for users with
  // jobon.edit and an open Job On; the route policy + service gate fail closed
  // regardless.
  // =============================================================
  const duplicateJobButton = $("#duplicateJobOn");
  const duplicateJobDialog = $("#duplicateJobDialog");
  if (duplicateJobButton && duplicateJobDialog && typeof duplicateJobDialog.showModal === "function") {
    duplicateJobButton.addEventListener("click", () => {
      const errorEl = $("#duplicateJobError");
      if (errorEl) { errorEl.textContent = ""; errorEl.classList.remove("visible"); }
      duplicateJobDialog.showModal();
    });
    $("#duplicateJobCancel")?.addEventListener("click", () => duplicateJobDialog.close());
    duplicateJobDialog.addEventListener("click", event => { if (event.target === duplicateJobDialog) duplicateJobDialog.close(); });
    $("#duplicateJobForm")?.addEventListener("submit", async event => {
      event.preventDefault();
      const form = event.currentTarget;
      const errorEl = $("#duplicateJobError");
      const submit = $("#duplicateJobSubmit");
      const showError = message => {
        if (errorEl) { errorEl.textContent = message; errorEl.classList.add("visible"); }
      };
      const jobOnId = $("meta[name='jobon-id']")?.getAttribute("content");
      if (!jobOnId) { showError("Não foi possível identificar o Job On de origem."); return; }
      const values = {
        productionCode: form.elements.productionCode.value.trim(),
        machineCode: form.elements.machineCode.value,
        plannedStartAt: form.elements.plannedStartAt.value || null,
        plannedEndAt: form.elements.plannedEndAt.value || null
      };
      if (!values.productionCode || !values.machineCode) {
        showError("Produção e Máquina são obrigatórias.");
        return;
      }
      submit.disabled = true;
      try {
        const response = await fetch(`/api/jobon/${encodeURIComponent(jobOnId)}/duplicate`, {
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
        let message = "Não foi possível duplicar o Job On. Verifique os dados e tente novamente.";
        try {
          const body = await response.json();
          if (body && body.message) message = body.message;
        } catch { /* keep the default message */ }
        showError(message);
      } catch {
        showError("Não foi possível duplicar o Job On. Verifique a ligação e tente novamente.");
      } finally {
        submit.disabled = false;
      }
    });
  }

  // =============================================================
  // REAL ALTER-DATE FLOW — "Alterar data" (POST /api/jobon/{id}/date).
  // The dialog collects only the NEW planned dates (and an optional change
  // reason). The service creates a NEW immutable revision of the SAME Job On
  // (never a new Job On), preserving the current setup; only the date context
  // changes. On success the SAME folha reopens via /jobon?id={sameJobOnId}
  // rendering the new current revision. The button is server-rendered only for
  // users with jobon.edit and an open Job On; the route policy + service gate
  // fail closed regardless.
  // =============================================================
  const alterDatesButton = $("#alterDatesJobOn");
  const alterDatesDialog = $("#alterDatesDialog");
  if (alterDatesButton && alterDatesDialog && typeof alterDatesDialog.showModal === "function") {
    alterDatesButton.addEventListener("click", () => {
      const errorEl = $("#alterDatesError");
      if (errorEl) { errorEl.textContent = ""; errorEl.classList.remove("visible"); }
      alterDatesDialog.showModal();
    });
    $("#alterDatesCancel")?.addEventListener("click", () => alterDatesDialog.close());
    alterDatesDialog.addEventListener("click", event => { if (event.target === alterDatesDialog) alterDatesDialog.close(); });
    $("#alterDatesForm")?.addEventListener("submit", async event => {
      event.preventDefault();
      const form = event.currentTarget;
      const errorEl = $("#alterDatesError");
      const submit = $("#alterDatesSubmit");
      const showError = message => {
        if (errorEl) { errorEl.textContent = message; errorEl.classList.add("visible"); }
      };
      const jobOnId = $("meta[name='jobon-id']")?.getAttribute("content");
      if (!jobOnId) { showError("Não foi possível identificar o Job On."); return; }
      const values = {
        plannedStartAt: form.elements.plannedStartAt.value || null,
        plannedEndAt: form.elements.plannedEndAt.value || null,
        changeReason: form.elements.changeReason.value.trim() || null
      };
      submit.disabled = true;
      try {
        const response = await fetch(`/api/jobon/${encodeURIComponent(jobOnId)}/date`, {
          method: "POST",
          credentials: "same-origin",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(values)
        });
        if (response.ok) {
          // Reopen the SAME Job On folha, now rendering the new current revision.
          window.location.assign(`/jobon?id=${encodeURIComponent(jobOnId)}`);
          return;
        }
        let message = "Não foi possível alterar a data. Verifique os dados e tente novamente.";
        try {
          const body = await response.json();
          if (body && body.message) message = body.message;
        } catch { /* keep the default message */ }
        showError(message);
      } catch {
        showError("Não foi possível alterar a data. Verifique a ligação e tente novamente.");
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

  // =============================================================
  // REAL EDIT / SAVE-NEW-REVISION / CANCEL FLOW.
  //
  // "Editar folha" enters edit mode (the existing design enables the editable
  // controls). "Guardar nova revisão" submits ONLY revision-owned values — the
  // general notes from the sheet and the complete edited component graph — to
  // POST /api/jobon/{id}/revision. Header-owned data (dates, production
  // identity, machine/line) is NEVER part of this payload: dates keep the
  // dedicated "Alterar data" flow and production/machine are not rewritten.
  //
  // The component graph starts from the CURRENT revision (embedded in
  // #jobon-revision-graph): every component, field, CAL row and verification is
  // copied under FRESH ids (R-002 — the repository re-pins children to the new
  // revision id), so the previous revision can never collide or be mutated.
  // Verification occurrences are copied WITH their current state — the same
  // production-occurrence rule the date-change flow documents (confirmed checks
  // are never silently reset; old-revision rows are never touched).
  //
  // "Cancelar edição" is a pure client-side reset: it discards the unsaved DOM
  // edits and performs ZERO writes (no fetch, no endpoint).
  // =============================================================
  const revisionGraphScript = $("#jobon-revision-graph");
  let revisionGraph = [];
  if (revisionGraphScript && revisionGraphScript.textContent) {
    try { revisionGraph = JSON.parse(revisionGraphScript.textContent); } catch { revisionGraph = []; }
  }
  const saveRevisionDialog = $("#saveRevisionDialog");
  const saveRevisionForm = $("#saveRevisionForm");
  const saveRevisionCancel = $("#saveRevisionCancel");
  const jobOnIdForSave = $("meta[name='jobon-id']")?.getAttribute("content");
  // The CURRENT revision id of this folha (embedded by the page): the pin every
  // component in the submitted graph carries — including a brand-new component
  // created client-side in edit mode (the non-nullable transport contract never
  // receives null; the repository stays authoritative and re-pins the graph to
  // the newly created revision id at persistence, R-002).
  const currentRevisionIdForSave =
    $("meta[name='jobon-revision-id']")?.getAttribute("content")
    || (revisionGraph[0] ? revisionGraph[0].jobOnRevisionId : null);
  const changeReasonRequired = root?.dataset.changeReasonRequired === "true";
  const initialGeneralNotes = $(".general-notes textarea")?.value ?? null;

  // uuid v4 fallback for browsers without crypto.randomUUID.
  function uuid() {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
      return crypto.randomUUID();
    }
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, character => {
      const random = Math.floor(Math.random() * 16);
      const value = character === "x" ? random : (random & 0x3) | 0x8;
      return value.toString(16);
    });
  }

  // Family enum value (MP_CM) → tool-card data-family code (CM). CAL has no
  // editable tool card in the sheet (rows are read-only), so it is excluded.
  const familyToCardCode = {
    MP_CM: "CM", MF: "MF", BQ: "BQ", PU: "PU", AN: "AN", ARR: "ARR",
    PI: "PI", CS: "CS", TP: "TP", FO: "FO"
  };

  function parsePtDate(value) {
    const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(String(value || "").trim());
    if (!match) return null;
    return `${match[3]}-${match[2].padStart(2, "0")}-${match[1].padStart(2, "0")}T00:00:00`;
  }

  function formatDateInput(iso) {
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(iso || ""));
    if (!match) return "";
    return `${match[3]}/${match[2]}/${match[1]}`;
  }

  // Applies a DOM input value onto a typed field record (the only revision-owned
  // value the tool cards edit). Empty input → null (never an invented value).
  function applyFieldValue(field, raw) {
    const value = String(raw ?? "").trim();
    const cleared = {
      valueText: null, valueInteger: null, valueDecimal: null,
      valueBoolean: null, valueDate: null
    };
    switch (field.valueType) {
      case "integer":
        return { ...field, ...cleared, valueInteger: value === "" ? null : parseInt(value, 10) };
      case "decimal":
        return { ...field, ...cleared, valueDecimal: value === "" ? null : parseFloat(value.replace(",", ".")) };
      case "boolean":
        return { ...field, ...cleared, valueBoolean: value === "" ? null : value === "Sim" };
      case "date":
        return { ...field, ...cleared, valueDate: value === "" ? null : parsePtDate(value) };
      default:
        return { ...field, ...cleared, valueText: value };
    }
  }

  // Reads the edited DOM value of a tool card (by family) and overlays it onto
  // the component loaded from the current revision graph. Absent cards or
  // missing inputs carry the stored values forward unchanged.
  function overlayEditedComponent(component) {
    const cardCode = familyToCardCode[component.family];
    const card = cardCode ? document.querySelector(`.tool-card[data-family="${cardCode}"]`) : null;
    if (!card) return component;

    const refInput = card.querySelector('input[aria-label^="Referência"]');
    const lotInput = card.querySelector('input[aria-label^="Lote"]');
    const notes = card.querySelector("textarea");

    const fields = (component.fields || []).map(field => ({ ...field }));
    if (fields.length > 0) {
      const fieldInputs = [...(card.querySelectorAll(".tool-fields input") || [])]
        .filter(input => input !== refInput && input !== lotInput);
      fields.forEach((field, index) => {
        const input = fieldInputs[index];
        if (input) Object.assign(field, applyFieldValue(field, input.value));
      });
    }

    return {
      ...component,
      referenceSnapshot: refInput ? refInput.value : component.referenceSnapshot,
      lotSnapshot: lotInput ? lotInput.value : component.lotSnapshot,
      notes: notes ? notes.value : component.notes,
      fields
    };
  }

  // Builds the new-revision graph: the current revision's components with the
  // edited values, every row regenerated under FRESH ids (R-002). Verification
  // occurrences are copied WITH their current state — same production; the
  // previous revision's rows are never touched.
  //
  // Staged picker selections ("Alterar CM/MF/BQ associado") are merged LAST:
  // the physical source links (sourceToolId/sourceLotId) + the reference/lot
  // snapshots come from the SELECTED REGISTERED tool lot, so the saved
  // association is always a real existing (tipo, referência, lote,
  // máquina/linha) combination — the server re-validates it. A family with no
  // stored component gains its association only through an explicit selection
  // (absent tools stay absent — no invented associations).
  function buildEditedComponentsGraph() {
    const source = Array.isArray(revisionGraph) ? revisionGraph : [];
    const graph = source.map(component => {
      const edited = overlayEditedComponent(component);
      const componentId = uuid();
      return {
        ...edited,
        jobOnComponentId: componentId,
        fields: (edited.fields || []).map(field => ({
          ...field,
          jobOnComponentFieldId: uuid(),
          jobOnComponentId: componentId
        })),
        rows: (edited.rows || []).map(row => ({
          ...row,
          jobOnComponentRowId: uuid(),
          jobOnComponentId: componentId
        })),
        verifications: (edited.verifications || []).map(verification => ({
          ...verification,
          jobOnVerificationOccurrenceId: uuid(),
          jobOnComponentId: componentId,
          completionSource: verification.completionSource || "manual_job_on"
        }))
      };
    });

    Object.entries(toolSelections).forEach(([cardCode, selection]) => {
      const family = cardCodeToFamily[cardCode];
      if (!family) return;
      const staged = {
        sourceToolId: selection.referenceId,
        sourceLotId: selection.loteId,
        referenceSnapshot: selection.reference,
        lotSnapshot: selection.lot,
        technicalNameSnapshot: selection.technicalName ?? null
      };
      const index = graph.findIndex(component => component.family === family);
      if (index >= 0) {
        graph[index] = { ...graph[index], ...staged };
      } else {
        graph.push({
          jobOnComponentId: uuid(),
          // Brand-new (not-yet-persisted) component: carries the CURRENT revision
          // id — the same pin as every stored component in this graph (the
          // established save-flow convention). Never null: the request DTO binds
          // a non-nullable Guid. The server creates the NEW revision id and the
          // repository re-pins this component to it at persistence (R-002).
          jobOnRevisionId: currentRevisionIdForSave,
          family,
          ...staged,
          plannedQuantity: null,
          stockSnapshot: null,
          usageSnapshot: null,
          notes: null,
          displayOrder: 0,
          fields: [],
          rows: [],
          verifications: []
        });
      }
    });

    return graph;
  }

  // Restores the ORIGINAL values (from the embedded graph) into the tool cards
  // and the notes textarea — the cancel-edit reset. Pure DOM, zero writes.
  function restoreOriginalValues() {
    resetPickerState(); // discards any staged (unsaved) tool selection
    const notes = $(".general-notes textarea");
    if (notes && initialGeneralNotes !== null) notes.value = initialGeneralNotes;
    (Array.isArray(revisionGraph) ? revisionGraph : []).forEach(component => {
      const cardCode = familyToCardCode[component.family];
      const card = cardCode ? document.querySelector(`.tool-card[data-family="${cardCode}"]`) : null;
      if (!card) return;
      const refInput = card.querySelector('input[aria-label^="Referência"]');
      const lotInput = card.querySelector('input[aria-label^="Lote"]');
      const notesInput = card.querySelector("textarea");
      if (refInput) refInput.value = component.referenceSnapshot ?? "";
      if (lotInput) lotInput.value = component.lotSnapshot ?? "";
      if (notesInput) notesInput.value = component.notes ?? "";
      const fieldInputs = [...(card.querySelectorAll(".tool-fields input") || [])]
        .filter(input => input !== refInput && input !== lotInput);
      (component.fields || []).forEach((field, index) => {
        const input = fieldInputs[index];
        if (!input) return;
        switch (field.valueType) {
          case "integer": input.value = field.valueInteger == null ? "" : String(field.valueInteger); break;
          case "decimal": input.value = field.valueDecimal == null ? "" : String(field.valueDecimal); break;
          case "boolean": input.value = field.valueBoolean == null ? "" : (field.valueBoolean ? "Sim" : "Não"); break;
          case "date": input.value = field.valueDate ? formatDateInput(field.valueDate) : ""; break;
          default: input.value = field.valueText ?? "";
        }
      });
    });
  }

  // =============================================================
  // REAL TOOL-SELECTION PICKER — "Alterar CM/MF/BQ associado" (Manual 10 §4/§8).
  //
  // A tool selection is identified by the tuple (tipo, referência, lote,
  // máquina/linha). CM, MF and BQ are DISTINCT tools: the same reference code
  // registered under another type — or for another machine/line — is a
  // different tool and never merges. The option list comes ONLY from the
  // Ferramentas register (GET /api/jobon/{id}/tool-options): real existing
  // (referência, lote) combinations registered for THIS Job On's machine/line.
  // The server rejects any persisted combination that does not exist in the
  // register — no invented tools; no Ferramentas/Armazém record is created.
  //
  // Flow: "Alterar X" loads the options for X → the Responsável filters by
  // reference and selects one row → "Associar selecionado" applies it to the
  // editable revision (tool card values + physical source links) → the
  // association only becomes real when "Guardar nova revisão" saves the NEW
  // immutable revision (the previous revision is never touched). PU is Job On
  // production-specific manual configuration (Manual 10 §6.1) and has no
  // register-backed selection.
  // =============================================================
  const toolSelections = {}; // card code (CM/MF/BQ) → staged selected option
  let pickerFamily = null;   // card code currently loaded in the picker
  let selectedOption = null; // the row selected in the current picker list

  const cardCodeToFamily = { CM: "MP_CM", MF: "MF", BQ: "BQ" };
  const pickerBody = $("#pickerOptionsBody");
  const pickerSelectionCount = $("#pickerSelectionCount");
  const applyToolSelectionButton = $("#applyToolSelection");
  const pickerReferenceFilter = $("#pickerReferenceFilter");
  const initialPickerReference = pickerReferenceFilter?.value ?? "";
  const PICKER_EMPTY_MESSAGE =
    "Carregue em “Alterar CM/MF/BQ associado” para listar as opções registadas.";

  function pickerMessage(text) {
    if (!pickerBody) return;
    pickerBody.textContent = "";
    const row = document.createElement("tr");
    const cell = document.createElement("td");
    cell.colSpan = 6;
    cell.textContent = text;
    row.appendChild(cell);
    pickerBody.appendChild(row);
  }

  function clearPickerSelection() {
    $$(".picker-row", pickerBody).forEach(row => {
      row.classList.remove("selected");
      row.setAttribute("aria-selected", "false");
    });
    selectedOption = null;
    if (applyToolSelectionButton) applyToolSelectionButton.disabled = true;
    if (pickerSelectionCount) pickerSelectionCount.textContent = "Sem opção selecionada.";
  }

  async function loadPickerOptions() {
    if (!pickerFamily || !jobOnIdForSave) {
      pickerMessage(PICKER_EMPTY_MESSAGE);
      return;
    }
    clearPickerSelection();
    if (!cardCodeToFamily[pickerFamily]) {
      // PU/CS & co. are manual production configuration — no tool register.
      pickerMessage(`${pickerFamily} é configuração manual de produção — sem registo de ferramentas neste catálogo.`);
      return;
    }
    pickerMessage("A carregar opções registadas…");
    const reference = (pickerReferenceFilter?.value || "").trim();
    const query = new URLSearchParams({ family: pickerFamily });
    if (reference) query.set("reference", reference);
    try {
      const response = await fetch(
        `/api/jobon/${encodeURIComponent(jobOnIdForSave)}/tool-options?${query.toString()}`,
        { credentials: "same-origin" });
      // An edit-capability denial surfaces as a redirect to /access-denied
      // (GET semantics) — treat it as a denial, never as data.
      if (response.redirected) throw new Error("Sem permissão para selecionar ferramentas.");
      let body = null;
      try { body = await response.json(); } catch { /* non-JSON body */ }
      if (!response.ok) throw new Error(body?.message || "Não foi possível carregar as opções de ferramenta.");
      renderPickerOptions(body);
    } catch (error) {
      pickerMessage(error?.message || "Não foi possível carregar as opções de ferramenta.");
    }
  }

  function renderPickerOptions(data) {
    clearPickerSelection();
    const items = Array.isArray(data?.items) ? data.items : [];
    if (items.length === 0) {
      pickerMessage("Sem lotes registados para esta família, referência e máquina/linha.");
      return;
    }
    pickerBody.textContent = "";
    items.forEach(option => {
      const row = document.createElement("tr");
      row.className = "picker-row";
      row.dataset.referenceId = option.referenceId;
      row.dataset.loteId = option.loteId;
      row.dataset.reference = option.reference;
      row.dataset.lot = option.lot;
      row.dataset.technicalName = option.technicalName || "";
      row.setAttribute("aria-selected", "false");

      const refCell = document.createElement("td");
      const strong = document.createElement("strong");
      strong.textContent = option.reference;
      refCell.appendChild(strong);
      const lotCell = document.createElement("td");
      lotCell.textContent = option.lot;
      if (Array.isArray(option.allowedLines) && option.allowedLines.length > 0) {
        const lines = document.createElement("small");
        lines.textContent = option.allowedLines.join(" · ");
        lotCell.appendChild(document.createElement("br"));
        lotCell.appendChild(lines);
      }
      const techCell = document.createElement("td");
      techCell.textContent = option.technicalName || "";

      row.append(refCell, lotCell, techCell);
      // Localização/Estado/Utilização stay blank: the picker lists registered
      // tool-lot identity only (no Armazém/estado real data is presented).
      row.append(document.createElement("td"), document.createElement("td"), document.createElement("td"));

      row.addEventListener("click", () => selectPickerOption(row));
      pickerBody.appendChild(row);
    });
  }

  function selectPickerOption(row) {
    $$(".picker-row", pickerBody).forEach(r => {
      r.classList.remove("selected");
      r.setAttribute("aria-selected", "false");
    });
    row.classList.add("selected");
    row.setAttribute("aria-selected", "true");
    selectedOption = {
      family: pickerFamily,
      referenceId: row.dataset.referenceId,
      loteId: row.dataset.loteId,
      reference: row.dataset.reference,
      lot: row.dataset.lot,
      technicalName: row.dataset.technicalName || null
    };
    if (applyToolSelectionButton) applyToolSelectionButton.disabled = false;
    if (pickerSelectionCount) {
      pickerSelectionCount.textContent =
        `Selecionado: ${pickerFamily} ${selectedOption.reference} · Lote ${selectedOption.lot}`;
    }
  }

  function applySelectedTool() {
    if (!selectedOption) return;
    const family = selectedOption.family;
    toolSelections[family] = selectedOption;
    const card = document.querySelector(`.tool-card[data-family="${family}"]`);
    if (card) {
      const refInput = card.querySelector('input[aria-label^="Referência"]');
      const lotInput = card.querySelector('input[aria-label^="Lote"]');
      if (refInput) refInput.value = selectedOption.reference;
      if (lotInput) lotInput.value = selectedOption.lot;
    }
    if (pickerSelectionCount) {
      pickerSelectionCount.textContent =
        `Aplicado: ${family} ${selectedOption.reference} · Lote ${selectedOption.lot}. Use “Guardar nova revisão” para persistir a associação.`;
    }
    if (applyToolSelectionButton) applyToolSelectionButton.disabled = true;
  }

  // Manual edits of the reference/lot of a CM/MF/BQ card replace the staged
  // picker selection: the register-backed association is dropped (snapshot-only
  // values stay editable); a linked component must always match the register.
  Object.keys(cardCodeToFamily).forEach(family => {
    const card = document.querySelector(`.tool-card[data-family="${family}"]`);
    if (!card) return;
    [card.querySelector('input[aria-label^="Referência"]'), card.querySelector('input[aria-label^="Lote"]')]
      .filter(Boolean)
      .forEach(input => input.addEventListener("input", () => {
        if (toolSelections[family]) {
          delete toolSelections[family];
          if (pickerFamily === family && pickerSelectionCount) {
            pickerSelectionCount.textContent =
              "Seleção registada substituída por edição manual — a associação física foi removida.";
          }
        }
      }));
  });

  function resetPickerState() {
    Object.keys(toolSelections).forEach(key => delete toolSelections[key]);
    selectedOption = null;
    if (pickerReferenceFilter && initialPickerReference !== undefined) {
      pickerReferenceFilter.value = initialPickerReference;
    }
    pickerMessage(PICKER_EMPTY_MESSAGE);
    if (pickerSelectionCount) pickerSelectionCount.textContent = "Sem opção selecionada.";
    if (applyToolSelectionButton) applyToolSelectionButton.disabled = true;
  }

  // Reference filter (fragment) — server-side real-data filtering.
  let pickerFilterTimer = null;
  pickerReferenceFilter?.addEventListener("input", () => {
    clearTimeout(pickerFilterTimer);
    pickerFilterTimer = setTimeout(loadPickerOptions, 250);
  });
  $("#pickerClear")?.addEventListener("click", () => {
    if (pickerReferenceFilter) pickerReferenceFilter.value = "";
    loadPickerOptions();
  });
  applyToolSelectionButton?.addEventListener("click", applySelectedTool);

  function triggerPickerChange(familyCode) {
    pickerFamily = familyCode;
    const title = $("#pickerTitle");
    if (title) title.textContent = `Alterar ${familyCode} associado`;
    $("#inventoryPicker")?.scrollIntoView({ behavior: "smooth", block: "center" });
    loadPickerOptions();
  }

  $("#editSheet")?.addEventListener("click", () => setMode("edit"));
  $("#saveSheet")?.addEventListener("click", () => {
    const errorEl = $("#saveRevisionError");
    if (errorEl) { errorEl.textContent = ""; errorEl.classList.remove("visible"); }
    if (saveRevisionForm) saveRevisionForm.reset();
    if (saveRevisionDialog && typeof saveRevisionDialog.showModal === "function") {
      saveRevisionDialog.showModal();
    }
  });
  saveRevisionCancel?.addEventListener("click", () => saveRevisionDialog?.close());
  saveRevisionDialog?.addEventListener("click", event => { if (event.target === saveRevisionDialog) saveRevisionDialog.close(); });
  saveRevisionForm?.addEventListener("submit", async event => {
    event.preventDefault();
    const errorEl = $("#saveRevisionError");
    const submit = $("#saveRevisionSubmit");
    const showError = message => {
      if (errorEl) { errorEl.textContent = message; errorEl.classList.add("visible"); }
    };
    if (!jobOnIdForSave) { showError("Não foi possível identificar o Job On."); return; }
    // Server-side authority: the change reason stays optional here, but the service
    // rejects a save of a fechado revision without it (JOBON_CHANGE_REASON_REQUIRED).
    const changeReason = saveRevisionForm?.elements.changeReason?.value.trim() || null;
    if (changeReasonRequired && !changeReason) {
      showError("Alterar uma produção fechada exige um motivo.");
      return;
    }
    const payload = {
      jobOnId: jobOnIdForSave,
      generalNotes: $(".general-notes textarea")?.value ?? null,
      changeReason,
      imageAssetId: null,
      components: buildEditedComponentsGraph()
    };
    submit.disabled = true;
    try {
      const response = await fetch(`/api/jobon/${encodeURIComponent(jobOnIdForSave)}/revision`, {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (response.ok) {
        // Reopen the SAME Job On folha, now rendering the new current revision.
        window.location.assign(`/jobon?id=${encodeURIComponent(jobOnIdForSave)}`);
        return;
      }
      let message = "Não foi possível guardar a nova revisão. Verifique os dados e tente novamente.";
      try {
        const body = await response.json();
        if (body && body.message) message = body.message;
      } catch { /* keep the default message */ }
      showError(message);
    } catch {
      showError("Não foi possível guardar a nova revisão. Verifique a ligação e tente novamente.");
    } finally {
      submit.disabled = false;
    }
  });
  $("#cancelEdit")?.addEventListener("click", () => {
    // CANCEL EDIT — discards client-side edits and exits edit mode. This is a
    // pure client-side reset: it performs ZERO writes (never calls the backend).
    setMode("view");
    restoreOriginalValues();
  });
  $$(".tool-change").forEach(button => button.addEventListener("click", () => {
    triggerPickerChange(button.dataset.family);
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

  // =============================================================
  // REAL VERIFICATION-CONFIRMATION FLOW — "Confirmar verificação"
  // (POST /api/jobon/{id}/verifications/{occurrenceId}/confirm).
  //
  // Marking a PENDING checkbox is the ONLY confirmation surface
  // (modules/05 §7, 05_BRIEF_VERIFICATIONS §10): the click shows
  // processing, persists the confirmation server-side — the operator is
  // resolved from the authenticated session and the timestamp is
  // generated on the server, never sent by the client — and the
  // persisted state is reloaded on success (the confirmed row, the
  // who/when of the persisted confirmation and the pending counter
  // all render from the server). A failed confirmation keeps the
  // occurrence pending and visible.
  //
  // The checkbox is server-rendered actionable only for users with
  // jobon.confirmar (disabled otherwise); the route-level capability
  // policy + the service gate fail closed server-side regardless.
  // Unchecking / cancelling performs ZERO writes.
  // =============================================================
  const checksSection = $("#checksSection");
  const checkListError = $("#checkListError");
  const jobOnIdForChecks = $("meta[name='jobon-id']")?.content;

  const showCheckError = message => {
    if (!checkListError) return;
    checkListError.textContent = message;
    checkListError.classList.add("visible");
  };
  const hideCheckError = () => {
    if (!checkListError) return;
    checkListError.textContent = "";
    checkListError.classList.remove("visible");
  };

  $$(".check-row", checksSection).forEach(row => {
    const checkbox = row.querySelector("input[type='checkbox']");
    if (!checkbox || checkbox.disabled) return; // unauthorized: server-rendered disabled
    const occurrenceId = row.dataset.occurrenceId;

    if (row.classList.contains("confirmed")) {
      // Persisted confirmation display: re-clicking must never uncheck it —
      // the server-rendered state is the source of truth (zero writes).
      checkbox.addEventListener("change", () => { checkbox.checked = true; });
      return;
    }

    if (!occurrenceId || !jobOnIdForChecks) return;

    let inflight = false;
    checkbox.addEventListener("change", async () => {
      if (inflight) return; // a duplicate click while processing is swallowed
      if (!checkbox.checked) return; // unchecking = cancel — zero writes
      inflight = true;
      hideCheckError();
      checkbox.disabled = true; // processing state until the server answers
      try {
        const response = await fetch(
          `/api/jobon/${encodeURIComponent(jobOnIdForChecks)}/verifications/${encodeURIComponent(occurrenceId)}/confirm`,
          { method: "POST", credentials: "same-origin" });
        // A capability denial surfaces as a redirect (App denial contract) —
        // treat it as a denial, never as a success.
        if (response.redirected) throw new Error("Sem permissão para confirmar verificações.");
        if (response.ok) {
          // Persisted: reopen the SAME folha rendering the confirmed row,
          // the persisted who/when and the updated pending counter.
          window.location.assign(`/jobon?id=${encodeURIComponent(jobOnIdForChecks)}`);
          return;
        }
        let message = "Não foi possível confirmar a verificação. Verifique a ligação e tente novamente.";
        try {
          const body = await response.json();
          if (body && body.message) message = body.message;
        } catch { /* keep the default message */ }
        checkbox.checked = false; // failure keeps the occurrence pending + visible
        showCheckError(message);
      } catch (error) {
        checkbox.checked = false;
        showCheckError(error?.message || "Não foi possível confirmar a verificação. Verifique a ligação e tente novamente.");
      } finally {
        checkbox.disabled = false;
        inflight = false;
      }
    });
  });

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
    // Documented hand-off rule: never print from unsaved DOM values — the sheet
    // must be saved as a new revision (or the edit cancelled) before printing.
    if (document.body.dataset.mode === "edit") {
      alert("Guarde a nova revisão antes de imprimir.");
      return;
    }
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
