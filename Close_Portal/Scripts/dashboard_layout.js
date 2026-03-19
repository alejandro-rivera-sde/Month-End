// ============================================================================
// DASHBOARD_layout.JS - JavaScript para Dashboard con Bootstrap 5
// ============================================================================

// ─── SIGNALR HANDLERS — deben registrarse antes de $.connection.hub.start() ─
(function () {
    if (typeof $.connection === 'undefined' || typeof $.connection.locationHub === 'undefined') return;
    if (window.CurrentRoleId !== 2) return;  // solo Manager

    if (!$.connection.locationHub.client.newRequest) {
        $.connection.locationHub.client.newRequest = function (data) {
            refreshBadge();
            showOsNotification('Nueva solicitud de cierre',
                data.locationName + ' — ' + data.requesterName);
        };
    }
    if (!$.connection.locationHub.client.badgeUpdate) {
        $.connection.locationHub.client.badgeUpdate = function () { refreshBadge(); };
    }
})();

// Regular — recibe notificación cuando su solicitud es respondida
(function () {
    if (typeof $.connection === 'undefined' || typeof $.connection.locationHub === 'undefined') return;
    if (window.CurrentRoleId !== 1) return;  // solo Regular

    if (!$.connection.locationHub.client.requestReviewed) {
        $.connection.locationHub.client.requestReviewed = function (data) {
            var statusLabel = data.newStatus === 'Approved' ? 'aprobada' : 'rechazada';
            showOsNotification('Solicitud ' + statusLabel,
                data.locationName + ' — por ' + data.reviewedBy);
            refreshBadge();
        };
    }
    if (!$.connection.locationHub.client.badgeUpdate) {
        $.connection.locationHub.client.badgeUpdate = function () { refreshBadge(); };
    }
})();

