/* ============================================================
   BA DMO — jobon.js (U-13)
   Job On page behavior per portal-dmo-design-final/job-on-v48-folha-producao.html,
   aligned with the canonical shared contracts (dmo-interactions.js +
   dmo-calendar.js loaded globally from _Layout).

   Selector contract (F-02/F-03/F-06/F-07/F-08 repair — Razor and JS
   now share one vocabulary):
   - Tabs: .jobon-tabs .tab[data-view] -> .jobon-view#<view>
   - Edit mode: #editSheet / #saveSheet / #sheetMode / #jobSheet
   - Inventory picker: #inventoryPicker + .tool-title-actions .btn.compact
   - CAL rows: #calRows / #addCalRow
   - Catalog options: #catalogRows / #addCatalogOption / #newCatalogOption
     / #editCatalogOption / #disableCatalogOption
   - Image: #imagePreview / #job-image-input / #link-image-dir-btn
     / #replace-image-btn / #remove-image-btn

   The canonical calendar and list selections are delegated to the
   shared scripts; this file only binds Job On domain-free UX.
   No domain logic; purely presentational interaction.
  ============================================================ */
(function () {
  const qs = (s, r = document) => r.querySelector(s);
  const qsa = (s, r = document) => [...r.querySelectorAll(s)];

  // Expose capability flags the shared design-system respects (the same the
  // server used for rendering). Defense-in-depth only: server-side checks are
  // authoritative, these merely drive presentation.
  function syncCapabilityAttributes() {
    document.body.setAttribute("data-can-edit-jobon", String(Boolean(qs("#editSheet"))));
    document.body.setAttribute(
      "data-can-confirm-verifications",
      String(Boolean(qs(".checks input[type=\"checkbox\"]:not([disabled])"))));
  }

  // Smallest safe HTML escaping helper — consistent with other application scripts.
  function esc(value) {
    var map = { "&": "&" + "amp;", "<": "&" + "lt;", ">": "&" + "gt;", "\"": "&" + "quot;", "'": "&" + "#39;" };
    return String(value ?? "").replace(/[&<>"']/g, function (c) { return map[c]; });
  }

  // Tab switching (F-02)
  function openView(viewId) {
    qsa(".jobon-tabs .tab").forEach(tab => {
      tab.classList.toggle("active", tab.dataset.view === viewId);
    });

    qsa(".jobon-view").forEach(v => v.classList.toggle("active", v.id === viewId));
  }

  // Initialize tab listeners
  qsa(".jobon-tabs .tab[data-view]").forEach(tab => {
    tab.onclick = () => openView(tab.dataset.view);
  });

  // Open the active tab on load. R011: the landing (`/jobon` with no ?id=) opens
  // Planeamento (calendar + list); opening a specific folha (?id=) selects Job On.
  const initialView = qs(".jobon-tabs .tab.active")?.dataset.view || "planeamento";
  openView(initialView);

  // ---- R011 Universal Landing: calendar + production list interaction ----
  // Single click on a day: select + filter the production list to that day by
  // reloading `/jobon?date=YYYY-MM-DD` (one server-side planning source for both
  // calendar and list — §8). Double click on a day: open the specific Job On that
  // day resolves to (exact job_on_id), without guessing when the day is ambiguous.
  const calendar = qs("[data-dmo-calendar]");
  const list = qs("#jobList");
  let dayNavTimer = null;

  function rowsForDate(date) {
    if (!list) return [];
    return [...list.querySelectorAll("tr[data-dmo-row]")]
      .filter(row => row.getAttribute("data-date") === date);
  }

  // Resolve the exact Job On to open from the list rows of a date (§10):
  // one row -> it; many rows -> the selected row when unambiguous; otherwise null.
  function resolveOpenUrl(date) {
    const rows = rowsForDate(date);
    if (rows.length === 0) return null;
    if (rows.length === 1) return rows[0].getAttribute("data-open-url");
    const selected = rows.find(r => r.classList.contains("selected"));
    return selected ? selected.getAttribute("data-open-url") : null;
  }

  if (calendar) {
    // Single click -> navigate to the selected day (filter the list). Debounced so
    // a double click can cancel it and open the Job On instead (§9/§10).
    calendar.addEventListener("dmo:date-select", (e) => {
      const date = e.detail && e.detail.date;
      if (!date) return;
      if (dayNavTimer) { clearTimeout(dayNavTimer); dayNavTimer = null; }
      dayNavTimer = setTimeout(() => {
        window.location.assign("/jobon?date=" + date);
      }, 280);
    });

    // Double click -> open the exact Job On of that day (cancel any pending single
    // click navigation). Ambiguous days (multiple productions, no explicit row) only
    // select the day and let the user pick from the list — never guess (§10).
    calendar.addEventListener("dblclick", (e) => {
      const day = e.target.closest("[data-date]");
      if (!day || day.disabled) return;
      if (dayNavTimer) { clearTimeout(dayNavTimer); dayNavTimer = null; }
      const url = resolveOpenUrl(day.getAttribute("data-date"));
      if (url) { window.location.assign(url); }
    });
  }

  // Sheet mode toggle (GLM-JOB-04 / F-03): #editSheet / #saveSheet / #sheetMode
  const sheet = qs("#jobSheet");
  const editBtn = qs("#editSheet");
  const saveBtn = qs("#saveSheet");
  const sheetMode = qs("#sheetMode");

  const setEditing = (editing) => {
    const editingMode = !!editing;
    sheet?.classList.toggle("editing", editingMode);
    if (editBtn) {
      editBtn.textContent = editingMode ? "Cancelar edição" : "Editar folha";
    }
    if (saveBtn) {
      saveBtn.hidden = !editingMode;
    }
    if (sheetMode) {
      sheetMode.textContent = editingMode ? "Modo edição" : "Modo consulta";
    }
    if (!editingMode) {
      qsa(".inventory-picker.open").forEach(p => p.classList.remove("open"));
    }
    // Persistence-before-UI: save is a presentational close here; real
    // revision persistence is server-side and returns before this runs.
    syncCapabilityAttributes();
  };

  if (editBtn) {
    editBtn.onclick = () => setEditing(!sheet?.classList.contains("editing"));
  }

  if (saveBtn) {
    saveBtn.onclick = () => {
      if (sheet?.classList.contains("editing")) {
        setEditing(false);
      }
    };
  }

  // Inventory picker (tool "Alterar" button)
  qsa(".tool-title-actions .btn.compact").forEach(btn => {
    btn.onclick = () => {
      const picker = qs("#inventoryPicker");
      if (picker) {
        picker.classList.add("open");
        picker.scrollIntoView({ behavior: "smooth", block: "center" });
      }
    };
  });

  // CAL rows management (F-06): #calRows / #addCalRow
  const calRows = qs("#calRows");
  const addCalRowBtn = qs("#addCalRow");

  if (addCalRowBtn && calRows) {
    addCalRowBtn.onclick = () => {
      calRows.insertAdjacentHTML("beforeend", `
        <tr data-testid="cal-row">
          <td><input type="text" aria-label="Elemento CAL" placeholder="Novo elemento" /></td>
          <td><input type="text" aria-label="Valor CAL" placeholder="Valor" /></td>
          <td><input type="number" aria-label="Quantidade em máquina" placeholder="0" /></td>
          <td class="edit-only"><button type="button" class="btn compact danger cal-remove" data-testid="btn-remove-cal-row">Remover</button></td>
        </tr>`);
    };
  }

  // CAL remove delegation (only while editing)
  if (calRows) {
    calRows.addEventListener("click", e => {
      const removing = sheet?.classList.contains("editing") && e.target.matches(".cal-remove");
      if (removing) {
        e.target.closest("tr")?.remove();
      }
    });
  }

  // Reference-owned article image. The browser selects a file from the
  // configured company image directory; only its safe file name is persisted.
  const imageInput = qs("#job-image-input");
  const linkImageDirBtn = qs("#link-image-dir-btn");
  const replaceImageBtn = qs("#replace-image-btn");
  const removeImageBtn = qs("#remove-image-btn");
  const imagePreview = qs("#imagePreview");
  const imageDirectoryStatus = qs("#image-directory-status");
  const serverImage = qs("#article-reference-image");
  const emptyImage = qs("#article-image-empty");
  let pendingImageAction = "attach";

  // Resolve the current Job On ID from the page context (rendered by the server
  // into a meta tag when a specific Job On is open).
  function getCurrentJobOnId() {
    const meta = qs('meta[name="jobon-id"]');
    return meta && meta.content ? meta.content : null;
  }

  // Call the server-side API to persist the image association.
  async function persistImageAction(action, imageAssetId) {
    const jobOnId = getCurrentJobOnId();
    if (!jobOnId) {
      console.warn("Job On ID not available; image action not persisted.");
      return null;
    }

    const url = `/api/jobon/${jobOnId}/image/${action}`;
    const body = action === "remove" ? null : JSON.stringify({ imageAssetId });
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: body
    });

    if (!response.ok) {
      const error = await response.json().catch(() => null);
      console.error(`Image ${action} failed:`, error);
      alert(`Não foi possível ${action === "attach" ? "ligar" : action === "replace" ? "substituir" : "remover"} a imagem. A alteração não foi guardada.`);
      return null;
    }

    return await response.json();
  }

  function showEmptyImage() {
    if (serverImage) serverImage.hidden = true;
    if (emptyImage) emptyImage.hidden = false;
  }

  function showServerImage() {
    if (serverImage) serverImage.hidden = false;
    if (emptyImage) emptyImage.hidden = true;
  }

  if (serverImage) {
    serverImage.addEventListener("load", showServerImage);
    serverImage.addEventListener("error", showEmptyImage);
    if (serverImage.complete) {
      if (serverImage.naturalWidth > 0) showServerImage();
      else showEmptyImage();
    }
  }

  if (linkImageDirBtn) {
    linkImageDirBtn.addEventListener("click", () => {
      pendingImageAction = "attach";
    });
  }

  if (replaceImageBtn) {
    replaceImageBtn.onclick = () => {
      pendingImageAction = "replace";
      imageInput?.click();
    };
  }

  if (removeImageBtn) {
    removeImageBtn.onclick = async () => {
      if (!confirm("Remover a imagem associada a esta Referência? Esta ação é auditada.")) {
        return;
      }

      const result = await persistImageAction("remove", null);
      if (result) {
        if (imageDirectoryStatus) {
          imageDirectoryStatus.textContent = "Sem imagem associada à Referência.";
        }
        showEmptyImage();
      }
    };
  }

  if (imageInput && imagePreview) {
    imageInput.onchange = async e => {
      const file = e.target.files[0];
      if (!file) return;
      const persisted = await persistImageAction(pendingImageAction, file.name);
      if (!persisted) return;

      if (imageDirectoryStatus) {
        imageDirectoryStatus.textContent = `Imagem da Referência: ${file.name}`;
      }

      if (serverImage) {
        serverImage.src = URL.createObjectURL(file);
        showServerImage();
      }

      pendingImageAction = "attach";
      imageInput.value = "";
    };
  }

  // Catalog options management (Definições) — F-07 selectors
  const catalogRows = qs("#catalogRows");
  const catalogInput = qs("#newCatalogOption");
  const addOptionBtn = qs("#addCatalogOption");
  const editOptionBtn = qs("#editCatalogOption");
  const disableOptionBtn = qs("#disableCatalogOption");
  const catalogSelect = qs("#piClampMaterial");
  let editingCatalogRow = null;

  const selectCatalogRow = (row) => {
    qsa("[data-option-row]").forEach(item => item.classList.toggle("selected", item === row));
    row?.setAttribute("aria-selected", "true");
  };

  if (catalogRows) {
    catalogRows.addEventListener("click", e => {
      const row = e.target.closest("[data-option-row]");
      if (row) {
        qsa("[data-option-row]").forEach(item => item.removeAttribute("aria-selected"));
        selectCatalogRow(row);
      }
    });
  }

  const renderCatalogSaveLabel = () => {
    if (addOptionBtn) {
      addOptionBtn.textContent = editingCatalogRow ? "Guardar alteração" : "Adicionar opção";
    }
  };

  if (addOptionBtn) {
    addOptionBtn.onclick = () => {
      const label = catalogInput?.value.trim();
      if (!label) return;

      const duplicate = qsa("strong", catalogRows)
        .some(item =>
          item.textContent.toLocaleLowerCase("pt-PT") === label.toLocaleLowerCase("pt-PT") &&
          item.closest("tr") !== editingCatalogRow);

      if (duplicate) {
        catalogInput?.focus();
        return;
      }

      if (editingCatalogRow) {
        const current = qs("strong", editingCatalogRow).textContent;
        qs("strong", editingCatalogRow).textContent = label;
        if (catalogSelect) {
          const option = qsa("option", catalogSelect).find(o => o.value === current);
          if (option) { option.value = label; option.textContent = label; }
        }
        editingCatalogRow = null;
        renderCatalogSaveLabel();
      } else {
        const order = qsa("[data-option-row]", catalogRows).length + 1;
        catalogRows.insertAdjacentHTML("beforeend", `
          <tr data-option-row>
            <td>${order}</td>
            <td><strong>${esc(label)}</strong></td>
            <td><span class="pill good">Ativa</span></td>
            <td>Disponível em novos registos</td>
          </tr>`);
        const row = catalogRows.lastElementChild;
        if (catalogSelect) {
          catalogSelect.add(new Option(label, label));
        }
        selectCatalogRow(row);
      }

      if (catalogInput) { catalogInput.value = ""; }
    };
  }

  if (editOptionBtn) {
    editOptionBtn.onclick = () => {
      const row = qs("[data-option-row].selected", catalogRows);
      if (!row || !catalogInput) return;
      editingCatalogRow = row;
      catalogInput.value = qs("strong", row).textContent || "";
      catalogInput.focus();
      renderCatalogSaveLabel();
    };
  }

  if (disableOptionBtn) {
    disableOptionBtn.onclick = () => {
      const row = qs("[data-option-row].selected", catalogRows);
      if (!row) return;
      const pill = qs(".pill", row);
      if (pill) { pill.className = "pill"; pill.textContent = "Inativa"; }
      row.lastElementChild.textContent = "Mantida apenas no histórico";
      editingCatalogRow = null;
      renderCatalogSaveLabel();
    };
  }

  // Double-click on a planning row opens the folha via the canonical
  // dmo:list-open event, whose shared bridge (dmo-interactions.js) navigates
  // the row's data-open-url. No competing handler needed here.

  // ============================================================
  // Sidepanel: live production context per line (R009 reuse).
  // Shows B1–C3 with current Job On production/reference from the
  // same projection used by Reparação Interna and Boquilhas sidepanel.
  // Hides automatically because it sits inside #planeamento (active-only).
  // ============================================================
  const LINES = ['B1', 'B2', 'B3', 'C1', 'C2', 'C3'];
  const linePanelEl = document.getElementById('linePanel');

  async function loadSidepanel() {
    if (!linePanelEl) return;
    try {
      const res = await fetch('/api/boquilhas/production-context');
      if (!res.ok) throw new Error('Erro ao carregar contexto');
      const cards = await res.json();
      const byLine = {};
      cards.forEach((c) => { byLine[c.line] = c; });
      linePanelEl.innerHTML = LINES.map((line) => {
        const card = byLine[line] || null;
        if (card && card.hasActiveContext) {
          return `<div class="line-card">
            <span class="line-name">${esc(line)}</span>
            <span class="line-production">${esc(card.productionCode || '')}</span>
            <span class="line-ref">${esc(card.reference || '')}</span>
          </div>`;
        }
        return `<div class="line-card empty"><span class="line-empty">Sem produção</span></div>`;
      }).join('');
    } catch (err) {
      linePanelEl.innerHTML = LINES.map((line) =>
        `<div class="line-card empty"><span class="line-empty">${esc(line)} — erro</span></div>`).join('');
    }
  }

  syncCapabilityAttributes();

  // ---- PDF document generation (Exportar) ----
  (function () {
    var btn = document.querySelector('.sheet-toolbar button[type="button"]:last-of-type');
    // Find the "Exportar" button by text content
    var buttons = document.querySelectorAll('.sheet-toolbar button');
    var exportBtn = null;
    for (var i = 0; i < buttons.length; i++) {
      if (buttons[i].textContent.trim() === 'Exportar') {
        exportBtn = buttons[i];
        break;
      }
    }
    if (!exportBtn) return;

    exportBtn.onclick = async function () {
      var meta = document.querySelector('meta[name="jobon-id"]');
      if (!meta || !meta.content) {
        alert('Nenhum Job On aberto para exportar.');
        return;
      }
      var jobOnId = meta.content;
      exportBtn.disabled = true;
      exportBtn.textContent = 'A gerar…';
      try {
        var tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
        var headers = { 'Content-Type': 'application/json' };
        if (tokenEl) headers['RequestVerificationToken'] = tokenEl.value;

        var res = await fetch('/api/jobon/' + jobOnId + '/document', {
          method: 'POST',
          headers: headers,
          credentials: 'same-origin'
        });

        if (!res.ok) {
          var err = await res.json().catch(function () { return null; });
          alert('Erro ao gerar documento: ' + (err ? err.message : res.statusText));
          return;
        }

        var blob = await res.blob();
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        // Extract filename from Content-Disposition or use default
        var cd = res.headers.get('Content-Disposition');
        var filename = 'JobOn_documento.pdf';
        if (cd) {
          var match = cd.match(/filename\*?=['"]?(?:UTF-\d['"]*)?([^;\r\n"']+)/i);
          if (match) filename = decodeURIComponent(match[1].trim());
        }
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
      } catch (e) {
        alert('Erro de rede ao gerar o documento.');
      } finally {
        exportBtn.disabled = false;
        exportBtn.textContent = 'Exportar';
      }
    };
  })();

  // Initial load on planeamento view (which is active by default).
  loadSidepanel();
})();
