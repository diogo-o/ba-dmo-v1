/* ============================================================
   BA DMO — dmo-interactions.js (U-08)
   Canonical interaction contract transcribed from the
   Design-Reference (dmo-interactions.js + DMO §13/§15/§26):
   - lists: one click SELECTS a single row; double click OPENS;
     rows are keyboard focusable; no functional shortcuts;
     events dmo:list-select / dmo:list-open bubble with data-id.
   - calendars: one click selects/filters the day (aria-pressed);
     month changes never auto-select; event dmo:date-select.
   - password reveal (reference login): local visibility only.
   No domain logic lives here (DMO §26).

   EXTENDED for the admin shared selection pattern
   (ADMIN_IMPLEMENTATION_CONTRACT §2, owner F):
   - keyboard ArrowDown/Up/Home/End + Enter open;
   - selection toolbar [data-dmo-toolbar-for="<tbodyId>"] enables
     its [data-dmo-act] actions, fills {id} in data-dmo-url and
     syncs [data-dmo-row-id-input] hidden inputs;
   - audit detail card [data-dmo-detail-for="<tbodyId>"] receives a
     dmo:list-select CustomEvent on each selection change;
   - a selected row removed from the DOM clears selection.
   Existing click-select / dblclick-open behavior is unchanged.
   ============================================================ */
