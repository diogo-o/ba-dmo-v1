(function () {
  const SELECTED = "selected";

  function rows(list) {
    return list.querySelectorAll("[data-dmo-row]");
  }

  function selectRow(list, row) {
    rows(list).forEach((item) => {
      item.classList.toggle(SELECTED, item === row);
      item.setAttribute("aria-selected", item === row ? "true" : "false");
    });
    list.dispatchEvent(new CustomEvent("dmo:list-select", {
      bubbles: true,
      detail: { id: row.dataset.id || null, row }
    }));
  }

  document.querySelectorAll("[data-dmo-list]").forEach((list) => {
    list.setAttribute("role", "listbox");
    rows(list).forEach((row) => {
      row.setAttribute("role", "option");
      row.tabIndex = 0;
      row.addEventListener("click", () => selectRow(list, row));
      row.addEventListener("dblclick", () => list.dispatchEvent(new CustomEvent("dmo:list-open", {
        bubbles: true,
        detail: { id: row.dataset.id || null, row }
      })));
      row.addEventListener("keydown", (event) => {
        if (event.key === "Enter") selectRow(list, row);
        if (event.key === "Enter" && event.ctrlKey) {
          list.dispatchEvent(new CustomEvent("dmo:list-open", {
            bubbles: true,
            detail: { id: row.dataset.id || null, row }
          }));
        }
      });
    });
  });

  document.querySelectorAll("[data-dmo-calendar]").forEach((calendar) => {
    calendar.addEventListener("click", (event) => {
      const day = event.target.closest("[data-date]");
      if (!day || day.disabled) return;
      calendar.querySelectorAll("[data-date]").forEach((item) => {
        item.classList.toggle(SELECTED, item === day);
        item.setAttribute("aria-pressed", item === day ? "true" : "false");
      });
      calendar.dispatchEvent(new CustomEvent("dmo:date-select", {
        bubbles: true,
        detail: { date: day.dataset.date }
      }));
    });
  });
})();
