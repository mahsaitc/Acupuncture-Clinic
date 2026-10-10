// Marks the current page in the public menu (visual only).
(function () {
  var path = location.pathname.replace(/\/+$/, '') || '/';
  document.querySelectorAll('.site-nav .navbar-nav a.nav-link[href]').forEach(function (a) {
    var href = new URL(a.href, location.href).pathname.replace(/\/+$/, '') || '/';
    var home = href === '/' || href === '/en';
    if (home ? path === href : (path === href || path.indexOf(href + '/') === 0)) {
      a.setAttribute('aria-current', 'page');
    }
  });
})();
