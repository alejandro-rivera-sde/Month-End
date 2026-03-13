// ============================================================================
// DASHBOARD_layout.JS - JavaScript para Dashboard con Bootstrap 5
// ============================================================================
(function () {
    'use strict';

    // ========== LANGUAGE TOGGLE ==========
    function initLanguageToggle() {
        const languageToggle = document.querySelector('.nav-link.language-toggle');

        if (!languageToggle) {
            return;
        }

        if (typeof translations === 'undefined') {
            return;
        }

        function translatePage(lang) {

            const texts = translations[lang];
            if (!texts) {
                return;
            }

            const elements = document.querySelectorAll('[data-translate-key]');

            elements.forEach(element => {
                const key = element.getAttribute('data-translate-key');
                const translation = texts[key];

                if (translation) {
                    if (element.tagName === 'INPUT' || element.tagName === 'TEXTAREA') {
                        if (element.hasAttribute('placeholder')) {
                            element.placeholder = translation;
                        } else {
                            element.value = translation;
                        }
                    } else {
                        // Si tiene un hijo .nav-text, solo cambiar ese
                        const navText = element.querySelector('.nav-text');
                        if (navText) {
                            navText.textContent = translation;  // ← solo el texto, respeta el icono
                        } else {
                            element.textContent = translation;  // ← sin hijos, comportamiento normal
                        }
                    }
                }
            });

        }

        function applyLanguage(lang) {
            document.documentElement.setAttribute('data-language', lang);
            localStorage.setItem('language', lang);

            const text = languageToggle.querySelector('.nav-text');

            if (text) {
                if (lang === 'es') {
                    text.textContent = 'Switch to English';
                } else {
                    text.textContent = 'Cambiar a español';
                }
            }

            translatePage(lang);

            if (typeof window.updateThemeToggleText === 'function') {
                window.updateThemeToggleText();
            }
        }

        const savedLang = localStorage.getItem('language') || 'es';
        applyLanguage(savedLang);

        languageToggle.addEventListener('click', function (e) {
            e.preventDefault();
            const currentLang = document.documentElement.getAttribute('data-language') || 'es';
            const newLang = currentLang === 'es' ? 'en' : 'es';
            applyLanguage(newLang);
        });

    }

    // ========== SIDEBAR TOGGLE ==========
    function initSidebarToggle() {
        const menuToggle = document.getElementById('menuToggle');
        const sidebar = document.getElementById('sidebar');
        if (!menuToggle || !sidebar) return;

        menuToggle.addEventListener('click', function (e) {
            e.preventDefault();
            if (window.innerWidth > 992) {
                sidebar.classList.toggle('collapsed');
                localStorage.setItem('sidebarCollapsed', sidebar.classList.contains('collapsed'));
            } else {
                sidebar.classList.toggle('open');
            }
        });

        if (window.innerWidth > 992) {
            const savedState = localStorage.getItem('sidebarCollapsed');
            if (savedState === 'true') {
                sidebar.classList.add('collapsed');
            }
        }

        document.addEventListener('click', function (e) {
            if (window.innerWidth <= 992) {
                if (!sidebar.contains(e.target) && !menuToggle.contains(e.target)) {
                    sidebar.classList.remove('open');
                }
            }
        });
    }

    // ========== HANDLE WINDOW RESIZE ==========
    function handleResize() {
        const sidebar = document.getElementById('sidebar');
        if (!sidebar) return;

        window.addEventListener('resize', function () {
            if (window.innerWidth > 992) {
                sidebar.classList.remove('open');
            } else {
                sidebar.classList.remove('collapsed');
            }
        });
    }

    // ========== ACTIVE NAV ITEM ==========
    function setActiveNavItem() {
        const currentPath = window.location.pathname;
        const navLinks = document.querySelectorAll('.sidebar .nav-link');

        navLinks.forEach(link => {
            const href = link.getAttribute('href');
            if (href && currentPath.includes(href) && href !== '#') {
                link.classList.add('active');
            }
        });
    }

    // ========== LOGOUT MODAL ==========
    function initLogoutModal() {

        const logoutLink = document.getElementById('logoutLink');
        const confirmLogoutBtn = document.getElementById('confirmLogout');

        if (!logoutLink) {
            return;
        }

        logoutLink.addEventListener('click', function (e) {
            e.preventDefault();

            const modalElement = document.getElementById('logoutModal');

            if (!modalElement) {
                return;
            }

            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        });

        if (confirmLogoutBtn) {
            confirmLogoutBtn.addEventListener('click', function () {

                // Limpiar session storage
                sessionStorage.clear();

                // Limpiar datos específicos

                // Redirigir
                window.location.href = '/Pages/Home/Logout.aspx'; // Mantener apuntando a Logout para limpiar la seesion.
            });
        } else {
        }

    }

    // ========== INICIALIZACIÓN ==========
    function init() {

        initLanguageToggle();
        initSidebarToggle();
        handleResize();
        setActiveNavItem();
        initLogoutModal();

    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();