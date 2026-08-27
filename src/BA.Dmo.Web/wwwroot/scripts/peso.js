/* ============================================================
   BA DMO — peso.js (U-10)
   Non-authoritative interaction only. Domain logic (weight/volume
   formulas, validation, persistence, authorization) lives in C#:
   this file NEVER duplicates the density/glass/volume formulas
   (GLM-PESO-05/13). The live reading preview asks the server side
   (/api/peso/...) and renders the returned engine result.
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
  const authHeader = () => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    return token ? token.value : "";
  };

  // ---- tab switching (Operador AND Responsável) ----
  document.querySelectorAll(".peso-tabs .tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      const view = tab.dataset.view;
      document.querySelectorAll(".peso-tabs .tab").forEach((t) => t.classList.toggle("active", t === tab));
      document.querySelectorAll(".peso-view").forEach((v) => v.classList.toggle("active", v.id === view));
    });
  });

  // ---- Novo controlo readings (Operador) ----
  function prepareReading(reading) {
    const pesoInput = reading.querySelector("input.peso");
    const cmInput = reading.querySelector("input.cm");
    if (pesoInput) {
      pesoInput.classList.add("decimal-2");
      pesoInput.step = "0.01";
      if (!reading.querySelector(".peso-reading-result")) {
        reading.insertAdjacentHTML("beforeend", '<small class="peso-reading-result pending">Peso do vidro: —</small>');
      }
      const update = () => {
        const result = reading.querySelector(".peso-reading-result");
        // The server is the engine; local updates only reset the pending marker.
        // No formula is computed here (GLM-PESO-05/13).
        result.textContent = "Peso do vidro: —";
        result.classList.add("pending");
        pesoInput.dataset.dirty = "1";
      };
      pesoInput.addEventListener("input", update);
      pesoInput.addEventListener("blur", () => okDirty());
    }
    if (cmInput) cmInput.addEventListener("input", () => okDirty());
  }

  // Track that a re-calculation is required after editing readings.
  let dirty = false;
  let comparisonMapping = null;
  function invalidateComparisonMapping() {
    comparisonMapping = null;
    const context = el("comparisonPreviousContext");
    const wrap = el("comparisonPairingWrap");
    const table = el("comparisonPairingTable");
    if (context) { context.hidden = true; context.innerHTML = ""; }
    if (wrap) wrap.hidden = true;
    if (table) table.innerHTML = "";
    if (el("createComparison")) el("createComparison").disabled = true;
  }
  function markDirty() { dirty = true; invalidateComparisonMapping(); }
  function okDirty() { markDirty(); }

  function addReading(containerId) {
    const container = document.querySelector(containerId);
    if (!container) return;
    const index = container.querySelectorAll(".peso-reading").length + 1;
    const div = document.createElement("div");
    div.className = "peso-reading";
    div.innerHTML =
      '<div class="dmo-field"><label>CM</label><input class="cm" type="number" step="1" placeholder="Ex.: ' + index * 10 + '"></div>' +
      '<div class="dmo-field"><label>Peso (g)</label><input class="peso decimal-2" type="number" step="0.01" placeholder="0,00"></div>' +
      '<small class="peso-reading-result pending">Peso do vidro: —</small>';
    container.appendChild(div);
    prepareReading(div);
    say("Leitura adicionada");
  }
  function removeReading(containerId) {
    const items = document.querySelectorAll(containerId + " .peso-reading");
    if (items.length <= 1) { say("É necessária pelo menos uma leitura", false); return; }
    items[items.length - 1].remove();
    markDirty();
  }
  if (el("addReading")) el("addReading").addEventListener("click", () => addReading("#readings"));
  if (el("removeReading")) el("removeReading").addEventListener("click", () => removeReading("#readings"));

  // ---- machine-choice toggle (Operador, lot creation) ----
  document.querySelectorAll("#machineGrid .peso-machine-choice").forEach((btn) => {
    btn.addEventListener("click", () => btn.classList.toggle("selected"));
  });

  // ---- Resolved report path preview ----
  const subfolderInput = el("reportSubfolder");
  const resolvedPath = el("resolvedPath");
  if (subfolderInput && resolvedPath) {
    const updatePath = () => { resolvedPath.value = "Capacidades / " + (subfolderInput.value || "").replace(/^\//, ""); };
    subfolderInput.addEventListener("input", updatePath);
    updatePath();
  }

  // ---- Result formatting helpers (presentation only, no calculation) ----
  const fmt = (v) => (v === null || v === undefined || Number.isNaN(Number(v))) ? "—" : Number(v).toFixed(2);

  // ---- Live calculation preview (server is the engine) ----
  const calculateBtn = el("calculate");
  if (calculateBtn) {
    calculateBtn.addEventListener("click", async () => {
      try {
        const controlId = await ensureControlId();
        if (!controlId) return;
        await saveControl(controlId);
        const res = await fetch("/api/peso/" + controlId + "/calculate", {
          method: "POST",
          headers: { "RequestVerificationToken": authHeader() }
        });
        const data = await res.json();
        if (!res.ok) { say(data.message || "Falha ao calcular", false); return; }
        renderCalculation(data);
        dirty = false;
        say("Resultados calculados pelo motor do domínio (C#)");
      } catch (err) {
        say("Erro de rede: " + err.message, false);
      }
    });
  }

  function renderCalculation(data) {
    if (el("resDensidade")) el("resDensidade").textContent = fmt(data.densidade);
    if (el("resCapacidade")) el("resCapacidade").textContent = fmt(data.capacidadeMedia);
    if (el("resPesoMedio")) el("resPesoMedio").textContent = fmt(data.pesoMedio);
    if (el("resDiferenca")) el("resDiferenca").textContent = fmt(data.diferenca);
    if (el("resDiferencaPct")) el("resDiferencaPct").textContent = data.diferencaPct != null ? fmt(data.diferencaPct) + " %" : "—";

    // Per-CM table
    const tbody = el("resultTable");
    if (tbody && Array.isArray(data.rows)) {
      tbody.innerHTML = "";
      if (data.rows.length === 0) {
        tbody.innerHTML = '<tr><td colspan="6" class="empty">Sem leituras válidas.</td></tr>';
      } else {
        data.rows.forEach((r) => {
          const tr = document.createElement("tr");
          tr.innerHTML =
            "<td>" + r.cmNumber + "</td>" +
            "<td>" + fmt(r.pesoEmAgua) + "</td>" +
            "<td>" + fmt(r.capacidade) + "</td>" +
            "<td>—</td><td>—</td>" +
            "<td>" + fmt(r.pesoVidro) + "</td>";
          tbody.appendChild(tr);
        });
      }
    }
    loadApprovedPreviousControls();
  }

  // ---- Explicit per-CM glass-weight comparison (inside Novo Controlo) ----
  async function loadApprovedPreviousControls() {
    const select = el("comparisonPreviousControl");
    if (!select || select.dataset.loaded === "1") return;
    try {
      const res = await fetch("/api/peso/controls?status=aprovado&type=novo_controlo", {
        headers: { "RequestVerificationToken": authHeader() }
      });
      const items = await res.json();
      if (!res.ok) return;
      const currentId = el("activeControlId")?.value || "";
      select.innerHTML = '<option value="">Selecionar produção…</option>';
      (items || []).filter((c) => c.controlId !== currentId).forEach((c) => {
        const option = document.createElement("option");
        option.value = c.controlId;
        option.textContent = (c.reference || "—") + " · " + (c.production || "—") +
          " · " + (c.machine || "—") + " · Lote " + (c.lote || "—") +
          " · Rev. " + (c.revision || 1);
        select.appendChild(option);
      });
      select.dataset.loaded = "1";
    } catch { /* comparison stays unavailable */ }
  }

  async function getControlAndCalculation(controlId) {
    const [detailResponse, calculationResponse] = await Promise.all([
      fetch("/api/peso/control/" + controlId, { headers: { "RequestVerificationToken": authHeader() } }),
      fetch("/api/peso/" + controlId + "/calculate", {
        method: "POST", headers: { "RequestVerificationToken": authHeader() }
      })
    ]);
    const detail = await detailResponse.json();
    const calculation = await calculationResponse.json();
    if (!detailResponse.ok || !calculationResponse.ok)
      throw new Error(detail.message || calculation.message || "Falha ao obter os pesos do vidro");
    return { detail, calculation };
  }

  function updatePairingState() {
    if (!comparisonMapping) return;
    const rows = [...document.querySelectorAll("#comparisonPairingTable tr[data-current-cm]")];
    const selected = rows.map((row) => row.querySelector("select")?.value || "");
    const complete = rows.length > 0 && selected.every(Boolean) && new Set(selected).size === selected.length;
    if (el("createComparison")) el("createComparison").disabled = !complete || dirty;
    if (el("submitComparison")) el("submitComparison").disabled = true;
    comparisonMapping.comparisonId = null;

    rows.forEach((row) => {
      const previous = comparisonMapping.previousRows.get(row.querySelector("select")?.value || "");
      row.querySelector("[data-previous-weight]").textContent = previous ? fmt(previous.pesoVidro) : "—";
      row.querySelector("[data-difference]").textContent = "—";
      row.querySelector("[data-percentage]").textContent = "—";
    });
  }

  const confirmComparisonPrevious = el("confirmComparisonPrevious");
  if (confirmComparisonPrevious) {
    confirmComparisonPrevious.addEventListener("click", async () => {
      const previousControlId = el("comparisonPreviousControl")?.value || "";
      if (!previousControlId) { say("Selecione a produção anterior aprovada", false); return; }
      try {
        const currentControlId = await ensureControlId();
        if (!currentControlId) return;
        await saveControl(currentControlId);
        const [current, previous] = await Promise.all([
          getControlAndCalculation(currentControlId),
          getControlAndCalculation(previousControlId)
        ]);
        const currentRows = new Map((current.calculation.rows || []).map((row) => [String(row.cmNumber), row]));
        const previousRows = new Map((previous.calculation.rows || []).map((row) => [String(row.cmNumber), row]));
        if (!currentRows.size || !previousRows.size ||
            [...currentRows.values()].some((row) => row.pesoVidro == null) ||
            [...previousRows.values()].some((row) => row.pesoVidro == null)) {
          say("Calcule pesos do vidro válidos para ambas as produções", false);
          return;
        }

        comparisonMapping = { currentControlId, previousControlId, currentRows, previousRows, comparisonId: null };
        dirty = false;
        const context = el("comparisonPreviousContext");
        if (context) {
          context.hidden = false;
          context.innerHTML =
            "<div><span>Produção anterior</span><strong>" + (previous.detail.productionCode || "—") + "</strong></div>" +
            "<div><span>Job On</span><strong>" + (previous.detail.jobOnId || "—") + "</strong></div>" +
            "<div><span>Revisão Job On</span><strong>" + (previous.detail.jobOnRevisionId || "—") + "</strong></div>" +
            "<div><span>Linha</span><strong>" + (previous.detail.line || "—") + "</strong></div>" +
            "<div><span>Lote</span><strong>" + (previous.detail.lote || "—") + "</strong></div>";
        }

        const table = el("comparisonPairingTable");
        if (table) {
          table.innerHTML = "";
          currentRows.forEach((row, cm) => {
            const tr = document.createElement("tr");
            tr.setAttribute("data-current-cm", cm);
            const options = [...previousRows.keys()].map((previousCm) =>
              '<option value="' + previousCm + '">CM ' + previousCm + '</option>').join("");
            tr.innerHTML =
              "<td>" + cm + "</td>" +
              '<td><select aria-label="CM anterior para CM ' + cm + '"><option value="">Selecionar CM…</option>' + options + "</select></td>" +
              "<td>" + fmt(row.pesoVidro) + "</td>" +
              "<td data-previous-weight>—</td><td data-difference>—</td><td data-percentage>—</td>";
            table.appendChild(tr);
          });
          table.querySelectorAll("select").forEach((select) => select.addEventListener("change", updatePairingState));
        }
        if (el("comparisonPairingWrap")) el("comparisonPairingWrap").hidden = false;
        updatePairingState();
        say("Produção anterior confirmada; associe cada CM explicitamente");
      } catch (err) {
        say(err.message || "Falha ao confirmar a produção anterior", false);
      }
    });
  }

  const createComparison = el("createComparison");
  if (createComparison) {
    createComparison.addEventListener("click", async () => {
      if (!comparisonMapping || dirty) { say("Reconfirme a produção anterior após alterar leituras", false); return; }
      const pairs = [...document.querySelectorAll("#comparisonPairingTable tr[data-current-cm]")].map((row) => ({
        currentCmNumber: row.getAttribute("data-current-cm"),
        previousCmNumber: row.querySelector("select")?.value || ""
      }));
      try {
        const res = await fetch("/api/peso/comparison", {
          method: "POST",
          headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
          body: JSON.stringify({
            currentControlId: comparisonMapping.currentControlId,
            previousApprovedControlId: comparisonMapping.previousControlId,
            notas: el("notas")?.value || null,
            pairs
          })
        });
        const data = await res.json();
        if (!res.ok) { say(data.message || "Falha ao criar a comparação", false); return; }
        const detailResponse = await fetch("/api/peso/control/" + data.id, {
          headers: { "RequestVerificationToken": authHeader() }
        });
        const detail = await detailResponse.json();
        if (!detailResponse.ok) throw new Error(detail.message || "Falha ao rever a comparação");
        const snapshot = typeof detail.previousControlJson === "string"
          ? JSON.parse(detail.previousControlJson) : detail.previousControlJson;
        const table = el("comparisonPairingTable");
        if (table) {
          table.innerHTML = "";
          (snapshot.rows || []).forEach((row) => {
            table.innerHTML += "<tr>" +
              "<td>" + row.currentCmNumber + "</td><td>" + row.previousCmNumber + "</td>" +
              "<td>" + fmt(row.currentGlassWeight) + "</td><td>" + fmt(row.previousGlassWeight) + "</td>" +
              "<td>" + fmt(row.difference) + "</td><td>" + fmt(row.differencePercent) + " %</td></tr>";
          });
        }
        comparisonMapping.comparisonId = data.id;
        createComparison.disabled = true;
        if (el("submitComparison")) el("submitComparison").disabled = false;
        say("Tabela criada; reveja antes de enviar");
      } catch (err) {
        say(err.message || "Falha ao criar a comparação", false);
      }
    });
  }

  const submitComparison = el("submitComparison");
  if (submitComparison) {
    submitComparison.addEventListener("click", async () => {
      const comparisonId = comparisonMapping?.comparisonId;
      if (!comparisonId) return;
      const res = await fetch("/api/peso/" + comparisonId + "/submit", {
        method: "POST", headers: { "RequestVerificationToken": authHeader() }
      });
      const data = await res.json();
      if (!res.ok) { say(data.message || "Falha ao enviar a comparação", false); return; }
      submitComparison.disabled = true;
      say("Comparação enviada para aprovação");
    });
  }

  // ---- Save (draft) and Submit (explicit, never automatic) ----
  // Reads the current form into a create body (Operador).
  function collectReadings() {
    return [...document.querySelectorAll("#readings .peso-reading")].map((r) => ({
      cmNumber: r.querySelector(".cm")?.value || "",
      pesoEmAgua: Number(String(r.querySelector(".peso")?.value || "").replace(",", ".")) || null
    }));
  }

  function collectCreateBody() {
    return {
      jobOnId: document.querySelector('meta[name="jobon-id"]')?.content,
      controlDate: el("controlDate")?.value,
      temperaturaC: Number(el("temperatura")?.value) || null,
      estadoMolde: el("estadoMolde")?.value,
      notas: el("notas")?.value,
      leituras: collectReadings()
    };
  }

  async function saveControl(controlId) {
    const body = {
      controlId,
      temperaturaC: Number(el("temperatura")?.value) || null,
      estadoMolde: el("estadoMolde")?.value,
      notas: el("notas")?.value,
      leituras: collectReadings()
    };
    const res = await fetch("/api/peso/" + controlId + "/save", {
      method: "POST",
      headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
      body: JSON.stringify(body)
    });
    const data = await res.json();
    if (!res.ok) { say(data.message || "Falha ao guardar", false); throw new Error(data.message || "save failed"); }
    return true;
  }

  // Creates a draft control if none is active, returns the control id.
  async function ensureControlId() {
    const existing = el("activeControlId")?.value;
    if (existing) return existing;
    const body = collectCreateBody();
    if (!body.jobOnId) { say("Selecione um Job On para criar o controlo", false); return null; }
    const res = await fetch("/api/peso/control", {
      method: "POST",
      headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
      body: JSON.stringify(body)
    });
    const data = await res.json();
    if (!res.ok) { say(data.message || "Falha ao criar o controlo", false); return null; }
    if (el("activeControlId")) el("activeControlId").value = data.controlId;
    return data.controlId;
  }

  const sendApproval = el("sendApproval");
  if (sendApproval) {
    sendApproval.addEventListener("click", async () => {
      try {
        const id = await ensureControlId();
        if (!id) return;
        // Save current readings into the draft before submit.
        await saveControl(id);
        const sub = await fetch("/api/peso/" + id + "/submit", {
          method: "POST",
          headers: { "RequestVerificationToken": authHeader() }
        });
        say(sub.ok ? "Controlo enviado para aprovação" : "Guardado; envie para aprovação", sub.ok);
      } catch (err) {
        say("Erro de rede: " + err.message, false);
      }
    });
  }

  // ---- Responsável approval flow ----
  const approveBtn = el("approve");
  const rejectBtn = el("reject");
  const getSelectedId = () => {
    const sel = document.querySelector("#approvalList [data-dmo-row].selected");
    return sel ? sel.getAttribute("data-id") : null;
  };

  async function reloadApprovalQueue() {
    try {
      const res = await fetch("/api/peso/controls?status=pendente", {
        headers: { "RequestVerificationToken": authHeader() }
      });
      if (!res.ok) return;
      const items = await res.json();
      const list = el("approvalList");
      if (!list) return;
      if (!items || items.length === 0) {
        list.innerHTML = '<p class="peso-hint">Sem controlos pendentes.</p>';
        return;
      }
      list.innerHTML = "";
      items.forEach((c) => {
        const kind = c.type === "Comparacao" ? "comparacao" : "control";
        const art = document.createElement("article");
        art.className = "peso-queue-item";
        art.setAttribute("data-dmo-row", "");
        art.setAttribute("data-id", c.controlId);
        art.setAttribute("data-kind", kind);
        art.setAttribute("data-status", c.status === "Aprovado" ? "aprovado" : (c.status === "NaoAprovado" ? "nao_aprovado" : "pendente"));
        art.innerHTML =
          "<strong>" + (c.reference || "") + " · " + (c.production || "") + "</strong>" +
          "<small>" + (c.machine || "") + " · Lote " + (c.lote || "") + " · Revisão " + (c.revision || 1) + " · Peso " + (c.peso != null ? Number(c.peso).toFixed(2) : "—") + " g</small>" +
          '<span class="dmo-pill pending">Pendente</span>';
        list.appendChild(art);
      });
    } catch { /* leave list as-is */ }
  }

  if (approveBtn) {
    approveBtn.addEventListener("click", async () => {
      const id = getSelectedId();
      if (!id) return;
      const res = await fetch("/api/peso/" + id + "/approve", {
        method: "POST", headers: { "RequestVerificationToken": authHeader() }
      });
      const data = await res.json();
      if (!res.ok) { say(data.message || "Falha", false); return; }
      say("Controlo aprovado — envio para produção preparado");
      if (el("emailPreview")) el("emailPreview").hidden = false;
      approveBtn.disabled = true; rejectBtn.disabled = true;
      reloadApprovalQueue();
    });
  }
  if (rejectBtn) {
    rejectBtn.addEventListener("click", async () => {
      const id = getSelectedId();
      if (!id) return;
      const nota = (el("dNota") || {}).value || "";
      const res = await fetch("/api/peso/" + id + "/reject", {
        method: "POST",
        headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
        body: JSON.stringify({ justification: nota })
      });
      const data = await res.json();
      if (!res.ok) { say(data.message || "Falha", false); return; }
      say("Controlo não aprovado — justificação registada");
      approveBtn.disabled = true; rejectBtn.disabled = true;
      reloadApprovalQueue();
    });
  }

  // ---- Approval detail render from server ----
  function loadApprovalDetail(id) {
    currentComparison = null;
    if (el("dSub")) el("dSub").textContent = "Controlo · " + id;
    fetch("/api/peso/control/" + id, {
      headers: { "RequestVerificationToken": authHeader() }
    })
      .then((r) => r.json())
      .then((c) => renderControlDetail(c))
      .catch(() => say("Falha ao carregar o controlo", false));
  }

  function renderControlDetail(c) {
    // Identification grid
    const identity = el("dIdentity");
    if (identity) {
      identity.innerHTML =
        "<div><span>Referência</span><strong>" + (c.reference || (c.moldNumber + c.neckringNumber)) + "</strong></div>" +
        "<div><span>CM</span><strong>" + (c.moldNumber || "—") + "</strong></div>" +
        "<div><span>Boquilha/Neckring</span><strong>" + (c.neckringNumber || "—") + "</strong></div>" +
        "<div><span>Máquina</span><strong>" + (c.line || "—") + "</strong></div>" +
        "<div><span>Lote</span><strong>" + (c.lote || "—") + "</strong></div>" +
        "<div><span>Processo</span><strong>" + (c.processo === "Ps" ? "PS" : "NNPB") + "</strong></div>" +
        "<div><span>Produção</span><strong>" + (c.productionCode || "—") + "</strong></div>" +
        "<div><span>Estado do molde</span><strong>" + (c.estadoMolde || "—") + "</strong></div>" +
        "<div><span>Operador</span><strong>" + (c.createdBy || "—") + "</strong></div>" +
        "<div><span>Data</span><strong>" + (c.controlDate ? new Date(c.controlDate).toLocaleDateString("pt-PT") : "—") + "</strong></div>" +
        "<div><span>Revisão</span><strong>" + (c.revision || 1) + "</strong></div>" +
        "<div><span>Constante usada</span><strong>" + fmt(c.constanteGlassUsada) + "</strong></div>";
    }

    // Current-control summary (not a previous-production comparison)
    const summary = el("dSummary");
    if (summary) {
      summary.innerHTML =
        "<div><span>Peso atual (g)</span><strong>" + fmt(c.pesoMedio) + "</strong></div>" +
        "<div><span>Capacidade atual (cm³)</span><strong>" + fmt(c.capacidadeMedia) + "</strong></div>" +
        "<div><span>Peso nominal (g)</span><strong>" + fmt(c.pesoNominal) + "</strong></div>" +
        "<div><span>Constante (NNPB/PS)</span><strong>" + fmt(c.constanteGlassUsada) + "</strong></div>";
    }

    // Nominal reference result
    const nominal = el("dNominal");
    if (nominal) {
      const dif = c.pesoMedio != null && c.pesoNominal != null && c.pesoNominal !== 0
        ? (c.pesoMedio - c.pesoNominal) : null;
      const pct = dif != null ? (dif / c.pesoNominal * 100) : null;
      nominal.innerHTML =
        "<div><span>Peso atual (g)</span><strong>" + fmt(c.pesoMedio) + "</strong></div>" +
        "<div><span>Peso nominal do desenho (g)</span><strong>" + fmt(c.pesoNominal) + "</strong></div>" +
        "<div><span>Diferença para novo</span><strong>" + fmt(dif) + "</strong></div>" +
        "<div><span>Variação %</span><strong>" + (pct != null ? pct.toFixed(2) + " %" : "—") + "</strong></div>";
    }

    // Per-CM table
    const cmBody = el("dCmTable");
    if (cmBody && Array.isArray(c.leituras)) {
      cmBody.innerHTML = "";
      if (c.leituras.length === 0) {
        cmBody.innerHTML = '<tr><td colspan="3" class="empty">Sem leituras.</td></tr>';
      } else {
        c.leituras.forEach((l) => {
          cmBody.innerHTML +=
            "<tr><td>" + (l.cmNumber || "—") + "</td>" +
            "<td>" + fmt(l.pesoEmAgua) + "</td><td>" + fmt(l.pesoVidro) + "</td></tr>";
        });
      }
    }

    // Observations
    if (el("dObservacoes")) el("dObservacoes").textContent = c.notas || "—";
    if (el("dRef")) el("dRef").textContent = "Controlo · " + (c.reference || (c.moldNumber + c.neckringNumber));
    if (el("dResponsavel")) el("dResponsavel").value = "";
    if (el("dNota")) el("dNota").value = "";
  }

  // ---- comparison decisions (Responsável) ----
  let currentComparison = null;
  function updateComparisonCounter() {
    const rows = [...document.querySelectorAll("#cDecisionTable tr[data-cm-decision]")];
    let kept = 0, aside = 0, pending = 0;
    rows.forEach((r) => {
      const d = r.dataset.decision;
      if (d === "manter") kept++; else if (d === "colocar_de_parte") aside++; else pending++;
    });
    if (el("cCounters")) el("cCounters").innerHTML =
      '<div><span>CM mantidos</span><strong>' + kept + '</strong></div>' +
      '<div><span>CM colocados de parte</span><strong>' + aside + '</strong></div>' +
      '<div><span>Sem decisão</span><strong>' + pending + '</strong></div>';
    if (el("confirmComparison")) el("confirmComparison").disabled = pending > 0;
  }
  document.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-decision]");
    if (!btn) return;
    const group = btn.closest(".peso-cm-decision");
    if (!group) return;
    group.querySelectorAll("button").forEach((b) => b.classList.toggle("selected", b === btn));
    group.closest("[data-cm-decision]").dataset.decision = btn.dataset.decision;
    updateComparisonCounter();
  });
  const confirmComparison = el("confirmComparison");
  if (confirmComparison) {
    confirmComparison.addEventListener("click", async () => {
      const id = getSelectedId() || (currentComparison && currentComparison.controlId);
      if (!id) return;
      const decisions = [...document.querySelectorAll("#cDecisionTable tr[data-cm-decision]")].map((r) => ({
        cmNumber: r.dataset.cm, decision: r.dataset.decision || "none"
      }));
      const justification = (el("cJustification") || {}).value || "";
      const res = await fetch("/api/peso/" + id + "/compare/decide", {
        method: "POST",
        headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
        body: JSON.stringify({ justification, decisions })
      });
      const data = await res.json();
      if (!res.ok) { say(data.message || "Falha", false); return; }
      say("Decisões individuais confirmadas; controlo aprovado preservado");
    });
  }

  // ---- Responsável approval detail on select ----
  const approvalList = el("approvalList");
  if (approvalList) {
    approvalList.addEventListener("dmo:list-select", (e) => {
      const kind = e.detail.row.getAttribute("data-kind");
      const isComparison = kind === "comparacao";
      if (el("controlDetail")) el("controlDetail").hidden = isComparison;
      if (el("comparisonDetail")) el("comparisonDetail").hidden = !isComparison;
      if (el("approve")) el("approve").disabled = false;
      if (el("reject")) el("reject").disabled = false;
      if (isComparison) {
        currentComparison = { controlId: e.detail.id };
        loadComparisonDetail(e.detail.id);
      } else {
        loadApprovalDetail(e.detail.id);
      }
    });
  }

  // ---- Comparison detail render (Responsável) ----
  function loadComparisonDetail(id) {
    if (el("cRef")) el("cRef").textContent = id;
    if (el("cSub")) el("cSub").textContent = "Comparação · " + id;
    fetch("/api/peso/control/" + id, {
      headers: { "RequestVerificationToken": authHeader() }
    })
      .then((r) => r.json())
      .then((c) => renderComparisonDetail(c))
      .catch(() => say("Falha ao carregar a comparação", false));
  }

  function renderComparisonDetail(c) {
    const snapshot = typeof c.previousControlJson === "string"
      ? JSON.parse(c.previousControlJson || "null") : c.previousControlJson;
    if (!snapshot || !Array.isArray(snapshot.rows)) {
      say("A comparação não contém um snapshot CM válido", false);
      return;
    }

    // Explicitly confirmed previous production identity
    const base = el("cBase");
    if (base) {
      base.innerHTML =
        "<div><span>Produção</span><strong>" + (snapshot.previousProductionCode || "—") + "</strong></div>" +
        "<div><span>Job On</span><strong>" + (snapshot.previousJobOnId || "—") + "</strong></div>" +
        "<div><span>Revisão Job On</span><strong>" + (snapshot.previousJobOnRevisionId || "—") + "</strong></div>" +
        "<div><span>Lote</span><strong>" + (snapshot.previousLote || "—") + "</strong></div>" +
        "<div><span>Máquina</span><strong>" + (snapshot.previousLine || "—") + "</strong></div>";
    }

    // Per-CM decision table
    const tableBody = el("cDecisionTable");
    if (tableBody) {
      tableBody.innerHTML = "";
      snapshot.rows.forEach((row) => {
        const tr = document.createElement("tr");
        tr.setAttribute("data-cm-decision", "");
        tr.setAttribute("data-cm", row.currentCmNumber || "");
        tr.setAttribute("data-decision", "none");
        tr.innerHTML =
          "<td>" + (row.currentCmNumber || "—") + "</td>" +
          "<td>" + (row.previousCmNumber || "—") + "</td>" +
          "<td>" + fmt(row.currentGlassWeight) + "</td>" +
          "<td>" + fmt(row.previousGlassWeight) + "</td>" +
          "<td>" + fmt(row.difference) + "</td>" +
          "<td>" + fmt(row.differencePercent) + " %</td>" +
          '<td><div class="peso-cm-decision">' +
          '<button type="button" class="dmo-button" data-decision="manter">Manter</button>' +
          '<button type="button" class="dmo-button" data-decision="colocar_de_parte">Colocar de parte</button>' +
          "</div></td>";
        tableBody.appendChild(tr);
      });
    }
    updateComparisonCounter();
    if (el("cJustification")) el("cJustification").value = "";
  }

  // ---- Operator comparison consultation (creation remains in Novo Controlo) ----
  async function loadComparisonConsultation() {
    const list = el("comparacaoList");
    if (!list) return;
    try {
      const res = await fetch("/api/peso/controls?type=comparacao", {
        headers: { "RequestVerificationToken": authHeader() }
      });
      const items = await res.json();
      if (!res.ok || !Array.isArray(items) || items.length === 0) {
        list.innerHTML = '<tr><td colspan="8" class="empty">Nenhuma comparação registada.</td></tr>';
        return;
      }
      list.innerHTML = "";
      items.forEach((c) => {
        const tr = document.createElement("tr");
        tr.setAttribute("data-dmo-row", "");
        tr.setAttribute("data-id", c.controlId);
        tr.innerHTML =
          "<td>" + (c.controlDate ? new Date(c.controlDate).toLocaleDateString("pt-PT") : "—") + "</td>" +
          "<td>" + (c.reference || "—") + "</td><td>" + (c.production || "—") + "</td>" +
          "<td>" + (c.machine || "—") + "</td><td>" + (c.lote || "—") + "</td>" +
          "<td>" + fmt(c.peso) + "</td><td>Produção confirmada</td>" +
          "<td>" + (c.status === "Aprovado" ? "Aprovado" : (c.status === "NaoAprovado" ? "Não aprovado" : (c.status === "Pendente" ? "Pendente" : "Rascunho"))) + "</td>";
        list.appendChild(tr);
      });
    } catch { /* leave the explicit empty state */ }
  }

  async function openComparisonConsultation(id) {
    const detail = el("comparacaoDetail");
    if (!detail) return;
    try {
      const res = await fetch("/api/peso/control/" + id, {
        headers: { "RequestVerificationToken": authHeader() }
      });
      const c = await res.json();
      if (!res.ok) { say(c.message || "Falha ao abrir a comparação", false); return; }
      const snapshot = typeof c.previousControlJson === "string"
        ? JSON.parse(c.previousControlJson || "null") : c.previousControlJson;
      const decisionSnapshot = typeof c.comparisonDecisionsJson === "string"
        ? JSON.parse(c.comparisonDecisionsJson || "null") : c.comparisonDecisionsJson;
      if (!snapshot || !Array.isArray(snapshot.rows)) {
        say("A comparação não contém um snapshot CM válido", false);
        return;
      }
      const decisions = new Map(((decisionSnapshot && decisionSnapshot.decisions) || [])
        .map((decision) => [String(decision.cmNumber), decision.decision]));
      detail.hidden = false;
      if (el("cCompRef")) el("cCompRef").textContent = (c.moldNumber || "") + (c.neckringNumber || "");
      if (el("cCompState")) el("cCompState").textContent = c.status || "Rascunho";
      if (el("cCompBase")) el("cCompBase").innerHTML =
        "<div><span>Produção anterior</span><strong>" + (snapshot.previousProductionCode || "—") + "</strong></div>" +
        "<div><span>Job On</span><strong>" + (snapshot.previousJobOnId || "—") + "</strong></div>" +
        "<div><span>Revisão Job On</span><strong>" + (snapshot.previousJobOnRevisionId || "—") + "</strong></div>" +
        "<div><span>Linha</span><strong>" + (snapshot.previousLine || "—") + "</strong></div>" +
        "<div><span>Lote</span><strong>" + (snapshot.previousLote || "—") + "</strong></div>";
      const table = el("cCompCmTable");
      if (table) {
        table.innerHTML = "";
        snapshot.rows.forEach((row) => {
          const decision = decisions.get(String(row.currentCmNumber));
          table.innerHTML += "<tr>" +
            "<td>" + row.currentCmNumber + "</td><td>" + row.previousCmNumber + "</td>" +
            "<td>" + fmt(row.currentGlassWeight) + "</td><td>" + fmt(row.previousGlassWeight) + "</td>" +
            "<td>" + fmt(row.difference) + "</td><td>" + fmt(row.differencePercent) + " %</td>" +
            "<td>" + (decision === "Manter" || decision === 1 ? "Manter" :
              (decision === "ColocarDeParte" || decision === 2 ? "Colocar de parte" : "Sem decisão")) + "</td></tr>";
        });
      }
      const kept = [...decisions.values()].filter((decision) => decision === "Manter" || decision === 1).length;
      const aside = [...decisions.values()].filter((decision) => decision === "ColocarDeParte" || decision === 2).length;
      if (el("cCompCounters")) el("cCompCounters").innerHTML =
        "<div><span>CM mantidos</span><strong>" + kept + "</strong></div>" +
        "<div><span>CM colocados de parte</span><strong>" + aside + "</strong></div>" +
        "<div><span>Sem decisão</span><strong>" + (snapshot.rows.length - kept - aside) + "</strong></div>";
      if (el("cCompJustification")) {
        el("cCompJustification").value = decisionSnapshot?.justification || "";
        el("cCompJustification").readOnly = true;
      }
    } catch (err) {
      say(err.message || "Falha ao abrir a comparação", false);
    }
  }

  const comparisonList = el("comparacaoList");
  if (comparisonList) {
    comparisonList.addEventListener("dmo:list-select", (event) => openComparisonConsultation(event.detail.id));
    comparisonList.addEventListener("dblclick", (event) => {
      const row = event.target.closest("tr[data-id]");
      if (row) openComparisonConsultation(row.getAttribute("data-id"));
    });
  }

  // ---- History: load from server ----
  async function loadHistory() {
    const search = el("historySearch")?.value || "";
    const status = el("historyStatus")?.value || "";
    const type = el("historyType")?.value || "";
    const from = el("historyFrom")?.value || "";
    const to = el("historyTo")?.value || "";
    const params = new URLSearchParams();
    if (search) params.set("search", search);
    if (status) params.set("status", status);
    if (type) params.set("type", type);
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    try {
      const res = await fetch("/api/peso/controls?" + params.toString(), {
        headers: { "RequestVerificationToken": authHeader() }
      });
      const items = await res.json();
      const tbody = document.querySelector("#historyTable tbody");
      if (!tbody) return;
      if (!items || items.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" class="empty">Nenhum controlo enviado para aprovação.</td></tr>';
        return;
      }
      tbody.innerHTML = "";
      items.forEach((c) => {
        const statusText = c.status === "Aprovado" ? "Aprovado" : (c.status === "NaoAprovado" ? "Não aprovado" : "Pendente");
        const statusClass = c.status === "Aprovado" ? "approved" : (c.status === "NaoAprovado" ? "rejected" : "pending");
        const tr = document.createElement("tr");
        tr.setAttribute("data-row", "");
        tr.setAttribute("data-id", c.controlId);
        tr.setAttribute("data-kind", c.type === "Comparacao" ? "comparacao" : "control");
        tr.setAttribute("data-status", c.status);
        tr.innerHTML =
          "<td>" + (c.controlDate ? new Date(c.controlDate).toLocaleDateString("pt-PT") : "—") + "</td>" +
          "<td>" + (c.reference || "—") + "</td>" +
          "<td>" + (c.production || "—") + "</td>" +
          "<td>" + (c.machine || "—") + "</td>" +
          "<td>" + (c.lote || "—") + "</td>" +
          "<td>" + (c.peso != null ? Number(c.peso).toFixed(2) : "—") + "</td>" +
          "<td>" + (c.revision || 1) + "</td>" +
          '<td><span class="dmo-pill ' + statusClass + '">' + statusText + "</span></td>";
        tbody.appendChild(tr);
      });
    } catch { /* leave empty */ }
  }

  // History search + filter triggers reload
  ["historySearch", "historyStatus", "historyType", "historyFrom", "historyTo"].forEach((id) => {
    const input = el(id);
    if (input) {
      const evt = input.tagName === "SELECT" ? "change" : (input.type === "date" ? "change" : "input");
      input.addEventListener(evt, loadHistory);
    }
  });

  // History: single-click select + double-click open sheet
  const historyTable = el("historyTable");
  let selectedHistoryId = null;
  if (historyTable) {
    historyTable.addEventListener("click", (e) => {
      const row = e.target.closest("tr[data-row]");
      if (!row) return;
      historyTable.querySelectorAll("tr[data-row]").forEach((r) => r.classList.remove("selected"));
      row.classList.add("selected");
      selectedHistoryId = row.getAttribute("data-id");
      const isApproved = row.getAttribute("data-status") === "Aprovado";
      if (el("generateDoc")) el("generateDoc").disabled = !isApproved;
      if (el("sendMail")) el("sendMail").disabled = !isApproved;
    });
    historyTable.addEventListener("dblclick", (e) => {
      const row = e.target.closest("tr[data-row]");
      if (!row) return;
      openSheet(row.getAttribute("data-id"));
    });
  }

  // ---- Sheet (Folha de controlo) modal ----
  async function openSheet(id) {
    const sheet = el("sheet");
    if (!sheet) return;
    sheet.hidden = false;
    sheet.classList.add("show");
    const ctx = el("sheetContext");
    const table = el("sheetTable");
    if (ctx) ctx.innerHTML = "<div><span>Controlo</span><strong>" + id + "</strong></div>";
    if (table) table.innerHTML = "<tr><td class=\"empty\">A carregar…</td></tr>";
    try {
      const res = await fetch("/api/peso/control/" + id, { headers: { "RequestVerificationToken": authHeader() } });
      const c = await res.json();
      if (ctx && c) {
        ctx.innerHTML =
          "<div><span>Referência</span><strong>" + (c.reference || (c.moldNumber + c.neckringNumber)) + "</strong></div>" +
          "<div><span>Produção</span><strong>" + (c.productionCode || "—") + "</strong></div>" +
          "<div><span>Linha</span><strong>" + (c.line || "—") + "</strong></div>" +
          "<div><span>Lote</span><strong>" + (c.lote || "—") + "</strong></div>" +
          "<div><span>Estado</span><strong>" + (c.status === "Aprovado" ? "Aprovado" : "—") + "</strong></div>" +
          "<div><span>Revisão</span><strong>" + (c.revision || 1) + "</strong></div>";
      }
      if (table && Array.isArray(c.leituras)) {
        table.innerHTML = "<thead><tr><th>CM</th><th>Peso (g)</th><th>Peso vidro</th></tr></thead><tbody>";
        c.leituras.forEach((l) => {
          table.innerHTML += "<tr><td>" + (l.cmNumber || "—") + "</td><td>" + fmt(l.pesoEmAgua) + "</td><td>" + fmt(l.pesoVidro) + "</td></tr>";
        });
        table.innerHTML += "</tbody>";
      }
    } catch {
      if (table) table.innerHTML = "<tr><td class=\"empty\">Não foi possível carregar.</td></tr>";
    }
    // Sheet generate button uses selected history id
    const sheetGen = sheet.querySelector("[data-sheet-generate]");
    if (sheetGen) sheetGen.onclick = () => { if (selectedHistoryId) generateDocument(selectedHistoryId); };
    // Sheet send button
    const sheetSend = sheet.querySelector("[data-sheet-send]");
    if (sheetSend) sheetSend.onclick = () => { if (selectedHistoryId) prepareEmail(selectedHistoryId); };
  }
  document.querySelectorAll("#sheet [data-close]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const sheet = el("sheet");
      if (sheet) { sheet.hidden = true; sheet.classList.remove("show"); }
    });
  });

  // ---- PDF generation (approved) ----
  async function generateDocument(id) {
    const fileName = await doGenerate(id);
    if (fileName) say("Folha de produção gerada: " + fileName);
  }
  async function doGenerate(id) {
    try {
      const res = await fetch("/api/peso/" + id + "/document", {
        method: "POST",
        headers: { "RequestVerificationToken": authHeader() }
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        say(data.message || "Falha ao gerar a folha", false);
        return null;
      }
      // Serve the PDF as browser Blob + download (GLM-PESO-09 boundary).
      const contentDisposition = res.headers.get("Content-Disposition") || "";
      const match = /filename="?([^"]+)"?/.exec(contentDisposition);
      const fileName = match ? match[1] : "folha-producao.pdf";
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
      return fileName;
    } catch (err) {
      say("Erro de rede: " + err.message, false);
      return null;
    }
  }

  // ---- Email preparation ----
  async function prepareEmail(id) {
    try {
      const res = await fetch("/api/peso/" + id + "/email/prepare", {
        method: "POST",
        headers: { "RequestVerificationToken": authHeader() }
      });
      const data = await res.json();
      if (!res.ok) { say(data.message || "Falha ao preparar o email", false); return; }
      const grid = el("emailGrid");
      if (grid) {
        grid.innerHTML =
          "<div><span>Máquina</span><strong>" + (data.machine || "—") + "</strong></div>" +
          "<div><span>Grupo de linhas</span><strong>" + (data.lineGroup || "—") + "</strong></div>" +
          "<div><span>Destinatários</span><strong>" + (data.recipients || "—") + "</strong></div>" +
          "<div><span>Assunto</span><strong>" + (data.subject || "—") + "</strong></div>" +
          "<div><span>Anexo</span><strong>" + (data.attachmentFileName || "—") + "</strong></div>";
      }
      if (el("approvedMsg")) el("approvedMsg").textContent = "Email de produção preparado.";
    } catch (err) {
      say("Erro de rede: " + err.message, false);
    }
  }
  if (el("sendMail")) el("sendMail").addEventListener("click", () => { if (selectedHistoryId) prepareEmail(selectedHistoryId); });
  if (el("generateDoc")) el("generateDoc").addEventListener("click", () => { if (selectedHistoryId) generateDocument(selectedHistoryId); });

  // ---- Reference list rendering + save reference ----
  async function loadReferences() {
    try {
      const res = await fetch("/api/peso/references", {
        headers: { "RequestVerificationToken": authHeader() }
      });
      const items = await res.json();
      const list = el("referenceList");
      if (list) {
        list.innerHTML = "";
        (items || []).forEach((r) => {
          const tr = document.createElement("tr");
          tr.setAttribute("data-dmo-row", "");
          tr.setAttribute("data-id", r.pesoReferenceId);
          tr.innerHTML =
            "<td>" + r.moldNumber + r.neckringNumber + "</td>" +
            "<td>" + (r.moldNumber || "—") + "</td>" +
            "<td>" + (r.neckringNumber || "—") + "</td>" +
            "<td>—</td>";
          list.appendChild(tr);
        });
      }
    } catch { /* leave empty */ }
  }

  // Reference save (+ optional first lot)
  const saveReference = el("saveReference");
  if (saveReference) {
    saveReference.addEventListener("click", async () => {
      const mold = el("refMold")?.value;
      const neckring = el("refNeck")?.value;
      if (!mold || !neckring) { say("Referência e neck são obrigatórios", false); return; }
      const body = {
        moldNumber: mold,
        neckringNumber: neckring,
        counterMold: el("refCm")?.value || null,
        capacity: null,
        volumeNeck: el("refVolNeck") ? Number(el("refVolNeck").value) || null : null,
        volumePu: el("refVolPu") ? Number(el("refVolPu").value) || null : null,
        caloteTp: el("refVolTampao") ? Number(el("refVolTampao").value) || null : null,
        changeReason: el("changeReason")?.value || null
      };
      try {
        const res = await fetch("/api/peso/reference", {
          method: "POST",
          headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
          body: JSON.stringify(body)
        });
        const data = await res.json();
        if (!res.ok) { say(data.message || "Falha ao guardar a referência", false); return; }
        const refId = data.id;

        // Optional first-lot creation (process + nominal weight + allowed lines)
        const processo = el("loteProcesso")?.value === "PS" ? 1 : 0;
        const subfolder = el("reportSubfolder")?.value || "";
        const lines = [...document.querySelectorAll("#machineGrid .peso-machine-choice.selected")]
          .map((b) => b.getAttribute("data-line"));
        if (refId && subfolder && lines.length > 0) {
          const lote = el("loteNome")?.value || "";
          const loteBody = {
            referenceId: refId,
            lote: lote || "1",
            processo,
            allowedLines: lines,
            reportSubfolder: subfolder,
            nominalWeight: el("lotePesoNominal") ? Number(el("lotePesoNominal").value) || null : null
          };
          const lr = await fetch("/api/peso/lote", {
            method: "POST",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
            body: JSON.stringify(loteBody)
          });
          const ld = await lr.json();
          if (!lr.ok) { say(ld.message || "Referência guardada, mas falhou criar o lote", false); }
        }
        say("Referência guardada");
        loadReferences();
      } catch (err) {
        say("Erro de rede: " + err.message, false);
      }
    });
  }

  // Reference editor toggles
  ["newRef", "createRefFromNew", "editActiveRef"].forEach((id) => {
    const btn = el(id);
    if (btn) btn.addEventListener("click", () => {
      const editor = el("refEditor");
      if (editor) editor.hidden = !editor.hidden;
    });
  });
  if (el("closeEditor")) el("closeEditor").addEventListener("click", () => {
    const editor = el("refEditor");
    if (editor) editor.hidden = true;
  });

  // ---- Settings: load current values on page init ----
  async function loadSettings() {
    const keys = [
      ["constant_nnpb", "setNnpb"],
      ["constant_ps", "setPs"],
      ["email_recipients_linhab", "setRecipB"],
      ["email_recipients_linhac", "setRecipC"]
    ];
    for (const [key, inputId] of keys) {
      const input = el(inputId);
      if (!input) continue;
      try {
        const res = await fetch("/api/peso/settings/" + encodeURIComponent(key), {
          headers: { "RequestVerificationToken": authHeader() }
        });
        const data = await res.json();
        if (res.ok && data && data.value) input.value = data.value.replace(/^"|"$/g, "");
      } catch { /* keep default */ }
    }
  }
  if (el("saveSettings")) {
    loadSettings();
    el("saveSettings").addEventListener("click", async () => {
      const entries = [
        { key: "constant_nnpb", input: el("setNnpb") },
        { key: "constant_ps", input: el("setPs") },
        { key: "email_recipients_linhab", input: el("setRecipB") },
        { key: "email_recipients_linhac", input: el("setRecipC") }
      ];
      let saved = true;
      for (const entry of entries) {
        if (!entry.input) continue;
        const raw = String(entry.input.value || "").replace(",", ".");
        const value = raw.trim();
        if (!value) continue;
        const res = await fetch("/api/peso/settings", {
          method: "POST",
          headers: { "Content-Type": "application/json", "RequestVerificationToken": authHeader() },
          body: JSON.stringify({ key: entry.key, jsonValue: JSON.stringify(value) })
        });
        if (!res.ok) { saved = false; say("Falha ao guardar " + entry.key, false); }
      }
      if (saved) say("Configurações guardadas (afetam apenas novos cálculos)");
    });
  }

  // ---- Init: load references + history + settings on Operador page ----
  if (el("referenceList")) loadReferences();
  if (el("historyTable")) loadHistory();
  if (el("comparacaoList")) loadComparisonConsultation();

  // ---- Folha de Controlo (R010) deep-link from the production-control area ----
  // Opens the associated Folha de Controlo for the SELECTED production row using the
  // already-selected production+machine context (no re-selection). If none is selected,
  // opens the Folha de Controlo page (empty state).
  function openFolhaControloForSelection(tableSel, productionCell, machineCell) {
    const row = (tableSel ? document.querySelector(tableSel + " tr[data-row].selected")
      : document.querySelector("#refControls tr.selected")) || document.querySelector("#historyTable tr.selected");
    const cells = row ? row.querySelectorAll("td") : [];
    const production = cells.length > productionCell ? (cells[productionCell].textContent || "").trim() : "";
    const machine = cells.length > machineCell ? (cells[machineCell].textContent || "").trim() : "";
    const q = new URLSearchParams();
    if (production) q.set("production", production);
    if (machine) q.set("machine", machine);
    window.location.href = "/controlo" + (q.toString() ? "?" + q.toString() : "");
  }

  if (el("btnFolhaControlo")) {
    el("btnFolhaControlo").addEventListener("click", () => openFolhaControloForSelection("#refControls", 1, 2));
  }
  if (el("btnFolhaControloHist")) {
    el("btnFolhaControloHist").addEventListener("click", () => openFolhaControloForSelection("#historyTable", 2, 3));
  }

  // ---- New-control-from-reference (wires to Job On context) ----
  if (el("newControlRef")) {
    el("newControlRef").addEventListener("click", () => {
      const tabs = document.querySelectorAll(".peso-tabs .tab");
      tabs.forEach((t) => t.classList.toggle("active", t.dataset.view === "new"));
      document.querySelectorAll(".peso-view").forEach((v) => v.classList.toggle("active", v.id === "new"));
      say("Novo controlo — inicie a partir do contexto do Job On selecionado");
    });
  }
})();
