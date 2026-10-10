// Grows the home page result bars when each chart scrolls into view. Without this script the bars simply show at full size.
(() => {
  const charts = document.querySelectorAll('.result-chart');
  if (!charts.length || !('IntersectionObserver' in window) || matchMedia('(prefers-reduced-motion: reduce)').matches) {
    return;
  }
  const observer = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting) {
        entry.target.classList.remove('is-waiting');
        observer.unobserve(entry.target);
      }
    }
  }, { threshold: 0.25 });
  charts.forEach(chart => {
    chart.classList.add('is-waiting');
    observer.observe(chart);
  });
})();
