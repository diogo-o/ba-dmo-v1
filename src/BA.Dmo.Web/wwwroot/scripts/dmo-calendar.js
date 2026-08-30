/* ============================================================
   BA DMO — dmo-calendar.js (U-09)
   The SINGLE canonical calendar behavior (Plan-V3 GLM-DSN-05;
   07_DESIGN §5; reference peso-responsavel/armazem calendars +
   dmo-interactions.js click contract). One implementation for
   every module — no variant per module.

   Contract reproduced from the Design-Reference:
   - week starts on Monday; seven columns; leading blanks disabled;
   - month label centered between prev/next controls;
   - each day is a <button> carrying data-date="YYYY-MM-DD";
   - one click selects/filters — handled by dmo-interactions.js,
     which toggles .selected + aria-pressed and dispatches
     dmo:date-select (no duplicate selection logic here);
   - changing the month NEVER auto-selects a day (GLM-DSN-05);
   - days with records expose .has-record (dot rendered in CSS);
   - "Mostrar todas as datas" clears only the date selection.

   Plan-V3 additions where the reference CSS/JS is silent
   (documented, smallest neutral behavior):
   - today carries .is-today + aria-current="date" (GLM-DSN-05);
   - arrow-key focus roving across the day grid (GLM-DSN-05
     "teclado completo"; Enter/Space selection comes free from
     native button semantics).

   Markup contract:
   <section class="dmo-card" data-dmo-calendar data-month="YYYY-MM"
            data-record-dates="YYYY-MM-DD,...">
     <div class="dmo-calendar__head">
       <button data-calendar-prev aria-label="Mês anterior">‹</button>
       <strong data-calendar-label></strong>
       <button data-calendar-next aria-label="Mês seguinte">›</button>
     </div>
     <div class="dmo-calendar__week">SEG..DOM</div>
     <div class="dmo-calendar__grid" data-calendar-grid></div>
   </section>
   A "[data-calendar-clear]" button inside the calendar clears the
   current selection (reference "Mostrar todas as datas").
   ============================================================ */
