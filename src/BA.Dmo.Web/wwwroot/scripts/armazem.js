/* ============================================================
   BA DMO — armazem.js (U-14)
   Non-authoritative interaction/bootstrap wiring only. Domain
   logic (occupation 1:1, 4-digit positions,
   fora derived, two-reference warning, actor attribution) lives
   in C#: this file NEVER duplicates rules. It calls the canonical
   backend endpoints and renders returned engine results.
   ============================================================ */
(function () {
  "use strict";

  const say = (text, ok) => {
    const toast = document.getElementById("toast");
    if (!toast) return;
    toast.textContent = text;
    toast.classList.add("show");
    toast.classList.remove("error");
    if (ok === false) toast.classList.add("error");
    setTimeout(() => toast.classList.remove("show"), 2200);
  };

  const el = (id) => document.getElementById(id);

  async function api(url, options) {
    const res = await fetch(url, options);
    if (!res.ok) {
      let message = "Não foi possível concluir o pedido.";
      try { const body = await res.json(); message = body.message || message; } catch (_) { /* ignore */ }
      const error = new Error(message);
      error.status = res.status;
      throw error;
    }
    const ct = res.headers.get("content-type") || "";
    if (ct.indexOf("application/json") >= 0) return await res.json();
    return null;
  }

  const json = (method, body) => ({
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });

  // ---- Tabs (Registo / Consulta) ----
  document.querySelectorAll(".armazem-tabs .tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      const view = tab.dataset.view;
      document.querySelectorAll(".armazem-tabs .tab").forEach((t) => t.classList.toggle("active", t === tab));
      document.querySelectorAll(".armazem-view").forEach((v) => v.classList.toggle("active", v.id === view));
      if (view === "consulta" && !consultationLoaded) runSeek();
      if (view === "historico" && !historyLoaded) loadHistory();
    });
  });

  // ---- Inline cards (Entrada / Saída) ----
  document.querySelectorAll("[data-open]").forEach((btn) => {
    btn.addEventListener("click", () => {
      document.querySelectorAll("#entradaForm,#saidaForm,#novoForm").forEach((c) => c.hidden = true);
      const empty = document.querySelector(".armazem-empty");
      if (empty) empty.hidden = true;
      const form = el(btn.dataset.open + "Form");
      if (form) form.hidden = false;
    });
  });
  document.querySelectorAll("[data-close]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const card = btn.closest(".armazem-card");
      if (card) card.hidden = true;
      const empty = document.querySelector(".armazem-empty");
      if (empty) empty.hidden = false;
    });
  });

  function readForm(formId) {
    const form = el(formId);
    const obj = {};
    form.querySelectorAll("input,select").forEach((field) => {
      const key = field.id;
      if (key) obj[key] = field.value;
    });
    return obj;
  }

  // ---- Submit handlers ----
  el("entradaForm").querySelector("[data-submit]").addEventListener("click", async () => {
    const v = readForm("entradaForm");
    try {
      const stockId = await api("/api/armazem/entrada", json("POST", {
        toolType: v.entradaType, reference: v.entradaRef, lot: v.entradaLot,
        positionCode: v.entradaPosition, destination: null, observations: v.entradaObs
      }));
      say("Entrada registada.");
      el("entradaForm").hidden = true;
      clearForm("entradaForm");
      el("registo").querySelector(".armazem-empty").hidden = false;
      await loadRecent();
    } catch (e) { say(e.message, false); }
  });

  el("saidaForm").querySelector("[data-submit]").addEventListener("click", async () => {
    const v = readForm("saidaForm");
    try {
      await api("/api/armazem/saida", json("POST", {
        toolType: v.saidaType, reference: v.saidaRef, lot: v.saidaLot,
        destination: v.saidaDest || null, observations: v.saidaObs
      }));
      say("Saída registada.");
      el("saidaForm").hidden = true;
      clearForm("saidaForm");
      el("registo").querySelector(".armazem-empty").hidden = false;
      await loadRecent();
    } catch (e) { say(e.message, false); }
  });

  const novoForm = el("novoForm");
  if (novoForm) {
    el("saveNewTool").addEventListener("click", async () => {
      const v = readForm("novoForm");
      const allowedLines = Array.from(novoForm.querySelectorAll("#novoLines input:checked"))
        .map((input) => input.value);
      const quantity = v.novoQty === "" ? null : Number(v.novoQty);

      try {
        await api("/api/ferramentas/reference", json("POST", {
          toolType: v.novoType,
          refCode: v.novoRef,
          technicalName: v.novoTechnicalName || null,
          ownerPlant: v.novoOwnerPlant || null,
          lote: v.novoLot,
          qty: quantity,
          allowedLines,
          drawingCode: v.novoDrawing || null,
          drawingRevision: v.novoDrawingRevision || null,
          processo: v.novoProcess || null
        }));
      } catch (e) {
        say(e.message, false);
        return;
      }

      try {
        await api("/api/armazem/entrada", json("POST", {
          toolType: v.novoType,
          reference: v.novoRef,
          lot: v.novoLot,
          positionCode: v.novoPosition,
          destination: null,
          observations: v.novoObservations || null
        }));
        say("Ferramenta criada e Entrada registada.");
        novoForm.hidden = true;
        clearForm("novoForm");
        el("registo").querySelector(".armazem-empty").hidden = false;
        await loadRecent();
      } catch (e) {
        // The Ferramentas master is valid and must not be duplicated. Move the
        // user into the normal Entrada form with the created identity prefilled.
        novoForm.hidden = true;
        el("entradaType").value = v.novoType;
        el("entradaRef").value = v.novoRef;
        el("entradaLot").value = v.novoLot;
        el("entradaPosition").value = v.novoPosition;
        el("entradaObs").value = v.novoObservations;
        el("entradaForm").hidden = false;
        say("Master criado em Ferramentas; a Entrada não foi registada: " + e.message, false);
      }
    });
  }

  // ---- Recent warehouse movements (real movement facts only) ----
  let recentRows = [];

  function movementKind(row) {
    if (row.destination === "correcao_localizacao") return "correcao";
    return row.direction === "in" ? "entrada" : "saida";
  }

  function movementLabel(kind) {
    if (kind === "entrada") return "Entrada";
    if (kind === "saida") return "Saída";
    return "Correção";
  }

  function formatMovementDate(value) {
    const date = new Date(value);
    if (Number.isNaN(date.valueOf())) return "—";
    return new Intl.DateTimeFormat("pt-PT", {
      day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit"
    }).format(date).replace(",", " ·");
  }

  function appendRecentCell(row, text, strong) {
    const cell = document.createElement("td");
    const value = text == null || text === "" ? "—" : String(text);
    if (strong && value !== "—") {
      const element = document.createElement("strong");
      element.textContent = value;
      cell.appendChild(element);
    } else {
      cell.textContent = value;
    }
    row.appendChild(cell);
  }

  function renderRecent() {
    const body = el("recentBody");
    if (!body) return;
    const query = el("recentSearch").value.trim().toLocaleLowerCase("pt-PT");
    const movement = el("recentMovement").value;
    const limit = Number(el("recentLimit").value) || 20;
    const visible = recentRows.filter((row) => {
      const kind = movementKind(row);
      const searchable = [row.type, row.reference, row.lot, row.positionCode,
        row.destination, row.actorId, movementLabel(kind)].filter(Boolean).join(" ").toLocaleLowerCase("pt-PT");
      return (!movement || movement === kind) && (!query || searchable.includes(query));
    }).slice(0, limit);

    body.replaceChildren();
    if (!visible.length) {
      const row = document.createElement("tr");
      const cell = document.createElement("td");
      cell.colSpan = 8;
      cell.className = "empty";
      cell.textContent = recentRows.length ? "Sem movimentos com estes filtros." : "Sem movimentos registados.";
      row.appendChild(cell);
      body.appendChild(row);
    } else {
      visible.forEach((item) => {
        const row = document.createElement("tr");
        const kind = movementKind(item);
        appendRecentCell(row, formatMovementDate(item.occurredAtUtc));
        appendRecentCell(row, item.type);
        appendRecentCell(row, item.reference, true);
        // Lote content is rendered exactly as stored (for example "4"), never
        // with the filename-only "L" prefix.
        appendRecentCell(row, item.lot);
        const movementCell = document.createElement("td");
        const badge = document.createElement("span");
        badge.className = "dmo-pill " + (kind === "entrada" ? "approved" : "pending");
        badge.textContent = movementLabel(kind);
        movementCell.appendChild(badge);
        row.appendChild(movementCell);
        appendRecentCell(row, item.positionCode);
        appendRecentCell(row, item.destination === "correcao_localizacao" ? "Correção de localização" : item.destination);
        appendRecentCell(row, item.actorId);
        body.appendChild(row);
      });
    }

    el("recentCount").textContent = visible.length + " registo(s) · Página 1 de 1";
  }

  async function loadRecent() {
    const body = el("recentBody");
    if (!body) return;
    try {
      recentRows = await api("/api/armazem/movimentos?limit=60") || [];
      historyLoaded = false;
      renderRecent();
    } catch (error) {
      recentRows = [];
      body.innerHTML = '<tr><td colspan="8" class="empty"></td></tr>';
      body.querySelector("td").textContent = error.message;
      el("recentCount").textContent = "0 registos · Página 1 de 1";
    }
  }

  ["recentSearch", "recentMovement", "recentLimit"].forEach((id) => {
    const control = el(id);
    if (control) control.addEventListener(id === "recentSearch" ? "input" : "change", renderRecent);
  });

  // ---- Movement-backed history calendar ----
  let historyRows = [];
  let historyLoaded = false;
  let historyMonth = new Date();
  historyMonth = new Date(historyMonth.getFullYear(), historyMonth.getMonth(), 1);
  let selectedHistoryDate = null;

  function localDateKey(value) {
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.valueOf())) return "";
    return date.getFullYear() + "-" + String(date.getMonth() + 1).padStart(2, "0") + "-" +
      String(date.getDate()).padStart(2, "0");
  }

  function historyMonthText() {
    const text = new Intl.DateTimeFormat("pt-PT", { month: "long", year: "numeric" })
      .format(historyMonth).replace(" de ", " ");
    return text.charAt(0).toLocaleUpperCase("pt-PT") + text.slice(1);
  }

  function populateHistoryOperators() {
    const select = el("historyOperator");
    const current = select.value;
    select.querySelectorAll("option:not(:first-child)").forEach((option) => option.remove());
    Array.from(new Set(historyRows.map((row) => row.actorId).filter(Boolean)))
      .sort((a, b) => a.localeCompare(b, "pt-PT"))
      .forEach((actor) => {
        const option = document.createElement("option");
        option.value = actor;
        option.textContent = actor;
        select.appendChild(option);
      });
    select.value = current;
  }

  function renderHistoryCalendar() {
    const grid = el("historyCalendarGrid");
    const label = historyMonthText();
    el("historyMonthLabel").textContent = label;
    el("historyMonthPill").textContent = label;
    grid.replaceChildren();

    const firstWeekday = (new Date(historyMonth.getFullYear(), historyMonth.getMonth(), 1).getDay() + 6) % 7;
    const daysInMonth = new Date(historyMonth.getFullYear(), historyMonth.getMonth() + 1, 0).getDate();
    const recordDays = new Set(historyRows.map((row) => localDateKey(row.occurredAtUtc)));
    for (let index = 0; index < firstWeekday; index += 1) {
      const placeholder = document.createElement("button");
      placeholder.type = "button";
      placeholder.className = "armazem-calendar-day placeholder";
      placeholder.disabled = true;
      placeholder.setAttribute("aria-hidden", "true");
      grid.appendChild(placeholder);
    }

    for (let day = 1; day <= daysInMonth; day += 1) {
      const date = new Date(historyMonth.getFullYear(), historyMonth.getMonth(), day);
      const key = localDateKey(date);
      const button = document.createElement("button");
      button.type = "button";
      button.className = "armazem-calendar-day";
      if (recordDays.has(key)) button.classList.add("has-record");
      if (selectedHistoryDate === key) button.classList.add("selected");
      button.textContent = String(day);
      button.setAttribute("aria-label", new Intl.DateTimeFormat("pt-PT", { dateStyle: "long" }).format(date));
      button.addEventListener("click", () => {
        selectedHistoryDate = selectedHistoryDate === key ? null : key;
        renderHistoryCalendar();
        renderHistoryRows();
      });
      grid.appendChild(button);
    }
  }

  function renderHistoryRows() {
    const body = el("historicoBody");
    if (!body) return;
    const query = el("historyQuery").value.trim().toLocaleLowerCase("pt-PT");
    const type = el("historyToolType").value;
    const movement = el("historyMovement").value;
    const actor = el("historyOperator").value;
    const limit = Number(el("historyLimit").value) || 20;
    const month = historyMonth.getMonth();
    const year = historyMonth.getFullYear();
    const rows = historyRows.filter((row) => {
      const occurred = new Date(row.occurredAtUtc);
      const kind = movementKind(row);
      const searchable = [row.reference, row.lot, row.positionCode, row.destination,
        row.actorId, row.type, movementLabel(kind)].filter(Boolean).join(" ").toLocaleLowerCase("pt-PT");
      return occurred.getFullYear() === year && occurred.getMonth() === month &&
        (!selectedHistoryDate || localDateKey(occurred) === selectedHistoryDate) &&
        (!query || searchable.includes(query)) && (!type || row.type === type) &&
        (!movement || kind === movement) && (!actor || row.actorId === actor);
    }).slice(0, limit);

    body.replaceChildren();
    if (!rows.length) {
      const row = document.createElement("tr");
      const cell = document.createElement("td");
      cell.colSpan = 8;
      cell.className = "empty";
      cell.textContent = "Sem movimentos no período.";
      row.appendChild(cell);
      body.appendChild(row);
    } else {
      rows.forEach((item) => {
        const row = document.createElement("tr");
        const kind = movementKind(item);
        row.className = kind === "entrada" ? "armazem-history-in" : "armazem-history-out";
        appendRecentCell(row, formatMovementDate(item.occurredAtUtc));
        appendRecentCell(row, item.type);
        appendRecentCell(row, item.reference, true);
        appendRecentCell(row, item.lot);
        const movementCell = document.createElement("td");
        const badge = document.createElement("span");
        badge.className = "dmo-pill " + (kind === "entrada" ? "approved" : "pending");
        badge.textContent = movementLabel(kind);
        movementCell.appendChild(badge);
        row.appendChild(movementCell);
        appendRecentCell(row, item.positionCode);
        appendRecentCell(row, item.destination === "correcao_localizacao" ? "Correção de localização" : item.destination);
        appendRecentCell(row, item.actorId);
        body.appendChild(row);
      });
    }
    el("historyCount").textContent = rows.length + " movimento(s) · Página 1 de 1";
  }

  async function loadHistory() {
    try {
      historyRows = await api("/api/armazem/movimentos?limit=500") || [];
      historyLoaded = true;
      populateHistoryOperators();
      renderHistoryCalendar();
      renderHistoryRows();
    } catch (error) {
      historyRows = [];
      const body = el("historicoBody");
      body.innerHTML = '<tr><td colspan="8" class="empty"></td></tr>';
      body.querySelector("td").textContent = error.message;
      renderHistoryCalendar();
    }
  }

  el("historyPreviousMonth").addEventListener("click", () => {
    historyMonth = new Date(historyMonth.getFullYear(), historyMonth.getMonth() - 1, 1);
    selectedHistoryDate = null;
    renderHistoryCalendar();
    renderHistoryRows();
  });
  el("historyNextMonth").addEventListener("click", () => {
    historyMonth = new Date(historyMonth.getFullYear(), historyMonth.getMonth() + 1, 1);
    selectedHistoryDate = null;
    renderHistoryCalendar();
    renderHistoryRows();
  });
  ["historyQuery", "historyToolType", "historyMovement", "historyOperator", "historyLimit"].forEach((id) => {
    const control = el(id);
    control.addEventListener(id === "historyQuery" ? "input" : "change", renderHistoryRows);
  });
  el("clearHistoryFilters").addEventListener("click", () => {
    ["historyQuery", "historyToolType", "historyMovement", "historyOperator"].forEach((id) => (el(id).value = ""));
    el("historyLimit").value = "20";
    selectedHistoryDate = null;
    renderHistoryCalendar();
    renderHistoryRows();
  });

  // ---- Consultation ----
  let selectedConsultationRow = null;
  let consultationRows = [];
  let consultationLoaded = false;

  async function runSeek() {
    try {
      consultationRows = await api("/api/armazem/consulta") || [];
      consultationLoaded = true;
      renderRows();
    } catch (e) { say(e.message, false); }
  }

  function renderRows() {
    const body = el("consultationBody");
    if (!body) return;
    const query = el("queryText").value.trim().toLocaleLowerCase("pt-PT");
    const type = el("queryType").value;
    const context = el("queryContext").value;
    const verification = el("queryVerification").value;
    const limit = Number(el("queryLimit").value) || 20;
    const rows = consultationRows.filter((row) => {
      const searchable = [row.type, row.reference, row.technicalName, row.lot,
        row.positionCode, locationLabel(row.locationContext)].filter(Boolean).join(" ").toLocaleLowerCase("pt-PT");
      const alertMatches = !verification ||
        (verification === "conflict" && row.hasReferenceConflict) ||
        (verification === "clear" && !row.hasReferenceConflict);
      return (!query || searchable.includes(query)) &&
        (!type || row.type === type) &&
        (!context || row.locationContext === context) && alertMatches;
    }).slice(0, limit);

    selectedConsultationRow = null;
    el("correctLocation").disabled = true;
    el("correctionForm").hidden = true;
    body.replaceChildren();
    if (!rows.length) {
      const row = document.createElement("tr");
      const cell = document.createElement("td");
      cell.colSpan = 7;
      cell.className = "empty";
      cell.textContent = consultationRows.length ? "Sem ferramentas com estes filtros." : "Sem ferramentas registadas.";
      row.appendChild(cell);
      body.appendChild(row);
      el("consultationCount").textContent = "0 registos · Página 1 de 1";
      return;
    }
    rows.forEach((row) => {
      const tr = document.createElement("tr");
      const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
      tr.appendChild(td(row.type));
      tr.appendChild(td(row.reference));
      // Visible Lote content stays exactly as stored; the L-prefix is reserved
      // exclusively for filenames such as CM_5447_L4.
      tr.appendChild(td(row.lot));
      tr.appendChild(td(row.positionCode));
      tr.appendChild(td(locationLabel(row.locationContext)));
      tr.appendChild(td(row.technicalName));
      const alertTd = document.createElement("td");
      if (row.hasReferenceConflict) {
        const badge = document.createElement("span");
        badge.className = "dmo-pill warning";
        badge.textContent = "Conflito de referências";
        alertTd.appendChild(badge);
      } else {
        alertTd.textContent = "—";
      }
      tr.appendChild(alertTd);
      tr.tabIndex = 0;
      tr.setAttribute("aria-selected", "false");
      const selectRow = () => {
        body.querySelectorAll("tr.selected").forEach((candidate) => {
          candidate.classList.remove("selected");
          candidate.setAttribute("aria-selected", "false");
        });
        tr.classList.add("selected");
        tr.setAttribute("aria-selected", "true");
        selectedConsultationRow = row;
        el("correctLocation").disabled = false;
      };
      tr.addEventListener("click", selectRow);
      tr.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          selectRow();
        }
      });
      body.appendChild(tr);
    });
    el("consultationCount").textContent = rows.length + " registo(s) · Página 1 de 1";
  }

  function locationLabel(ctx) {
    if (ctx === "armazem") return "Armazém";
    if (ctx === "fora") return "Fora do armazém";
    return "Localização operacional não registada";
  }

  ["queryText", "queryType", "queryContext", "queryVerification", "queryLimit"].forEach((id) => {
    const control = el(id);
    control.addEventListener(id === "queryText" ? "input" : "change", renderRows);
  });
  el("clearQuery").addEventListener("click", () => {
    ["queryText", "queryType", "queryContext", "queryVerification"].forEach((id) => (el(id).value = ""));
    el("queryLimit").value = "20";
    renderRows();
  });

  // ---- Auditable physical-location correction ----
  el("correctLocation").addEventListener("click", () => {
    if (!selectedConsultationRow) return;
    el("correctionTool").value = selectedConsultationRow.type + " · " +
      selectedConsultationRow.reference + " · Lote " + selectedConsultationRow.lot;
    el("correctionRegisteredPosition").value = selectedConsultationRow.positionCode || "Fora do Armazém";
    el("correctionFoundPosition").value = selectedConsultationRow.positionCode || "";
    el("correctionFoundPosition").disabled = false;
    el("correctionNotPresent").checked = false;
    el("correctionObservations").value = "";
    el("correctionForm").hidden = false;
    el("correctionFoundPosition").focus();
  });

  el("correctionNotPresent").addEventListener("change", () => {
    const notPresent = el("correctionNotPresent").checked;
    if (notPresent) el("correctionFoundPosition").value = "";
    el("correctionFoundPosition").disabled = notPresent;
  });

  document.querySelector("[data-correction-close]").addEventListener("click", () => {
    el("correctionForm").hidden = true;
  });

  el("saveLocationCorrection").addEventListener("click", async () => {
    if (!selectedConsultationRow) return;
    const foundPosition = el("correctionNotPresent").checked
      ? null
      : el("correctionFoundPosition").value;
    try {
      await api("/api/armazem/corrigir-localizacao", json("POST", {
        toolId: selectedConsultationRow.toolId,
        foundPositionCode: foundPosition,
        observations: el("correctionObservations").value || null
      }));
      say("Localização corrigida.");
      el("correctionForm").hidden = true;
      await runSeek();
      await loadRecent();
    } catch (e) { say(e.message, false); }
  });

  const params = new URLSearchParams(window.location.search);
  if (params.has("position")) {
    el("queryText").value = params.get("position");
    document.querySelector(".armazem-tabs .tab[data-view='consulta']").click();
  }

  function clearForm(id) {
    el(id).querySelectorAll("input,select").forEach((f) => { if (f.tagName === "SELECT") f.selectedIndex = 0; else f.value = ""; });
  }

  loadRecent();
})();
