// =============================================================
// i18n.js — Sistema de traducción universal
//
// Funciona en CUALQUIER página que cargue translations.js antes
// que este script. No depende de DashboardLayout ni jQuery.
//
// Expone: window.I18n = { apply(lang), t(key), currentLang() }
// Auto-aplica el idioma guardado en localStorage al cargar el DOM.
// =============================================================

(function () {
    'use strict';

    function currentLang() {
        return localStorage.getItem('language') || 'es';
    }

    function t(key) {
        if (typeof translations === 'undefined') return key;
        var lang = currentLang();
        return (translations[lang] && translations[lang][key]) || key;
    }

    function apply(lang) {
        if (typeof translations === 'undefined') return;
        var texts = translations[lang];
        if (!texts) return;

        document.querySelectorAll('[data-translate-key]').forEach(function (el) {
            var key = el.getAttribute('data-translate-key');
            var val = texts[key];
            if (!val) return;

            if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                if (el.hasAttribute('placeholder')) {
                    el.placeholder = val;
                } else {
                    el.value = val;
                }
            } else {
                el.textContent = val;
            }
        });

        document.documentElement.setAttribute('data-language', lang);
        localStorage.setItem('language', lang);
    }

    window.I18n = { apply: apply, t: t, currentLang: currentLang };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { apply(currentLang()); });
    } else {
        apply(currentLang());
    }
})();
