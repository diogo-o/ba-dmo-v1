const $$=(selector,root=document)=>[...root.querySelectorAll(selector)];
const $=(selector,root=document)=>root.querySelector(selector);

const views={planning:$('#planningView'),sheet:$('#sheetView'),control:$('#controlView'),history:$('#historyView'),settings:$('#settingsView')};

// Organiza a folha por fluxo vertical, evitando linhas com alturas artificiais.
const priorityGrid=$('.priority-grid');
const secondaryGrid=$('.secondary-grid');
if(priorityGrid&&secondaryGrid){
  const secondaryCard=code=>$$(':scope > .tool-card',secondaryGrid).find(card=>$('.tool-code',card)?.textContent.trim()===code);
  const board=document.createElement('section');
  board.className='operational-board';
  const makeColumn=(name,nodes)=>{
    const column=document.createElement('div');
    column.className=`operational-column ${name}`;
    nodes.filter(Boolean).forEach(node=>column.appendChild(node));
    return column;
  };
  board.append(
    makeColumn('column-cm',[priorityGrid.querySelector('[data-family="CM"]'),secondaryCard('TP'),secondaryCard('CAL')]),
    makeColumn('column-bq-pu',[priorityGrid.querySelector('.bq-stack'),priorityGrid.querySelector('.pu-stack')]),
    makeColumn('column-mf',[priorityGrid.querySelector('[data-family="MF"]'),secondaryCard('CS'),secondaryCard('PI')]),
    makeColumn('column-visual',[priorityGrid.querySelector('.priority-image'),secondaryCard('FO')])
  );
  priorityGrid.replaceWith(board);
  secondaryGrid.remove();
}

let currentJob={id:'job-c3-202602',production:'202602',reference:'7080C002',line:'C3',revision:'3'};
const renderCurrentJob=()=>{
  $('#currentJobId').textContent=currentJob.id;
  $('#currentProduction').textContent=currentJob.production;
  $('#currentReference').textContent=currentJob.reference;
  $('#currentLine').textContent=currentJob.line;
  $('#currentRevision').textContent=currentJob.revision;
};
const setCurrentJob=row=>{
  currentJob={id:row.dataset.jobId,production:row.dataset.production,reference:row.dataset.reference,line:row.dataset.line,revision:row.dataset.revision};
  renderCurrentJob();
};
let loadedPlanningRow=null;
const clearLoadedJob=()=>{
  loadedPlanningRow=null;
  $$('.job-row').forEach(row=>row.classList.remove('selected'));
  $('#loadedJobContext').classList.add('empty');
  $('.loaded-job-empty').hidden=false;
  $('.loaded-job-content').hidden=true;
};
const loadJobFromPlanning=row=>{
  loadedPlanningRow=row;
  setCurrentJob(row);
  $$('.job-row').forEach(item=>item.classList.toggle('selected',item===row));
  $('#loadedJobTitle').textContent=`${row.dataset.reference} · ${row.dataset.production}`;
  $('#loadedJobReference').textContent=row.dataset.reference;
  $('#loadedJobProduction').textContent=row.dataset.production;
  $('#loadedJobMachine').textContent=row.dataset.line;
  $('#loadedJobRevision').textContent=row.dataset.revision;
  $('#loadedJobContext').classList.remove('empty');
  $('.loaded-job-empty').hidden=true;
  $('.loaded-job-content').hidden=false;
};
function openView(name){
  Object.entries(views).forEach(([key,node])=>node?.classList.toggle('active',key===name));
  $$('.module-tabs [data-tab]').forEach(button=>button.classList.toggle('active',button.dataset.tab===name));
  document.body.dataset.view=name;
  window.scrollTo({top:0,behavior:'instant'});
}
$$('.module-tabs [data-tab]').forEach(button=>button.addEventListener('click',()=>openView(button.dataset.tab)));
$('#backPlanning').addEventListener('click',()=>openView('planning'));

const productionSelect=$('#productionSelect');
const changeProduction=delta=>{
  const next=Math.max(0,Math.min(productionSelect.options.length-1,productionSelect.selectedIndex+delta));
  productionSelect.selectedIndex=next;
  productionSelect.dispatchEvent(new Event('change'));
};
$('#previousProduction').addEventListener('click',()=>changeProduction(1));
$('#nextProduction').addEventListener('click',()=>changeProduction(-1));
productionSelect.addEventListener('change',()=>{
  const production=productionSelect.value;
  const option=productionSelect.selectedOptions[0];
  currentJob={id:option.dataset.jobId,production,reference:'7080C002',line:option.dataset.line,revision:option.dataset.revision};
  renderCurrentJob();
  $('#sheetProduction').value=production;
  $('#sheetMachine').value=option.dataset.line;
  $('#previousProduction').disabled=productionSelect.selectedIndex===productionSelect.options.length-1;
  $('#nextProduction').disabled=productionSelect.selectedIndex===0;
});
$('#openReferenceHistory').addEventListener('click',()=>openView('history'));

