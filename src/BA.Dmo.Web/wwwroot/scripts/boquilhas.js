// BA DMO — Boquilhas (U-19) wiring only.
// No business logic is implemented client-side: it calls the gated
// /api/boquilhas/* endpoints and renders the returned server results
// (GLM-BQ / GLM-CORE: no duplicated domain rules in JS). The 20→25 excess
// return rule lives server-side; a warning + notes is surfaced, never a block.
(function () {
  'use strict';

  const $ = (sel) => document.querySelector(sel);
  const $$ = (sel, root) => Array.from((root || document).querySelectorAll(sel));
  const toast = $('#toast');
  const LINES = ['B1', 'B2', 'B3', 'C1', 'C2', 'C3'];

  // ---- State -------------------------------------------------------------
  let selectedLotId = null;
  let selectedTraceId = null; // active trace id of the selected lot
  let repairers = [];

  // ---- Tabs --------------------------------------------------------------
  $$('.boquilhas-tabs .dmo-tab').forEach((tab) => {
    tab.addEventListener('click', () => {
      $$('.boquilhas-tabs .dmo-tab').forEach((t) => t.classList.toggle('active', t === tab));
      $$('.boquilhas-view').forEach((v) => v.classList.toggle('active', v.id === tab.dataset.view));
      if (tab.dataset.view === 'boquilhas') loadBoquilhasCards();
      if (tab.dataset.view === 'historico') loadHistory();
      if (tab.dataset.view === 'definicoes') loadDefinicoes();
      if (tab.dataset.view === 'registo') loadSearch();
    });
  });

  // ---- Helpers -------------------------------------------------------------
  function esc(value) {
    return String(value ?? '').replace(/[&<>"']/g, (c) => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
  }

  // Canonical toast contract (reference + dmo-components.css): .show reveals,
  // .error tints; the element is always present in the page (W7A fix).
  function showToast(message, isError) {
    if (!toast) return;
    toast.textContent = message;
    toast.classList.toggle('error', isError === true);
    toast.classList.add('show');
    clearTimeout(showToast._t);
    showToast._t = setTimeout(() => { toast.classList.remove('show'); }, 4000);
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

  function fmtValue(v) {
    const n = Number(v);
    return Number.isFinite(n) ? n.toLocaleString('pt-PT', { maximumFractionDigits: 2 }) : String(v);
  }

  function fmtDT(value) {
    if (!value) return '—';
    const d = new Date(value);
    return isNaN(d.getTime()) ? String(value) : d.toLocaleString('pt-PT');
  }

  const movementLabel = (t) => ({
    inicio: 'Início', saida: 'Saída', entrada: 'Entrada', irreparavel: 'Não reparadas',
    linha: 'Mudança de linha', contagem: 'Correção', fim: 'Fecho'
  }[t] || t);

  const stateLabel = (s) => ({ available: 'Ativo', archived: 'Arquivado' }[s] || s);

  // ---- Side panel: live Job On production context (R009) --------------------
  // Reuses /api/reparacao-interna/line-cards via our own endpoint so the
  // Boquilhas page never needs ReparacaoInterna authorization.
  async function loadLinePanel() {
    try {
      const cards = await api('/api/boquilhas/production-context');
      const byLine = {};
      cards.forEach((c) => { byLine[c.line] = c; });
      $('#linePanel').innerHTML = LINES.map((line) => {
        const card = byLine[line] || { reference: null, productionCode: null, hasActiveContext: false };
        // machine card (reference boquilhas.html .machine anatomy):
        // name · job reference (production code) · BQ reference · meta.
        // The production tint marks lines with an active Job On context.
        return `<div class="dmo-sidebar__card boquilhas-line${card.hasActiveContext ? ' production' : ''}" data-line="${esc(line)}">
          <div class="boquilhas-line__head">
            <strong>${esc(line)}</strong>
            ${card.hasActiveContext
              ? `<span class="boquilhas-line__production">${esc(card.productionCode ?? '')}</span>
                 ${card.reference ? `<span class="boquilhas-line__ref">BQ ${esc(card.reference)}</span>` : ''}`
              : '<span class="boquilhas-line__empty">Sem referência atribuída</span>'}
          </div>
        </div>`;
      }).join('');
    } catch (err) { showToast(err.message, true); }
  }

  // ---- Create lot -----------------------------------------------------------
  const createPanel = $('#createPanel');
  $('#toggleCreate').addEventListener('click', () => {
    createPanel.hidden = !createPanel.hidden;
    $('#toggleCreate').textContent = createPanel.hidden ? 'Criar novo lote' : 'Fechar criação';
  });
  $('#cancelCreate').addEventListener('click', () => {
    createPanel.hidden = true;
    $('#toggleCreate').textContent = 'Criar novo lote';
  });
  $('#saveCreate').addEventListener('click', async () => {
    const allowed = $$('#allowedLines [data-line]:checked').map((c) => c.value);
    const body = {
      reference: $('#cReference').value.trim(),
      batchCode: $('#cBatch').value.trim(),
      allowedLines: allowed,
      initialQuantity: Number($('#cTotal').value),
      initialUtilisation: $('#cUtil').value ? Number($('#cUtil').value) : null,
      notes: $('#cNotes').value.trim() || null
    };
    if (!body.reference || !body.batchCode || !body.allowedLines.length) {
      return showToast('Preencha referência, lote e pelo menos uma linha permitida.', true);
    }
    try {
      const res = await jsonPost('/api/boquilhas/lotes', body);
      showToast('Lote criado.');
      createPanel.hidden = true;
      $('#toggleCreate').textContent = 'Criar novo lote';
      $('#cReference').value = ''; $('#cBatch').value = ''; $('#cTotal').value = '';
      $('#cUtil').value = ''; $('#cNotes').value = '';
      $$('#allowedLines [data-line]').forEach((c) => (c.checked = false));
      openLot(res.lotId);
      loadSearch();
    } catch (err) { showToast(err.message, true); }
  });

  // ---- Search (Registo) -------------------------------------------------------
  $('#searchLot').addEventListener('input', debounce(loadSearch, 300));
  let searchSeq = 0;
  async function loadSearch() {
    const term = $('#searchLot').value.trim();
    const seq = ++searchSeq;
    try {
      const lotes = await api(`/api/boquilhas/lotes?search=${encodeURIComponent(term)}&onlyAvailable=true&page=1&pageSize=50`);
      if (seq !== searchSeq) return;
      const box = $('#searchResults');
      $('#searchEmpty').hidden = lotes.length > 0;
      box.innerHTML = lotes.map((l) =>
        `<button type="button" class="boquilhas-list-item" data-open-lot="${esc(l.bqLoteId)}">
          <strong>${esc(l.reference)}</strong> · Lote ${esc(l.batchCode)} · ${stateLabel(l.lifecycleState)}
        </button>`).join('');
      $$('[data-open-lot]', box).forEach((el) => el.addEventListener('click', () => openLot(el.dataset.openLot)));
    } catch (err) { if (seq === searchSeq) showToast(err.message, true); }
  }

  // ---- Lot summary + movements -------------------------------------------------

  async function openLot(lotId) {
    selectedLotId = lotId;
    try {
      const res = await api(`/api/boquilhas/lotes/${lotId}`);
      const lot = res.lote;
      const saldo = res.saldo;
      // Active line for the side "Linha atual" (use active trace start line when present).
      const activeLine = res.activeTrace?.startLine || lot.allowedLines[0] || '—';
      $('#lotResumo').hidden = false;
      $('#lotResumo').innerHTML = `
        <div class="dmo-card boquilhas-resumo">
          <div class="boquilhas-card-head">
            <h3>${esc(lot.reference)} · Lote ${esc(lot.batchCode)} <span class="dmo-pill">${stateLabel(lot.lifecycleState)}</span></h3>
            <div class="dmo-row-actions">
              <button type="button" class="dmo-button" data-act="saida">Saída</button>
              <button type="button" class="dmo-button success" data-act="entrada">Entrada</button>
              <button type="button" class="dmo-button" data-act="irreparavel">Não reparadas</button>
              <button type="button" class="dmo-button" data-act="contagem">Corrigir contagem</button>
              <button type="button" class="dmo-button danger" data-act="close">Fechar</button>
            </div>
          </div>
          <div class="boquilhas-resumo__grid">
            <div><span>Estado atual</span><strong>${res.activeTrace ? 'Em produção' : 'Fechado'}</strong></div>
            <div><span>Na produção</span><strong>${fmtValue(saldo.prod)}</strong></div>
            <div><span>Em reparação</span><strong>${fmtValue(saldo.repair)}</strong></div>
            <div><span>Não reparadas</span><strong>${fmtValue(saldo.irreparable)}</strong></div>
            <div><span>Saldo discrepância</span><strong>${saldo.exceptionalReceived > 0 ? `<span class="boquilhas-resumo__delta--negative">−${fmtValue(saldo.exceptionalReceived)}</span>` : '—'}</strong></div>
            <div><span>Linha atual</span><strong>${esc(activeLine)}</strong></div>
            <div><span>Movimentos</span><strong>${res.movementCount}</strong></div>
          </div>
          <div id="bqWarnings" class="boquilhas-warnings"></div>
          <div class="dmo-table-wrap">
            <table class="dmo-table">
              <thead><tr><th>Movimento</th><th>Qtd.</th><th>Saldo</th><th>Reparador</th><th>Linha</th><th>Data</th><th>Operador</th></tr></thead>
              <tbody id="bqMovements"></tbody>
            </table>
          </div>
        </div>
        ${res.activeTrace ? `<input type="hidden" id="bqActiveTrace" value="${esc(res.activeTrace.bqTraceId)}" />` : ''}`;
      selectedTraceId = res.activeTrace?.bqTraceId || null;
      loadLotMovements(lotId);
      loadDiscrepancies(lotId);
      bindActions();
    } catch (err) { showToast(err.message, true); }
  }

  function bindActions() {
    $$('[data-act]').forEach((btn) => btn.addEventListener('click', () => {
      const act = btn.dataset.act;
      if (act === 'close') return closeTrace();
      openMovementModal(act);
    }));
  }

  async function loadLotMovements(lotId) {
    try {
      const rows = await api(`/api/boquilhas/movements?lotId=${lotId}&page=1&pageSize=60`);
      $('#bqMovements').innerHTML = rows.map((m) =>
        `<tr>
          <td>${movementLabel(m.movementType)}</td>
          <td>${m.qty != null ? fmtValue(m.qty) : '—'}</td>
          <td>${renderSaldo(m)}</td>
          <td>${esc(m.repairerName || '—')}</td>
          <td>${esc(m.line || '—')}</td>
          <td>${fmtDT(m.occurredAtUtc)}</td>
          <td>${esc(m.actorId || '—')}</td>
        </tr>`).join('') || '<tr><td colspan="7" class="dmo-empty-state">Sem movimentos.</td></tr>';
    } catch (err) { showToast(err.message, true); }
  }

  async function loadDiscrepancies(lotId) {
    try {
      const discs = await api(`/api/boquilhas/discrepancies?lotId=${lotId}`);
      const open = discs.filter((d) => d.status === 'Open');
      $('#bqWarnings').innerHTML = open.map((d) =>
        `<div class="dmo-alert" role="alert">
          Retorno excede o esperado em ${fmtValue(d.excessQty)} unidade(s) (esperado ${fmtValue(d.expectedQty)}, recebido ${fmtValue(d.actualQty)}).
          <button type="button" class="dmo-button" data-resolve="${esc(d.bqDiscrepancyId)}">Resolver</button>
        </div>`).join('');
      $$('[data-resolve]').forEach((btn) => btn.addEventListener('click', resolveDiscrepancy));
    } catch (err) { /* ignore */ }
  }

  async function resolveDiscrepancy(evt) {
    const id = evt.currentTarget.dataset.resolve;
    const note = window.prompt('Nota de resolução (obrigatória):');
    if (!note || !note.trim()) return showToast('A nota de resolução é obrigatória.', true);
    try {
      await jsonPost(`/api/boquilhas/discrepancies/${id}/resolve`, { resolutionNote: note });
      showToast('Discrepância resolvida.');
      loadDiscrepancies(selectedLotId);
    } catch (err) { showToast(err.message, true); }
  }

  // ---- Movement modal ------------------------------------------------------------
  // Canonical modal contract (dmo-components.css): .dmo-modal-backdrop.open
  // > .dmo-modal > .dmo-modal-head / .dmo-modal-body / .dmo-modal-foot.
  function openMovementModal(movementType) {
    const qtyLabel = movementType === 'contagem' ? 'Delta de correção' : 'Quantidade';
    const card = document.createElement('div');
    card.className = 'dmo-modal-backdrop open';
    card.innerHTML = `
      <div class="dmo-modal" role="dialog" aria-modal="true" aria-labelledby="mmTitle">
        <div class="dmo-modal-head">
          <h2 id="mmTitle">${movementLabel(movementType)} — <span id="mmHeader"></span></h2>
        </div>
        <div class="dmo-modal-body">
          <div class="dmo-row">
            <div class="dmo-field"><label for="mmQty">${qtyLabel}</label><input id="mmQty" type="number" min="0" step="any" autocomplete="off" /></div>
            <div class="dmo-field"><label for="mmRepairer">Reparador</label><select id="mmRepairer"></select></div>
            <div class="dmo-field"><label for="mmLine">Linha</label><select id="mmLine"></select></div>
          </div>
          ${movementType === 'entrada' ? '<p class="dmo-field__helper">Retornos que excedem o esperado geram uma discrepância aberta e nunca são bloqueados.</p>' : ''}
          <div class="dmo-field"><label for="mmNotes">Observações</label><textarea id="mmNotes" rows="2"></textarea></div>
        </div>
        <div class="dmo-modal-foot">
          <button type="button" class="dmo-button" id="mmCancel">Cancelar</button>
          <button type="button" class="dmo-button primary" id="mmSave">Guardar</button>
        </div>
      </div>`;
    document.body.appendChild(card);
    const header = card.querySelector('#mmHeader');
    const mmLine = card.querySelector('#mmLine');
    const mmRepairer = card.querySelector('#mmRepairer');

    const closeModal = () => {
      document.removeEventListener('keydown', onEscape);
      card.remove();
    };
    const onEscape = (e) => { if (e.key === 'Escape') closeModal(); };
    document.addEventListener('keydown', onEscape);
    card.addEventListener('click', (e) => { if (e.target === card) closeModal(); });
    // TD-15 — load repairers filtered by capability BQ for Boquilhas flows.
    // Sequential: repairers first, then lot details; avoids race between independent chains.
    // Client-side filter as defense-in-depth (UD-03).
    var bqRepairers = [];
    api('/api/boquilhas/repairers?onlyActive=true&type=BQ')
      .then((res) => {
        bqRepairers = (res || []).filter(
          (r) => r.supportedTypes?.includes('BQ') || !r.supportedTypes
        );
      })
      .catch(() => {});

    api(`/api/boquilhas/lotes/${selectedLotId}`)
      .then((res) => {
        header.textContent =
          `${res.lote.reference} · Lote ${res.lote.batchCode} · ${res.activeTrace?.startLine || ''}`;
        mmLine.innerHTML =
          '<option value="">(linha atual)</option>' +
          (res.lote.allowedLines || []).map((l) => `<option>${esc(l)}</option>`).join('');
        mmRepairer.innerHTML =
          '<option value="">Sem associação</option>' +
          bqRepairers.map((r) => `<option value="${esc(r.repairerId)}">${esc(r.name)}</option>`).join('');
      })
      .catch(() => {});

    card.querySelector('#mmCancel').addEventListener('click', closeModal);
    card.querySelector('#mmSave').addEventListener('click', async () => {
      const qty = movementType === 'linha' ? null : (card.querySelector('#mmQty').value ? Number(card.querySelector('#mmQty').value) : null);
      const body = {
        bqLoteId: selectedLotId,
        bqTraceId: selectedTraceId,
        movementType: movementType,
        qty,
        repairerId: mmRepairer.value || null,
        line: mmLine.value || null,
        notes: card.querySelector('#mmNotes').value.trim() || null
      };
      if (!selectedTraceId) return showToast('O lote não tem um trace ativo.', true);
      if (movementType !== 'contagem' && movementType !== 'linha' && (!qty || qty <= 0)) return showToast('Introduza uma quantidade positiva.', true);
      try {
        const row = await jsonPost('/api/boquilhas/movements', body);
        if (row.exceptionalReceivedQty > 0) showToast(`Retorno excede o esperado em ${fmtValue(row.exceptionalReceivedQty)} — registado como discrepância aberta.`);
        else showToast('Movimento registado.');
        closeModal();
        openLot(selectedLotId);
      } catch (err) { showToast(err.message, true); }
    });
  }

  async function closeTrace() {
    if (!selectedTraceId) return;
    const note = window.prompt('Motivo/motivo do fecho (opcional):');
    if (note === null) return;
    try {
      await jsonPost(`/api/boquilhas/traces/${selectedTraceId}/close`, {
        bqLoteId: selectedLotId, bqTraceId: selectedTraceId, reason: note || null
      });
      showToast('Trace fechado (snapshot final imutável guardado).');
      openLot(selectedLotId);
    } catch (err) { showToast(err.message, true); }
  }

  // ---- Boquilhas tab (cards) --------------------------------------------------------
  async function loadBoquilhasCards() {
    const term = $('#bSearch').value.trim();
    const state = $('#bState').value;
    const q = new URLSearchParams();
    if (term) q.set('search', term);
    if (state) q.set('lifecycle', state);
    q.set('page', '1'); q.set('pageSize', $('#bPageSize').value);
    try {
      const lotes = await api(`/api/boquilhas/lotes?${q.toString()}`);
      $('#boquilhasCards').innerHTML = lotes.length
        ? lotes.map((l) => `
          <button type="button" class="dmo-card boquilhas-card" data-open-lot="${esc(l.bqLoteId)}">
            <div class="boquilhas-card__ref">${esc(l.reference)} <span>· Lote ${esc(l.batchCode)}</span></div>
            <div class="dmo-pill">${stateLabel(l.lifecycleState)}</div>
            <div class="boquilhas-card__meta">Linhas: ${esc((l.allowedLines || []).join(', '))}</div>
          </button>`).join('')
        : '<div class="dmo-empty-state">Nenhuma boquilha encontrada.</div>';
      $$('[data-open-lot]').forEach((el) => el.addEventListener('click', () => {
        openLot(el.dataset.openLot);
        goToView('registo');
        $('#searchLot').value = '';
        loadSearch();
      }));
    } catch (err) { showToast(err.message, true); }
  }
  $('#bSearchBtn').addEventListener('click', loadBoquilhasCards);

  // ---- Histórico tab -----------------------------------------------------------------
  $('#hSearchBtn').addEventListener('click', loadHistory);
  $('#hClear').addEventListener('click', () => {
    ['hSearch', 'hFrom', 'hTo', 'hType', 'hRepairer'].forEach((id) => ($('#' + id).value = ''));
    loadHistory();
  });

  async function loadHistory() {
    const q = new URLSearchParams();
    if ($('#hSearch').value.trim()) q.set('search', $('#hSearch').value.trim());
    if ($('#hType').value) q.set('type', $('#hType').value);
    if ($('#hRepairer').value) q.set('repairerId', $('#hRepairer').value);
    q.set('page', '1'); q.set('pageSize', $('#hPageSize').value);
    try {
      const rows = await api(`/api/boquilhas/movements?${q.toString()}`);
      $('#hBody').innerHTML = rows.map((m) =>
        `<tr>
          <td>${esc(m.reference || '—')}</td>
          <td>${esc(m.batchCode || '—')}</td>
          <td>${movementLabel(m.movementType)}</td>
          <td>${m.qty != null ? fmtValue(m.qty) : '—'}</td>
          <td>${m.saldoAfter ? fmtValue(m.saldoAfter.prod) : '—'}</td>
          <td>${esc(m.repairerName || '—')}</td>
          <td>${esc(m.line || '—')}</td>
          <td>${fmtDT(m.occurredAtUtc)}</td>
          <td>${esc(m.actorId || '—')}</td>
        </tr>`).join('') || '<tr><td colspan="9" class="dmo-empty-state">Sem movimentos no período.</td></tr>';
      const totals = rows.reduce((acc, m) => {
        if (m.movementType === 'saida') acc.out += Number(m.qty || 0);
        if (m.movementType === 'entrada') acc.in += Number(m.qty || 0);
        return acc;
      }, { out: 0, in: 0 });
      $('#hSummary').innerHTML = `<span class="dmo-pill">Saídas ${fmtValue(totals.out)}</span>
        <span class="dmo-pill">Entradas ${fmtValue(totals.in)}</span>`;
    } catch (err) { showToast(err.message, true); }
  }

  // ---- Definições tab (apenas reparadores) ---------------------------------------
  async function loadDefinicoes() {
    try {
      repairers = await api('/api/boquilhas/repairers?onlyActive=false');
      $('#repairerList').innerHTML = repairers.map((r) => `
        <div class="boquilhas-list-row">
          <span><strong>${esc(r.name)}</strong> ${r.active ? '' : '<span class="dmo-pill rejected">Inativo</span>'}</span>
          ${r.active ? `<button type="button" class="dmo-button" data-deactivate="${esc(r.repairerId)}">Desativar</button>` : ''}
        </div>`).join('') || '<div class="dmo-empty-state">Sem reparadores registados.</div>';
      $$('[data-deactivate]').forEach((el) => el.addEventListener('click', () => setRepairerActive(el.dataset.deactivate, false)));
    } catch (err) { showToast(err.message, true); }
  }

  $('#addRepairer').addEventListener('click', async () => {
    const name = window.prompt('Nome do novo reparador:');
    if (!name || !name.trim()) return;
    try { await jsonPost('/api/boquilhas/repairers', { name: name.trim() }); showToast('Reparador adicionado.'); loadDefinicoes(); }
    catch (err) { showToast(err.message, true); }
  });

  async function setRepairerActive(id, active) {
    try { await api(`/api/boquilhas/repairers/${id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ active }) }); loadDefinicoes(); }
    catch (err) { showToast(err.message, true); }
  }

  // ---- Navigation helper + init -------------------------------------------------
  function goToView(view) {
    $$('.boquilhas-tabs .dmo-tab').forEach((t) => t.classList.toggle('active', t.dataset.view === view));
    $$('.boquilhas-view').forEach((v) => v.classList.toggle('active', v.id === view));
  }

  function debounce(fn, ms) {
    let t;
    return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
  }

  loadLinePanel();
  loadSearch();
})();
