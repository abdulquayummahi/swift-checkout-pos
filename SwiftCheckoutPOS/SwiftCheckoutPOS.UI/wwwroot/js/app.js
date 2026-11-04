// SwiftCheckoutPOS shell behaviour: theme, toasts, sidebar, confirm dialogs. No external requests.
(function () {
    'use strict';
    var root = document.documentElement;

    // ---- theme (light / dark / follow Windows) -------------------------------------------------
    function storedTheme() { try { return localStorage.getItem('scpos-theme'); } catch (e) { return null; } }
    function applyTheme(t) {
        var dark = t === 'dark' || (t !== 'light' && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
        root.setAttribute('data-bs-theme', dark ? 'dark' : 'light');
        document.querySelectorAll('[data-theme-toggle]').forEach(function (b) {
            b.setAttribute('aria-pressed', dark ? 'true' : 'false');
            b.title = dark ? 'Switch to light mode' : 'Switch to dark mode';
            var use = b.querySelector('use');
            if (use) use.setAttribute('href', '/img/icons.svg#i-' + (dark ? 'sun' : 'moon'));
        });
    }
    applyTheme(storedTheme());
    if (window.matchMedia) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function () { if (!storedTheme()) applyTheme(null); });
    }
    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-theme-toggle]');
        if (!btn) return;
        var next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem('scpos-theme', next); } catch (err) { }
        applyTheme(next);
    });

    // ---- sidebar on small screens ---------------------------------------------------------------
    document.addEventListener('click', function (e) {
        var sidebar = document.querySelector('.sidebar');
        if (!sidebar) return;
        if (e.target.closest('[data-sidebar-open]')) { sidebar.classList.add('is-open'); return; }
        if (e.target.closest('.sidebar-scrim') || e.target.closest('[data-sidebar-close]')) sidebar.classList.remove('is-open');
    });

    // ---- toasts ---------------------------------------------------------------------------------
    function dismiss(note) {
        if (!note || note.classList.contains('is-leaving')) return;
        note.classList.add('is-leaving');
        setTimeout(function () { note.remove(); }, 220);
    }
    window.scposToast = function (kind, text) {
        var stack = document.querySelector('.toast-stack');
        if (!stack) { stack = document.createElement('div'); stack.className = 'toast-stack'; stack.setAttribute('aria-live', 'polite'); document.body.appendChild(stack); }
        var note = document.createElement('div');
        note.className = 'toast-note is-' + kind;
        note.setAttribute('role', kind === 'error' ? 'alert' : 'status');
        var icon = kind === 'success' ? 'check-circle' : kind === 'error' ? 'circle-x' : 'alert';
        note.innerHTML = '<svg class="icon" aria-hidden="true"><use href="/img/icons.svg#i-' + icon + '"></use></svg><div></div>' +
            '<button type="button" class="toast-x" aria-label="Dismiss"><svg class="icon" aria-hidden="true"><use href="/img/icons.svg#i-x"></use></svg></button>';
        note.querySelector('div').textContent = text;
        stack.appendChild(note);
        if (kind === 'success') setTimeout(function () { dismiss(note); }, 5000);
    };
    document.addEventListener('click', function (e) {
        var x = e.target.closest('.toast-x');
        if (x) dismiss(x.closest('.toast-note'));
    });
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.toast-note.is-success').forEach(function (n) { setTimeout(function () { dismiss(n); }, 5000); });
    });

    // ---- key figures never hide digits: a value too wide for its box is shrunk, not cut off with "…" ----------
    function fitFigures() {
        document.querySelectorAll('.figure-value').forEach(function (el) {
            if (el.dataset.fs === undefined) el.dataset.fs = el.style.fontSize || '';
            el.style.fontSize = el.dataset.fs;
            var size = parseFloat(getComputedStyle(el).fontSize);
            while (el.scrollWidth > el.clientWidth + 1 && size > 14) { size -= 1; el.style.fontSize = size + 'px'; }
            el.title = el.scrollWidth > el.clientWidth + 1 ? el.textContent.trim() : '';
        });
    }
    var fitTimer;
    document.addEventListener('DOMContentLoaded', fitFigures);
    window.addEventListener('resize', function () { clearTimeout(fitTimer); fitTimer = setTimeout(fitFigures, 120); });
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(fitFigures);

    // ---- confirm dialogs: <form data-confirm="Question" data-confirm-ok="Button text" data-confirm-danger> --------
    var pending = null, pendingSubmitter = null;
    function confirmModal() {
        var el = document.getElementById('confirmDialog');
        if (el) return el;
        el = document.createElement('div');
        el.className = 'modal fade'; el.id = 'confirmDialog'; el.tabIndex = -1; el.setAttribute('aria-hidden', 'true');
        el.innerHTML = '<div class="modal-dialog modal-dialog-centered" style="max-width:420px"><div class="modal-content">' +
            '<div class="modal-body p-4"><h2 class="h5 mb-2" data-confirm-title></h2><p class="text-muted mb-0" data-confirm-text></p></div>' +
            '<div class="modal-footer"><button type="button" class="btn btn-quiet" data-bs-dismiss="modal">Cancel <kbd>Esc</kbd></button>' +
            '<button type="button" class="btn btn-primary" data-confirm-ok>OK <kbd>Enter</kbd></button></div></div></div>';
        document.body.appendChild(el);
        el.querySelector('[data-confirm-ok]').addEventListener('click', function () {
            var form = pending, submitter = pendingSubmitter; pending = null; pendingSubmitter = null;
            bootstrap.Modal.getOrCreateInstance(el).hide();
            if (form) { form.dataset.confirmed = '1'; if (form.requestSubmit) form.requestSubmit(submitter && submitter.form === form ? submitter : undefined); else form.submit(); }
        });
        el.addEventListener('shown.bs.modal', function () { el.querySelector('[data-confirm-ok]').focus(); });
        return el;
    }
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form.matches('form[data-confirm]')) return;
        if (form.dataset.confirmed === '1') { delete form.dataset.confirmed; return; }
        if (typeof bootstrap === 'undefined') { if (!window.confirm(form.dataset.confirm)) e.preventDefault(); return; }
        e.preventDefault();
        e.stopImmediatePropagation();
        pending = form; pendingSubmitter = e.submitter || null;
        var el = confirmModal();
        el.querySelector('[data-confirm-title]').textContent = form.dataset.confirm;
        el.querySelector('[data-confirm-text]').textContent = form.dataset.confirmText || '';
        var ok = el.querySelector('[data-confirm-ok]');
        ok.className = 'btn ' + (form.hasAttribute('data-confirm-danger') ? 'btn-danger' : 'btn-primary');
        ok.innerHTML = '';
        ok.append(document.createTextNode((form.dataset.confirmOk || 'OK') + ' '));
        var k = document.createElement('kbd'); k.textContent = 'Enter'; ok.append(k);
        bootstrap.Modal.getOrCreateInstance(el).show();
    }, true);

    // ---- busy state: <button data-busy="Saving…"> disables itself once its form submits ----------
    document.addEventListener('submit', function (e) {
        if (e.defaultPrevented) return;
        var btn = e.submitter;
        if (btn && btn.dataset.busy) { setTimeout(function () { btn.disabled = true; btn.textContent = btn.dataset.busy; }, 0); }
    });
})();
