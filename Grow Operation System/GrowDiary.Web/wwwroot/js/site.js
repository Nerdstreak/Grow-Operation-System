(function () {
  const root = document.documentElement;
  const key = "growdiary-theme";
  const toggle = document.getElementById("theme-toggle");

  const applyTheme = (theme) => {
    root.setAttribute("data-theme", theme);
    if (toggle) toggle.checked = theme === "dark";
  };

  const saved = localStorage.getItem(key);
  applyTheme(saved === "light" || saved === "dark" ? saved : "dark");

  if (toggle) {
    toggle.addEventListener("change", () => {
      const next = toggle.checked ? "dark" : "light";
      applyTheme(next);
      localStorage.setItem(key, next);
    });
  }

  document.querySelectorAll("[data-photo-filters]").forEach((filterWrap) => {
    const buttons = filterWrap.querySelectorAll("[data-photo-filter]");
    const grid = filterWrap.parentElement?.querySelector("[data-photo-grid]");
    if (!grid) return;

    buttons.forEach((button) => {
      button.addEventListener("click", () => {
        const filter = button.getAttribute("data-photo-filter") || "all";
        buttons.forEach((b) => b.classList.toggle("is-active", b === button));
        grid.querySelectorAll("[data-photo-tag]").forEach((card) => {
          const tag = card.getAttribute("data-photo-tag");
          card.toggleAttribute("hidden", !(filter === "all" || tag === filter));
        });
      });
    });
  });

  initChartTooltips();
  initLiveRefresh();
  initWizard();
  initTabs();

  function initChartTooltips() {
    const points = document.querySelectorAll("[data-chart-point]");
    if (!points.length) return;

    const tooltip = document.createElement("div");
    tooltip.className = "chart-tooltip";
    tooltip.hidden = true;
    document.body.appendChild(tooltip);

    const buildGroupTooltip = (element) => {
      const chart = element.getAttribute("data-tooltip-chart") || "";
      const group = element.getAttribute("data-tooltip-group") || "";
      const time = element.getAttribute("data-tooltip-time") || "";
      const samePoints = Array.from(document.querySelectorAll(`[data-chart-point][data-tooltip-chart="${cssEscape(chart)}"][data-tooltip-group="${cssEscape(group)}"]`));
      const unique = [];
      const seen = new Set();
      samePoints.forEach((point) => {
        const label = point.getAttribute("data-tooltip-title") || "Wert";
        if (seen.has(label)) return;
        seen.add(label);
        unique.push({
          label,
          value: point.getAttribute("data-tooltip-value") || "–",
          color: point.getAttribute("data-tooltip-color") || "#fff"
        });
      });
      const rows = unique.map((item) => `
        <div class="chart-tooltip-row">
          <span class="chart-tooltip-label"><i class="chart-tooltip-dot" style="background:${escapeHtml(item.color)}"></i>${escapeHtml(item.label)}</span>
          <span class="chart-tooltip-value">${escapeHtml(item.value)}</span>
        </div>`).join("");
      tooltip.innerHTML = `<strong>${escapeHtml(time)}</strong><div class="chart-tooltip-list">${rows}</div>`;
    };

    const move = (event) => {
      if (tooltip.hidden) return;
      tooltip.style.left = `${event.clientX + 14}px`;
      tooltip.style.top = `${event.clientY - 14}px`;
    };

    const show = (event, point) => {
      buildGroupTooltip(point);
      tooltip.hidden = false;
      move(event);
    };

    const hide = () => { tooltip.hidden = true; };

    points.forEach((point) => {
      point.addEventListener("mouseenter", (event) => show(event, point));
      point.addEventListener("mousemove", move);
      point.addEventListener("mouseleave", hide);
      point.addEventListener("focus", () => {
        const rect = point.getBoundingClientRect();
        show({ clientX: rect.left + rect.width / 2, clientY: rect.top }, point);
      });
      point.addEventListener("blur", hide);
    });
  }

  function initLiveRefresh() {
    const homeGrid = document.querySelector("[data-live-home-endpoint]");
    if (homeGrid) {
      const endpoint = homeGrid.getAttribute("data-live-home-endpoint");
      const interval = Number(homeGrid.getAttribute("data-live-interval-ms") || 10000);
      if (endpoint) {
        const refreshHome = async () => {
          const response = await fetch(endpoint, { headers: { Accept: "application/json" }, cache: "no-store" });
          if (!response.ok) return;
          const payload = await response.json();
          (payload.tents || []).forEach((tent) => {
            const cards = homeGrid.querySelectorAll(`[data-live-tent-card][data-tent-id="${tent.tentId}"]`);
            cards.forEach((card) => applyTentPayload(card, tent));
          });
        };
        window.setInterval(() => safeRun(refreshHome), interval);
      }
    }

    const tentDetail = document.querySelector("[data-live-tent-endpoint]");
    if (tentDetail) {
      const endpoint = tentDetail.getAttribute("data-live-tent-endpoint");
      const interval = Number(tentDetail.getAttribute("data-live-interval-ms") || 10000);
      if (endpoint) {
        const refreshTent = async () => {
          const response = await fetch(endpoint, { headers: { Accept: "application/json" }, cache: "no-store" });
          if (!response.ok) return;
          const payload = await response.json();
          applyMetricPayloads(document, payload.metrics || []);
          if (payload.cameraUrl) {
            document.querySelectorAll("[data-live-camera-thumb]").forEach((img) => { img.src = payload.cameraUrl; });
          }
        };
        window.setInterval(() => safeRun(refreshTent), interval);
      }
    }
  }

  function applyTentPayload(card, payload) {
    card.classList.remove("ops-card--healthy", "ops-card--attention", "ops-card--critical", "ops-card--neutral");
    if (payload.stateTone) card.classList.add(`ops-card--${payload.stateTone}`);
    const pill = card.querySelector("[data-live-state-pill]");
    if (pill) {
      pill.className = `state-pill state-pill--${payload.stateTone || "neutral"}`;
      pill.textContent = payload.stateLabel || "neutral";
    }
    applyMetricPayloads(card, payload.metrics || []);
    if (payload.cameraUrl) {
      const image = card.querySelector("[data-live-camera-thumb]");
      if (image) image.src = payload.cameraUrl;
    }
  }

  function applyMetricPayloads(rootElement, metrics) {
    metrics.forEach((metric) => {
      const card = rootElement.querySelector(`[data-live-metric][data-metric-key="${metric.key}"]`);
      if (!card) return;
      card.classList.remove("accent", "warning", "info", "danger");
      if (metric.tone && metric.tone !== "default") card.classList.add(metric.tone);
      const value = card.querySelector("[data-live-value]");
      const unit = card.querySelector("[data-live-unit]");
      const hint = card.querySelector("[data-live-hint]");
      if (value) value.textContent = metric.value ?? "–";
      if (unit) unit.textContent = metric.unit ?? "";
      if (hint) hint.textContent = metric.hint ?? "";
    });
  }

  function initWizard() {
    document.querySelectorAll("[data-grow-wizard]").forEach((wizard) => {
      const steps = Array.from(wizard.querySelectorAll("[data-step-target]"));
      const panels = Array.from(wizard.querySelectorAll("[data-step-panel]"));
      if (!steps.length || !panels.length) return;

      let index = 0;
      const mediumSelect = wizard.querySelector("[data-medium-select]");
      const mediumPanels = Array.from(wizard.querySelectorAll("[data-medium-panel]"));
      const irrigationSelects = Array.from(wizard.querySelectorAll("[data-irrigation-select]"));
      const containerLabel = wizard.querySelector("[data-container-label]");
      const reservoirRow = wizard.querySelector("[data-reservoir-row]");

      const syncMedium = () => {
        const medium = mediumSelect?.value || "Soil";
        const activePanel = wizard.querySelector(`[data-medium-panel="${medium}"]`);
        mediumPanels.forEach((panel) => {
          const active = panel === activePanel;
          panel.hidden = !active;
          panel.querySelectorAll("input, select, textarea").forEach((field) => {
            if (field.hasAttribute("data-medium-select")) return;
            field.disabled = !active;
          });
        });

        const activeIrrigation = activePanel?.querySelector("[data-irrigation-select]");
        const irrigation = activeIrrigation?.value || "";
        const showReservoir = medium === "Hydro" || /autopot/i.test(irrigation);
        if (reservoirRow) reservoirRow.hidden = !showReservoir;

        if (containerLabel) {
          containerLabel.textContent = showReservoir && medium !== "Soil" ? "Topf / Pflanzbehälter" : medium === "Hydro" ? "Netztopf / Pflanzenplatz" : "Topf / Behälter";
        }
      };

      const syncSteps = () => {
        steps.forEach((step, i) => {
          step.classList.toggle("is-active", i === index);
          step.classList.toggle("is-done", i < index);
        });
        panels.forEach((panel, i) => panel.classList.toggle("is-active", i === index));
        wizard.classList.toggle("is-last-step", index === panels.length - 1);
      };

      steps.forEach((step, i) => step.addEventListener("click", () => { index = i; syncSteps(); }));
      wizard.querySelector("[data-step-next]")?.addEventListener("click", () => { index = Math.min(panels.length - 1, index + 1); syncSteps(); });
      wizard.querySelector("[data-step-prev]")?.addEventListener("click", () => { index = Math.max(0, index - 1); syncSteps(); });
      mediumSelect?.addEventListener("change", syncMedium);
      irrigationSelects.forEach((select) => select.addEventListener("change", syncMedium));

      syncMedium();
      syncSteps();

      // Radio-Button Gruppen
      const radioGroups = Array.from(wizard.querySelectorAll('.btn-group-radio'));
      radioGroups.forEach(function(group) {
        group.querySelectorAll('.btn-radio').forEach(function(label) {
          label.addEventListener('click', function() {
            var radio = label.querySelector('input[type=radio]');
            if (!radio || radio.disabled) return;
            radio.checked = true;
            group.querySelectorAll('.btn-radio')
                 .forEach(function(l) { l.classList.remove('is-active'); });
            label.classList.add('is-active');
            // Conditional fields aktualisieren
            updateWizardConditionals(wizard);
          });
        });
      });

      // Initiale conditional visibility setzen
      updateWizardConditionals(wizard);
    });
  }

  function updateWizardConditionals(wizard) {
    function radioVal(name) {
      var el = wizard.querySelector(
        'input[type=radio][name="' + name + '"]:checked');
      return el ? el.value : '';
    }
    function showEl(id) {
      var el = wizard.querySelector('#' + id);
      if (el) el.style.display = '';
    }
    function hideEl(id) {
      var el = wizard.querySelector('#' + id);
      if (el) el.style.display = 'none';
    }

    var seedType   = radioVal('SeedType');
    var startMat   = radioVal('StartMaterial');
    var entryPoint = radioVal('EntryPoint');
    var isAuto     = seedType === 'Autoflower';
    var isClone    = startMat === 'Clone';

    isAuto  ? hideEl('breeder-weeks-row')   : showEl('breeder-weeks-row');
    isAuto  ? showEl('autoflower-days-row') : hideEl('autoflower-days-row');
    isClone ? hideEl('germination-row')     : showEl('germination-row');
    isClone ? showEl('clone-row')           : hideEl('clone-row');

    var needsDays = entryPoint !== '' &&
                    entryPoint !== 'Germination' && !isAuto;
    needsDays ? showEl('days-in-phase-row') : hideEl('days-in-phase-row');

    var needsFlip = entryPoint === 'Flower' && !isAuto;
    needsFlip ? showEl('flip-date-row') : hideEl('flip-date-row');

    // EntryPoint-Buttons basierend auf StartMaterial filtern
    var entryGroup = wizard.querySelector('#entry-point-group');
    if (entryGroup) {
      entryGroup.querySelectorAll('.btn-radio[data-entry-for]')
      .forEach(function(label) {
        var allowed = label.getAttribute('data-entry-for').split(' ');
        var mat = isClone ? 'Clone' : 'Seed';
        var visible = allowed.indexOf(mat) !== -1;
        label.style.display = visible ? '' : 'none';
        // Wenn aktuell aktiver Button versteckt wird:
        // ersten sichtbaren aktivieren
        if (!visible && label.classList.contains('is-active')) {
          var radio = label.querySelector('input[type=radio]');
          if (radio) radio.checked = false;
          label.classList.remove('is-active');
          // Ersten sichtbaren aktivieren
          var first = entryGroup.querySelector(
            '.btn-radio:not([style*="display: none"]) ' +
            'input[type=radio]');
          if (first) {
            first.checked = true;
            first.closest('.btn-radio').classList.add('is-active');
          }
        }
      });
    }
  }

  function initTabs() {
    document.querySelectorAll("[data-gos-tabs]").forEach((wrap) => {
      const buttons = Array.from(wrap.querySelectorAll("[data-tab-target]"));
      const panelsWrap = wrap.nextElementSibling?.matches("[data-gos-panels]") ? wrap.nextElementSibling : wrap.parentElement?.querySelector("[data-gos-panels]");
      if (!panelsWrap) return;
      const panels = Array.from(panelsWrap.querySelectorAll(".gos-panel"));
      const activate = (id) => {
        buttons.forEach((button) => button.classList.toggle("is-active", button.getAttribute("data-tab-target") === id));
        panels.forEach((panel) => panel.classList.toggle("is-active", panel.id === id));
      };
      buttons.forEach((button) => button.addEventListener("click", () => activate(button.getAttribute("data-tab-target"))));
      const initial = buttons.find((b) => b.classList.contains("is-active"))?.getAttribute("data-tab-target") || buttons[0]?.getAttribute("data-tab-target");
      if (initial) activate(initial);
    });
  }

  async function safeRun(callback) {
    try { await callback(); } catch { }
  }

  function cssEscape(value) {
    if (window.CSS && typeof window.CSS.escape === "function") return window.CSS.escape(value);
    return String(value).replace(/["']/g, "\\$&");
  }

  function escapeHtml(value) {
    return value
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }
})();
