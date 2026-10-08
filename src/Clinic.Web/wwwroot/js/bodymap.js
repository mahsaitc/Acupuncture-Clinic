// Point chart editor for a treatment session. Points live in a hidden JSON field that the server re-validates.
// A standard point is kept as code + side and drawn from the library on every chart it appears on;
// a point of the doctor's own is placed by clicking a chart, named, and keeps that chart and position.
// Nothing is added until the Add button is pressed.
(function () {
    'use strict';
    const form = document.getElementById('session-form');
    if (!form) return;

    const read = id => JSON.parse(document.getElementById(id)?.textContent || 'null');
    const library = read('acupoint-library');
    const text = read('body-map-text');
    const previous = read('previous-points');
    const byCode = new Map(library.points.map(p => [p.code.toUpperCase(), p]));
    const views = new Map(library.views.map(v => [v.key, v]));
    const field = document.getElementById('points-json');
    const list = document.getElementById('point-list');
    const rowTemplate = document.getElementById('point-row');
    const search = document.getElementById('point-search');
    const sideSelect = document.getElementById('point-side');
    const unknown = document.getElementById('point-unknown');
    const pendingNote = document.getElementById('point-pending');
    const svgNS = 'http://www.w3.org/2000/svg';

    let points = [];
    try { points = JSON.parse(field.value || '[]'); } catch { points = []; }
    let selected = -1;
    let pending = null; // a place clicked on a chart, waiting for a name and the Add button

    const round = v => Math.round(v * 10) / 10;
    const known = p => p.code ? byCode.get(p.code.toUpperCase()) : null;

    // On the front and the face the patient's right is on our left; on the back it is the other way round.
    function xOf(view, dx, side) {
        const mid = view.width / 2;
        if (side === 'Midline') return mid;
        const towardsViewerLeft = (side === 'Right') === view.facesViewer;
        return towardsViewerLeft ? mid - dx : mid + dx;
    }

    function sideOf(x, view) {
        const mid = view.width / 2;
        if (Math.abs(x - mid) < 2) return 'Midline';
        return (x < mid) === view.facesViewer ? 'Right' : 'Left';
    }

    // Same rules as AcupointLibrary.Markers on the server.
    function markers(p, view) {
        const lib = known(p);
        if (!lib) return p.view === view.key && p.x != null ? [{ x: p.x, y: p.y }] : [];
        const place = lib.places.find(pl => pl.view === view.key);
        if (!place) return [];
        if (!view.symmetric) return [{ x: place.x, y: place.y }];
        const sides = lib.midline || place.x === 0 ? ['Midline']
            : p.side === 'Both' || p.side === 'Midline' ? ['Right', 'Left'] : [p.side];
        return sides.map(s => ({ x: xOf(view, place.x, s), y: place.y }));
    }

    function pageOf(viewKey) {
        return views.get(viewKey)?.page;
    }

    function pagesOf(p) {
        const lib = known(p);
        return lib ? [...new Set(lib.places.map(pl => pageOf(pl.view)))] : [pageOf(p.view)];
    }

    function save() {
        field.value = JSON.stringify(points);
    }

    function render() {
        document.querySelectorAll('.body-map svg[data-view]').forEach(svg => {
            const view = views.get(svg.dataset.view);
            const layer = svg.querySelector('.markers');
            layer.replaceChildren();
            points.forEach((p, i) => markers(p, view).forEach(m => {
                const g = document.createElementNS(svgNS, 'g');
                g.classList.add('marker');
                g.dataset.index = i;
                if (i === selected) g.classList.add('selected');
                const c = document.createElementNS(svgNS, 'circle');
                c.setAttribute('cx', m.x); c.setAttribute('cy', m.y); c.setAttribute('r', 4);
                // Labels go outwards so the left and right points, and neighbours, do not cover each other.
                const before = view.symmetric ? m.x < view.width / 2 : m.x > view.width * 0.65;
                const t = document.createElementNS(svgNS, 'text');
                t.setAttribute('x', before ? m.x - 6 : m.x + 6); t.setAttribute('y', m.y + 2.4);
                t.setAttribute('text-anchor', before ? 'end' : 'start');
                t.textContent = p.code || p.label;
                const title = document.createElementNS(svgNS, 'title');
                title.textContent = p.label + (p.note ? ' - ' + p.note : '');
                g.append(c, t, title);
                g.addEventListener('click', e => { e.stopPropagation(); select(i, true); });
                layer.append(g);
            }));
            if (pending && pending.view === view.key) {
                const g = document.createElementNS(svgNS, 'g');
                g.classList.add('marker', 'pending');
                const c = document.createElementNS(svgNS, 'circle');
                c.setAttribute('cx', pending.x); c.setAttribute('cy', pending.y); c.setAttribute('r', 5);
                const t = document.createElementNS(svgNS, 'text');
                t.setAttribute('x', pending.x + 7); t.setAttribute('y', pending.y + 2.4);
                t.textContent = search.value.trim() || '?';
                g.append(c, t);
                layer.append(g);
            }
        });
        pendingNote.hidden = !pending;

        document.querySelectorAll('[data-page-count]').forEach(badge => {
            badge.textContent = points.filter(p => pagesOf(p).includes(badge.dataset.pageCount)).length;
        });

        list.replaceChildren();
        points.forEach((p, i) => {
            const row = rowTemplate.content.firstElementChild.cloneNode(true);
            if (i === selected) row.classList.add('selected');
            const lib = known(p);
            const label = row.querySelector('.point-label');
            const note = row.querySelector('.point-note');
            const side = row.querySelector('.point-side-select');
            label.value = p.label;
            note.value = p.note || '';
            // The side can be changed where the chart cannot tell: bilateral standard points and
            // hand-placed points on the side, arm, leg and ear charts.
            const choosable = lib ? !lib.midline : !views.get(p.view)?.symmetric;
            if (choosable) {
                side.value = ['Right', 'Left'].includes(p.side) ? p.side : 'Both';
                side.addEventListener('change', () => { p.side = side.value; save(); render(); });
            } else {
                side.remove();
                row.querySelector('.point-side').textContent = text[p.side] || '';
            }
            label.addEventListener('input', () => { p.label = label.value; save(); });
            label.addEventListener('change', render);
            note.addEventListener('input', () => { p.note = note.value || null; save(); });
            label.addEventListener('focus', () => highlight(i));
            row.querySelector('.point-locate').addEventListener('click', () => { select(i, false); reveal(i); });
            row.querySelector('.point-remove').addEventListener('click', () => {
                points.splice(i, 1);
                selected = -1;
                save(); render();
            });
            list.append(row);
        });
        document.getElementById('points-count').textContent = points.length;
        document.getElementById('points-empty').hidden = points.length > 0;
    }

    function highlight(i) {
        selected = i;
        document.querySelectorAll('.body-map .marker').forEach(m => m.classList.toggle('selected', +m.dataset.index === i));
        list.querySelectorAll('.point-row').forEach((r, n) => r.classList.toggle('selected', n === i));
    }

    function select(i, focus) {
        selected = i;
        render();
        if (focus) list.children[i]?.querySelector('.point-label').focus();
    }

    function showPage(page) {
        const tab = document.querySelector(`.chart-pages [data-page="${page}"]`);
        if (tab && !tab.classList.contains('active')) bootstrap.Tab.getOrCreateInstance(tab).show();
    }

    // Switches to a chart page that shows the point (unless the current one does) and makes its markers pulse.
    function reveal(i) {
        const current = document.querySelector('.chart-pages .nav-link.active')?.dataset.page;
        const pages = pagesOf(points[i]);
        if (!pages.includes(current)) showPage(pages[0]);
        document.querySelectorAll(`.body-map .marker[data-index="${i}"]`).forEach(m => {
            m.classList.remove('pulse');
            void m.getBoundingClientRect();
            m.classList.add('pulse');
        });
    }

    /** Adds a standard point, or widens an existing one to both sides. Returns its index. */
    function addStandard(lib, side) {
        side = lib.midline ? 'Midline' : side;
        const at = points.findIndex(q => q.code && q.code.toUpperCase() === lib.code.toUpperCase());
        if (at >= 0) {
            const q = points[at];
            if (q.side !== side) q.side = lib.midline ? 'Midline' : 'Both';
            return at;
        }
        points.push({ code: lib.code, label: lib.label, side: side, view: null, x: null, y: null, note: null });
        return points.length - 1;
    }

    function findTyped(value) {
        const typed = value.trim().toUpperCase();
        if (!typed) return null;
        const code = typed.split(/\s+/)[0];
        return byCode.get(code)
            || library.points.find(p => p.label.toUpperCase() === typed)
            || library.points.find(p => p.label.toUpperCase().split(' ').slice(1).join(' ') === typed);
    }

    function addTyped() {
        const typed = search.value.trim();
        const lib = findTyped(typed);
        let i;
        if (lib) {
            i = addStandard(lib, sideSelect.value);
        } else if (pending) {
            // A point of the doctor's own, at the place clicked on the chart, under the name typed.
            const custom = points.filter(p => !p.code).length + 1;
            const side = views.get(pending.view).symmetric ? pending.side : sideSelect.value;
            points.push({ code: null, label: typed || text.custom.replace('{0}', custom), side: side, view: pending.view, x: pending.x, y: pending.y, note: null });
            i = points.length - 1;
        } else {
            unknown.hidden = !typed;
            return;
        }
        unknown.hidden = true;
        pending = null;
        search.value = '';
        save();
        select(i, false);
        reveal(i);
    }

    document.getElementById('point-add').addEventListener('click', addTyped);
    // Enter must not send the whole form; points are added only with the Add button.
    search.addEventListener('keydown', e => {
        if (e.key === 'Enter') e.preventDefault();
        if (e.key === 'Escape' && pending) { pending = null; render(); }
    });
    search.addEventListener('input', () => {
        unknown.hidden = true;
        if (pending) render();
    });
    document.getElementById('point-pending-cancel').addEventListener('click', () => { pending = null; render(); });

    document.querySelectorAll('[data-protocol]').forEach(button => button.addEventListener('click', () => {
        const protocol = library.protocols.find(p => p.key === button.dataset.protocol);
        if (points.length && !confirm(text.replace)) return;
        points = [];
        protocol.codes.forEach(code => addStandard(byCode.get(code.toUpperCase()), 'Both'));
        selected = -1;
        save(); render();
        if (points.length) reveal(0);
    }));

    document.getElementById('copy-previous')?.addEventListener('click', () => {
        if (points.length && !confirm(text.replace)) return;
        points = previous.map(p => ({ ...p }));
        selected = -1;
        save(); render();
    });

    document.getElementById('points-clear').addEventListener('click', () => {
        if (points.length && confirm(text.clear)) {
            points = [];
            selected = -1;
            save(); render();
        }
    });

    // A click on a chart marks the place of a point of the doctor's own; it is added, with the name typed
    // in the search box, when Add is pressed.
    document.querySelectorAll('.body-map svg[data-view]').forEach(svg => svg.addEventListener('click', e => {
        const view = views.get(svg.dataset.view);
        const pt = svg.createSVGPoint();
        pt.x = e.clientX; pt.y = e.clientY;
        const local = pt.matrixTransform(svg.getScreenCTM().inverse());
        if (local.x < 0 || local.x > view.width || local.y < 0 || local.y > view.height) return;
        const x = round(local.x), y = round(local.y);
        pending = { view: view.key, x: x, y: y, side: view.symmetric ? sideOf(x, view) : sideSelect.value };
        unknown.hidden = true;
        render();
        search.focus();
    }));

    form.addEventListener('submit', save);
    render();
})();
