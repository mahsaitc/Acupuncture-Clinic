// Live BMI and BMR on the medical record form (BMR: Mifflin-St Jeor, from the weight before treatment).
(function () {
    'use strict';
    const box = document.getElementById('measurements');
    if (!box) return;
    const num = id => {
        const raw = (document.getElementById(id)?.value || '')
            .replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d)).replace(/[٫,]/g, '.');
        const v = parseFloat(raw);
        return isFinite(v) && v > 0 ? v : null;
    };
    const fmt = v => v.toLocaleString(document.documentElement.lang || undefined, { maximumFractionDigits: 1 });
    function update() {
        const h = num('Input_HeightCm'), w = num('Input_WeightKg');
        const age = parseInt(box.dataset.age, 10), gender = box.dataset.gender;
        document.getElementById('bmi').textContent = h && w ? fmt(w / Math.pow(h / 100, 2)) : '-';
        document.getElementById('bmr').textContent = h && w && age > 0 && gender
            ? fmt(Math.round(10 * w + 6.25 * h - 5 * age + (gender === 'Male' ? 5 : -161))) : '-';
    }
    box.addEventListener('input', update);
    update();
})();