(function () {
  var MONTHS = [
    "janeiro", "fevereiro", "março", "abril", "maio", "junho",
    "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
  ];

  function pad(value) {
    return String(value).padStart(2, "0");
  }

  function iso(year, monthIndex, day) {
    return year + "-" + pad(monthIndex + 1) + "-" + pad(day);
  }

  function todayIso() {
    var now = new Date();
    return iso(now.getFullYear(), now.getMonth(), now.getDate());
  }

  // R011 — parse the data-record-lines attribute: a JSON object date -> [line keys].
  // Returns {} when the attribute is absent/invalid (safe default).
  function readRecordLines(calendar) {
    var raw = calendar.getAttribute("data-record-lines");
    if (!raw) { return {}; }
    try {
      var value = JSON.parse(raw);
      return (value && typeof value === "object") ? value : {};
    } catch (err) {
      return {};
    }
  }

  function render(calendar) {
    var grid = calendar.querySelector("[data-calendar-grid]");
    var label = calendar.querySelector("[data-calendar-label]");
    if (!grid) { return; }

    var year = calendar._year;
    var monthIndex = calendar._monthIndex;
    var records = calendar._recordDates;
    var today = todayIso();

    if (label) {
      label.textContent = MONTHS[monthIndex] + " de " + year;
    }

    grid.textContent = "";

    // Monday-first offset (reference weeks render SEG..DOM).
    var firstWeekday = new Date(year, monthIndex, 1).getDay();
    var leading = (firstWeekday + 6) % 7;
    var daysInMonth = new Date(year, monthIndex + 1, 0).getDate();

    for (var blank = 0; blank < leading; blank++) {
      var filler = document.createElement("button");
      filler.type = "button";
      filler.className = "dmo-calendar__day";
      filler.disabled = true;
      filler.setAttribute("aria-hidden", "true");
      filler.tabIndex = -1;
      grid.appendChild(filler);
    }

    var previousSelected = calendar._selectedDate;
    calendar._selectedDate = null;

    for (var day = 1; day <= daysInMonth; day++) {
      var date = iso(year, monthIndex, day);
      var button = document.createElement("button");
      button.type = "button";
      button.className = "dmo-calendar__day";
      button.textContent = String(day);
      button.setAttribute("data-date", date);
      button.setAttribute("aria-pressed", "false");

      var hasRecord = records.has(date);
      var lineKeys = calendar._recordLines[date];
      if (hasRecord) {
        button.classList.add("has-record");
      }
      // R011 line-color chips: one per distinct production line on this day.
      if (Array.isArray(lineKeys) && lineKeys.length > 0) {
        var chips = document.createElement("span");
        chips.className = "dmo-line-chips";
        chips.setAttribute("aria-hidden", "true");
        lineKeys.forEach(function (key) {
          var chip = document.createElement("i");
          chip.className = "dmo-line-chip";
          if (key) {
            chip.classList.add("dmo-line-" + key);
          }
          chips.appendChild(chip);
        });
        button.appendChild(chips);
      }
      if (date === today) {
        button.classList.add("is-today");
        button.setAttribute("aria-current", "date");
      }
      // Selection survives month navigation only when it belongs to the
      // rendered month; navigation itself never auto-selects (GLM-DSN-05).
      if (previousSelected === date) {
        button.classList.add("selected");
        button.setAttribute("aria-pressed", "true");
        calendar._selectedDate = date;
      }

      grid.appendChild(button);
    }
  }

  function shiftMonth(calendar, delta) {
    var index = calendar._monthIndex + delta;
    calendar._year += Math.floor(index / 12);
    calendar._monthIndex = ((index % 12) + 12) % 12;
    render(calendar);
    // PHASE 4: the displayed month changed client-side (no page reload).
    // Consumers that drive the calendar markers from server data (Job On
    // planning) refresh HERE — the event carries the new YYYY-MM. It never
    // auto-selects a day (GLM-DSN-05) and changes no other calendar state.
    calendar.dispatchEvent(new CustomEvent("dmo:month-change", {
      bubbles: true,
      detail: {
        year: calendar._year,
        monthIndex: calendar._monthIndex,
        month: calendar._year + "-" + pad(calendar._monthIndex + 1)
      }
    }));
  }

  function clearSelection(calendar) {
    calendar._selectedDate = null;
    calendar.querySelectorAll("[data-date]").forEach(function (day) {
      day.classList.remove("selected");
      day.setAttribute("aria-pressed", "false");
    });
  }

  function bindKeyboard(calendar) {
    var grid = calendar.querySelector("[data-calendar-grid]");
    if (!grid) { return; }

    grid.addEventListener("keydown", function (event) {
      var current = event.target.closest("[data-date]");
      if (!current) { return; }

      var days = Array.prototype.filter.call(
        grid.querySelectorAll("[data-date]"),
        function (day) { return !day.disabled; });
      var index = days.indexOf(current);
      var next = null;

      switch (event.key) {
        case "ArrowRight": next = days[index + 1]; break;
        case "ArrowLeft": next = days[index - 1]; break;
        case "ArrowDown": next = days[index + 7]; break;
        case "ArrowUp": next = days[index - 7]; break;
        case "Home": next = days[0]; break;
        case "End": next = days[days.length - 1]; break;
        default: return;
      }

      event.preventDefault();
      if (next) { next.focus(); }
    });
  }

  document.querySelectorAll("[data-dmo-calendar]").forEach(function (calendar) {
    if (calendar._dmoBound) { return; }
    calendar._dmoBound = true;

    var monthAttr = calendar.getAttribute("data-month") || "";
    var parts = monthAttr.split("-");
    var now = new Date();
    calendar._year = Number(parts[0]) || now.getFullYear();
    calendar._monthIndex = parts[1] ? Number(parts[1]) - 1 : now.getMonth();
    if (calendar._monthIndex < 0 || calendar._monthIndex > 11) {
      calendar._monthIndex = now.getMonth();
    }
    calendar._selectedDate = null;
    calendar._recordDates = new Set(
      (calendar.getAttribute("data-record-dates") || "")
        .split(",")
        .map(function (value) { return value.trim(); })
        .filter(Boolean));

    // R011 — data-record-lines carries date -> production-line color keys
    // (e.g. {"2026-08-20":["b1","c2"]}). When present, each day renders one small
    // colored chip per DISTINCT line (deterministic JobOnLineColor mapping), so
    // multiple productions/lines on the same day are all represented. Backward
    // compatible: absent -> the generic .has-record dot is used unchanged.
    calendar._recordLines = readRecordLines(calendar);

    render(calendar);
    bindKeyboard(calendar);

    var prev = calendar.querySelector("[data-calendar-prev]");
    var next = calendar.querySelector("[data-calendar-next]");
    if (prev) {
      prev.addEventListener("click", function () { shiftMonth(calendar, -1); });
    }
    if (next) {
      next.addEventListener("click", function () { shiftMonth(calendar, 1); });
    }

    calendar.querySelectorAll("[data-calendar-clear]").forEach(function (button) {
      button.addEventListener("click", function () { clearSelection(calendar); });
    });

    // PHASE 4 (Job On planning isolation): in-place planning data refresh.
    // The page updates data-month / data-record-dates / data-record-lines and
    // dispatches `dmo:calendar-data`; the grid re-reads the attributes and
    // re-renders WITHOUT any page reload. An optional detail.selectedDate
    // (YYYY-MM-DD) marks the selected day for the render — it is preserved
    // only when it belongs to the rendered month (the same rule as month
    // navigation; the event itself never auto-selects a day).
    calendar.addEventListener("dmo:calendar-data", function (event) {
      var monthAttr = calendar.getAttribute("data-month") || "";
      var parts = monthAttr.split("-");
      var now = new Date();
      calendar._year = Number(parts[0]) || now.getFullYear();
      calendar._monthIndex = parts[1] ? Number(parts[1]) - 1 : now.getMonth();
      if (calendar._monthIndex < 0 || calendar._monthIndex > 11) {
        calendar._monthIndex = now.getMonth();
      }
      calendar._recordDates = new Set(
        (calendar.getAttribute("data-record-dates") || "")
          .split(",")
          .map(function (value) { return value.trim(); })
          .filter(Boolean));
      calendar._recordLines = readRecordLines(calendar);
      var detail = event.detail;
      if (detail && typeof detail.selectedDate === "string" && detail.selectedDate) {
        calendar._selectedDate = detail.selectedDate;
      }
      render(calendar);
    });
  });
})();
