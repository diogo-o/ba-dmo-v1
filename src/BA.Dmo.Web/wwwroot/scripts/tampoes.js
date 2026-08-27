// BA DMO — Tampões (U-17) wiring only.
// No business logic is implemented client-side: it calls the gated
// /api/tampoes/* endpoints and renders the returned server results
// (GLM-TP / GLM-CORE: no duplicated domain rules in JS).
(function () {
  'use strict';

  const $ = (sel) => document.querySelector(sel);
  const $$ = (sel) => Array.from(document.querySelectorAll(sel));
  const toast = $('#toast');

  // ---- State ---------------------------------------------------------------
  let fields = [];          // TampaoFieldDefDto[]
  let valuesByField = {};   // fieldId -> TampaoFieldValueDto[]
  let selectedConfig = null; // selected TampaoConfigurationDto for actions
  let configs = [];         // all active configurations for the consult
  let selectedMovementId = null;

  // ---- Tabs ----------------------------------------------------------------
  $$('.tampoes-tabs .tab').forEach((tab) => {
    tab.addEventListener('click', () => {
      $$('.tampoes-tabs .tab').forEach((t) => t.classList.toggle('active', t === tab));
      $$('.tampoes-view').forEach((v) => v.classList.toggle('active', v.id === tab.dataset.view));
      if (tab.dataset.view === 'opcoes') loadOptions();
      if (tab.dataset.view === 'consulta') consult();
      if (tab.dataset.view === 'historico') loadHistory();
      if (tab.dataset.view === 'registo') loadDropdowns();
      if (tab.dataset.view === 'linhas') loadLinesMachines();
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

  function fillSelect(select, items, valueKey, labelKey) {
    select.innerHTML = items.map((it) =>
      `<option value="${esc(it[valueKey])}">${esc(it[labelKey])}</option>`).join('');
  }

  function fmtValue(v) {
    const n = Number(v);
    return Number.isFinite(n) ? n.toLocaleString('pt-PT', { maximumFractionDigits: 2 }) : String(v);
  }

  function fmtDT(value) {
    if (!value) return '—';
    const d = new Date(value);
    return isNaN(d.getTime()) ? String(value) : d.toLocaleString('pt-PT');
  }

  // ---- Dropdowns -----------------------------------------------------------------
  async function loadDropdowns(active = null) {
    try {
      fields = await api('/api/tampoes/opcoes/fields?onlyActive=true');
      valuesByField = {};
      for (const f of fields) {
        valuesByField[f.fieldDefId] = await api(`/api/tampoes/opcoes/fields/${f.fieldDefId}/values?onlyActive=true`);
      }
      const diaField = fields.find((f) => f.fieldName === 'Diâmetro') || fields[0];
      const calField = fields.find((f) => f.fieldName.includes('Calote')) || fields[1] || fields[0];
      if (diaField) fillSelect($('#rDiameter'), valuesByField[diaField.fieldDefId] || [], 'valueNumeric', 'valueLabel');
      if (calField) fillSelect($('#rCalote'), valuesByField[calField.fieldDefId] || [], 'valueNumeric', 'valueLabel');
      fillSelect($('#xDiameterNovo'), valuesByField[diaField?.fieldDefId] || [], 'valueNumeric', 'valueLabel');
      fillSelect($('#xCaloteNovo'), valuesByField[calField?.fieldDefId] || [], 'valueNumeric', 'valueLabel');
      fillSelect($('#cDiameter'), [{ valueNumeric: '', valueLabel: 'Todos' }, ...(valuesByField[diaField?.fieldDefId] || [])], 'valueNumeric', 'valueLabel');
      fillSelect($('#cCalote'), [{ valueNumeric: '', valueLabel: 'Todos' }, ...(valuesByField[calField?.fieldDefId] || [])], 'valueNumeric', 'valueLabel');
      if (active) loadOptions();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  // ---- Registo: adicionar / remover -------------------------------------------------
  $('[data-add-qty]').addEventListener('click', () => updateQuantity(1));
  $('[data-remove-qty]').addEventListener('click', () => updateQuantity(-1));

  async function updateQuantity(sign) {
    const config = await findConfigurationByDropdown($('#rDiameter').value, $('#rCalote').value);
    if (!config) return showToast('Não foi encontrada uma configuração com estes valores.', true);
    const qty = parseInt($('#rQty').value, 10);
    if (!qty || qty < 1) return showToast('Introduza uma quantidade inteira positiva.', true);
    const balance = $('#rSaldo').value;
    try {
      const path = sign > 0 ? '/api/tampoes/quantidade/adicionar' : '/api/tampoes/quantidade/remover';
      await jsonPost(path, { configurationId: config.configurationId, balance, qty });
      showToast(sign > 0 ? 'Quantidade adicionada.' : 'Quantidade removida.');
      $('#registerFeedback').hidden = true;
      $('#rQty').value = '';
      $('#rQty').focus();
      consult();
    } catch (err) {
      showToast(err.message, true);
    }
  }

  async function findConfigurationByDropdown(dia, calote) {
    try {
      const list = await api('/api/tampoes/consulta');
      return list.find((c) => {
        const d = c.values['Diâmetro'];
        const ca = c.values['Profundidade/Calote'];
        const matchD = (dia === null || dia === '') || (d !== undefined && String(d) === String(Number(dia)));
        const matchCa = (calote === null || calote === '') || (ca !== undefined && String(ca) === String(Number(calote)));
        return matchD && matchCa;
      }) || null;
    } catch (err) { showToast(err.message, true); return null; }
  }

  // ---- Consulta ----------------------------------------------------------------
  $('[data-consult]').addEventListener('click', consult);

  async function consult() {
    try {
      configs = await api('/api/tampoes/consulta');
      const dia = $('#cDiameter').value;
      const calote = $('#cCalote').value;
      const filtered = configs.filter((c) => {
        const d = c.values['Diâmetro'];
        const ca = c.values['Profundidade/Calote'];
        const matchD = (dia === '') || (d !== undefined && String(d) === String(Number(dia)));
        const matchCa = (calote === '') || (ca !== undefined && String(ca) === String(Number(calote)));
        return matchD && matchCa;
      });
      const body = $('#consultaBody');
      const empty = $('#consultaEmpty');
      const actions = $('#consultaActions');
      body.innerHTML = filtered.map((c) =>
        `<tr data-config-id="${esc(c.configurationId)}">
          <td>${esc(configLabel(c))}</td>
          <td class="tampoes-numeric">${fmtValue(c.enchidos)}</td>
          <td class="tampoes-numeric">${fmtValue(c.porEncher)}</td>
        </tr>`).join('');
      empty.hidden = body.children.length > 0;
      actions.hidden = body.children.length === 0;
      Array.from(body.children).forEach((tr) => {
        tr.addEventListener('click', () => {
          selectedConfig = filtered.find((c) => c.configurationId === tr.dataset.configId) || null;
          Array.from(body.children).forEach((x) => x.classList.toggle('selected', x === tr));
          if (selectedConfig) showSelectedSaldos(selectedConfig);
        });
        tr.addEventListener('dblclick', () => openDetalhe(tr.dataset.configId));
      });
    } catch (err) { showToast(err.message, true); }
  }

  function configLabel(c) {
    return Object.entries(c.values || {})
      .map(([k, v]) => `${k} ${fmtValue(v)} mm`).join(' · ');
  }

  function showSelectedSaldos(cfg) {
    $('#selectedSaldos').hidden = false;
    $('[data-saldo-enchidos]').textContent = fmtValue(cfg.enchidos);
    $('[data-saldo-por-encher]').textContent = fmtValue(cfg.porEncher);
  }

  // ---- Alterar estado ------------------------------------------------------------
  $('[data-alterar-estado]').addEventListener('click', () => {
    if (!selectedConfig) return showToast('Selecione uma configuração.', true);
    $('#estadoCard').hidden = false;
  });
  $('[data-cancel-estado]').addEventListener('click', () => { $('#estadoCard').hidden = true; });
  $('[data-confirm-estado]').addEventListener('click', async () => {
    if (!selectedConfig) return showToast('Selecione uma configuração.', true);
    const qty = parseInt($('#eQty').value, 10);
    if (!qty || qty < 1) return showToast('Introduza uma quantidade inteira positiva.', true);
    try {
      await jsonPost('/api/tampoes/estado/alterar', {
        configurationId: selectedConfig.configurationId,
        destination: $('#eSaldo').value,
        qty
      });
      showToast('Transferência de estado concluída.');
      $('#estadoCard').hidden = true;
      $('#eQty').value = '';
      consult();
    } catch (err) { showToast(err.message, true); }
  });

  // ---- Alterar configuração ----------------------------------------------------------
  $('[data-alterar-config]').addEventListener('click', () => {
    if (!selectedConfig) return showToast('Selecione uma configuração.', true);
    $('[data-config-origin]').textContent = configLabel(selectedConfig);
    $('#configCard').hidden = false;
  });
  $('[data-cancel-config]').addEventListener('click', () => { $('#configCard').hidden = true; });
  $('[data-confirm-config]').addEventListener('click', async () => {
    if (!selectedConfig) return showToast('Selecione uma configuração.', true);
    const qty = parseInt($('#xQty').value, 10);
    if (!qty || qty < 1) return showToast('Introduza uma quantidade inteira positiva.', true);
    const destinationValues = {};
    const diaField = fields.find((f) => f.fieldName === 'Diâmetro') || fields[0];
    const calField = fields.find((f) => f.fieldName.includes('Calote')) || fields[1] || fields[0];
    if (diaField) destinationValues['Diâmetro'] = Number($('#xDiameterNovo').value);
    if (calField) destinationValues['Profundidade/Calote'] = Number($('#xCaloteNovo').value);
    try {
      await jsonPost('/api/tampoes/configuracao/alterar', {
        originConfigurationId: selectedConfig.configurationId,
        destinationValues,
        qty
      });
      showToast('Transformação de configuração concluída.');
      $('#configCard').hidden = true;
      $('#xQty').value = '';
      consult();
    } catch (err) { showToast(err.message, true); }
  });

  // ---- Detalhe ------------------------------------------------------------------------
  async function openDetalhe(configId) {
    try {
      const d = await api(`/api/tampoes/configuracao/${configId}/detalhe`);
      const c = d.configuration;
      const LINES = ['B1', 'B2', 'B3', 'C1', 'C2', 'C3'];
      const machines = c.machines || [];
      const machineChips = LINES.map((m) =>
        `<label class="dmo-line-chip"><input type="checkbox" value="${m}" data-dmachine ${machines.includes(m) ? 'checked' : ''}> ${m}</label>`).join('');
      const notes = (d.notes || []).map((n) =>
        `<div class="tampoes-note"><em>${esc(n.note)}</em> · ${esc(n.actorId || '—')} · ${fmtDT(n.occurredAtUtc)}</div>`).join('');
      const evs = (d.machineEvents || []).map((e) =>
        `<div class="tampoes-event">${esc(e.machine)} — ${esc(e.action)} · ${esc(e.actorId || '—')} · ${fmtDT(e.occurredAtUtc)}</div>`).join('');
      $('#detalheBody').innerHTML =
        `<tr><th>Configuração</th><td>${esc(configLabel(c))}</td></tr>` +
        `<tr><th>Enchidos</th><td>${fmtValue(c.enchidos)}</td></tr>` +
        `<tr><th>Por encher</th><td>${fmtValue(c.porEncher)}</td></tr>`;
      $('#detalheMachines').innerHTML =
        `<div class="dmo-field"><label>Máquinas</label><div class="tampoes-machine-picker">${machineChips}</div>` +
        `<button type="button" class="dmo-button" data-save-machines data-cfg="${esc(configId)}">Confirmar</button></div>`;
      $('#detalheComments').innerHTML =
        `<div class="dmo-field"><label>Observações</label><textarea id="detalheNote" rows="2"></textarea></div>` +
        `<button type="button" class="dmo-button" data-save-note data-cfg="${esc(configId)}">Guardar observação</button>` +
        (notes ? `<div class="tampoes-notes">${notes}</div>` : '');
      $('#detalheHistory').innerHTML = evs || '<div class="dmo-empty-state">Sem histórico de máquinas.</div>';
      $('#detalheCard').hidden = false;
      $('#detalheCard').dataset.cfg = configId;
    } catch (err) { showToast(err.message, true); }
  }
  $('[data-close-detalhe]').addEventListener('click', () => { $('#detalheCard').hidden = true; });
  $('[data-abrir-detalhe]').addEventListener('click', () => {
    if (!selectedConfig) return showToast('Selecione uma configuração.', true);
    openDetalhe(selectedConfig.configurationId);
  });

  // R008 — machine assignment + comments from the detail sheet.
  document.addEventListener('click', async (evt) => {
    const saveMachines = evt.target.closest('[data-save-machines]');
    if (saveMachines) {
      const cfg = saveMachines.dataset.cfg;
      const selected = Array.from(document.querySelectorAll('#detalheMachines [data-dmachine]:checked')).map((cb) => cb.value);
      try {
        await jsonPost(`/api/tampoes/configuracao/${cfg}/maquinas`, { configurationId: cfg, machines: selected });
        showToast('Máquinas atualizadas.');
        openDetalhe(cfg);
      } catch (err) { showToast(err.message, true); }
      return;
    }
    const saveNote = evt.target.closest('[data-save-note]');
    if (saveNote) {
      const cfg = saveNote.dataset.cfg;
      const note = document.querySelector('#detalheNote').value.trim();
      if (!note) return showToast('A observação é obrigatória.', true);
      try {
        await jsonPost(`/api/tampoes/configuracao/${cfg}/observacao`, { configurationId: cfg, note });
        showToast('Observação guardada.');
        openDetalhe(cfg);
      } catch (err) { showToast(err.message, true); }
    }
  });

  // ---- Histórico ----------------------------------------------------------------------
  $('[data-apply-history]').addEventListener('click', loadHistory);

  async function loadHistory() {
    const q = new URLSearchParams();
    if ($('#hFrom').value) q.set('from', new Date($('#hFrom').value + 'T00:00:00Z').toISOString());
    if ($('#hTo').value) q.set('to', new Date($('#hTo').value + 'T23:59:59Z').toISOString());
    if ($('#hType').value) q.set('type', $('#hType').value.toLowerCase());
    try {
      const movements = await api(`/api/tampoes/movimentos?${q.toString()}`);
      renderMovements(movements);
    } catch (err) { showToast(err.message, true); }
  }

  function renderMovements(movements) {
    const body = $('#historyBody');
    const empty = $('#historyEmpty');
    body.innerHTML = movements.map((m) =>
      `<tr>
        <td>${fmtDT(m.occurredAtUtc)}</td>
        <td>${m.originConfigurationId ? m.originConfigurationId.slice(0, 8) : '—'}</td>
        <td>${m.destinationConfigurationId ? m.destinationConfigurationId.slice(0, 8) : '—'}</td>
        <td>${movementLabel(m.movementType)}</td>
        <td>${saldoKey(m)}</td>
        <td>${fmtValue(m.qty)}</td>
        <td>${m.balancesBefore ? esc(m.balancesBefore) : '—'}</td>
        <td>${m.balancesAfter ? esc(m.balancesAfter) : '—'}</td>
        <td>${esc(m.actorId || '—')}</td>
      </tr>`).join('');
    empty.hidden = body.children.length > 0;
  }

  function movementLabel(t) {
    return { adicionar: 'Adicionar', remover: 'Remover', alterar_estado: 'Estado', alterar_configuracao: 'Configuração' }[t] || t;
  }
  function saldoKey(m) {
    if (m.movementType === 'adicionar' || m.movementType === 'remover') return '—';
    return '—';
  }

  // ---- Opções -------------------------------------------------------------------------
  async function loadOptions() {
    try {
      fields = await api('/api/tampoes/opcoes/fields?onlyActive=false');
      $('#fieldsBody').innerHTML = fields.map((f) =>
        `<tr>
          <td>${esc(f.fieldName)}</td><td>${esc(f.unit || '—')}</td><td>${f.precisionDigits ?? '—'}</td>
          <td>${f.active ? 'Sim' : 'Não'}</td>
        </tr>`).join('');
      fillSelect($('#vField'), fields, 'fieldDefId', 'fieldName');
      if (fields[0]) {
        valuesByField[fields[0].fieldDefId] = await api(`/api/tampoes/opcoes/fields/${fields[0].fieldDefId}/values?onlyActive=false`);
        renderValues(fields[0].fieldDefId);
      }
    } catch (err) { showToast(err.message, true); }
  }

  $('#vField').addEventListener('change', async () => {
    const fieldId = $('#vField').value;
    if (!fieldId) return;
    try {
      valuesByField[fieldId] = await api(`/api/tampoes/opcoes/fields/${fieldId}/values?onlyActive=false`);
      renderValues(fieldId);
    } catch (err) { showToast(err.message, true); }
  });

  function renderValues(fieldId) {
    const values = valuesByField[fieldId] || [];
    $('#valuesBody').innerHTML = values.map((v) =>
      `<tr><td>${fmtValue(v.valueNumeric)}</td><td>${esc(v.valueLabel)}</td><td>${v.active ? 'Sim' : 'Não'}</td></tr>`).join('');
  }

  $('[data-add-field]').addEventListener('click', async () => {
    try {
      await jsonPost('/api/tampoes/opcoes/fields', {
        fieldName: $('#oFieldName').value.trim(),
        unit: $('#oUnit').value.trim() || null,
        precisionDigits: $('#oPrecision').value ? parseInt($('#oPrecision').value, 10) : null,
        displayOrder: 0
      });
      showToast('Campo adicionado.');
      $('#oFieldName').value = ''; $('#oUnit').value = ''; $('#oPrecision').value = '';
      loadOptions();
    } catch (err) { showToast(err.message, true); }
  });

  $('[data-add-value]').addEventListener('click', async () => {
    const fieldId = $('#vField').value;
    if (!fieldId) return showToast('Crie/designe um campo primeiro.', true);
    try {
      await jsonPost('/api/tampoes/opcoes/values', {
        fieldDefId: fieldId,
        valueNumeric: Number($('#vValue').value),
        valueLabel: $('#vLabel').value.trim() || $('#vValue').value
      });
      showToast('Valor adicionado.');
      $('#vValue').value = ''; $('#vLabel').value = '';
      const f = await api(`/api/tampoes/opcoes/fields/${fieldId}/values?onlyActive=true`);
      valuesByField[fieldId] = f;
      renderValues(fieldId);
      loadDropdowns();
      consult();
    } catch (err) { showToast(err.message, true); }
  });

  // ---- Linhas e Máquinas -------------------------------------------------------------------
  const LINES = ['B1', 'B2', 'B3', 'C1', 'C2', 'C3'];
  let currentLinha = null;
  let linhasData = {}; // linha -> { machines: [], comments: [] }

  async function loadLinesMachines() {
    try {
      // Carregar todas as configurações para obter as máquinas por linha
      configs = await api('/api/tampoes/consulta');
      renderLinesPanel();
    } catch (err) { showToast(err.message, true); }
  }

  function renderLinesPanel() {
    const panel = $('#tampoesLinePanel');
    const LINES = ['B1', 'B2', 'B3', 'C1', 'C2', 'C3'];
    
    panel.innerHTML = LINES.map((line) => {
      // Agrupar máquinas por linha a partir das configurações
      const machinesInLine = [];
      configs.forEach((c) => {
        if (c.machines && c.machines.includes(line)) {
          machinesInLine.push({ machine: line, configId: c.configurationId, label: configLabel(c) });
        }
      });
      
      return `<div class="tampoes-line-card" data-line="${line}">
        <div class="tampoes-line-header">
          <strong>${line}</strong>
          <span class="dmo-pill">${machinesInLine.length} configuração(ões)</span>
        </div>
        <div class="tampoes-line-machines">
          ${machinesInLine.length > 0 ? machinesInLine.map((m) => 
            `<span class="tampoes-machine-chip" data-config-id="${m.configId}">${m.label}</span>`).join('') : 
            '<span class="dmo-u-muted">Sem tampões nesta linha</span>'}
        </div>
        <button type="button" class="dmo-button" data-abrir-linha-linha="${line}">Gerir linha</button>
      </div>`;
    }).join('');

    // Bind events
    $$('[data-abrir-linha-linha]').forEach((btn) => {
      btn.addEventListener('click', () => openLinhaDetalhe(btn.dataset.abrirLinhaLinha));
    });
  }

  async function openLinhaDetalhe(line) {
    currentLinha = line;
    $('#linhaDetalheTitle').textContent = `Detalhes da linha ${line}`;
    $('#linhaDetalheCard').hidden = false;
    
    // Carregar informações desta linha
    await loadLinhaInfo(line);
  }

  async function loadLinhaInfo(line) {
    try {
      // Obter todas as configurações associadas a esta linha
      const lineConfigs = configs.filter((c) => c.machines && c.machines.includes(line));
      
      // Listar máquinas únicas
      const allMachines = new Set();
      lineConfigs.forEach((c) => {
        if (c.machines) c.machines.forEach((m) => allMachines.add(m));
      });
      
      renderLinhaMachines(Array.from(allMachines));
      
      // Carregar comentários/observações (usar o detalhe de cada configuração)
      const comments = [];
      for (const cfg of lineConfigs) {
        try {
          const d = await api(`/api/tampoes/configuracao/${cfg.configurationId}/detalhe`);
          if (d.notes) {
            d.notes.forEach((n) => comments.push({ ...n, configLabel: configLabel(cfg) }));
          }
        } catch (e) { /* ignore individual errors */ }
      }
      renderLinhaComments(comments);
    } catch (err) { showToast(err.message, true); }
  }

  function renderLinhaMachines(machines) {
    const list = $('#linhaMachinesList');
    if (!machines || machines.length === 0) {
      list.innerHTML = '<div class="dmo-empty-state">Sem máquinas nesta linha.</div>';
      return;
    }
    
    list.innerHTML = machines.map((m) =>
      `<div class="tampoes-machine-item">
        <span>${m}</span>
        <button type="button" class="dmo-button ghost" data-remover-maquina-linha data-line="${currentLinha}" data-machine="${m}">Remover</button>
      </div>`).join('');
    
    // Bind remove events
    $$('[data-remover-maquina-linha]').forEach((btn) => {
      btn.addEventListener('click', async (evt) => {
        const machine = evt.target.dataset.machine;
        const line = evt.target.dataset.line;
        try {
          // Remover máquina da configuração correspondente
          const cfg = configs.find((c) => c.machines && c.machines.includes(line));
          if (cfg) {
            await jsonPost(`/api/tampoes/configuracao/${cfg.configurationId}/maquinas`, {
              configurationId: cfg.configurationId,
              machines: (cfg.machines || []).filter((m) => m !== machine)
            });
            showToast('Máquina removida.');
            await loadLinhaInfo(currentLinha);
            renderLinesPanel();
          }
        } catch (err) { showToast(err.message, true); }
      });
    });
  }

  function renderLinhaComments(comments) {
    const list = $('#linhaCommentsList');
    if (!comments || comments.length === 0) {
      list.innerHTML = '<div class="dmo-empty-state">Sem observações para esta linha.</div>';
      return;
    }
    
    list.innerHTML = comments.map((c) =>
      `<div class="tampoes-comment-item">
        <em>${esc(c.note)}</em>
        <span class="dmo-u-muted">· ${esc(c.actorId || '—')} · ${fmtDT(c.occurredAtUtc)} · ${esc(c.configLabel || '')}</span>
      </div>`).join('');
  }

  $('[data-close-linha-det]').addEventListener('click', () => { 
    $('#linhaDetalheCard').hidden = true; 
    currentLinha = null;
  });

  $('[data-add-linha-maquina]').addEventListener('click', async () => {
    if (!currentLinha) return showToast('Selecione uma linha primeiro.', true);
    const machine = $('#novaMaquina').value;
    if (!machine) return showToast('Selecione uma máquina.', true);
    
    try {
      // Adicionar máquina à configuração mais relevante
      const cfg = configs.find((c) => c.machines && c.machines.includes(currentLinha));
      if (cfg) {
        const existingMachines = cfg.machines || [];
        if (!existingMachines.includes(machine)) {
          await jsonPost(`/api/tampoes/configuracao/${cfg.configurationId}/maquinas`, {
            configurationId: cfg.configurationId,
            machines: [...existingMachines, machine]
          });
          showToast('Máquina adicionada.');
          await loadLinhaInfo(currentLinha);
          renderLinesPanel();
        } else {
          showToast('Esta máquina já está nesta linha.');
        }
      } else {
        showToast('Nenhuma configuração encontrada para esta linha.');
      }
    } catch (err) { showToast(err.message, true); }
  });

  $('[data-save-linha-comentario]').addEventListener('click', async () => {
    if (!currentLinha) return showToast('Selecione uma linha primeiro.', true);
    const note = $('#novaObservacao').value.trim();
    if (!note) return showToast('A observação é obrigatória.', true);
    
    try {
      // Guardar comentário na configuração mais relevante
      const cfg = configs.find((c) => c.machines && c.machines.includes(currentLinha));
      if (cfg) {
        await jsonPost(`/api/tampoes/configuracao/${cfg.configurationId}/observacao`, {
          configurationId: cfg.configurationId,
          note
        });
        showToast('Observação guardada.');
        $('#novaObservacao').value = '';
        await loadLinhaInfo(currentLinha);
      } else {
        showToast('Nenhuma configuração encontrada para esta linha.');
      }
    } catch (err) { showToast(err.message, true); }
  });

  // ---- Init -------------------------------------------------------------------------
  loadDropdowns();
  consult();
})();
