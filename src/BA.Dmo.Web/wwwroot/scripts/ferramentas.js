/* ============================================================
   BA DMO — ferramentas.js (U-12)
   Non-authoritative interaction/bootstrap wiring only.
   Domain logic (identity integrity, atomicity, duplication
   semantics, validation) lives in C#: this file NEVER duplicates
   rules. It calls the canonical backend endpoints and renders
   returned engine results.
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
  const q = (sel, root) => (root || document).querySelector(sel);
  const qa = (sel, root) => Array.from((root || document).querySelectorAll(sel));

  async function api(url, options) {
    const res = await fetch(url, options);
    if (!res.ok) {
      let message = "Não foi possível concluir o pedido.";
      try { const body = await res.json(); message = body.message || message; } catch (_) { /* ignore */ }
      const error = new Error(message);
      error.status = res.status;
      throw error;
    }
    if (res.status === 204) return null;
    const ct = res.headers.get("content-type") || "";
    if (ct.indexOf("application/json") >= 0) return await res.json();
    return null;
  }

  const json = (method, body) => ({
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });

  const canConfigure = () => el("canConfigure")?.value === "true";

  // ---- Tab switching (CM / MF) ----
  document.querySelectorAll(".ferramentas-tabs .tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      const view = tab.dataset.view;
      document.querySelectorAll(".ferramentas-tabs .tab").forEach((t) => t.classList.toggle("active", t === tab));
      document.querySelectorAll(".ferramentas-view").forEach((v) => v.classList.toggle("active", v.id === view));
    });
  });

  // ---- Reference list (distinct CM and MF surfaces) ----
  const viewer = {
    reference: null,
    lote: null
  };

  function buildQuery(toolType) {
    const qs = new URLSearchParams();
    qs.set("type", toolType);
    ["reference", "technicalName", "lote", "drawing", "line", "processo", "ownerPlant"].forEach((k) => {
      const input = q(`[data-filter="${k}"]`, q(`#${toolType.toLowerCase()}`));
      if (input && input.value) qs.set(k, input.value);
    });
    return qs.toString();
  }

  async function loadReferences(toolType) {
    const list = q("[data-ref-list]", el(toolType.toLowerCase()));
    if (!list) return;
    try {
      const items = await api("/api/ferramentas/references?" + buildQuery(toolType));
      renderReferenceList(list, items, toolType);
    } catch (err) {
      say(err.message, false);
    }
  }

  function renderReferenceList(list, items, toolType) {
    if (!items || items.length === 0) {
      list.innerHTML = '<tr><td colspan="7" class="empty">Sem referências para os critérios indicados.</td></tr>';
      viewer.reference = null;
      return;
    }
    const rows = items
      .filter((it) => it.toolType === toolType)
      .map((it) =>
        `<tr class="ferramentas-ref-row" data-row data-id="${it.referenceId}" aria-selected="false">
          <td><span class="dmo-pill">${escapeHtml(it.toolType)}</span></td>
          <td><strong>${escapeHtml(it.refCode)}</strong></td>
          <td>${escapeHtml(it.technicalName || "—")}</td>
          <td>${escapeHtml(it.ownerPlant || "—")}</td>
          <td>${it.lotesCount}</td>
          <td>${escapeHtml(it.processo || "—")}</td>
          <td>${escapeHtml(it.allowedLinesCsv || "—")}</td>
        </tr>`).join("");
    list.innerHTML = rows;
    list.querySelectorAll(".ferramentas-ref-row").forEach((row) => {
      row.addEventListener("click", () => {
        list.querySelectorAll(".ferramentas-ref-row").forEach((r) => { r.classList.remove("selected"); r.setAttribute("aria-selected", "false"); });
        row.classList.add("selected");
        row.setAttribute("aria-selected", "true");
        viewer.reference = row.dataset.id;
        enableDuplicate();
      });
      row.addEventListener("dblclick", () => { window.location.href = "/ferramentas/" + row.dataset.id; });
    });
  }

  function enableDuplicate() {
    const btns = qa("[data-new-lote]").concat([el("btnDuplicate")]).filter(Boolean);
    btns.forEach((b) => { b.disabled = !viewer.reference; });
  }

  document.addEventListener("click", (e) => {
    const newRegisto = e.target.closest("[data-new-registo]");
    if (newRegisto) {
      const type = newRegisto.dataset.type || "CM";
      window.location.href = "/ferramentas/criar?type=" + type;
      return;
    }
    const newLote = e.target.closest("[data-new-lote]");
    if (newLote && viewer.reference) {
      window.location.href = "/ferramentas/criar?type=" + (el("ferramentasView")?.value || "CM") + "&base=" + viewer.reference;
      return;
    }
  });

  document.addEventListener("click", (e) => {
    const searchBtn = e.target.closest("[data-search]");
    if (searchBtn) {
      const type = searchBtn.dataset.type || "CM";
      loadReferences(type);
    }
  });

  // Preload each active view on load.
  ["cm", "mf"].forEach((v) => { const list = q(`[data-ref-list]`, el(v)); if (list) loadReferences(v); });

  // ---- Criar novo registo ----
  qa(".ferramentas-machine-choice").forEach((btn) => {
    btn.addEventListener("click", () => btn.classList.toggle("selected"));
  });

  if (el("fSave")) {
    // Pre-select the tool type from the query.
    const params = new URLSearchParams(window.location.search);
    if (params.get("type")) {
      const typeSel = el("fType");
      if (typeSel) typeSel.value = params.get("type");
    }
    el("fSave").addEventListener("click", async () => {
      const selTypes = qa(".ferramentas-machine-choice.selected").map((b) => b.dataset.line);
      const body = {
        toolType: el("fType").value,
        refCode: el("fRefCode").value.trim(),
        technicalName: el("fTechnicalName").value.trim() || null,
        ownerPlant: el("fOwnerPlant").value.trim(),
        lote: el("fLote").value.trim(),
        qty: el("fQty").value === "" ? null : Number(el("fQty").value),
        allowedLines: selTypes,
        drawingCode: el("fDrawing").value.trim() || null,
        drawingRevision: el("fDrawingRevision").value.trim() || null,
        processo: el("fProcesso").value || null
      };
      try {
        const created = await api("/api/ferramentas/reference", json("POST", body));
        say("Referência e lote criados.");
        window.location.href = "/ferramentas/" + created.referenceId;
      } catch (err) { say(err.message, false); }
    });
  }

  // ---- Ficha da referência ----
  const referenceId = el("referenceId") ? el("referenceId").value : null;
  if (referenceId) {
    let currentLoteId = null;

    async function loadFicha() {
      try {
        const ref = await api("/api/ferramentas/references/" + referenceId);
        wireHeader(ref);
        renderLotes(ref.lotes || []);
        qa(".ferramentas-machine-choice").forEach((b) => b.classList.remove("selected"));
      } catch (err) { say(err.message, false); }
    }

    function wireHeader(ref) {
      set("tipo", ref.toolType);
      set("refCode", ref.refCode);
      set("technicalName", ref.technicalName || "—");
      set("ownerPlant", ref.ownerPlant || "—");
      if (el("fichaTitle")) el("fichaTitle").textContent = ref.technicalName || ref.refCode;
      if (el("fichaSubtitle")) el("fichaSubtitle").textContent = ref.toolType + " · " + ref.refCode;
      if (el("fichaType")) el("fichaType").textContent = ref.toolType;
    }

    function set(name, text) {
      const node = q('[data-node="' + name + '"]');
      if (node) node.textContent = text;
    }

    function renderLotes(lotes) {
      const body = el("loteList");
      if (!body) return;
      if (!lotes || lotes.length === 0) {
        body.innerHTML = '<tr><td colspan="6" class="empty">Sem lotes registados para esta referência.</td></tr>';
        return;
      }
      body.innerHTML = lotes.map((l) =>
        `<tr class="ferramentas-lote-row" data-row data-id="${l.loteId}" aria-selected="false">
          <td><strong>${escapeHtml(l.lote)}</strong></td>
          <td>${escapeHtml(l.processo || "—")}</td>
          <td>${l.qty ?? "—"}</td>
          <td>${escapeHtml((l.allowedLines || []).join(", ") || "—")}</td>
          <td>${escapeHtml(l.drawingCode || "Não definido")}</td>
          <td>${escapeHtml(l.drawingRevision || "—")}</td>
        </tr>`).join("");
      body.querySelectorAll(".ferramentas-lote-row").forEach((row) => {
        row.addEventListener("click", () => {
          body.querySelectorAll(".ferramentas-lote-row").forEach((r) => { r.classList.remove("selected"); r.setAttribute("aria-selected", "false"); });
          row.classList.add("selected");
          row.setAttribute("aria-selected", "true");
          currentLoteId = row.dataset.id;
          if (el("btnDuplicate")) el("btnDuplicate").disabled = false;
          selectLote(currentLoteId);
        });
        row.addEventListener("dblclick", () => selectLote(row.dataset.id));
      });
    }

    function selectLote(loteId) {
      currentLoteId = loteId;
      loadRules(loteId);
      if (el("verificacoesCard")) el("verificacoesCard").hidden = false;
    }

    if (el("btnDuplicate")) el("btnDuplicate").addEventListener("click", async () => {
      if (!currentLoteId) { say("Selecione um lote para duplicar.", false); return; }
      const params = new URLSearchParams(window.location.search);
      void params;
      const base = currentLoteId;
      window.location.href = "/ferramentas/criar?base=" + base;
    });

    async function loadRules(loteId) {
      const list = el("ruleList");
      if (!list) return;
      try {
        const rules = await api("/api/ferramentas/lotes/" + loteId + "/rules");
        renderRules(rules || []);
      } catch (err) { say(err.message, false); }
    }

    function renderRules(rules) {
      const list = el("ruleList");
      if (!list) return;
      if (!rules || rules.length === 0) {
        list.innerHTML = '<tr><td colspan="4" class="empty">Sem regras configuradas neste lote.</td></tr>';
        return;
      }
      const config = canConfigure();
      list.innerHTML = rules.map((r) =>
        `<tr>
          <td>${escapeHtml(r.ruleText)}</td>
          <td>${r.frequency === "uma_vez_no_lote" ? "Uma vez no lote" : "Por fabrico"}</td>
          <td><span class="dmo-pill ${r.active ? "approved" : ""}">${r.active ? "Ativa" : "Inativa"}</span></td>
          <td class="ferramentas-row-actions">
            ${config ? `<button class="dmo-button compact" data-edit-rule="${r.ruleId}" type="button">Editar</button>
            <button class="dmo-button compact" data-toggle-rule="${r.ruleId}" data-active="${r.active ? "true" : "false"}" type="button">${r.active ? "Desativar" : "Reativar"}</button>` : ""}
          </td>
        </tr>`).join("");
      if (config && el("btnAddRule")) el("btnAddRule").hidden = false;
    }

    if (canConfigure() && el("btnAddRule")) el("btnAddRule").hidden = false;

    document.addEventListener("click", async (e) => {
      if (!canConfigure()) return;
      const edit = e.target.closest("[data-edit-rule]");
      if (edit) { openRuleEditor(edit.dataset.editRule); return; }
      const toggle = e.target.closest("[data-toggle-rule]");
      if (toggle) {
        try {
          await api("/api/ferramentas/rules/" + toggle.dataset.toggleRule + "/toggle", json("POST", {
            ruleId: toggle.dataset.toggleRule,
            active: toggle.dataset.active !== "true"
          }));
          loadRules(currentLoteId);
        } catch (err) { say(err.message, false); }
      }
    });

    if (el("btnAddRule")) el("btnAddRule").addEventListener("click", () => openRuleEditor(null));

    function openRuleEditor(ruleId) {
      if (el("ruleEditor")) el("ruleEditor").hidden = false;
      if (el("btnSaveRule")) el("btnSaveRule").dataset.ruleId = ruleId || "";
    }

    if (el("btnCancelRule")) el("btnCancelRule").addEventListener("click", () => {
      if (el("ruleEditor")) el("ruleEditor").hidden = true;
    });

    if (el("btnSaveRule")) el("btnSaveRule").addEventListener("click", async () => {
      const text = el("ruleText").value.trim();
      const freq = el("ruleFrequency").value;
      const ruleId = el("btnSaveRule").dataset.ruleId;
      if (!text) { say("Indique o texto da regra.", false); return; }
      try {
        if (ruleId) {
          await api("/api/ferramentas/rules/" + ruleId, json("PUT", { loteId: currentLoteId, ruleText: text, frequency: freq }));
        } else {
          await api("/api/ferramentas/lotes/" + currentLoteId + "/rules", json("POST", { loteId: currentLoteId, ruleText: text, frequency: freq }));
        }
        el("ruleText").value = "";
        if (el("ruleEditor")) el("ruleEditor").hidden = true;
        loadRules(currentLoteId);
        say("Regra guardada.");
      } catch (err) { say(err.message, false); }
    });

    loadFicha();
  }

  function set(name, text) {
    const node = q('[data-node="' + name + '"]');
    if (node) node.textContent = text;
  }

  function escapeHtml(s) {
    return String(s == null ? "" : s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }
})();