// BA DMO — Controlo unified production workspace (R012) wiring only.
// No business logic in JS. The workspace consumes the R011 current-open Job On
// context (GET /api/jobon/current) into an ACTIVE PRODUCTION CARD that binds all
// tabs (Resumo / Peso / Comparação / Pegamentos) to the same production context.
// No second calendar; no re-selection inside each tab; free mode when no card.
(function () {
  'use strict';

  const $ = (sel) => document.querySelector(sel);
  const $$ = (sel) => Array.from(document.querySelectorAll(sel));
  const toast = $('#toast');

  const canEdit = !!$('#canEdit');
  const canSubmit = !!$('#canSubmit');
  const canReview = !!$('#canReview');

  // Active production context (workspace state). Set by "Carregar Job On atual".
  // job_on_id is the stable identity; production/reference/machine are display context.
  let active = null; // { jobOnId, productionCode, reference, machineCode }

  function esc(value) {
    return String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }

  function showToast(message, isError) {
    toast.textContent = message;
    toast.classList.toggle('error', isError === true);
    toast.hidden = false;
    clearTimeout(showToast._t);
    showToast._t = setTimeout(() => { toast.hidden = true; }, 4500);
  }

  async function api(url, options) {
    const res = await fetch(url, options);
    if (res.ok) return await res.json();
    let payload = { code: 'ERROR', message: 'Erro de servidor.' };
    try { payload = await res.json(); } catch (e) { /* ignore */ }
    throw Object.assign(new Error(payload.message), { code: payload.code });
  }

  const jsonPost = (url, body) => api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}) });

  const stateLabel = (s) => ({ rascunho: 'Rascunho', submetido: 'Submetido', aprovado: 'Aprovado', rejeitado: 'Rejeitado' }[s] || s);

  // ---- Tabs -----------------------------------------------------------------
  function selectSection(section) {
    const tabs = $$('.controlo-tabs .tab');
    const target = tabs.find((tab) => tab.dataset.tab === section) ?? tabs[0];
    if (!target) return;
    tabs.forEach((tab) => tab.classList.toggle('active', tab === target));
    $$('.controlo-tab-view').forEach((view) =>
      view.classList.toggle('active', view.id === 'view-' + target.dataset.tab));
  }

  $$('.controlo-tabs .tab').forEach((tab) => {
    tab.addEventListener('click', () => selectSection(tab.dataset.tab));
  });

  // ---- Carregar Job On atual (R011 current-open context) ---------------------
  $('#btnCarregarJobOn').addEventListener('click', async () => {
    try {
      const ctx = await api('/api/jobon/current');
      // ctx = { jobOnId, productionCode, reference, machineCode, openedAtUtc }
      activateCard(ctx);
    } catch (err) {
      if (err && err.status === 404 || (err && err.code === 'JOBON_CURRENT_NOT_FOUND')) {
        showEmpty('Nenhum Job On selecionado.');
      } else {
        showToast(err.message || 'Não foi possível carregar o Job On atual.', true);
      }
    }
  });

  function showEmpty(text) {
    active = null;
    $('#activeCard').hidden = true;
    $('#workTabs').hidden = true;
    $$('.controlo-tab-view').forEach((v) => { v.hidden = true; });
    $('#controloEmpty').hidden = false;
    $('#controloEmpty').textContent = text || 'Nenhum Job On selecionado.';
  }

  function activateCard(ctx) {
    active = { jobOnId: ctx.jobOnId, productionCode: ctx.productionCode, reference: ctx.reference, machineCode: ctx.machineCode };
    $('#controloEmpty').hidden = true;
    $('#controloError').hidden = true;
    // Active production card.
    const display = `${ctx.productionCode} · ${ctx.reference} · ${ctx.machineCode}`;
    $('#cardDisplay').textContent = display;
    $('#cardSub').textContent = `Job On ${ctx.jobOnId || ''} · aberto por si no Job On`;
    $('#activeCard').hidden = false;
    $('#workTabs').hidden = false;
    $$('.controlo-tab-view').forEach((v) => { v.hidden = false; });
    refreshTabStates();
    showToast('Produção ativa: ' + display);
  }

  // ---- Active card interactions (workspace state only) -----------------------
  // Single click → detach/release the production (future work no longer auto-bound).
  // Double click → clear the selection (card becomes empty). No business data touched.
  let cardClickTimer = null;
  $('#activeCard').addEventListener('click', (e) => {
    if (e.target.closest('a')) return;
    if (cardClickTimer) {
      clearTimeout(cardClickTimer);
      cardClickTimer = null;
      clearCard();
      return;
    }
    cardClickTimer = setTimeout(() => {
      cardClickTimer = null;
      detachCard();
    }, 250);
  });

  function detachCard() {
    if (!active) return;
    active = null;
    refreshTabStates();
    $('#cardDisplay').textContent = 'Produção libertada';
    $('#cardSub').textContent = 'Novos registos deixam de ser associados automaticamente a uma produção.';
    showToast('Produção libertada. Os registos guardados permanecem associados à produção original.');
  }

  function clearCard() {
    active = null;
    showEmpty('Nenhum Job On selecionado.');
    showToast('Seleção de produção limpa.');
  }

  // ---- Shared context binding per tab ----------------------------------------
  function refreshTabStates() {
    const hasActive = !!active;
    // Resumo
    $('#resumoNeedsContext').hidden = hasActive;
    $('#resumoNeedsContext').textContent = hasActive ? '' : 'Carregue um Job On atual para criar/abrir o Resumo desta produção.';
    $('#controloContext').hidden = !hasActive;
    $('#controloItemsCard').hidden = !hasActive;
    $('#controloHistoryCard').hidden = !hasActive;
    if (hasActive) {
      $('[data-ctx-producao]').textContent = active.productionCode || '—';
      $('[data-ctx-referencia]').textContent = active.reference || '—';
      $('[data-ctx-maquina]').textContent = active.machineCode || '—';
      loadResumo();
    } else {
      $('#controloItems tbody').innerHTML = '';
    }
    // Peso / Comparação / Pegamentos (reuse existing module UIs under active context)
    $('#pesoNeedsContext').hidden = hasActive;
    $('#comparacaoNeedsContext').hidden = hasActive;
    $('#pegamentosNeedsContext').hidden = hasActive;
    $('#btnOpenPeso').hidden = !hasActive;
    $('#btnOpenComparacao').hidden = !hasActive;
    $('#btnOpenPegamentos').hidden = !hasActive;
    $('#histBtnOpenPeso').style.display = hasActive ? '' : 'none';
  }

  // ---- Resumo tab (new control sheet, bound to active production context) -----
  let sheetId = null;
  let sheet = null;

  async function loadResumo() {
    if (!active) return;
    try {
      sheet = await api('/api/controlo/production?jobOnId=' + encodeURIComponent(active.jobOnId));
      sheetId = sheet.sheetId;
      $('[data-ctx-estado]').textContent = stateLabel(sheet.status);
      renderItems();
      renderHistory();
      renderActions();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  function renderItems() {
    const tbody = $('#controloItems tbody');
    tbody.innerHTML = (sheet.items || []).map((it) =>
      `<tr data-item="${it.itemId}">
        <td>${esc(it.family)}</td>
        <td>${esc(it.referenceSnapshot ?? '—')}</td>
        <td>${esc(it.lotSnapshot ?? '—')}</td>
        <td>${esc(it.technicalNameSnapshot ?? '—')}</td>
        <td><select data-field="result" ${canEdit ? '' : 'disabled'}><option value="">—</option><option value="OK">OK</option><option value="NOK">NOK</option></select></td>
        <td><input data-field="observation" value="${esc(it.observation ?? '')}" ${canEdit ? '' : 'disabled'}></td>
        <td><input data-field="mcaliperLink" placeholder="https://…" value="${esc(it.mcaliperLink ?? '')}" ${canEdit ? '' : 'disabled'}></td>
      </tr>`).join('');
    if (!tbody.innerHTML) tbody.innerHTML = '<tr><td colspan="7" class="empty">Sem componentes para esta produção/revisão.</td></tr>';
    if (sheet.items) {
      tbody.querySelectorAll('tr[data-item]').forEach((tr) => {
        const it = (sheet.items || []).find((i) => i.itemId === tr.getAttribute('data-item'));
        if (it) { const exp = tr.querySelector('[data-field="result"]'); if (exp) exp.value = it.result || ''; }
      });
    }
  }

  function renderHistory() {
    const hbody = $('#controloHistory tbody');
    hbody.innerHTML = (sheet.events || []).map((e) =>
      `<tr><td>${fmtDT(e.occurredAtUtc)}</td><td>${esc(e.eventType)}</td><td>${esc(e.actorId || '—')}</td><td>${esc(e.note || '')}</td></tr>`).join('');
    $('#controloHistoryCard').hidden = !(sheet.events && sheet.events.length);
  }

  function renderActions() {
    const actions = $('#controloActions');
    const s = sheet.status;
    const isDraft = s === 'rascunho';
    const isSubmitted = s === 'submetido';
    let html = '';
    if (canEdit && (isDraft || isSubmitted)) html += '<button class="dmo-button success" data-action="save">Guardar controlos</button>';
    if (canSubmit && (isDraft || isSubmitted)) html += '<button class="dmo-button" data-action="submit">Submeter</button>';
    if (canEdit && !isDraft) html += '<button class="dmo-button" data-action="reopen">Reabrir</button>';
    if (canReview && isSubmitted) html += '<button class="dmo-button" data-action="approve">Aprovar</button>';
    if (canReview && isSubmitted) html += '<button class="dmo-button" data-action="reject">Rejeitar</button>';
    actions.innerHTML = html;
    actions.querySelectorAll('[data-action]').forEach((b) => b.addEventListener('click', () => handleAction(b.dataset.action)));
  }

  function collectEdits() {
    return $$('#controloItems tbody tr[data-item]').map((tr) => {
      const val = (sel) => { const el = tr.querySelector(sel); return el ? (el.tagName === 'SELECT' ? el.value : el.value.trim()) : null; };
      return { itemId: tr.getAttribute('data-item'), result: val('[data-field="result"]') || null, observation: val('[data-field="observation"]') || null, mcaliperLink: val('[data-field="mcaliperLink"]') || null };
    });
  }

  async function handleAction(action) {
    if (!sheetId) return;
    try {
      if (action === 'save') { await jsonPost('/api/controlo/' + sheetId + '/items', { sheetId, edits: collectEdits() }); showToast('Resumo guardado.'); }
      else if (action === 'submit') { const n = prompt('Nota de submissão (opcional):') || null; await jsonPost('/api/controlo/' + sheetId + '/submit', { sheetId, note: n }); showToast('Resumo submetido.'); }
      else if (action === 'reopen') { await jsonPost('/api/controlo/' + sheetId + '/reopen', { sheetId }); showToast('Resumo reaberto.'); }
      else if (action === 'approve') { const n = prompt('Nota de aprovação (opcional):') || null; await jsonPost('/api/controlo/' + sheetId + '/decide', { sheetId, decision: 'Aprovado', note: n }); showToast('Resumo aprovado.'); }
      else if (action === 'reject') { const n = prompt('Motivo da rejeição:') || ''; await jsonPost('/api/controlo/' + sheetId + '/decide', { sheetId, decision: 'Rejeitado', note: n }); showToast('Resumo rejeitado.'); }
      await loadResumo();
    } catch (err) { showToast(err.message, true); }
  }

  // ---- Peso / Comparação / Pegamentos open-under-context buttons --------------
  $('#btnOpenPeso').addEventListener('click', () => { if (active) window.location.href = '/peso'; });
  $('#btnOpenComparacao').addEventListener('click', () => { if (active) window.location.href = '/peso'; });
  $('#btnOpenPegamentos').addEventListener('click', () => { if (active) window.location.href = '/pegamentos'; });

  // ---- Histórico tab ----------------------------------------------------------
  $$('.controlo-tabs .tab').forEach((t) => {
    if (t.dataset.tab === 'historico') {
      t.addEventListener('click', loadHistoryList);
    }
  });
  async function loadHistoryList() {
    try {
      const rows = await api('/api/controlo/list');
      const tbody = $('#controloHistoryTable tbody');
      tbody.innerHTML = (rows || []).map((r) =>
        `<tr data-id="${esc(r.sheetId)}">` +
        `<td>${fmtDT(r.createdAtUtc)}</td><td>${esc(r.productionCode ?? '—')}</td><td>${esc(r.reference ?? '—')}</td><td>${esc(r.machineCode ?? '—')}</td><td>${esc(stateLabel(r.status))}</td></tr>`).join('');
      $('#historyEmpty').hidden = (rows || []).length > 0;
    } catch (err) { showToast(err.message, true); }
  }

  function fmtDT(value) {
    if (!value) return '—';
    const d = new Date(value);
    return isNaN(d.getTime()) ? String(value) : d.toLocaleString('pt-PT');
  }

  // ---- Init: prefer a projected production from the query param (backcompat) ---
  (async function init() {
    const params = new URLSearchParams(location.search);
    const jobOn = params.get('jobOn');
    selectSection(params.get('section') || 'resumo');
    if (jobOn) {
      // Backcompat: resolve a stable context for a directly-supplied job_on.
      try { await activateFromJobOnId(jobOn); } catch (e) { showEmpty('Nenhum Job On selecionado.'); }
    } else {
      showEmpty('Nenhum Job On selecionado. Carregue o Job On atual.');
    }
  })();

  async function activateFromJobOnId(jobOnId) {
    const dto = await api('/api/controlo/production?jobOnId=' + encodeURIComponent(jobOnId));
    // dto carries productionCode/reference/machineCode/status.
    activateCard({ jobOnId, productionCode: dto.productionCode, reference: dto.reference, machineCode: dto.machineCode });
  }
})();