const calendar=$('#calendar');
for(let i=0;i<5;i++)calendar.insertAdjacentHTML('beforeend','<button class="day dmo-calendar__day blank" aria-hidden="true"></button>');
const dayLines={4:['b1'],6:['c3'],10:['b2'],14:['b3'],17:['c1'],18:['b1','c3'],19:['b2'],21:['c2'],25:['b1','c1'],28:['c3']};
for(let day=1;day<=31;day++){
  const has=[4,6,10,14,17,18,19,21,25,28].includes(day);
  const markers=(dayLines[day]||[]).map(line=>`<i class="calendar-line ${line}"></i>`).join('');
  calendar.insertAdjacentHTML('beforeend',`<button class="day dmo-calendar__day ${has?'has has-record':''} ${day===18?'selected':''}"><span>${day}</span><em>${markers}</em></button>`);
}
calendar.addEventListener('click',event=>{const day=event.target.closest('.day:not(.blank)');if(!day)return;$$('.day',calendar).forEach(item=>item.classList.remove('selected'));day.classList.add('selected');clearLoadedJob()});

$$('[data-job-row]').forEach(row=>{
  row.addEventListener('click',()=>loadJobFromPlanning(row));
  row.addEventListener('dblclick',()=>{loadJobFromPlanning(row);openView('sheet')});
});
$('#openLoadedSheet').addEventListener('click',()=>{if(loadedPlanningRow)openView('sheet')});
const linkedContextUrl=(page,job=currentJob)=>`${page}?job_on_id=${encodeURIComponent(job.id)}&job_on_revision_id=${encodeURIComponent(job.revision)}&production=${encodeURIComponent(job.production)}&reference=${encodeURIComponent(job.reference)}&line=${encodeURIComponent(job.line)}`;
$('#openLoadedControl').addEventListener('click',()=>{if(loadedPlanningRow)location.href=linkedContextUrl('21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html')});
$('#openLoadedRepairs').addEventListener('click',()=>{if(loadedPlanningRow)location.href=linkedContextUrl('34_REPARACAO_INTERNA_01_VISUAL_AUTHORITY_reparacao-interna.html')+'&view=consulta'});
$('#viewSheetControl').addEventListener('click',()=>{location.href=linkedContextUrl('21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html')});
$('#viewSheetRepairs').addEventListener('click',()=>{location.href=linkedContextUrl('34_REPARACAO_INTERNA_01_VISUAL_AUTHORITY_reparacao-interna.html')+'&view=consulta'});
$$('.line-card').forEach(card=>card.addEventListener('click',()=>{$$('.line-card').forEach(item=>item.classList.toggle('active',item===card));if(!card.querySelector('.idle'))openView('sheet')}));

const setMode=mode=>{
  document.body.dataset.mode=mode;
  $('#modeIndicator strong').textContent=mode==='edit'?'Modo edição':'Modo consulta';
  if(mode==='view')$('#inventoryPicker').dataset.closed='true';
};
$('#editSheet').addEventListener('click',()=>setMode('edit'));
$('#saveSheet').addEventListener('click',()=>setMode('view'));
$('#cancelEdit').addEventListener('click',()=>setMode('view'));
$$('.tool-change').forEach(button=>button.addEventListener('click',()=>{$('#pickerTitle').textContent=`Alterar ${button.dataset.family} associado`;$('#inventoryPicker').scrollIntoView({behavior:'smooth',block:'center'})}));

const closeToolMenus=()=>{$$('.tool-menu').forEach(menu=>menu.classList.remove('open'));$$('.tool-menu-trigger').forEach(button=>button.setAttribute('aria-expanded','false'))};
$$('.tool-card').forEach(card=>{
  const header=$(':scope > header',card); if(!header)return;
  const family=$('.tool-code',header)?.textContent.trim()||'Ferramenta';
  let trigger=$('.tool-menu-trigger',header);
  if(!trigger){trigger=document.createElement('button');trigger.className='tool-menu-trigger';trigger.type='button';trigger.setAttribute('aria-label',`Ações de ${family}`);trigger.textContent='•••';header.appendChild(trigger)}
  const menu=document.createElement('div');menu.className='tool-menu';menu.innerHTML=`<button type="button">Ver detalhe</button><button type="button">Ver histórico</button><button type="button">Abrir ficha</button><button type="button">Abrir template</button>${['CM','BQ','MF'].includes(family)?'<button type="button">Substituir associação</button>':''}`;card.appendChild(menu);
  trigger.addEventListener('click',event=>{event.stopPropagation();const open=!menu.classList.contains('open');closeToolMenus();menu.classList.toggle('open',open);trigger.setAttribute('aria-expanded',String(open))});
});
$$('.tool-control-card').forEach(card=>{
  const trigger=$('.tool-menu-trigger',card); if(!trigger)return;
  const menu=document.createElement('div');menu.className='tool-menu';menu.innerHTML='<button type="button">Abrir controlo detalhado</button><a href="#" data-mcaliper>Abrir registo MCaliper</a><button type="button">Abrir template</button><button type="button">Copiar ligação</button>';card.appendChild(menu);
  trigger.addEventListener('click',event=>{event.stopPropagation();const open=!menu.classList.contains('open');closeToolMenus();menu.classList.toggle('open',open);trigger.setAttribute('aria-expanded',String(open))});
});
document.addEventListener('click',closeToolMenus);
$$('[data-mcaliper]').forEach(link=>link.addEventListener('click',event=>event.preventDefault()));
$$('.expand-note').forEach(button=>button.addEventListener('click',event=>{event.preventDefault();const field=button.closest('.notes-field');field.classList.toggle('expanded');button.textContent=field.classList.contains('expanded')?'Recolher':'Expandir'}));
$('#goChecks').addEventListener('click',()=>$('#checksSection').scrollIntoView({behavior:'smooth',block:'start'}));
$('#printJobOn').addEventListener('click',()=>{
  const image=$('#articleImage img');
  const printUrl=new URL('job-on-impressao-4-folhas.html',location.href);
  if(image?.currentSrc||image?.src)printUrl.searchParams.set('image',image.currentSrc||image.src);
  window.open(printUrl.href,'_blank');
});

