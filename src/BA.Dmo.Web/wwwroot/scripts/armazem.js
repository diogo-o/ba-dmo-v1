/* ============================================================
   BA DMO — armazem.js (U-14)
   Non-authoritative interaction/bootstrap wiring only. Domain
   logic (occupation 1:1, 4-digit positions, atomic Substituir,
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

  const qs = (params) => {
    const parts = [];
    for (const key in params) {
      const v = params[key];
      if (v !== undefined && v !== null && v !== "") parts.push(encodeURIComponent(key) + "=" + encodeURIComponent(v));
    }
    return parts.length ? "?" + parts.join("&") : "";
  };

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
    });
  });

  // ---- Inline cards (Entrada / Saída / Substituir) ----
  document.querySelectorAll("[data-open]").forEach((btn) => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".armazem-card").forEach((c) => c.hidden = true);
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
    } catch (e) { say(e.message, false); }
  });

  el("substituirForm").querySelector("[data-submit]").addEventListener("click", async () => {
    const v = readForm("substituirForm");
    try {
      await api("/api/armazem/substituir", json("POST", {
        positionCode: v.substPosition, newToolType: v.substType,
        newReference: v.substRef, newLot: v.substLot, observations: v.substObs
      }));
      say("Posição substituída.");
      el("substituirForm").hidden = true;
      clearForm("substituirForm");
    } catch (e) { say(e.message, false); }
  });

  // ---- Consultation ----
  async function runSeek() {
    const params = { type: el("seekType").value, reference: el("seekRef").value, lot: el("seekLot").value, position: el("seekPosition").value };
    try {
      const rows = await api("/api/armazem/consulta" + qs(params));
      renderRows(rows || []);
    } catch (e) { say(e.message, false); }
  }

  function renderRows(rows) {
    const body = el("consultationBody");
    const empty = el("consultationEmpty");
    body.innerHTML = "";
    if (!rows.length) {
      empty.hidden = false;
      return;
    }
    empty.hidden = true;
    rows.forEach((row) => {
      const tr = document.createElement("tr");
      const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
      tr.appendChild(td(row.type));
      tr.appendChild(td(row.reference));
      tr.appendChild(td(row.technicalName));
      tr.appendChild(td(row.lot));
      tr.appendChild(td(locationLabel(row.locationContext)));
      tr.appendChild(td(row.positionCode));
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
      body.appendChild(tr);
    });
  }

  function locationLabel(ctx) {
    if (ctx === "armazem") return "Armazém";
    if (ctx === "fora") return "Fora do armazém";
    return "Localização operacional não registada";
  }

  document.querySelector("[data-seek]").addEventListener("click", runSeek);
  document.querySelector("[data-seek-clear]").addEventListener("click", () => {
    ["seekType", "seekRef", "seekLot", "seekPosition"].forEach((id) => (el(id).value = ""));
    renderRows([]);
  });

  const params = new URLSearchParams(window.location.search);
  if (params.has("position")) {
    el("seekPosition").value = params.get("position");
    document.querySelector(".armazem-tabs .tab[data-view='consulta']").click();
    runSeek();
  }

  function clearForm(id) {
    el(id).querySelectorAll("input,select").forEach((f) => { if (f.tagName === "SELECT") f.selectedIndex = 0; else f.value = ""; });
  }
})();
