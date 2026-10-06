// Body diagram editor for a treatment session. Points live in a hidden JSON field that the server re-validates.
(function () {
    'use strict';
    const form = document.getElementById('session-form');
    if (!form) return;

    const read = id => JSON.parse(document.getElementById(id)?.textContent || 'null');
    const library = read('acupoint-library');
    const text = read('body-map-text');
    const previous = read('previous-points');
    const byCode = new Map(library.points.map(p => [p.code, p]));
    const field = document.getElementById('points-json');
    const list = document.getElementById('point-list');
    const rowTemplate = document.getElementById('point-row');
    const svgNS = 'http://www.w3.org/2000/svg';

    let points = [];
    try { points = JSON.parse(field.value || '[]'); } catch { points = []; }
    let selected = -1;

    // Front view faces us, so the patient's right is on our left; the back view is the reverse.
    const sideOf = (x, view) => Math.abs(x - 100) < 2 ? 'Midline' : ((x < 100) === (view === 'Front') ? 'Right' : 'Left');
    const round = v => Math.round(v * 10) / 10;

    function save() {
        field.value = JSON.stringify(points);
    }

    function render() {
        document.querySelectorAll('.body-map svg').forEach(svg => {
            const layer = svg.querySelector('.markers');
            layer.replaceChildren();
            points.forEach((p, i) => {
                if (p.view !== svg.dataset.view) return;
                const g = document.createElementNS(svgNS, 'g');
                g.classList.add('marker');
                if (i === selected) g.classList.add('selected');
                const c = document.createElementNS(svgNS, 'circle');
                c.setAttribute('cx', p.x); c.setAttribute('cy', p.y); c.setAttribute('r', 4.2);
                const t = document.createElementNS(svgNS, 'text');
                // Labels go on the outer side so the left and right points do not cover each other.
                t.setAttribute('x', p.x < 100 ? p.x - 6 : p.x + 6); t.setAttribute('y', p.y + 2.5);
                t.setAttribute('text-anchor', p.x < 100 ? 'end' : 'start');
                t.textContent = p.code || p.label;
                const title = document.createElementNS(svgNS, 'title');
                title.textContent = p.label + (p.note ? ' - ' + p.note : '');
                g.append(c, t, title);
                g.addEventListener('click', e => { e.stopPropagation(); select(i, true); });
                layer.append(g);
            });
        });

        list.replaceChildren();
        points.forEach((p, i) => {
            const row = rowTemplate.content.firstElementChild.cloneNode(true);
            if (i === selected) row.classList.add('selected');
            const label = row.querySelector('.point-label');
            const note = row.querySelector('.point-note');
            label.value = p.label;
            note.value = p.note || '';
            row.querySelector('.point-side').textContent = text[p.side] || '';
            label.addEventListener('input', () => { p.label = label.value; save(); });
            label.addEventListener('change', render);
            note.addEventListener('input', () => { p.note = note.value || null; save(); });
            label.addEventListener('focus', () => highlight(i));
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
        document.querySelectorAll('.body-map .marker').forEach(m => m.classList.remove('selected'));
        list.querySelectorAll('.point-row').forEach((r, n) => r.classList.toggle('selected', n === i));
        const p = points[i];
        const svg = document.querySelector(`.body-map svg[data-view="${p.view}"]`);
        svg.querySelectorAll('.marker').forEach(m => {
            const c = m.querySelector('circle');
            if (+c.getAttribute('cx') === p.x && +c.getAttribute('cy') === p.y) m.classList.add('selected');
        });
    }

    function select(i, focus) {
        selected = i;
        render();
        if (focus) list.children[i]?.querySelector('.point-label').focus();
    }

    function addStandard(code, side) {
        const p = byCode.get(code);
        if (!p) return;
        const sides = p.left === null ? ['Midline'] : side === 'Both' ? ['Right', 'Left'] : [side];
        sides.forEach(s => {
            if (points.some(q => q.code === p.code && q.side === s)) return;
            const x = s === 'Left' ? p.left : p.right;
            points.push({ code: p.code, label: p.label, view: p.view, side: s, x: x, y: p.y, note: null });
        });
    }

    document.getElementById('point-add').addEventListener('click', () => {
        const code = document.getElementById('point-select').value;
        if (!code) return;
        addStandard(code, document.getElementById('point-side').value);
        save(); render();
    });

    document.querySelectorAll('[data-protocol]').forEach(button => button.addEventListener('click', () => {
        const protocol = library.protocols.find(p => p.key === button.dataset.protocol);
        if (points.length && !confirm(text.replace)) return;
        points = [];
        protocol.codes.forEach(code => addStandard(code, 'Both'));
        selected = -1;
        save(); render();
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

    // A click on the diagram adds a free point there (ear points, catgut sites, local points).
    document.querySelectorAll('.body-map svg').forEach(svg => svg.addEventListener('click', e => {
        const pt = svg.createSVGPoint();
        pt.x = e.clientX; pt.y = e.clientY;
        const local = pt.matrixTransform(svg.getScreenCTM().inverse());
        if (local.x < 0 || local.x > 200 || local.y < 0 || local.y > 480) return;
        const view = svg.dataset.view;
        const x = round(local.x), y = round(local.y);
        const custom = points.filter(p => !p.code).length + 1;
        points.push({ code: null, label: text.custom.replace('{0}', custom), view: view, side: sideOf(x, view), x: x, y: y, note: null });
        save();
        select(points.length - 1, true);
        list.children[points.length - 1]?.querySelector('.point-label').select();
    }));

    form.addEventListener('submit', save);
    render();
})();
