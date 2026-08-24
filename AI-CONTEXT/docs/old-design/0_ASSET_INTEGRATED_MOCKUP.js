const $$=(s,r=document)=>[...r.querySelectorAll(s)];
$$('[data-tabs]').forEach(root=>{$$('[data-tab]',root).forEach(button=>button.addEventListener('click',()=>{const id=button.dataset.tab;$$('[data-tab]',root).forEach(x=>x.classList.toggle('active',x===button));$$('[data-panel]',root.parentElement).forEach(x=>x.classList.toggle('active',x.dataset.panel===id))}))});
$$('[data-selectable]').forEach(root=>{$$('[data-row]',root).forEach(row=>row.addEventListener('click',()=>{$$('[data-row]',root).forEach(x=>x.classList.toggle('selected',x===row))}))});
$$('.choice').forEach(choice=>choice.addEventListener('click',()=>{const list=choice.closest('.list');if(list)$$('.choice',list).forEach(x=>x.classList.toggle('active',x===choice))}));

$$('[data-control-piece]').forEach(card=>{
  const input=card.querySelector('[data-mcaliper-url]');
  const save=card.querySelector('[data-mcaliper-save]');
  const open=card.querySelector('[data-mcaliper-open]');
  const state=card.querySelector('[data-mcaliper-state]');
  const linkBox=card.querySelector('.mcaliper-link');
  let savedValue=input.value.trim();
  const sync=()=>{
    open.disabled=!savedValue;
    save.textContent=savedValue?'Atualizar ligação':'Adicionar ligação';
  };
  input.addEventListener('input',()=>{linkBox.classList.remove('saved','error');state.textContent='Alterações ainda não guardadas.';sync()});
  save.addEventListener('click',()=>{
    const value=input.value.trim();
    linkBox.classList.remove('saved','error');
    if(!value){linkBox.classList.add('error');state.textContent='Adicione uma ligação MCaliper válida.';sync();return}
    savedValue=value;
    linkBox.classList.add('saved');
    state.textContent=`Ligação guardada para ${card.dataset.controlPiece} nesta folha.`;
    sync();
  });
  open.addEventListener('click',()=>{if(savedValue)window.open(savedValue,'_blank','noopener')});
  sync();
});

const historyCalendar=document.querySelector('[data-control-history-calendar]');
if(historyCalendar){
  for(let blank=0;blank<5;blank++)historyCalendar.insertAdjacentHTML('beforeend','<button class="history-day blank" aria-hidden="true"></button>');
  const daysWithHistory=new Set([17,18,20,24]);
  for(let day=1;day<=31;day++)historyCalendar.insertAdjacentHTML('beforeend',`<button class="history-day ${daysWithHistory.has(day)?'has-events':''} ${day===18?'selected':''}" data-history-day="${day}">${day}</button>`);
  const production=document.querySelector('[data-history-production]');
  const kindFilters=$$('[data-history-kind]');
  const documentRows=$$('[data-history-document]');
  const noResults=document.querySelector('.history-no-results');
  const updateHistory=()=>{
    const enabledKinds=new Set(kindFilters.filter(input=>input.checked).map(input=>input.value));
    let visibleCount=0;
    documentRows.forEach(row=>{
      const visible=row.dataset.historyProductionId===production.value&&enabledKinds.has(row.dataset.historyDocument);
      row.hidden=!visible;
      if(visible)visibleCount++;
    });
    noResults.hidden=visibleCount!==0;
  };
  historyCalendar.addEventListener('click',event=>{
    const day=event.target.closest('[data-history-day]');
    if(!day)return;
    $$('.history-day',historyCalendar).forEach(item=>item.classList.toggle('selected',item===day));
    document.querySelector('[data-history-documents-title]').textContent=`Documentos de ${day.dataset.historyDay} de agosto`;
    updateHistory();
  });
  production.addEventListener('change',updateHistory);
  kindFilters.forEach(input=>input.addEventListener('change',updateHistory));
  document.querySelector('[data-history-clear]').addEventListener('click',()=>{production.value='202602';kindFilters.forEach(input=>input.checked=true);updateHistory()});
  updateHistory();
}