(function () {
    'use strict';

    console.log('✅ dashboard.js cargado');

    // ========== LANGUAGE TOGGLE ==========
    function initLanguageToggle() {
        console.log('🌐 Inicializando language toggle...');
        const languageToggle = document.querySelector('.nav-link.language-toggle');
        console.log('Language toggle encontrado:', languageToggle);

        if (!languageToggle) {
            console.warn('No se encontró .nav-link.language-toggle');
            return;
        }

        if (typeof translations === 'undefined') {
            console.error('❌ translations.js NO está cargado');
            return;
        }

        console.log('✅ translations.js disponible');

        function translatePage(lang) {
            console.log('📝 Traduciendo página a:', lang);

            const texts = translations[lang];
            if (!texts) {
                console.error('❌ Idioma no encontrado:', lang);
                return;
            }

            const elements = document.querySelectorAll('[data-translate-key]');
            console.log('Elementos a traducir encontrados:', elements.length);

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

            console.log('✅ Página traducida');
        }

        function applyLanguage(lang) {
            console.log('🌐 Aplicando idioma:', lang);
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

        console.log('✅ Language toggle inicializado');
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
        console.log('🚪 Inicializando logout modal...');

        const logoutLink = document.getElementById('logoutLink');
        const confirmLogoutBtn = document.getElementById('confirmLogout');

        console.log('logoutLink encontrado:', logoutLink);
        console.log('confirmLogout encontrado:', confirmLogoutBtn);

        if (!logoutLink) {
            console.error('❌ No se encontró #logoutLink');
            return;
        }

        logoutLink.addEventListener('click', function (e) {
            e.preventDefault();
            console.log('🖱️ Click en logout link');

            const modalElement = document.getElementById('logoutModal');
            console.log('Modal element:', modalElement);

            if (!modalElement) {
                console.error('❌ No se encontró #logoutModal');
                return;
            }

            const modal = new bootstrap.Modal(modalElement);
            console.log('Modal creado:', modal);
            modal.show();
            console.log('✅ Modal mostrado');
        });

        if (confirmLogoutBtn) {
            confirmLogoutBtn.addEventListener('click', function () {
                console.log('========================================');
                console.log('🚪 LOGOUT CONFIRMADO');
                console.log('========================================');

                // Limpiar session storage
                console.log('Limpiando sessionStorage...');
                sessionStorage.clear();
                console.log('sessionStorage limpiado');

                // Limpiar datos específicos
                console.log('Session keys antes de limpiar:', Object.keys(sessionStorage));

                // Redirigir
                console.log('Redirigiendo a Login...');
                window.location.href = '/Pages/Home/Logout.aspx'; // Mantener apuntando a Logout para limpiar la seesion.
            });
            console.log('✅ Listener de confirmLogout agregado');
        } else {
            console.error('❌ No se encontró #confirmLogout');
        }

        console.log('✅ Logout modal inicializado');
    }

    // ========== INICIALIZACIÓN ==========
    function init() {
        console.log('========================================');
        console.log('🚀 Dashboard Init');
        console.log('========================================');

        initLanguageToggle();
        initSidebarToggle();
        handleResize();
        setActiveNavItem();
        initLogoutModal();
        initOsNotifications();
        loadUnreadCount();

        console.log('========================================');
        console.log('✅ Dashboard inicializado');
        console.log('========================================');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

// ============================================================================
// OS NOTIFICATIONS — disponible globalmente para todas las páginas
// ============================================================================

function initOsNotifications() {
    if (!('Notification' in window)) return;
    if (Notification.permission === 'default') {
        Notification.requestPermission();
    }
}

function showOsNotification(title, body, icon) {
    if (!('Notification' in window)) return;
    if (Notification.permission !== 'granted') return;
    var options = { body: body, icon: icon || '/favicon.ico' };
    var n = new Notification(title, options);
    setTimeout(function () { n.close(); }, 6000);
}

// ============================================================================
// NOTIFICATION INBOX
// ============================================================================

function loadUnreadCount() {
    $.ajax({
        type: 'POST',
        url: '/Pages/Main/Live.aspx/GetUnreadCount',
        data: '{}',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (resp) {
            var d = resp.d !== undefined ? resp.d : resp;
            if (d && d.success) updateNotifBadge(d.count);
        },
        error: function () { }
    });
    subscribeToNotifGroup();
}

function subscribeToNotifGroup() {
    if (typeof $.connection === 'undefined' || typeof $.connection.locationHub === 'undefined') return;
    var hub = $.connection.locationHub;
    var roleId = window.CurrentRoleId;
    var userId = window.CurrentUserId;

    function joinGroups() {
        if (roleId === 2) hub.server.joinValidate();
        if (roleId === 1 && userId) hub.server.joinAsRequester(String(userId));
    }

    $.connection.hub.stateChanged(function (change) {
        if (change.newState === 1) joinGroups();
    });

    // Reconexión automática al desconectarse
    $.connection.hub.disconnected(function () {
        setTimeout(function () {
            $.connection.hub.start().done(joinGroups);
        }, 5000);
    });

    if ($.connection.hub.state === 1) {
        joinGroups();
    } else {
        $.connection.hub.start().done(joinGroups);
    }
}

function updateNotifBadge(count) {
    var badge = document.getElementById('notifBadge');
    if (!badge) return;
    if (count > 0) {
        badge.textContent = count > 99 ? '99+' : count;
        badge.style.display = 'block';
    } else {
        badge.style.display = 'none';
    }
}

// Llamado desde SignalR (badgeUpdate) — recarga conteo desde BD
function refreshBadge() {
    loadUnreadCount();
}

// ── Panel de notificaciones ───────────────────────────────────

function toggleNotifPanel() {
    var panel = document.getElementById('notifPanel');
    if (!panel) return;
    var isOpen = panel.style.display !== 'none';
    if (isOpen) {
        panel.style.display = 'none';
    } else {
        panel.style.display = 'block';
        loadNotifications();
    }
    // Cerrar al hacer click fuera
    if (!isOpen) {
        setTimeout(function () {
            document.addEventListener('click', closeNotifOnOutside);
        }, 10);
    }
}

function closeNotifOnOutside(e) {
    var panel = document.getElementById('notifPanel');
    var btn = document.getElementById('notifBtn');
    if (panel && !panel.contains(e.target) && !btn.contains(e.target)) {
        panel.style.display = 'none';
        document.removeEventListener('click', closeNotifOnOutside);
    }
}

function loadNotifications() {
    var list = document.getElementById('notifList');
    list.innerHTML = '<div class="notif-loading"><span class="material-icons notif-spin">autorenew</span></div>';

    $.ajax({
        type: 'POST',
        url: '/Pages/Main/Live.aspx/GetNotifications',
        data: '{}',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (resp) {
            var d = resp.d !== undefined ? resp.d : resp;
            if (!d || !d.success) { list.innerHTML = '<div class="notif-empty">Error al cargar</div>'; return; }
            renderNotifications(d.data || []);
        },
        error: function () {
            list.innerHTML = '<div class="notif-empty">Error de comunicación</div>';
        }
    });
}

function renderNotifications(items) {
    var list = document.getElementById('notifList');
    if (items.length === 0) {
        list.innerHTML = '<div class="notif-empty">Sin notificaciones</div>';
        return;
    }

    var iconMap = {
        'new_request': 'assignment',
        'request_reviewed': 'assignment_turned_in'
    };

    list.innerHTML = items.map(function (n) {
        var icon = iconMap[n.type] || 'notifications';
        var unreadClass = n.isRead ? '' : ' notif-item-unread';
        return '<div class="notif-item' + unreadClass + '">' +
            '  <span class="material-icons notif-item-icon">' + icon + '</span>' +
            '  <div class="notif-item-body">' +
            '    <p class="notif-item-msg">' + escapeNotif(n.message) + '</p>' +
            '    <span class="notif-item-time">' + n.createdAt + '</span>' +
            '  </div>' +
            '</div>';
    }).join('');
}

function markAllRead() {
    $.ajax({
        type: 'POST',
        url: '/Pages/Main/Live.aspx/MarkAllRead',
        data: '{}',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function () {
            updateNotifBadge(0);
            loadNotifications();
        }
    });
}

function escapeNotif(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}