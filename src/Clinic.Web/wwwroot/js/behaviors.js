// Small page behaviours declared with data attributes instead of inline onclick/onchange handlers,
// which the Content Security Policy blocks:
//   data-autosubmit         on a select: submit its form when the choice changes
//   data-confirm="question" on a form (on submit) or a button (on click): ask before going on
//   data-print              on a button: open the print dialog
(function () {
    document.addEventListener('change', function (e) {
        var el = e.target;
        if (el.matches && el.matches('[data-autosubmit]') && el.form) {
            el.form.submit();
        }
    });

    document.addEventListener('click', function (e) {
        var el = e.target.closest && e.target.closest('[data-confirm], [data-print]');
        if (!el || el.tagName === 'FORM') {
            return;
        }
        if (el.hasAttribute('data-print')) {
            e.preventDefault();
            window.print();
            return;
        }
        if (!window.confirm(el.getAttribute('data-confirm'))) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (form.hasAttribute && form.hasAttribute('data-confirm') && !window.confirm(form.getAttribute('data-confirm'))) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);
})();
