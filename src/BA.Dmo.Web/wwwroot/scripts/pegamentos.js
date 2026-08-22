/* ============================================================
   BA DMO — pegamentos.js (U-11)
   Non-authoritative interaction/bootstrap wiring only.
   Domain logic (ovalização/média/tolerance, validation,
   persistence, authorization) lives in C#: this file NEVER
   duplicates formulas or validation (GLM-PEG-05 rule).
   It calls the canonical backend endpoints and renders returned
   engine results.
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
    if (res.status === 204) return null;
    if (ct.indexOf("application/pdf") >= 0) return await res.arrayBuffer();
    return await res.json();
  }

  const json = (method, body) => ({
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });

  // ---- Tab switching (Index) ----
  document.querySelectorAll(".pegamentos-tabs .tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      const view = tab.dataset.view;
      document.querySelectorAll(".pegamentos-tabs .tab").forEach((t) => t.classList.toggle("active", t === tab));
      document.querySelectorAll(".pegamentos-view").forEach((v) => v.classList.toggle("active", v.id === view));
    });
  });

  // ---- Search / consultation (Index) ----
  let selectedControloId = null;

  function renderList(items) {
    const body = el("controlList");
    const canOpen = el("btnOpen") && !el("btnOpen").disabled;
    void canOpen;
    if (!items || items.length === 0) {
      body.innerHTML = '<tr><td colspan="5" class="empty">Sem controlos para os critérios indicados.</td></tr>';
      setSelection(null);
      return;
    }
    body.innerHTML = items.map((it) =>
      `<tr class="peg-row" data-id="${it.controloId}" aria-selected="false">
        <td>${it.createdAtUtc ? new Date(it.createdAtUtc).toLocaleDateString() : "—"}</td>
        <td><strong>${escapeHtml(it.reference)}</strong></td>
        <td>${escapeHtml(it.productionCode)}</td>
        <td>${escapeHtml(it.machineCode)}</td>
        <td><span class="dmo-pill ${it.status === "Fechado" ? "approved" : ""}">${escapeHtml(it.status)}</span></td>
      </tr>`).join("");
    body.querySelectorAll(".peg-row").forEach((row) => {
      row.addEventListener("click", () => {
        body.querySelectorAll(".peg-row").forEach((r) => { r.classList.remove("selected"); r.setAttribute("aria-selected", "false"); });
        row.classList.add("selected");
        row.setAttribute("aria-selected", "true");
        setSelection(row.dataset.id);
      });
      row.addEventListener("dblclick", () => openFolha(row.dataset.id));
    });
  }

  function setSelection(id) {
    selectedControloId = id || null;
    if (el("btnOpen")) el("btnOpen").disabled = !selectedControloId;
    if (el("btnHistory")) el("btnHistory").disabled = !selectedControloId;
  }

  async function doSearch() {
    const qs = new URLSearchParams();
    if (el("searchReference") && el("searchReference").value) qs.set("reference", el("searchReference").value);
    if (el("searchProduction") && el("searchProduction").value) qs.set("productionCode", el("searchProduction").value);
    if (el("searchMachine") && el("searchMachine").value) qs.set("machine", el("searchMachine").value);
    if (el("searchFrom") && el("searchFrom").value) qs.set("from", el("searchFrom").value);
    if (el("searchTo") && el("searchTo").value) qs.set("to", el("searchTo").value);
    try {
      const items = await api("/api/pegamentos/search?" + qs.toString());
      renderList(items);
    } catch (err) {
      say(err.message, false);
    }
  }

  if (el("btnSearch")) el("btnSearch").addEventListener("click", doSearch);

  const openFolha = (id) => { window.location.href = "/pegamentos/" + id; };

  if (el("btnOpen")) el("btnOpen").addEventListener("click", () => { if (selectedControloId) openFolha(selectedControloId); });
  if (el("btnHistory")) el("btnHistory").addEventListener("click", async () => {
    if (!selectedControloId) return;
    try {
      const history = await api("/api/pegamentos/" + selectedControloId + "/history");
      say("Histórico com " + history.length + " medição(ões).");
    } catch (err) { say(err.message, false); }
  });

  // ---- Nova folha: resolve context from revision (Index) ----
  if (el("btnResolve")) el("btnResolve").addEventListener("click", async () => {
    const revisionId = el("revisionId").value.trim();
    if (!revisionId) { say("Indique o identificador da revisão do Job On.", false); return; }
    try {
      const ctx = await api("/api/pegamentos/context/" + revisionId);
      wireContext(ctx);
      el("incompleteBlock").hidden = true;
      say("Contexto resolvido.");
    } catch (err) {
      el("incompleteBlock").hidden = false;
      say(err.message, false);
    }
  });

  let resolvedContext = null;
  function wireContext(ctx) {
    resolvedContext = ctx;
    set("ctxReferencia", ctx.reference);
    set("ctxProducao", ctx.productionCode);
    set("ctxMaquina", ctx.machineCode);
    set("ctxCm", ctx.cmSnapshot ? ctx.cmSnapshot.referenceSnapshot : "—");
    set("ctxBq", ctx.bqSnapshot ? ctx.bqSnapshot.referenceSnapshot : "—");
    set("ctxMf", ctx.mfSnapshot ? ctx.mfSnapshot.referenceSnapshot : "—");
  }
  function set(name, text) {
    const node = document.querySelector('[data-node="' + name + '"]');
    if (node) node.textContent = text;
  }

  if (el("btnCreate")) el("btnCreate").addEventListener("click", async () => {
    const revisionId = el("revisionId").value.trim();
    if (!revisionId) { say("Indique o identificador da revisão do Job On.", false); return; }
    try {
      const result = await api("/api/pegamentos", json("POST", { jobOnRevisionId: revisionId }));
      say("Folha criada.");
      openFolha(result.id);
    } catch (err) { say(err.message, false); }
  });

  if (el("btnFixTools")) el("btnFixTools").addEventListener("click", () => {
    window.location.href = "/jobon";
  });

  // ---- Detail: control sheet ----
  const controloId = el("controloId") ? el("controloId").value : null;
  if (controloId) {
    let control = null;

    async function load() {
      try {
        control = await api("/api/pegamentos/" + controloId);
        wireDetail(control);
      } catch (err) { say(err.message, false); }
    }

    function wireDetail(c) {
      set("ctxReferencia", c.reference);
      set("ctxProducao", c.productionCode);
      set("ctxMaquina", c.machineCode);
      set("ctxCm", c.cmReference || "—");
      set("ctxBq", c.bqReference || "—");
      set("ctxMf", c.mfReference || "—");
      set("serverStatus", c.status);
      if (el("statusPill")) el("statusPill").textContent = c.status;
      if (el("tolerance")) el("tolerance").value = c.tolerance;
      if (el("notas")) el("notas").value = c.notas || "";
      if (el("sheetSubtitle")) el("sheetSubtitle").textContent = c.reference + " · " + c.productionCode + " · " + c.machineCode;
      renderMeasurements(c.measurements || []);
      const open = c.status === "Aberto";
      if (el("btnAddMeasurement")) el("btnAddMeasurement").disabled = !open;
      if (el("btnClose")) el("btnClose").disabled = !open;
    }

    function renderMeasurements(rows) {
      const body = el("measureTable");
      if (!body) return;
      if (!rows || rows.length === 0) {
        body.innerHTML = '<tr><td colspan="7" class="empty">Sem medições registadas.</td></tr>';
        return;
      }
      body.innerHTML = rows.map((m) =>
        `<tr>
          <td>${m.toolNumber ?? "—"}</td>
          <td><strong>${escapeHtml(m.componentKey)}</strong></td>
          <td>${fmt(m.costura)}</td>
          <td>${fmt(m.contraCostura)}</td>
          <td>${fmt(m.ovalizacao)}</td>
          <td>${fmt(m.media)}</td>
          <td><span class="dmo-pill ${pillClass(m.toleranceStatus)}">${escapeHtml(m.toleranceStatus)}</span></td>
        </tr>`).join("");
    }

    function pillClass(status) {
      if (status === "Exceeded") return "danger";
      if (status === "Warning") return "warning";
      if (status === "NotEvaluable") return "warning";
      return "";
    }

    const fmt = (v) => (v === null || v === undefined) ? "—" : Number(v).toFixed(2);

    if (el("btnAddMeasurement")) el("btnAddMeasurement").addEventListener("click", async () => {
      const tool = Number(el("mtTool").value);
      const costura = Number(el("mtCostura").value);
      const contraRaw = el("mtContra").value;
      if (!tool || isNaN(costura)) { say("Indique o número e a costura.", false); return; }
      const body = {
        controloId,
        component: el("mtComponent").value,
        toolNumber: tool,
        costura,
        contraCostura: contraRaw === "" ? null : Number(contraRaw)
      };
      try {
        await api("/api/pegamentos/" + controloId + "/measurements", json("POST", body));
        el("mtTool").value = ""; el("mtCostura").value = ""; el("mtContra").value = "";
        await load();
        say("Medição adicionada.");
      } catch (err) { say(err.message, false); }
    });

    if (el("btnSave")) el("btnSave").addEventListener("click", async () => {
      try {
        await api("/api/pegamentos/" + controloId, json("PUT", {
          controloId,
          tolerance: Number(el("tolerance").value),
          notas: el("notas").value
        }));
        say("Controlo guardado.");
      } catch (err) { say(err.message, false); }
    });

    if (el("btnClose")) el("btnClose").addEventListener("click", async () => {
      try {
        await api("/api/pegamentos/" + controloId + "/close", json("POST", {}));
        set("serverStatus", "Fechado");
        say("Folha fechada.");
        await load();
      } catch (err) { say(err.message, false); }
    });

    if (el("btnGeneratePdf")) el("btnGeneratePdf").addEventListener("click", async () => {
      try {
        const bytes = await api("/api/pegamentos/" + controloId + "/document/generate", json("POST", {}));
        // The browser physically writes the file (File System Access / download).
        // The server only generated bytes; pegamento_documentos is NOT yet persisted.
        const blob = new Blob([bytes], { type: "application/pdf" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = "pegamentos_folha.pdf";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        set("localStatus", "Guardado localmente");
        // Only after the physical write succeeded, confirm to the server.
        await api("/api/pegamentos/" + controloId + "/document/confirm", json("POST", {}));
        say("PDF guardado. Documento registado no servidor.");
      } catch (err) { say(err.message, false); }
    });

    load();
  }

  function escapeHtml(s) {
    return String(s == null ? "" : s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }
})();