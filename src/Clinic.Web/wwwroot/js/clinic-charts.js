// Draws the admin's clinic charts from the JSON the page carries: for each breakdown a stacked bar chart over time and a
// pie of the totals; for pain, bars of the average score with a line for the average reduction.
(() => {
    const root = document.getElementById('clinic-charts');
    const data = JSON.parse(document.getElementById('charts-data').textContent);
    if (!root || !window.Chart) return;

    const rtl = document.documentElement.dir === 'rtl';
    const persian = document.documentElement.lang === 'fa';
    const colors = ['#5d7139', '#c0563b', '#d4a24c', '#3f7f8c', '#8a5a9e', '#9bb06a', '#b07d62', '#4d5d8f', '#7a8a7a', '#c9c2a8', '#2f4a2a', '#e08f6a'];
    const toFa = s => String(s).replace(/\d/g, d => '۰۱۲۳۴۵۶۷۸۹'[d]);
    const num = v => persian ? toFa(v) : String(v);

    Chart.defaults.font.family = 'Vazirmatn, Tahoma, sans-serif';
    Chart.defaults.color = '#555';
    Chart.defaults.plugins.legend.rtl = rtl;
    Chart.defaults.plugins.tooltip.rtl = rtl;
    Chart.defaults.maintainAspectRatio = false;

    const charts = [];
    const card = (title, wide) => {
        const col = document.createElement('div');
        col.className = wide ? 'col-12' : 'col-xl-6';
        col.innerHTML = `<section class="card card-soft h-100 chart-card"><div class="card-body"><h2 class="h6 mb-3"></h2><div class="chart-body"></div></div></section>`;
        col.querySelector('h2').textContent = title;
        root.appendChild(col);
        return col.querySelector('.chart-body');
    };
    const canvas = (parent, cls) => {
        const box = document.createElement('div');
        box.className = cls;
        const c = document.createElement('canvas');
        box.appendChild(c);
        parent.appendChild(box);
        return c;
    };
    const ticks = { callback(value) { const label = this.getLabelForValue ? this.getLabelForValue(value) : value; return num(label); } };

    for (const b of data.breakdowns) {
        const total = b.key === 'total';
        const body = card(b.title, total);
        const row = document.createElement('div');
        row.className = 'row g-3 align-items-center';
        body.appendChild(row);
        const barCol = document.createElement('div');
        barCol.className = total ? 'col-12' : 'col-md-7';
        row.appendChild(barCol);
        charts.push(new Chart(canvas(barCol, 'chart-box'), {
            type: 'bar',
            data: { labels: b.periods, datasets: b.series.map((s, i) => ({ label: s.name, data: s.values, backgroundColor: colors[i % colors.length], borderRadius: 4 })) },
            options: {
                plugins: { legend: { display: !total, position: 'bottom' }, tooltip: { callbacks: { label: c => `${c.dataset.label}: ${num(c.parsed.y)}` } } },
                scales: { x: { stacked: true, reverse: rtl }, y: { stacked: true, beginAtZero: true, position: rtl ? 'right' : 'left', ticks: { precision: 0, callback: v => num(v) } } },
            },
        }));
        if (!total && b.totals.some(t => t > 0)) {
            const pieCol = document.createElement('div');
            pieCol.className = 'col-md-5';
            row.appendChild(pieCol);
            const sum = b.totals.reduce((a, c) => a + c, 0);
            charts.push(new Chart(canvas(pieCol, 'chart-box pie'), {
                type: 'pie',
                data: { labels: b.series.map(s => s.name), datasets: [{ data: b.totals, backgroundColor: b.series.map((_, i) => colors[i % colors.length]) }] },
                options: { plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { label: c => `${c.label}: ${num(c.parsed)} (${num(Math.round(c.parsed / sum * 100))}%)` } } } },
            }));
        }
    }

    const pain = data.pain;
    const body = card(pain.title, true);
    charts.push(new Chart(canvas(body, 'chart-box'), {
        data: {
            labels: pain.periods,
            datasets: [
                { type: 'bar', label: pain.series[0].name, data: pain.series[0].values, backgroundColor: '#c0563b', borderRadius: 4 },
                { type: 'line', label: pain.series[1].name, data: pain.series[1].values, borderColor: '#5d7139', backgroundColor: '#5d7139', tension: .3 },
            ],
        },
        options: {
            plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { label: c => `${c.dataset.label}: ${num(c.parsed.y)}` } } },
            scales: { x: { reverse: rtl }, y: { beginAtZero: true, suggestedMax: 10, position: rtl ? 'right' : 'left', ticks: { callback: v => num(v) } } },
        },
    }));

    // Canvas charts keep their screen size on paper unless they are resized for printing.
    window.addEventListener('beforeprint', () => charts.forEach(c => c.resize()));
    window.addEventListener('afterprint', () => charts.forEach(c => c.resize()));
})();
