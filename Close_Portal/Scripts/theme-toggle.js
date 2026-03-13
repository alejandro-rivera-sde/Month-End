// ===================================
// theme-toggle.js - Control de Tema Dark/Light
// ===================================

(function () {
    'use strict';

    // Obtener tema guardado o usar preferencia del sistema
    function getInitialTheme() {
        const savedTheme = localStorage.getItem('theme');
        if (savedTheme) {
            return savedTheme;
        }

        // Detectar preferencia del sistema
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            return 'dark';
        }

        return 'light';
    }

    // Obtener traducción según idioma
    function getThemeText(theme) {
        const lang = document.documentElement.getAttribute('data-language') || 'es';

        // Si existe translations global
        if (typeof translations !== 'undefined' && translations[lang]) {
            if (theme === 'dark') {
                return translations[lang]['theme.light'] || 'Modo Claro';
            } else {
                return translations[lang]['theme.dark'] || 'Modo Oscuro';
            }
        }

        // Fallback si no hay translations
        if (lang === 'en') {
            return theme === 'dark' ? 'Light Mode' : 'Dark Mode';
        } else {
            return theme === 'dark' ? 'Modo Claro' : 'Modo Oscuro';
        }
    }

    // Aplicar tema
    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('theme', theme);

        // Actualizar icono y texto de TODOS los botones de toggle
        updateToggleIcons(theme);
    }

    // Actualizar iconos y texto de toggle (para múltiples botones)
    function updateToggleIcons(theme) {
        const toggles = document.querySelectorAll('.theme-toggle');

        toggles.forEach(toggle => {
            // Obtener el icono (material-icons)
            const icon = toggle.querySelector('.material-icons');
            const text = toggle.querySelector('.nav-text');

            if (icon) {
                // Toggle en sidebar - cambiar icono Y texto
                if (theme === 'dark') {
                    icon.textContent = 'light_mode';
                } else {
                    icon.textContent = 'dark_mode';
                }

                // Actualizar texto según idioma
                if (text) {
                    text.textContent = getThemeText(theme);
                }
            } else {
                // Toggle flotante (Login) - SVG
                if (theme === 'dark') {
                    toggle.innerHTML = `
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <circle cx="12" cy="12" r="5"></circle>
                            <line x1="12" y1="1" x2="12" y2="3"></line>
                            <line x1="12" y1="21" x2="12" y2="23"></line>
                            <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
                            <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
                            <line x1="1" y1="12" x2="3" y2="12"></line>
                            <line x1="21" y1="12" x2="23" y2="12"></line>
                            <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
                            <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
                        </svg>
                    `;
                } else {
                    toggle.innerHTML = `
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
                        </svg>
                    `;
                }
            }
        });
    }

    // Toggle del tema
    function toggleTheme() {
        const currentTheme = document.documentElement.getAttribute('data-theme');
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        applyTheme(newTheme);
    }

    // Crear botón flotante (solo para Login)
    function createFloatingButton() {
        if (document.querySelector('.theme-toggle')) return;

        const toggle = document.createElement('button');
        toggle.className = 'theme-toggle theme-toggle-floating';
        toggle.setAttribute('aria-label', 'Toggle theme');
        toggle.setAttribute('type', 'button');
        toggle.addEventListener('click', toggleTheme);
        document.body.appendChild(toggle);

        // ← Actualizar icono ahora que el botón ya existe en el DOM
        const currentTheme = document.documentElement.getAttribute('data-theme') || 'light';
        updateToggleIcons(currentTheme);
    }

    // Inicializar theme toggles existentes en el HTML
    function initExistingToggles() {
        const toggles = document.querySelectorAll('.theme-toggle');
        toggles.forEach(toggle => {
            toggle.addEventListener('click', function (e) {
                e.preventDefault();
                toggleTheme();
            });
        });
    }

    // Exponer función toggle globalmente para uso en HTML
    window.toggleTheme = toggleTheme;

    // Exponer función para actualizar texto (cuando cambia idioma)
    window.updateThemeToggleText = function () {
        const currentTheme = document.documentElement.getAttribute('data-theme') || 'light';
        updateToggleIcons(currentTheme);
    };

    // Inicializar
    function init() {
        const initialTheme = getInitialTheme();
        applyTheme(initialTheme);

        // Inicializar toggles que ya existen en el HTML
        initExistingToggles();

        // Crear botón flotante solo si no hay ninguno (para Login)
        createFloatingButton();

        // Detectar cambios en la preferencia del sistema
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
                if (!localStorage.getItem('theme')) {
                    applyTheme(e.matches ? 'dark' : 'light');
                }
            });
        }
    }

    // Ejecutar cuando el DOM esté listo
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();