const dialog=$('#imageDialog');
$('#articleImage').addEventListener('click',()=>dialog.showModal());
$('.dialog-close',dialog).addEventListener('click',()=>dialog.close());
dialog.addEventListener('click',event=>{if(event.target===dialog)dialog.close()});

const documentDialog=$('#documentDialog');
$$('[data-document-open]').forEach(button=>button.addEventListener('click',()=>{
  const type=button.dataset.documentType;
  const state=button.dataset.documentState;
  documentDialog.dataset.documentType=type;
  const openedProduction=productionSelect?.value||currentJob.production;
  $('#documentDialogTitle').textContent=`${type} · ${openedProduction}`;
  $('#documentDialogMeta').textContent=`Job On ${currentJob.id} · revisão ${currentJob.revision}`;
  $('#documentPreviewTitle').textContent=type==='Resumo'?'Resumo consolidado no programa':`Snapshot de ${type} no programa`;
  $('#documentPreviewState').textContent=state;
  $('#documentVersions').hidden=state!=='Versões disponíveis';
  $('#openSavedPdf').disabled=!['Disponível','Versões disponíveis'].includes(state);
  $('.dialog-actions .button.primary',documentDialog).disabled=state==='A aguardar aprovação'||state==='Ainda não gerado';
  documentDialog.showModal();
}));
$('.dialog-close',documentDialog).addEventListener('click',()=>documentDialog.close());
documentDialog.addEventListener('click',event=>{if(event.target===documentDialog)documentDialog.close()});
$('#openSavedPdf').addEventListener('click',()=>{
  const type=documentDialog.dataset.documentType;
  if(type==='Peso')window.open('22_PESO_OPERADOR_02_VISUAL_AUTHORITY_PRINT_peso.html','_blank');
  else if(type==='Pegamentos')window.open('24_PEGAMENTOS_01_VISUAL_AUTHORITY_pegamentos.html','_blank');
  else window.open('21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html','_blank');
});
$('.dialog-actions .button.primary',documentDialog).addEventListener('click',()=>{
  const type=documentDialog.dataset.documentType;
  if(type==='Peso')window.open('22_PESO_OPERADOR_02_VISUAL_AUTHORITY_PRINT_peso.html','_blank');
  else if(type==='Pegamentos')window.open('24_PEGAMENTOS_01_VISUAL_AUTHORITY_pegamentos.html','_blank');
  else window.open('21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html','_blank');
});

$('#railToggle').addEventListener('click',()=>{const rail=$('#productionRail');rail.classList.toggle('open');const open=rail.classList.contains('open');$('#railToggle').setAttribute('aria-expanded',String(open));$('#railToggle').textContent=open?'Ocultar linhas':'Ver linhas'});

const controlPanels={summary:$('#controlSummary'),weight:$('#controlWeight'),comparison:$('#controlComparison'),gluing:$('#controlGluing'),'control-history':$('#controlHistory')};
$$('[data-control-tab]').forEach(button=>button.addEventListener('click',()=>{$$('[data-control-tab]').forEach(item=>item.classList.toggle('active',item===button));Object.entries(controlPanels).forEach(([key,panel])=>panel.classList.toggle('active',key===button.dataset.controlTab))}));
$('#loadCurrentJob').addEventListener('click',()=>{
  renderCurrentJob();
  const card=$('#currentJobCard');
  card.classList.add('loaded');
  setTimeout(()=>card.classList.remove('loaded'),900);
});
renderCurrentJob();

const params=new URLSearchParams(location.search);
const requestedView=params.get('view');
if(requestedView&&views[requestedView])openView(requestedView);
if(params.get('mode')==='edit'){openView('sheet');setMode('edit')}
if(params.get('focus')==='checks'){openView('sheet');requestAnimationFrame(()=>$('#checksSection').scrollIntoView({block:'start'}))}
if(innerWidth<=980)$('#productionRail').classList.add('open');