(function () {
  var SELECTED = "selected";

  function rows(list) {
    return list.querySelectorAll("[data-dmo-row]");
  }

  function selectedRow(list) {
    return list.querySelector('[data-dmo-row][aria-selected="true"]');
  }

  /* --- Selection toolbar (contract §2.2) -------------------------------- */

  function updateToolbar(list) {
    if (!list.id) { return; }
    var row = selectedRow(list);
    var rowId = row ? (row.getAttribute("data-id") || "") : "";
    document.querySelectorAll('[data-dmo-toolbar-for="' + list.id + '"]')
      .forEach(function (toolbar) {
        toolbar.querySelectorAll("[data-dmo-act]").forEach(function (act) {
          var disabled = !rowId;
          // Toggle the real `disabled` attribute, not the `.disabled` property:
          // on <a> anchors `.disabled` is not a standard reflecting property, so
          // assigning it alone leaves the `disabled` attribute in place and the
          // Editar (open) action never appears enabled. The attribute toggle works
          // uniformly for <button> and <a>.
          if (disabled) { act.setAttribute("disabled", ""); } else { act.removeAttribute("disabled"); }
          act.setAttribute("aria-disabled", disabled ? "true" : "false");
          var url = act.getAttribute("data-dmo-url");
          if (url) {
            var filled = url.split("{id}").join(encodeURIComponent(rowId));
            act.setAttribute("href", filled);
          }
        });
        toolbar.querySelectorAll("[data-dmo-row-id-input]").forEach(function (input) {
          input.value = rowId;
        });
      });
  }

  function bindToolbar(toolbar) {
    toolbar.addEventListener("click", function (event) {
      var act = event.target.closest("[data-dmo-act]");
      if (!act) { return; }
      // Guard on the real `disabled` attribute (covers <a> reliably; `.disabled`
      // is not a reflecting property on anchors) plus the aria-disabled state.
      if (act.hasAttribute("disabled") || act.getAttribute("aria-disabled") === "true") { return; }
      event.preventDefault();
      var listId = toolbar.getAttribute("data-dmo-toolbar-for");
      var list = listId ? document.getElementById(listId) : null;
      var row = list ? selectedRow(list) : null;
      var rowId = row ? (row.getAttribute("data-id") || "") : "";
      var actType = act.getAttribute("data-dmo-act");
      if (actType === "open") {
        var url = (act.getAttribute("data-dmo-url") || "")
          .split("{id}").join(encodeURIComponent(rowId));
        if (url) { window.location.assign(url); }
      } else if (actType === "form") {
        var confirmMsg = act.getAttribute("data-dmo-confirm");
        if (confirmMsg && !window.confirm(confirmMsg)) { return; }
        var formId = act.getAttribute("data-dmo-form");
        var form = formId ? document.getElementById(formId) : null;
        if (form) { form.submit(); }
      }
    });
  }

  /* --- Audit detail event (contract §2.3) ------------------------------- */

  function notifyDetail(list, row, selected) {
    if (!list.id) { return; }
    var detailEl = document.querySelector('[data-dmo-detail-for="' + list.id + '"]');
    if (!detailEl) { return; }
    detailEl.dispatchEvent(new CustomEvent("dmo:list-select", {
      detail: {
        row: row || null,
        selected: !!selected,
        dataset: row ? row.dataset : null
      }
    }));
  }

  /* --- Single-click selection (existing) + toolbar/detail hooks --------- */

  function selectRow(list, row) {
    rows(list).forEach(function (item) {
      item.classList.toggle(SELECTED, item === row);
      item.setAttribute("aria-selected", item === row ? "true" : "false");
    });
    list.dispatchEvent(new CustomEvent("dmo:list-select", {
      bubbles: true,
      detail: { id: row.dataset.id || null, row: row }
    }));
    updateToolbar(list);
    notifyDetail(list, row, true);
  }

  /* --- Open behavior (dblclick / Enter) -------------------------------- */

  function openRow(list, row) {
    list.dispatchEvent(new CustomEvent("dmo:list-open", {
      bubbles: true,
      detail: { id: row.dataset.id || null, row: row }
    }));
  }

  document.querySelectorAll("[data-dmo-list]").forEach(function (list) {
    list.setAttribute("role", "listbox");

    /** A row that was selected but left the DOM: clear stale selection and
        disable the toolbar (contract §2.4). Rows re-rendered server-side on a
        navigation/filter change simply reset to no selection. */
    function reconcileSelection() {
      var row = selectedRow(list);
      if (!row) {
        rows(list).forEach(function (item) {
          item.classList.remove(SELECTED);
          item.setAttribute("aria-selected", "false");
        });
      }
      updateToolbar(list);
    }

    rows(list).forEach(function (row) {
      row.setAttribute("role", "option");
      row.tabIndex = 0;
      row.addEventListener("click", function () { selectRow(list, row); });
      row.addEventListener("dblclick", function () { openRow(list, row); });
      row.addEventListener("keydown", function (event) {
        var current = event.target.closest("[data-dmo-row]");
        if (!current) { return; }
        var listRows = Array.prototype.slice.call(rows(list));
        var index = listRows.indexOf(current);
        var target = null;
        switch (event.key) {
          case "ArrowDown": target = listRows[index + 1]; break;  /* no wrap */
          case "ArrowUp":   target = listRows[index - 1]; break;  /* no wrap */
          case "Home":      target = listRows[0]; break;
          case "End":       target = listRows[listRows.length - 1]; break;
          case "Enter":
            event.preventDefault();
            selectRow(list, current);
            openRow(list, current);
            return;
        }
        if (target) {
          event.preventDefault();
          selectRow(list, target);
          target.focus();
        }
      });
    });

    /* Initial toolbar/selection reconciliation for server-pre-selected rows. */
    updateToolbar(list);

    var observer = new window.MutationObserver(reconcileSelection);
    observer.observe(list, { childList: true });
  });

  document.querySelectorAll("[data-dmo-toolbar-for]").forEach(bindToolbar);

  document.querySelectorAll("[data-dmo-calendar]").forEach(function (calendar) {
    calendar.addEventListener("click", function (event) {
      var day = event.target.closest("[data-date]");
      if (!day || day.disabled) { return; }
      calendar.querySelectorAll("[data-date]").forEach(function (item) {
        item.classList.toggle(SELECTED, item === day);
        item.setAttribute("aria-pressed", item === day ? "true" : "false");
      });
      calendar.dispatchEvent(new CustomEvent("dmo:date-select", {
        bubbles: true,
        detail: { date: day.dataset.date }
      }));
    });
  });

  /* Reference login password reveal: local visibility only. */
  document.querySelectorAll("[data-dmo-password-toggle]").forEach(function (toggle) {
    var target = document.getElementById(toggle.getAttribute("data-dmo-password-toggle"));
    if (!target) { return; }
    toggle.addEventListener("click", function () {
      var show = target.type === "password";
      target.type = show ? "text" : "password";
      toggle.textContent = show
        ? toggle.getAttribute("data-label-hide") || "Ocultar"
        : toggle.getAttribute("data-label-show") || "Mostrar";
    });
  });

  /* Double click opens the record (DMO §13): rows carrying data-open-url
     navigate on dmo:list-open. Isolated bridge for server-rendered pages. */
  document.addEventListener("dmo:list-open", function (event) {
    var row = event.detail && event.detail.row;
    var url = row && row.getAttribute("data-open-url");
    if (url) { window.location.assign(url); }
  });
})();