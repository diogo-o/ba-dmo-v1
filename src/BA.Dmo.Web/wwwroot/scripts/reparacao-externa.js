/* ============================================================
   BA DMO — reparacao-externa.js (U-15)
   Non-authoritative interaction/bootstrap wiring only. Domain
   logic (status machine, duplicate-in-open-exit, snapshot of
   repairer, atomic pickup/return with Armazém, actor attribution)
   lives in C#: this file NEVER duplicates rules. It calls the
   canonical backend endpoints and renders returned engine results.
   BQ is out of U-15 scope (deferred to U-19), so no fake BQ wiring.
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

  // ---- Tabs (six canonical tabs) ----
  document.querySelectorAll(".reparacao-externa-tabs .tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      const view = tab.dataset.view;
      document.querySelectorAll(".reparacao-externa-tabs .tab").forEach((t) => t.classList.toggle("active", t === tab));
      document.querySelectorAll(".reparacao-externa-view").forEach((v) => v.classList.toggle("active", v.id === view));
    });
  });

  // NOTE (F1 fix): a second `loadRepairersInto(select)` (1-arg) and a second
  // `const selectedItems` used to be declared later in this IIFE. The duplicate
  // const was a SyntaxError at parse time that disabled the ENTIRE module
  // script (tabs/search/envios/historico/definicoes). The stale 1-arg function
  // and first const are removed; the 2-arg `loadRepairersInto` below is the
  // live implementation (later same-name declarations win).

  // ---- Last-seen repairer per tool type (UX default) ---------------------------
  const LAST_SEEN_KEY = "repex.lastSeenRepairer";

  function getLastSeenRepairer(type) {
    try {
      const map = JSON.parse(localStorage.getItem(LAST_SEEN_KEY) || "{}");
      return map[type] || null;
    } catch (_) { return null; }
  }

  function saveLastSeenRepairer(type, repairerId) {
    try {
      const map = JSON.parse(localStorage.getItem(LAST_SEEN_KEY) || "{}");
      if (repairerId) {
        map[type] = repairerId;
      } else {
        delete map[type];
      }
      localStorage.setItem(LAST_SEEN_KEY, JSON.stringify(map));
    } catch (_) { /* ignore */ }
  }

  // ---- List builder (CM / MF): search → add items → create list ----
  function loadRepairersInto(select, type) {
    api("/api/reparacao-externa/repairers").then((list) => {
      select.innerHTML = "";
      const emptyOpt = document.createElement("option");
      emptyOpt.value = "";
      emptyOpt.textContent = "— (sem associação)";
      select.appendChild(emptyOpt);
      // remember selected value before rebuilding
      const currentlySelected = select.value;
      (list || [])
        .filter((r) => r.active)
        .forEach((r) => {
          const opt = document.createElement("option");
          opt.value = r.repairerId;
          opt.textContent = r.name;
          select.appendChild(opt);
        });
      // Restore previously selected or apply remembered default
      if (currentlySelected) {
        select.value = currentlySelected;
      } else {
        const defaultId = getLastSeenRepairer(type);
        if (defaultId) select.value = defaultId;
      }
    }).catch(() => { /* keep empty */ });
  }

  // Track selected items (physicalPieceId → {reference, lot, number}) per type.
  const selectedItems = { CM: [], MF: [] };

  function renderItems(type) {
    const body = el((type === "CM" ? "cm" : "mf") + "ItemsBody");
    const table = document.querySelector(`[data-items-table="${type}"]`);
    const empty = el((type === "CM" ? "cm" : "mf") + "ItemsEmpty");
    body.innerHTML = "";
    const items = selectedItems[type];
    if (!items.length) {
      empty.hidden = false;
      table.hidden = true;
      return;
    }
    empty.hidden = true;
    table.hidden = false;
    items.forEach((it, idx) => {
      const tr = document.createElement("tr");
      const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
      tr.appendChild(td(it.reference));
      tr.appendChild(td(it.lot));
      tr.appendChild(td(it.number));
      const actionTd = document.createElement("td");
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "dmo-button danger";
      btn.textContent = "Remover";
      btn.addEventListener("click", () => {
        selectedItems[type] = selectedItems[type].filter((_, i) => i !== idx);
        renderItems(type);
      });
      actionTd.appendChild(btn);
      tr.appendChild(actionTd);
      body.appendChild(tr);
    });
  }

  function searchTools(type) {
    const prefix = type === "CM" ? "cm" : "mf";
    const params = {
      type,
      reference: el(prefix + "Ref").value,
      lot: el(prefix + "Lot").value,
      number: el(prefix + "Number").value
    };
    api("/api/reparacao-externa/tools" + qs(params)).then((tools) => {
      const body = el(prefix + "ToolsBody");
      const table = document.querySelector(`[data-tools-table="${type}"]`);
      const empty = el(prefix + "ToolsEmpty");
      body.innerHTML = "";
      if (!tools || !tools.length) {
        empty.hidden = false;
        table.hidden = true;
        return;
      }
      empty.hidden = true;
      table.hidden = false;
      tools.forEach((t) => {
        const tr = document.createElement("tr");
        const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
        tr.appendChild(td(t.reference));
        tr.appendChild(td(t.lot));
        tr.appendChild(td(t.number));
        tr.appendChild(td(t.technicalName));
        const actionTd = document.createElement("td");
        const addBtn = document.createElement("button");
        addBtn.type = "button";
        addBtn.className = "dmo-button";
        addBtn.textContent = "Adicionar";
        const already = selectedItems[type].some((x) => x.physicalPieceId === t.physicalPieceId);
        addBtn.disabled = already;
        addBtn.addEventListener("click", () => {
          if (selectedItems[type].some((x) => x.physicalPieceId === t.physicalPieceId)) return;
          selectedItems[type].push({ physicalPieceId: t.physicalPieceId, reference: t.reference, lot: t.lot, number: t.number });
          renderItems(type);
          addBtn.disabled = true;
        });
        actionTd.appendChild(addBtn);
        tr.appendChild(actionTd);
        body.appendChild(tr);
      });
    }).catch((e) => say(e.message, false));
  }

  function createList(type) {
    const prefix = type === "CM" ? "cm" : "mf";
    const items = selectedItems[type].map((x) => ({ physicalPieceId: x.physicalPieceId, number: x.number }));
    const sel = el(prefix + "Repairer");
    const repairerId = sel ? sel.value || null : null;
    const plannedDate = el(prefix + "PlannedDate").value || null;
    if (!items.length) { say("Adicione pelo menos um item à lista.", false); return; }
    
    // Persist last seen repairer for this type (UX default)
    saveLastSeenRepairer(type, repairerId);
    
    api("/api/reparacao-externa", json("POST", {
      repairType: type,
      repairerId,
      plannedDate,
      items,
      productionContext: null
    })).then(() => {
      say("Lista criada.");
      selectedItems[type] = [];
      renderItems(type);
      el(prefix + "ItemsEmpty").textContent = "Lista sem itens. Adicione ferramentas acima.";
    }).catch((e) => say(e.message, false));
  }

  document.querySelectorAll("[data-search-tools]").forEach((btn) => {
    btn.addEventListener("click", () => searchTools(btn.dataset.searchTools));
  });
  document.querySelectorAll("[data-create-list]").forEach((btn) => {
    btn.addEventListener("click", () => createList(btn.dataset.createList));
  });
  // Pass the tool type so loadRepairersInto can pre-select the remembered default
  document.querySelectorAll("[data-repairer-for]").forEach((sel) => loadRepairersInto(sel, sel.dataset.repairerFor));

  // ---- Envios ----
  let selectedExitId = null;

  function renderExits(exits) {
    const body = el("exitsBody");
    const empty = el("exitsEmpty");
    body.innerHTML = "";
    if (!exits || !exits.length) {
      empty.hidden = false;
      el("exitsActions").hidden = true;
      return;
    }
    empty.hidden = true;
    exits.forEach((exit) => {
      const tr = document.createElement("tr");
      tr.addEventListener("click", () => {
        selectedExitId = exit.repairExitId;
        [...body.children].forEach((r) => r.classList.remove("selected"));
        tr.classList.add("selected");
        el("exitsActions").hidden = false;
      });
      tr.addEventListener("dblclick", () => openExitDetail(exit.repairExitId));
      const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
      tr.appendChild(td(exit.repairExitId.slice(0, 8)));
      tr.appendChild(td(exit.repairType));
      tr.appendChild(td(exit.repairerName));
      tr.appendChild(td(exit.plannedDate));
      tr.appendChild(td(statusLabel(exit.status)));
      tr.appendChild(td(exit.createdBy));
      const act = document.createElement("td");
      const openBtn = document.createElement("button");
      openBtn.type = "button";
      openBtn.className = "dmo-button";
      openBtn.textContent = "Ver";
      openBtn.addEventListener("click", () => openExitDetail(exit.repairExitId));
      act.appendChild(openBtn);
      tr.appendChild(act);
      body.appendChild(tr);
    });
  }

  function statusLabel(status) {
    const map = { preparacao: "Preparação", a_retirar: "A retirar", enviado: "Enviado", retorno_parcial: "Retorno parcial", concluido: "Concluído", cancelado: "Cancelado" };
    return map[status] || status;
  }

  function openExitDetail(exitId) {
    api("/api/reparacao-externa/" + exitId).then((exit) => {
      renderExitDetail(exit);
      document.querySelector(".reparacao-externa-tabs .tab[data-view='envios']").classList.add("active");
      document.querySelectorAll(".reparacao-externa-view").forEach((v) => v.classList.toggle("active", v.id === "envios"));
    }).catch((e) => say(e.message, false));
  }

  function renderExitDetail(exit) {
    const detailCard = el("exitDetailCard");
    if (!detailCard) return;
    detailCard.hidden = false;
    detailCard.querySelector("[data-detail-title]").textContent = "Lista " + exit.repairExitId.slice(0, 8) + " — " + statusLabel(exit.status);
    const tbody = detailCard.querySelector("[data-detail-body]");
    tbody.innerHTML = "";
    (exit.items || []).forEach((item) => {
      const tr = document.createElement("tr");
      const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
      tr.appendChild(td(item.reference));
      tr.appendChild(td(item.lot));
      tr.appendChild(td(item.number));
      tr.appendChild(td(item.outOperatorId));
      tr.appendChild(td(item.inOperatorId));
      const act = document.createElement("td");
      if (!item.inAtUtc && item.outAtUtc) {
        const retBtn = document.createElement("button");
        retBtn.type = "button";
        retBtn.className = "dmo-button success";
        retBtn.textContent = "Confirmar retorno";
        retBtn.addEventListener("click", () => confirmReturn(item.repairExitItemId));
        act.appendChild(retBtn);
      } else if (!item.outAtUtc) {
        const pickBtn = document.createElement("button");
        pickBtn.type = "button";
        pickBtn.className = "dmo-button";
        pickBtn.textContent = "Confirmar recolha";
        pickBtn.addEventListener("click", () => confirmPickup(item.repairExitItemId));
        act.appendChild(pickBtn);
      }
      tr.appendChild(act);
      tbody.appendChild(tr);
    });
  }

  function confirmPickup(itemId) {
    api("/api/reparacao-externa/items/" + itemId + "/recolha", json("POST", {})).then(() => {
      say("Recolha confirmada (posição libertada).");
      refreshExits();
    }).catch((e) => say(e.message, false));
  }

  function confirmReturn(itemId) {
    const position = el("returnPosition").value || "";
    if (!/^\d{4}$/.test(position)) { say("Indique a posição de retorno (4 dígitos).", false); return; }
    api("/api/reparacao-externa/items/" + itemId + "/retorno", json("POST", { positionCode: position })).then(() => {
      say("Retorno confirmado.");
      el("returnPosition").value = "";
      refreshExits();
    }).catch((e) => say(e.message, false));
  }

  function refreshExits() {
    api("/api/reparacao-externa").then(renderExits).catch((e) => say(e.message, false));
  }

  document.querySelector("[data-refresh-exits]").addEventListener("click", refreshExits);
  document.querySelector("[data-open-exit]").addEventListener("click", () => {
    if (selectedExitId) openExitDetail(selectedExitId);
  });
  document.querySelector("[data-disponibilizar]").addEventListener("click", () => {
    if (!selectedExitId) { say("Selecione uma lista.", false); return; }
    api("/api/reparacao-externa/" + selectedExitId + "/disponibilizar", json("POST", {})).then(() => {
      say("Lista disponibilizada para retirada.");
      refreshExits();
    }).catch((e) => say(e.message, false));
  });

  // ---- Histórico ----
  function renderHistory(rows) {
    const body = el("historyBody");
    const empty = el("historyEmpty");
    body.innerHTML = "";
    if (!rows || !rows.length) { empty.hidden = false; return; }
    empty.hidden = true;
    rows.forEach((row) => {
      const tr = document.createElement("tr");
      const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
      tr.appendChild(td(row.listId ? row.listId.slice(0, 8) : ""));
      tr.appendChild(td(row.type));
      tr.appendChild(td(row.reference));
      tr.appendChild(td(row.lot));
      tr.appendChild(td(row.number));
      tr.appendChild(td(row.repairerName));
      tr.appendChild(td(formatDateTime(row.saida)));
      tr.appendChild(td(row.operadorSaida));
      tr.appendChild(td(formatDateTime(row.entrada)));
      tr.appendChild(td(row.operadorEntrada));
      tr.appendChild(td(statusLabel(row.state)));
      body.appendChild(tr);
    });
  }

  function formatDateTime(v) {
    if (!v) return "—";
    const d = new Date(v);
    return isNaN(d.getTime()) ? "—" : d.toLocaleString("pt-PT");
  }

  document.querySelector("[data-refresh-history]").addEventListener("click", () => {
    api("/api/reparacao-externa/historico").then(renderHistory).catch((e) => say(e.message, false));
  });

  // ---- Definições: repairers ----
  function renderRepairers(list) {
    const body = el("repairersBody");
    body.innerHTML = "";
    (list || []).forEach((r) => {
      const tr = document.createElement("tr");
      const td = (text) => { const c = document.createElement("td"); c.textContent = text == null ? "—" : text; return c; };
      tr.appendChild(td(r.name));
      tr.appendChild(td(r.active ? "Ativo" : "Inativo"));
      const act = document.createElement("td");
      if (r.active) {
        const deactBtn = document.createElement("button");
        deactBtn.type = "button";
        deactBtn.className = "dmo-button danger";
        deactBtn.textContent = "Desativar";
        deactBtn.addEventListener("click", () => deactivateRepairer(r.repairerId));
        act.appendChild(deactBtn);
      }
      tr.appendChild(act);
      body.appendChild(tr);
    });
  }

  function refreshRepairers() {
    api("/api/reparacao-externa/repairers").then((list) => {
      loadAllRepairers();
    }).catch((e) => say(e.message, false));
  }

  document.querySelector("[data-create-repairer]").addEventListener("click", () => {
    const name = el("newRepairerName").value;
    if (!name) { say("Indique o nome.", false); return; }
    api("/api/reparacao-externa/repairers", json("POST", { name })).then(() => {
      say("Reparador adicionado.");
      el("newRepairerName").value = "";
      refreshRepairers();
      document.querySelectorAll("[data-repairer-for]").forEach((sel) => loadRepairersInto(sel));
    }).catch((e) => say(e.message, false));
  });

  function deactivateRepairer(id) {
    api("/api/reparacao-externa/repairers/" + id + "/deactivate", json("POST", {})).then(() => {
      say("Reparador desativado.");
      loadAllRepairers();
    }).catch((e) => say(e.message, false));
  }

  // ---- Definições: line associations (compact form + list) ----
  let allRepairers = [];     // { repairerId, name } — cached for autocomplete
  let currentLineAssociations = {}; // line -> repairerId

  // Load all repairers once (used by autocomplete + dropdowns)
  function loadAllRepairers() {
    api("/api/reparacao-externa/repairers").then((list) => {
      allRepairers = list || [];
      renderRepairers(list);
      // Update all repairer dropdowns on the page
      document.querySelectorAll("[data-repairer-for]").forEach((sel) => loadRepairersInto(sel));
    }).catch(() => { /* ignore */ });
  }

  function renderLineAssociations() {
    const list = el("lineAssociationsList");
    if (!list) return;
    
    const LINES = ["B1", "B2", "B3", "C1", "C2", "C3"];
    list.innerHTML = "";
    
    LINES.forEach((line) => {
      const li = document.createElement("li");
      const repairerId = currentLineAssociations[line];
      const repairer = allRepairers.find((r) => r.repairerId === repairerId);
      
      let nameHtml;
      if (repairer) {
        nameHtml = `<span class="repairer-name">${esc(repairer.name)}</span>`;
      } else {
        nameHtml = `<span class="no-association">—</span>`;
      }
      
      li.innerHTML = `
        <span><span class="line-label">${esc(line)}</span> <span>${nameHtml}</span></span>
        <button type="button" class="remove-btn" data-remove-line-association="${esc(line)}" title="Remover associação">×</button>
      `;
      
      const removeBtn = li.querySelector(".remove-btn");
      removeBtn.addEventListener("click", () => removeLineAssociation(line));
      
      list.appendChild(li);
    });
  }

  // Autocomplete for repairer field
  function showRepairerAutocomplete(filter = "") {
    const input = el("lineAssocRepairer");
    const dropdown = el("lineAssocRepairerDropdown");
    if (!input || !dropdown) return;
    
    if (!allRepairers.length) loadAllRepairers();
    
    const filtered = allRepairers.filter((r) => 
      r.name.toLowerCase().includes(filter.toLowerCase()) && r.active
    );
    
    dropdown.innerHTML = "";
    if (!filtered.length) {
      dropdown.classList.remove("visible");
      return;
    }
    
    filtered.forEach((r) => {
      const li = document.createElement("li");
      li.textContent = r.name;
      li.dataset.id = r.repairerId;
      li.addEventListener("click", () => {
        input.value = r.name;
        selectedRepairerId = r.repairerId;
        dropdown.classList.remove("visible");
      });
      dropdown.appendChild(li);
    });
    
    dropdown.classList.add("visible");
  }

  function hideRepairerAutocomplete() {
    const dropdown = el("lineAssocRepairerDropdown");
    if (dropdown) dropdown.classList.remove("visible");
  }

  function clearLineForm() {
    const input = el("lineAssocRepairer");
    if (input) input.value = "";
    selectedRepairerId = null;
  }

  document.querySelector("[data-save-line-association]").addEventListener("click", () => {
    const line = el("lineAssocLine").value;
    const repairerId = selectedRepairerId;
    
    if (!repairerId) {
      say("Selecione um reparador.", false);
      return;
    }
    
    api("/api/reparacao-externa/line-defaults", json("POST", {
      line,
      toolType: "CM",
      repairerId
    })).then(() => {
      say("Associação guardada.");
      currentLineAssociations[line] = repairerId;
      clearLineForm();
      hideRepairerAutocomplete();
      renderLineAssociations();
    }).catch((e) => say(e.message, false));
  });

  // Remove association handler
  document.addEventListener("click", (evt) => {
    if (evt.target.dataset.removeLineAssociation) {
      evt.preventDefault();
      const line = evt.target.dataset.removeLineAssociation;
      delete currentLineAssociations[line];
      say(`Associação ${line} removida.`, true);
      renderLineAssociations();
    }
  });

  // Repairer autocomplete input events
  const lineAssocInput = () => el("lineAssocRepairer");
  let lineAssocInputHandler = null;
  
  (function setupAutocomplete() {
    const input = lineAssocInput();
    if (!input) return;
    
    input.addEventListener("input", () => {
      showRepairerAutocomplete(input.value);
    });
    
    input.addEventListener("focus", () => {
      showRepairerAutocomplete(input.value);
    });
    
    input.addEventListener("blur", () => {
      setTimeout(hideRepairerAutocomplete, 200);
    });
  })();

  // Initial loads
  refreshExits();
  api("/api/reparacao-externa/historico").then(renderHistory).catch(() => { /* ignore */ });
  loadAllRepairers();
})();