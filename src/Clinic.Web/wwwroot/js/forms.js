// Form helpers used on every page:
//  - checks required fields, formats (national code, phone numbers, postal code) and file sizes before a
//    form is sent, and lists every problem with a red cross at the bottom of the form;
//  - a calendar for date fields (inputs with data-date): Jalali in Persian, Gregorian in English, with a
//    month and a year list. The server still validates everything.
(function () {
    'use strict';
    const text = JSON.parse(document.getElementById('form-text')?.textContent || '{}');
    const persian = document.documentElement.lang !== 'en';

    const toLatin = s => s.replace(/[۰-۹]/g, d => d.charCodeAt(0) - 0x06F0).replace(/[٠-٩]/g, d => d.charCodeAt(0) - 0x0660);
    const toPersian = s => s.replace(/\d/g, d => '۰۱۲۳۴۵۶۷۸۹'[d]);
    const num = n => persian ? toPersian(String(n)) : String(n);

    // ------------------------------------------------------------------ validation

    const fieldsOf = form => [...form.querySelectorAll('input, select, textarea')]
        .filter(el => !el.disabled && el.type !== 'hidden' && el.type !== 'submit' && el.type !== 'button'
            && (el.dataset.val === 'true' || el.dataset.maxBytes));

    function problem(el) {
        if (el.type === 'file') {
            const max = +el.dataset.maxBytes;
            if (max && [...el.files].some(f => f.size > max)) return el.dataset.maxBytesMsg;
            return null;
        }
        if (el.type === 'checkbox' || el.type === 'radio') return null;
        const value = el.value.trim();
        if (!value) return el.dataset.valRequired || null;
        if (el.dataset.valRegexPattern && !new RegExp('^(?:' + el.dataset.valRegexPattern + ')$').test(el.value)) {
            return el.dataset.valRegex;
        }
        if (el.dataset.valEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) return el.dataset.valEmail;
        const max = +el.dataset.valLengthMax;
        if (max && value.length > max) return el.dataset.valLength;
        return null;
    }

    function messageSpan(el) {
        return el.form?.querySelector(`[data-valmsg-for="${CSS.escape(el.name)}"]`);
    }

    function mark(el, message) {
        el.classList.toggle('is-invalid', !!message);
        const span = messageSpan(el);
        if (span) {
            span.textContent = message || '';
            span.classList.toggle('field-validation-error', !!message);
            span.classList.toggle('field-validation-valid', !message);
        }
    }

    function summaryBox(form) {
        let box = form.querySelector('.form-errors');
        if (!box) {
            box = document.createElement('div');
            box.className = 'form-errors';
            box.setAttribute('role', 'alert');
            const actions = form.querySelector('.sticky-actions');
            if (actions) actions.prepend(box);
            else {
                const submit = form.querySelector('[type=submit]');
                (submit?.parentElement || form).insertBefore(box, submit || null);
            }
        }
        return box;
    }

    // items: [{ message, field }]
    function showSummary(form, items) {
        const box = summaryBox(form);
        box.replaceChildren();
        box.hidden = items.length === 0;
        if (!items.length) return;
        const title = document.createElement('div');
        title.className = 'form-errors-title';
        title.textContent = text.fixThese || '';
        const list = document.createElement('ul');
        const seen = new Set();
        items.forEach(({ message, field }) => {
            if (!message || seen.has(message)) return;
            seen.add(message);
            const li = document.createElement('li');
            const icon = document.createElement('i');
            icon.className = 'bi bi-x-circle-fill';
            const span = document.createElement(field ? 'button' : 'span');
            if (field) {
                span.type = 'button';
                span.addEventListener('click', () => { field.focus(); field.scrollIntoView({ block: 'center', behavior: 'smooth' }); });
            }
            span.textContent = message;
            li.append(icon, span);
            list.append(li);
        });
        box.append(title, list);
    }

    function check(form) {
        const items = [];
        fieldsOf(form).forEach(el => {
            const message = problem(el);
            mark(el, message);
            if (message) items.push({ message, field: el });
        });
        return items;
    }

    document.querySelectorAll('form').forEach(form => {
        if (form.method.toLowerCase() !== 'post' || form.hasAttribute('data-no-check')) return;

        // Errors the server sent back (after a save that failed) are listed at the bottom too.
        const server = [];
        form.querySelectorAll('.validation-summary-errors li').forEach(li => li.textContent.trim() && server.push({ message: li.textContent.trim() }));
        form.querySelectorAll('.field-validation-error').forEach(span => {
            const field = form.querySelector(`[name="${CSS.escape(span.dataset.valmsgFor || '')}"]`);
            field?.classList.add('is-invalid');
            if (span.textContent.trim()) server.push({ message: span.textContent.trim(), field });
        });
        if (server.length) showSummary(form, server);

        form.addEventListener('submit', e => {
            if (e.submitter?.formNoValidate) return;
            const items = check(form);
            showSummary(form, items);
            if (items.length) {
                e.preventDefault();
                e.stopImmediatePropagation();
            }
        }, true);

        // A field is checked as soon as the user leaves it, so a short national code is caught at once.
        form.addEventListener('focusout', e => {
            const el = e.target;
            if (!el.matches?.('input, textarea, select') || !fieldsOf(form).includes(el)) return;
            if (!el.value.trim() && !el.classList.contains('is-invalid')) return;
            mark(el, problem(el));
        });
        form.addEventListener('change', e => {
            if (e.target.type === 'file') mark(e.target, problem(e.target));
        });
    });

    // ------------------------------------------------------------------ calendar

    const persianParts = new Intl.DateTimeFormat('en-u-ca-persian-nu-latn', { timeZone: 'UTC', year: 'numeric', month: 'numeric', day: 'numeric' });
    const DAY = 86400000;

    function toJalali(utc) {
        const parts = Object.fromEntries(persianParts.formatToParts(new Date(utc)).map(p => [p.type, p.value]));
        return { y: parseInt(parts.year, 10), m: +parts.month, d: +parts.day };
    }

    const dayOfYear = (m, d) => (m <= 6 ? (m - 1) * 31 : 186 + (m - 7) * 30) + d;

    const compare = (a, b) => Math.sign(a.y - b.y || a.m - b.m || a.d - b.d);

    function fromJalali(y, m, d) {
        let utc = Date.UTC(y + 621, 2, 21);
        const j = toJalali(utc);
        utc += ((y - j.y) * 365 + dayOfYear(m, d) - dayOfYear(j.m, j.d)) * DAY;
        // The guess above can be a day out around leap years; step until it matches.
        for (let i = 0; i < 4; i++) {
            const c = compare(toJalali(utc), { y, m, d });
            if (!c) break;
            utc -= c * DAY;
        }
        return utc;
    }

    // A calendar abstraction: months are 1-12 in both calendars.
    const cal = persian ? {
        today() { return toJalali(Date.UTC(new Date().getFullYear(), new Date().getMonth(), new Date().getDate())); },
        daysIn(y, m) { return m <= 6 ? 31 : m <= 11 ? 30 : Math.round((fromJalali(y + 1, 1, 1) - fromJalali(y, 12, 1)) / DAY); },
        weekday(y, m, d) { return (new Date(fromJalali(y, m, d)).getUTCDay() + 1) % 7; }, // 0 = Saturday
        months: ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'],
        weekdays: ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'],
        format(v) { return toPersian(`${v.y}/${String(v.m).padStart(2, '0')}/${String(v.d).padStart(2, '0')}`); },
        parse(s) {
            const p = toLatin(s || '').trim().split(/[\/\-.]/).map(Number);
            return p.length === 3 && p[0] > 1200 && p[1] >= 1 && p[1] <= 12 && p[2] >= 1 && p[2] <= 31 ? { y: p[0], m: p[1], d: p[2] } : null;
        },
    } : {
        today() { const t = new Date(); return { y: t.getFullYear(), m: t.getMonth() + 1, d: t.getDate() }; },
        daysIn(y, m) { return new Date(Date.UTC(y, m, 0)).getUTCDate(); },
        weekday(y, m, d) { return (new Date(Date.UTC(y, m - 1, d)).getUTCDay() + 6) % 7; }, // 0 = Monday
        months: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
        weekdays: ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'],
        format(v) { return `${v.y}-${String(v.m).padStart(2, '0')}-${String(v.d).padStart(2, '0')}`; },
        parse(s) {
            const p = toLatin(s || '').trim().split(/[\/\-.]/).map(Number);
            return p.length === 3 && p[0] > 1800 && p[1] >= 1 && p[1] <= 12 && p[2] >= 1 && p[2] <= 31 ? { y: p[0], m: p[1], d: p[2] } : null;
        },
    };

    let open = null; // { input, popup }

    function close() {
        open?.popup.remove();
        open = null;
    }

    function option(value, label, selected) {
        const o = document.createElement('option');
        o.value = value; o.textContent = label; o.selected = selected;
        return o;
    }

    function button(label, cls, onClick) {
        const b = document.createElement('button');
        b.type = 'button';
        b.className = cls;
        b.textContent = label;
        b.addEventListener('click', e => { e.preventDefault(); onClick(); });
        return b;
    }

    function draw(input, popup, view) {
        const today = cal.today();
        const picked = cal.parse(input.value);
        popup.replaceChildren();

        const head = document.createElement('div');
        head.className = 'dp-head';
        const month = document.createElement('select');
        month.className = 'form-select form-select-sm';
        cal.months.forEach((name, i) => month.append(option(i + 1, name, i + 1 === view.m)));
        const year = document.createElement('select');
        year.className = 'form-select form-select-sm';
        const minYear = +(input.dataset.dateMinYear || today.y - 110), maxYear = +(input.dataset.dateMaxYear || today.y + 2);
        for (let y = maxYear; y >= minYear; y--) year.append(option(y, num(y), y === view.y));
        month.addEventListener('change', () => draw(input, popup, { y: view.y, m: +month.value }));
        year.addEventListener('change', () => draw(input, popup, { y: +year.value, m: view.m }));
        const step = delta => {
            let m = view.m + delta, y = view.y;
            if (m < 1) { m = 12; y--; } else if (m > 12) { m = 1; y++; }
            draw(input, popup, { y, m });
        };
        // The arrow pointing towards the start of the line goes back in both directions of writing.
        const prev = button('‹', 'btn btn-sm btn-light dp-nav', () => step(-1));
        const next = button('›', 'btn btn-sm btn-light dp-nav', () => step(1));
        prev.setAttribute('aria-label', text.previousMonth || '');
        next.setAttribute('aria-label', text.nextMonth || '');
        if (persian) { prev.textContent = '›'; next.textContent = '‹'; }
        head.append(prev, month, year, next);

        const grid = document.createElement('div');
        grid.className = 'dp-grid';
        cal.weekdays.forEach(w => {
            const c = document.createElement('span');
            c.className = 'dp-weekday';
            c.textContent = w;
            grid.append(c);
        });
        const first = cal.weekday(view.y, view.m, 1);
        for (let i = 0; i < first; i++) grid.append(document.createElement('span'));
        for (let d = 1; d <= cal.daysIn(view.y, view.m); d++) {
            const v = { y: view.y, m: view.m, d };
            const b = button(num(d), 'dp-day', () => {
                input.value = cal.format(v);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
                close();
                input.focus();
            });
            if (today.y === v.y && today.m === v.m && today.d === d) b.classList.add('today');
            if (picked && picked.y === v.y && picked.m === v.m && picked.d === d) b.classList.add('picked');
            grid.append(b);
        }

        const foot = document.createElement('div');
        foot.className = 'dp-foot';
        foot.append(
            button(text.today || 'Today', 'btn btn-sm btn-outline-success', () => {
                input.value = cal.format(today);
                input.dispatchEvent(new Event('change', { bubbles: true }));
                close();
            }),
            button(text.clear || 'Clear', 'btn btn-sm btn-light', () => {
                input.value = '';
                input.dispatchEvent(new Event('change', { bubbles: true }));
                close();
            }));
        popup.append(head, grid, foot);
        if (open?.popup === popup) place();
    }

    function show(input) {
        if (open?.input === input) return;
        close();
        const popup = document.createElement('div');
        popup.className = 'date-picker shadow';
        popup.dir = persian ? 'rtl' : 'ltr';
        popup.addEventListener('mousedown', e => e.stopPropagation());
        const start = cal.parse(input.value) || cal.today();
        draw(input, popup, { y: start.y, m: start.m });
        document.body.append(popup);
        open = { input, popup };
        place();
    }

    // The calendar floats above the page (cards clip their content), under the field or above it if
    // there is no room below, lined up with the field's start edge.
    function place() {
        if (!open) return;
        const field = open.input.closest('.date-field').getBoundingClientRect();
        const popup = open.popup;
        const width = popup.offsetWidth, height = popup.offsetHeight;
        let left = persian ? field.right - width : field.left;
        left = Math.max(8, Math.min(left, window.innerWidth - width - 8));
        let top = field.bottom + 4;
        if (top + height > window.innerHeight - 8 && field.top - height - 4 > 8) top = field.top - height - 4;
        popup.style.left = left + 'px';
        popup.style.top = top + 'px';
    }
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);

    document.querySelectorAll('input[data-date]').forEach(input => {
        input.autocomplete = 'off';
        const wrap = document.createElement('div');
        wrap.className = 'input-group date-field';
        input.parentElement.insertBefore(wrap, input);
        wrap.append(input);
        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'btn btn-outline-secondary';
        toggle.setAttribute('aria-label', text.pickDate || '');
        toggle.title = text.pickDate || '';
        toggle.innerHTML = '<i class="bi bi-calendar3"></i>';
        toggle.addEventListener('mousedown', e => e.stopPropagation());
        toggle.addEventListener('click', () => open?.input === input ? close() : show(input));
        wrap.append(toggle);
        input.addEventListener('click', () => show(input));
    });

    document.addEventListener('mousedown', e => {
        if (open && e.target !== open.input) close();
    });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });
})();
