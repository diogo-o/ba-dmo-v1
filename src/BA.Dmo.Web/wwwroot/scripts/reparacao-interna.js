// BA DMO — Reparação Interna (U-16) wiring only.
// No business logic is implemented client-side: it calls the gated
// /api/reparacao-interna/* endpoints and renders the returned server results
// (GLM-DSN / GLM-CORE: no duplicated domain rules in JS).
(function () {
  'use strict';

  const $ = (sel) => document.querySelector(sel);
  const $$ = (sel) => Array.from(document.querySelectorAll(sel));
  const toast = $('#toast');

  // ---- State ---------------------------------------------------------------
  let selectedLine = null;
  let currentType = null;
  let activeContext = null;       // resolved InternalRepairContextDto (Single)
  let selectedRecordId = null;    // history row selected (click)
  let historyRows = [];
  let pageState = { from: 0, pageSize: 20 };
  let openCorrection = false;

  // ---- Tabs ----------------------------------------------------------------
  const tabs = $$('.reparacao-interna-tabs .tab');
  tabs.forEach((tab) => {
    tab.addEventListener('click', () => {
      tabs.forEach((t) => t.classList.toggle('active', t === tab));
      $$('.reparacao-interna-view').forEach((v) =>
        v.classList.toggle('active', v.id === tab.dataset.view));
    });
  });

  // ---- Helpers ---------------------------------------------------------------
  function esc(value) {
    return String(value ?? '').replace(/[&<>"']/g, (c) => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
  }

  function showToast(message, isError) {
    toast.textContent = message;
    toast.classList.toggle('error', isError === true);
    toast.hidden = false;
    clearTimeout(showToast._t);
    showToast._t = setTimeout(() => { toast.hidden = true; }, 4000);
  }

  async function api(url, options) {
    const res = await fetch(url, options);
    if (res.ok) return await res.json();
    let payload = { code: 'ERROR', message: 'Erro de servidor.' };
    try { payload = await res.json(); } catch (e) { /* ignore */ }
    throw Object.assign(new Error(payload.message), { code: payload.code });
  }

  const jsonPost = (url, body) => api(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });

  // ---- Line-card selector (Registo) ------------------------------------------
  async function loadLineCards() {
    try {
      const cards = await api('/api/reparacao-interna/line-cards');
      const container = $('#lineChoice');
      container.innerHTML = cards.map((card) =>
        `<button type="button" class="line-card${card.Line === selectedLine ? ' active' : ''}" data-line="${esc(card.Line)}" data-testid="line-card-${esc(card.Line)}">
            <span class="line-card-label">${esc(card.Line)}</span>
            <span class="line-card-ref">${card.HasActiveContext ? esc(card.Reference) : 'Sem Job On ativo'}</span>
          </button>`).join('');
      $$('#lineChoice .line-card').forEach((c) =>
        c.addEventListener('click', () => selectLine(c.dataset.line)));
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function selectLine(line) {
    selectedLine = line;
    currentType = null;
    selectedRecordId = null;
    activeContext = null;
    $$('#lineChoice .line-card').forEach((c) =>
      c.classList.toggle('active', c.dataset.line === line));
    resetRegistoForm();
    await resolveContext(line);
  }

  async function resolveContext(line) {
    const contextDetail = $('#contextDetail');
    const blocked = $('#contextBlocked');
    const ambiguous = $('#contextAmbiguous');
    const note = $('#contextNote');
    contextDetail.hidden = true;
    blocked.hidden = true;
    ambiguous.hidden = true;
    note.hidden = true;
    try {
      const ctx = await api(`/api/reparacao-interna/context?line=${encodeURIComponent(line)}`);
      $('[data-context-readonly]').textContent =
        `Linha ${line}: Referência ativa do Job On — ${ctx.reference ?? '—'}`;

      // R009: context is assistance, never a block. Single → prefill. None/Ambiguous →
      // show a non-blocking note; the register is still possible (override available).
      if (ctx.kind === 1) {
        // Single
        activeContext = ctx;
        $('[data-ctx-producao]').textContent = ctx.productionCode ?? '—';
        $('[data-ctx-referencia]').textContent = ctx.reference ?? '—';
        $('[data-ctx-linha]').textContent = line;
        $('[data-ctx-jobon]').textContent = ctx.jobOnId ? String(ctx.jobOnId).slice(0, 8) : '—';
        $('[data-ctx-periodo]').textContent = ctx.validFromUtc ? `${formatDT(ctx.validFromUtc)} – ${formatDT(ctx.validToUtc)}` : '—';
        contextDetail.hidden = false;
        note.hidden = true;
      } else if (ctx.kind === 2) {
        // Ambiguous: multiple auto-contexts; do not auto-prefill. Non-blocking.
        activeContext = null;
        contextDetail.hidden = false;
        note.hidden = false;
        note.textContent = 'Existem vários contextos ativos. O contexto automático fica por escolher; pode registar ou usar Editar contexto.';
      } else {
        // None: no auto-context. Non-blocking.
        activeContext = null;
        contextDetail.hidden = false;
        note.hidden = false;
        note.textContent = 'Sem produção/Job On ativo para esta Linha e data — contexto automático indisponível. Pode registar o facto ou usar Editar contexto.';
      }
    } catch (err) {
      showToast(err.message, true);
    }
  }

  // ---- Type + register -------------------------------------------------------
  $$('#contextDetail [data-type]').forEach((b) =>
    b.addEventListener('click', () => {
      currentType = b.dataset.type;
      $$('#contextDetail [data-type]').forEach((x) =>
        x.classList.toggle('active', x === b));
      $('#individualNumbers').focus();
    }));

  function resetRegistoForm() {
    $('#individualNumbers').value = '';
    currentType = null;
    $$('#contextDetail [data-type]').forEach((x) => x.classList.remove('active'));
    $('#overridePanel').hidden = true;
    $('#contextNote').hidden = true;
    $('#registerSummaryCard').hidden = true;
  }

  // Parse the multi-number textarea into occurrence entries, preserving repeats.
  // Accepts newline, comma, or whitespace separated numbers; empty tokens dropped.
  function parseNumbers(raw) {
    const tokens = String(raw || '').split(/[\s,;]+/).map(s => s.trim()).filter(s => s.length > 0);
    return tokens; // repeated values kept as distinct occurrences
  }

  $('#individualNumbers').addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && e.ctrlKey) openRegisterSummary();
  });

  $('[data-registrar]').addEventListener('click', openRegisterSummary);

  $('[data-toggle-override]').addEventListener('click', () => {
    const p = $('#overridePanel');
    p.hidden = !p.hidden;
    if (!p.hidden) {
      $('#ovProduction').value = activeContext && activeContext.productionCode ? activeContext.productionCode : '';
      $('#ovReference').value = activeContext && activeContext.reference ? activeContext.reference : '';
    }
  });

  function openRegisterSummary() {
    if (!selectedLine) return showToast('Escolha uma Linha primeiro.', true);
    if (!currentType) return showToast('Escolha o tipo CM, MF ou BQ.', true);
    const numbers = parseNumbers($('#individualNumbers').value);
    if (!numbers.length) return showToast('Introduza pelo menos um número reparado.', true);

    $('#registerSummary').textContent =
      `Linha ${selectedLine} · ${currentType} · N.ºs ${numbers.join(', ')}` +
      (activeContext && activeContext.reference ? ` · Referência ${activeContext.reference}` : ' · contexto por preencher');
    $('#registerSummaryCard').hidden = false;
  }

  $('[data-cancel-register]').addEventListener('click', () => {
    $('#registerSummaryCard').hidden = true;
  });

  $('[data-confirm-register]').addEventListener('click', async () => {
    try {
      const numbers = parseNumbers($('#individualNumbers').value);
      const overrideProduction = $('#overridePanel').hidden ? null : $('#ovProduction').value.trim() || null;
      const overrideReference = $('#overridePanel').hidden ? null : $('#ovReference').value.trim() || null;
      const result = await jsonPost('/api/reparacao-interna', {
        line: selectedLine,
        toolType: currentType,
        numbers: numbers,
        overrideProduction: overrideProduction,
        overrideReference: overrideReference
      });
      showToast(`Reparação registada (${result.count ?? numbers.length} ocorrência(s)).`);
      $('#registerSummaryCard').hidden = true;
      $('#individualNumbers').value = '';
      $('#individualNumbers').focus();
      await loadLineCards();
      await applyFilter();
    } catch (err) {
      showToast(err.message, true);
    }
  });

  $('[data-refresh-context]').addEventListener('click', () => {
    if (selectedLine) { resetRegistoForm(); resolveContext(selectedLine); }
  });

  // ---- Historical --------------------------------------------------------------
  function collectFilter() {
    const type = $('#fType').value;
    return {
      from: $('#fFrom').value ? new Date($('#fFrom').value + 'T00:00:00Z').toISOString() : null,
      to: $('#fTo').value ? new Date($('#fTo').value + 'T23:59:59Z').toISOString() : null,
      line: $('#fLine').value || null,
      type: type || null,
      number: $('#fNumber').value.trim() || null,
      operatorId: $('#fOperator').value.trim() || null,
      onlyCorrected: $('#fOnlyCorrected').checked
    };
  }

  async function applyFilter() {
    const f = collectFilter();
    const q = new URLSearchParams();
    if (f.from) q.set('from', f.from);
    if (f.to) q.set('to', f.to);
    if (f.line) q.set('line', f.line);
    if (f.type) q.set('type', f.type);
    if (f.number) q.set('number', f.number);
    if (f.operatorId) q.set('operatorId', f.operatorId);
    if (f.onlyCorrected) q.set('onlyCorrected', 'true');
    try {
      const rows = await api(`/api/reparacao-interna/historico?${q.toString()}`);
      historyRows = rows || [];
      renderHistory();
    } catch (err) { showToast(err.message, true); }
  }

  function renderHistory() {
    const body = $('#historyBody');
    const empty = $('#historyEmpty');
    const actions = $('#historyActions');
    selectedRecordId = null;
    const page = historyRows.slice(pageState.from, pageState.from + pageState.pageSize);
    body.innerHTML = page.map((r) =>
      `<tr data-record-id="${esc(r.recordId)}" data-corrected="${r.isCorrected}">
        <td>${formatDT(r.dataHora)}</td>
        <td>${esc(r.line)}</td>
        <td>${esc(r.productionCode ?? '—')}</td>
        <td>${esc(r.reference ?? '—')}</td>
        <td>${esc(r.lote ?? '—')}</td>
        <td>${esc(r.toolType)}</td>
        <td>${esc(r.individualNumber)}</td>
        <td>${esc(r.operatorId ?? '—')}</td>
        <td>${r.isCorrected ? 'Corrigido' : '—'}</td>
      </tr>`).join('');
    empty.hidden = body.children.length > 0;
    actions.hidden = body.children.length === 0;
    $('#page-info').textContent = `${Math.min(pageState.from + 1, historyRows.length || 0)}–${Math.min(pageState.from + pageState.pageSize, historyRows.length)} de ${historyRows.length}`;
    $('#page-prev').disabled = pageState.from === 0;
    $('#page-next').disabled = pageState.from + pageState.pageSize >= historyRows.length;

    // one click selects, double click opens detail.
    Array.from(body.children).forEach((tr) => {
      tr.addEventListener('click', () => {
        selectedRecordId = tr.dataset.recordId;
        Array.from(body.children).forEach((x) => x.classList.toggle('selected', x === tr));
      });
      tr.addEventListener('dblclick', () => openDetail(tr.dataset.recordId));
    });
  }

  $('[data-apply-filter]').addEventListener('click', () => { pageState.from = 0; applyFilter(); });
  $('[data-reset-filter]').addEventListener('click', () => {
    ['fFrom', 'fTo', 'fNumber', 'fOperator'].forEach((id) => { $('#' + id).value = ''; });
    ['fLine', 'fType'].forEach((id) => { $('#' + id).value = ''; });
    $('#fOnlyCorrected').checked = false;
    pageState.from = 0;
    applyFilter();
  });
  $('[data-page-prev]').addEventListener('click', () => { if (pageState.from > 0) { pageState.from -= pageState.pageSize; renderHistory(); } });
  $('[data-page-next]').addEventListener('click', () => { if (pageState.from + pageState.pageSize < historyRows.length) { pageState.from += pageState.pageSize; renderHistory(); } });

  // ---- Detail ------------------------------------------------------------------
  async function openDetail(recordId) {
    try {
      const d = await api(`/api/reparacao-interna/${recordId}`);
      $('#detailBody').innerHTML =
        `<tr><th>Linha</th><td>${esc(d.line)}</td></tr>` +
        `<tr><th>Produção</th><td>${esc(d.productionCode ?? '—')}</td></tr>` +
        `<tr><th>Referência</th><td>${esc(d.reference ?? '—')}</td></tr>` +
        `<tr><th>Lote</th><td>${esc(d.lote ?? '—')}</td></tr>` +
        `<tr><th>Tipo</th><td>${esc(d.toolType)}</td></tr>` +
        `<tr><th>N.º individual</th><td>${esc(d.individualNumber)}</td></tr>` +
        `<tr><th>Operador</th><td>${esc(d.operatorId ?? '—')}</td></tr>` +
        `<tr><th>Data/hora original</th><td>${formatDT(d.occurredAtUtc)}</td></tr>` +
        `<tr><th>Estado</th><td>${d.isCorrected ? 'Corrigido' : 'Atual'}</td></tr>` +
        (d.correctionReason ? `<tr><th>Motivo da correção</th><td>${esc(d.correctionReason)}</td></tr>` : '');
      const chainBody = $('#detailChain tbody');
      const seq = (d.correctionChain && d.correctionChain.length ? d.correctionChain : [d]);
      chainBody.innerHTML = seq.map((n) =>
        `<tr><td>${esc(n.line)}</td><td>${esc(n.toolType)}</td><td>${esc(n.individualNumber)}</td><td>${esc(n.operatorId ?? '—')}</td><td>${formatDT(n.occurredAtUtc)}</td><td>${n.isCorrected ? 'Corrigido' : 'Atual'}</td></tr>`).join('');
      $('#detailCard').hidden = false;
    } catch (err) { showToast(err.message, true); }
  }

  $('[data-close-detail]').addEventListener('click', () => { $('#detailCard').hidden = true; });

  // ---- Correction ----------------------------------------------------------------
  $('[data-corrigir]').addEventListener('click', () => {
    const recordId = selectedRecordId;
    if (!recordId) return showToast('Selecione uma linha para corrigir.', true);
    openCorrection(recordId);
  });

  async function openCorrection(recordId) {
    try {
      const d = await api(`/api/reparacao-interna/${recordId}`);
      if (d.isCorrected) return showToast('Não é possível corrigir uma correção existente; corrija o registo original.', true);
      selectedRecordId = recordId;
      $('#cLine').value = d.line;
      $('#cType').value = d.toolType;
      $('#cNumber').value = d.individualNumber;
      $('#cReason').value = '';
      $('#correctionCard').hidden = false;
    } catch (err) { showToast(err.message, true); }
  }

  $('[data-cancel-correction]').addEventListener('click', () => { $('#correctionCard').hidden = true; });

  $('[data-toggle-ovcorrection]').addEventListener('click', () => {
    const p = $('#ovCorrectionPanel');
    p.hidden = !p.hidden;
    if (!p.hidden) {
      $('#cProduction').value = '';
      $('#cReference').value = '';
    }
  });

  $('[data-confirm-correction]').addEventListener('click', async () => {
    if (!selectedRecordId) return showToast('Selecione um registo para corrigir.', true);
    try {
      await jsonPost(`/api/reparacao-interna/${selectedRecordId}/corrigir`, {
        recordId: selectedRecordId,
        line: $('#cLine').value,
        toolType: $('#cType').value,
        individualNumber: $('#cNumber').value.trim(),
        jobOnId: null,
        jobOnRevisionId: null,
        productionCode: $('#ovCorrectionPanel').hidden ? null : ($('#cProduction').value.trim() || null),
        reference: $('#ovCorrectionPanel').hidden ? null : ($('#cReference').value.trim() || null),
        lotId: null,
        reason: $('#cReason').value.trim() || null
      });
      showToast('Correção guardada. O original foi preservado e o Job On não foi alterado.');
      $('#correctionCard').hidden = true;
      await applyFilter();
    } catch (err) { showToast(err.message, true); }
  });

  // ---- Format helpers -----------------------------------------------------------
  function formatDT(value) {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d.getTime())) return String(value);
    return d.toLocaleString('pt-PT');
  }

  // ---- Init ---------------------------------------------------------------------
  loadLineCards();
  applyFilter();
})();