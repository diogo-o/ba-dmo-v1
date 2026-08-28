/* Canonical Job On visual structure adapter.
   Keeps existing Razor/service behavior, but arranges the rendered DOM to match
   20_JOB_ON_01_VISUAL_AUTHORITY_job-on.html. Safe no-op outside /jobon. */
(function () {
  const page = document.querySelector('.jobon-page');
  const tabs = document.querySelector('.jobon-tabs');
  if (!page || !tabs) return;

  const planning = page.querySelector('#planeamento');
  const split = planning?.querySelector('.dmo-work-split');
  const rail = split?.querySelector('.dmo-sidebar');

  // Canonical HTML: production rail is a sibling of the workspace, not a child
  // of Planeamento. Moving the existing node preserves its live #linePanel and
  // the API-backed content populated by jobon.js.
  if (rail && !document.querySelector('.jobon-authority-layout')) {
    const shell = document.createElement('div');
    shell.className = 'jobon-authority-layout';
    page.parentNode.insertBefore(shell, page);
    rail.classList.add('production-rail');
    shell.appendChild(rail);
    shell.appendChild(page);
  }

  // Canonical Planeamento heading: title/description on the left and Criar Job On
  // on the right. Reuse the existing server-gated button rather than duplicating it.
  const planningTitle = planning?.querySelector('.page-title');
  const createButton = planning?.querySelector('#newJob');
  if (planningTitle && createButton && !planningTitle.querySelector('.authority-title-copy')) {
    const copy = document.createElement('div');
    copy.className = 'authority-title-copy';
    const title = planningTitle.querySelector('h2');
    const description = planningTitle.querySelector('p');
    if (title) copy.appendChild(title);
    if (description) copy.appendChild(description);
    planningTitle.prepend(copy);
    planningTitle.appendChild(createButton);
  }

  // Canonical module navigation contains Controlo between Job On and Histórico.
  // Controlo is already a real server-side module/page, so this is navigation to
  // the existing implementation, not a fake client-side view.
  if (!tabs.querySelector('.jobon-control-link')) {
    const control = document.createElement('a');
    control.className = 'dmo-module-tab tab jobon-control-link';
    control.href = '/controlo';
    control.textContent = 'Controlo';
    const history = tabs.querySelector('[data-view="historico"]');
    tabs.insertBefore(control, history || null);
  }
})();
