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
                // Drawn around the origin and moved into place, so zooming can keep the marker the same size.
                g.setAttribute('transform', `translate(${m.x} ${m.y})`);
                const inner = document.createElementNS(svgNS, 'g');
                const c = document.createElementNS(svgNS, 'circle');
                c.setAttribute('cx', 0); c.setAttribute('cy', 0); c.setAttribute('r', 4);
                // Labels go outwards so the left and right points, and neighbours, do not cover each other.
                const before = view.symmetric ? m.x < view.width / 2 : m.x > view.width * 0.65;
                const t = document.createElementNS(svgNS, 'text');
                t.setAttribute('x', before ? -6 : 6); t.setAttribute('y', 2.4);
                t.setAttribute('text-anchor', before ? 'end' : 'start');
                t.textContent = p.code || p.label;
                const title = document.createElementNS(svgNS, 'title');
                title.textContent = p.label + (p.note ? ' - ' + p.note : '');
                inner.append(c, t);
                g.append(inner, title);
                g.addEventListener('click', e => { e.stopPropagation(); select(i, true); });
                layer.append(g);
            }));
            if (pending && pending.view === view.key) {
                const g = document.createElementNS(svgNS, 'g');
                g.classList.add('marker', 'pending');
                g.setAttribute('transform', `translate(${pending.x} ${pending.y})`);
                const inner = document.createElementNS(svgNS, 'g');
                const c = document.createElementNS(svgNS, 'circle');
                c.setAttribute('cx', 0); c.setAttribute('cy', 0); c.setAttribute('r', 5);
                const t = document.createElementNS(svgNS, 'text');
                t.setAttribute('x', 7); t.setAttribute('y', 2.4);
                t.textContent = search.value.trim() || '?';
                inner.append(c, t);
                g.append(inner);
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
            row.querySelector('.point-locate').addEventListener('click', () => { select(i, false); reveal(i, true); });
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

    // The first page is the whole body; the others are close-ups (head, arm, leg, ear).
    const overviewPage = document.querySelector('.chart-pages [data-page]')?.dataset.page;

    // Switches to a chart page that shows the point and makes its markers pulse. With closeUp, a point picked while
    // the whole body is shown opens the close-up chart where it can be seen clearly (LI4 opens the arm charts).
    function reveal(i, closeUp) {
        const current = document.querySelector('.chart-pages .nav-link.active')?.dataset.page;
        const pages = pagesOf(points[i]);
        const closeUps = pages.filter(pg => pg !== overviewPage);
        if (closeUp && closeUps.length && !closeUps.includes(current)) showPage(closeUps[0]);
        else if (!pages.includes(current)) showPage(pages[0]);
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

    // The suggestion list numbers every point: "36. ST5 Daying", channel points first. Typing just the number works too.
    const byNumber = new Map([...document.querySelectorAll('#point-names option')]
        .map(o => /^(\d+)\.\s+(\S+)/.exec(o.value)).filter(Boolean).map(m => [m[1], byCode.get(m[2].toUpperCase())]));

    function findTyped(value) {
        let typed = value.trim().toUpperCase().replace(/[۰-۹]/g, d => d.charCodeAt(0) - 0x06F0);
        if (!typed) return null;
        if (byNumber.has(typed)) return byNumber.get(typed);
        typed = typed.replace(/^\d+\.\s*/, '');
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
        reveal(i, true);
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

    // Each protocol can be added to the points already chosen (several protocols combine, repeats merge)
    // or replace them. The menu stays open so more protocols can be added in a row.
    document.querySelectorAll('[data-protocol]').forEach(button => button.addEventListener('click', () => {
        const protocol = library.protocols.find(p => p.key === button.dataset.protocol);
        const first = button.dataset.mode === 'replace' ? 0 : points.length;
        if (button.dataset.mode === 'replace') points = [];
        protocol.codes.forEach(code => addStandard(byCode.get(code.toUpperCase()), 'Both'));
        selected = -1;
        save(); render();
        if (points.length > first) reveal(first);
        button.classList.add('done');
        setTimeout(() => button.classList.remove('done'), 900);
    }));

    // The previous session's points can likewise be added to the current ones (repeats merge) or replace them.
    document.querySelectorAll('[data-copy-previous]').forEach(button => button.addEventListener('click', () => {
        const replace = button.dataset.copyPrevious === 'replace';
        const first = replace ? 0 : points.length;
        if (replace) points = [];
        previous.forEach(p => {
            const lib = p.code && byCode.get(p.code.toUpperCase());
            if (lib) {
                const at = addStandard(lib, p.side);
                if (at >= first && p.note) points[at].note = p.note;
            } else if (!points.some(q => !q.code && q.label === p.label && q.view === p.view && q.x === p.x && q.y === p.y)) {
                points.push({ ...p });
            }
        });
        selected = -1;
        save(); render();
        if (points.length > first) reveal(first);
        button.classList.add('done');
        setTimeout(() => button.classList.remove('done'), 900);
    }));

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


    // ------------------------------------------------------------------ zoom
    // Each chart zooms with its own + / - buttons, a double click, or Ctrl + mouse wheel, and pans by dragging
    // when zoomed in. A drag never counts as a click, so it does not place a point.
    const MAX_ZOOM = 6;
    let dragged = false;

    function setBox(svg, box) {
        const view = views.get(svg.dataset.view);
        box.w = Math.min(view.width, Math.max(view.width / MAX_ZOOM, box.w));
        box.h = box.w * view.height / view.width;
        box.x = Math.min(view.width - box.w, Math.max(0, box.x));
        box.y = Math.min(view.height - box.h, Math.max(0, box.y));
        svg.setAttribute('viewBox', `${box.x} ${box.y} ${box.w} ${box.h}`);
        svg.classList.toggle('zoomed', box.w < view.width - 0.01);
        // Markers keep their size on screen; only the picture is magnified.
        svg.style.setProperty('--marker-scale', box.w / view.width);
        svg.closest('.body-view').querySelector('.zoom-level').textContent = Math.round(view.width / box.w * 10) / 10 + '×';
    }

    function boxOf(svg) {
        const b = svg.viewBox.baseVal;
        return { x: b.x, y: b.y, w: b.width, h: b.height };
    }

    function toLocal(svg, clientX, clientY) {
        const pt = svg.createSVGPoint();
        pt.x = clientX; pt.y = clientY;
        return pt.matrixTransform(svg.getScreenCTM().inverse());
    }

    /** Zooms by factor around a chart point (the centre when none is given). */
    function zoom(svg, factor, at) {
        const box = boxOf(svg);
        const cx = at ? at.x : box.x + box.w / 2, cy = at ? at.y : box.y + box.h / 2;
        const w = box.w / factor;
        setBox(svg, { x: cx - (cx - box.x) * w / box.w, y: cy - (cy - box.y) * w / box.w, w: w });
    }

    document.querySelectorAll('.body-map svg[data-view]').forEach(svg => {
        const view = views.get(svg.dataset.view);
        const figure = svg.closest('.body-view');
        const tools = document.createElement('div');
        tools.className = 'zoom-tools';
        const button = (label, title, onClick) => {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'btn btn-sm btn-light';
            b.innerHTML = label;
            b.title = title; b.setAttribute('aria-label', title);
            b.addEventListener('click', onClick);
            return b;
        };
        const level = document.createElement('span');
        level.className = 'zoom-level';
        tools.append(
            button('<i class="bi bi-zoom-in"></i>', text.zoomIn, () => zoom(svg, 1.5)),
            button('<i class="bi bi-zoom-out"></i>', text.zoomOut, () => zoom(svg, 1 / 1.5)),
            button('<i class="bi bi-arrows-fullscreen"></i>', text.zoomReset, () => setBox(svg, { x: 0, y: 0, w: view.width })),
            level);
        figure.prepend(tools);
        setBox(svg, { x: 0, y: 0, w: view.width });

        svg.addEventListener('wheel', e => {
            if (!e.ctrlKey) return;
            e.preventDefault();
            zoom(svg, e.deltaY < 0 ? 1.25 : 0.8, toLocal(svg, e.clientX, e.clientY));
        }, { passive: false });
        svg.addEventListener('dblclick', e => {
            e.preventDefault();
            // The first click of the double click marked a place for a point of one's own: undo that.
            if (pending) { pending = null; render(); }
            zoom(svg, 2, toLocal(svg, e.clientX, e.clientY));
        });

        let start = null;
        svg.addEventListener('pointerdown', e => {
            dragged = false;
            if (!svg.classList.contains('zoomed')) return;
            start = { x: e.clientX, y: e.clientY, box: boxOf(svg), scale: svg.getScreenCTM().a };
        });
        svg.addEventListener('pointermove', e => {
            if (!start) return;
            const dx = e.clientX - start.x, dy = e.clientY - start.y;
            if (!dragged && Math.hypot(dx, dy) < 5) return;
            dragged = true;
            svg.setPointerCapture(e.pointerId);
            setBox(svg, { x: start.box.x - dx / start.scale, y: start.box.y - dy / start.scale, w: start.box.w });
        });
        const end = () => { start = null; };
        svg.addEventListener('pointerup', end);
        svg.addEventListener('pointercancel', end);
        // A drag (or the second click of a double click) must not place or pick a point.
        svg.addEventListener('click', e => {
            if (dragged || e.detail > 1) { e.stopPropagation(); e.preventDefault(); dragged = false; }
        }, true);
    });

    form.addEventListener('submit', save);
    render();
})();
