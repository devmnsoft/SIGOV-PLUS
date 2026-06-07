(function (window, $) {
  class SigovGridDataSource { load() { return $.Deferred().resolve([]).promise(); } }
  class SigovGridRenderer { renderEmpty(target) { $(target).html('<div class="p-4 text-center text-muted">Nenhum registro encontrado para os filtros atuais.</div>'); } }
  class SigovGridPagination { constructor(pageSize) { this.pageSize = pageSize || 20; } }
  class SigovGridFilters { read(form) { return $(form).serializeArray(); } clear(form) { form.reset(); } }
  class SigovGridExporter { exportJson(rows) { return JSON.stringify(rows || []); } }
  class SigovGridEmptyState { show(target, message) { $(target).html('<div class="sigov-empty-state p-4 text-center">' + message + '</div>'); } }
  class SigovGrid { constructor(element) { this.element = element; this.renderer = new SigovGridRenderer(); } init() { if (!$(this.element).find('tbody tr').length) { this.renderer.renderEmpty($(this.element).find('[data-sigov-grid-body]').first()); } } }
  window.Sigov = window.Sigov || {}; window.Sigov.SigovGrid = SigovGrid; window.Sigov.SigovGridDataSource = SigovGridDataSource; window.Sigov.SigovGridRenderer = SigovGridRenderer; window.Sigov.SigovGridPagination = SigovGridPagination; window.Sigov.SigovGridFilters = SigovGridFilters; window.Sigov.SigovGridExporter = SigovGridExporter; window.Sigov.SigovGridEmptyState = SigovGridEmptyState; $(function () { $('[data-sigov-grid="true"]').each(function () { new SigovGrid(this).init(); }); });
})(window, window.jQuery);
