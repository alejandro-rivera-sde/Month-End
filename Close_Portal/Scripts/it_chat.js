// =============================================================================
// it_chat.js — Chat IT Support en tiempo real (SignalR 2.x + jQuery)
//
// Modos de operación (window.ChatMode):
//   'client'       → Support.aspx     — página dedicada, usuario → IT
//   'agent'        → ITSupport.aspx   — historial de casos (visor de registros)
//   'widget'       → cualquier página — widget flotante, usuario → IT
//   'agent-widget' → cualquier página — widget flotante, agente recibe y responde
//
// El agente IT usa 'agent-widget' como interfaz PRINCIPAL: recibe mensajes en
// tiempo real en la burbuja flotante y puede responder sin cambiar de página.
// 'agent' (ITSupport.aspx) es un visor de registros opcional.
//
// Variables de configuración (window.*):
//   ChatMode          — modo de operación (resuelto por ChatWidget.ascx.cs)
//   ChatWebMethodBase — URL base para WebMethods del chat
//   AgentName         — nombre del agente (modos agent / agent-widget)
//   CurrentUserId     — inyectado por DashboardLayout.Master
//
// Dependencias cargadas por DashboardLayout.Master ANTES que este script:
//   jQuery 3.x · jquery.signalR-2.4.3.min.js · /signalr/hubs
// =============================================================================

(function () {
    'use strict';

    var HUB_CONNECTED    = 1;
    var chatHub          = null;

    // Caso/cliente seleccionado actualmente (modos agent / agent-widget)
    var selectedCaseId   = null;
    var selectedClientId = null;

    // Estado del widget (modos widget / agent-widget)
    // Tres estados: closed | minimized | open
    var widgetOpen       = false;
    var widgetMinimized  = false; // barra flotante visible, cuerpo oculto
    var widgetLoaded     = false;
    var widgetUnread     = 0;
    var widgetInConvView = false; // agent-widget: ¿vista de conversación activa?

    // ==========================================================================
    // FUNCIONES PÚBLICAS DEL WIDGET
    // ==========================================================================

    window.toggleChatWidget = function () {
        // FAB solo visible en estado closed — siempre abre
        openChatWidgetInternal();
    };

    window.closeChatWidget = function () { closeChatWidgetInternal(); };

    // Click en el header: open → minimized, minimized → open
    window.minimizeChatWidget = function () {
        if (widgetMinimized) {
            // Barra minimizada → expandir de nuevo
            openChatWidgetInternal();
            return;
        }
        if (!widgetOpen) return; // estado closed — no debería ocurrir desde el header

        // Open → Minimized: colapsar a barra
        widgetOpen      = false;
        widgetMinimized = true;

        var panel = document.getElementById('chatWidgetPanel');
        var fab   = document.getElementById('chatWidgetBtn');
        // El panel sigue visible (display:flex), solo se añade la clase
        if (panel) panel.classList.add('chat-minimized');
        if (fab)   fab.classList.add('chat-fab-hidden');
    };

    // Volver a la lista de casos (agent-widget: conversación → lista)
    window.widgetGoBack = function () {
        selectedCaseId   = null;
        selectedClientId = null;
        widgetInConvView = false;

        var caseView = document.getElementById('widgetCaseListView');
        var convView = document.getElementById('widgetConvView');
        if (caseView) caseView.style.display = 'flex';
        if (convView) convView.style.display  = 'none';

        clearMessages();
    };

    function openChatWidgetInternal() {
        widgetOpen      = true;
        widgetMinimized = false;

        var panel = document.getElementById('chatWidgetPanel');
        var fab   = document.getElementById('chatWidgetBtn');
        var icon  = document.getElementById('chatFabIcon');

        if (panel) {
            panel.style.display = 'flex';
            panel.classList.remove('chat-minimized');
            panel.classList.remove('widget-anim');
            void panel.offsetWidth; // forzar reflow para reiniciar animación
            panel.classList.add('widget-anim');
        }
        if (fab)  fab.classList.add('chat-fab-hidden');
        if (icon) icon.textContent = 'close';

        clearWidgetBadge();

        if (!widgetLoaded) {
            widgetLoaded = true;
            if (window.ChatMode === 'agent-widget') {
                loadCases();
            } else {
                loadHistorialCliente();
            }
        } else {
            scrollToBottom();
        }
    }

    function closeChatWidgetInternal() {
        widgetOpen      = false;
        widgetMinimized = false;

        var panel = document.getElementById('chatWidgetPanel');
        var fab   = document.getElementById('chatWidgetBtn');
        var icon  = document.getElementById('chatFabIcon');

        if (panel) {
            panel.style.display = 'none';
            panel.classList.remove('chat-minimized');
        }
        if (fab)  fab.classList.remove('chat-fab-hidden');
        if (icon) {
            icon.textContent = (window.ChatMode === 'agent-widget')
                ? 'mark_chat_unread'
                : 'support_agent';
        }
    }

    function clearWidgetBadge() {
        widgetUnread = 0;
        var badge  = document.getElementById('chatWidgetBadge');   // sobre el FAB
        var hBadge = document.getElementById('chatHeaderBadge');   // en la barra minimizada
        if (badge)  badge.style.display  = 'none';
        if (hBadge) hBadge.style.display = 'none';
    }

    function addWidgetUnread() {
        widgetUnread++;
        var count  = widgetUnread > 99 ? '99+' : String(widgetUnread);
        var badge  = document.getElementById('chatWidgetBadge');
        var hBadge = document.getElementById('chatHeaderBadge');

        if (widgetMinimized) {
            // Widget minimizado → badge en el header de la barra
            if (hBadge) { hBadge.textContent = count; hBadge.style.display = 'inline-block'; }
        } else {
            // Widget cerrado → badge sobre el FAB
            if (badge) { badge.textContent = count; badge.style.display = 'block'; }
        }
    }

    // ==========================================================================
    // REGISTRO DE HANDLERS SIGNALR (síncrono al cargar el script)
    // ==========================================================================

    function initSignalRHandlers() {
        if (typeof $.connection === 'undefined' || !$.connection.chatHub) return;
        chatHub = $.connection.chatHub;

        // Pasar userId en el query string de la conexión SignalR.
        // El servidor lo usa como fallback cuando Session no está disponible en OWIN.
        // El rol NUNCA se acepta del cliente — ChatHub lo valida en BD.
        if (window.CurrentUserId && window.CurrentUserId !== '0') {
            $.connection.hub.qs = $.connection.hub.qs || {};
            $.connection.hub.qs.chatUserId = window.CurrentUserId;
        }

        // ── Modo CLIENTE (Support.aspx — página completa) ─────────────────────
        if (window.ChatMode === 'client') {
            chatHub.client.recibirRespuestaIT = function (data) {
                appendMessage(data.message, data.senderName, data.sentAt, false);
                scrollToBottom();
            };
        }

        // ── Modo WIDGET (usuario en burbuja flotante) ──────────────────────────
        if (window.ChatMode === 'widget') {
            chatHub.client.recibirRespuestaIT = function (data) {
                if (widgetOpen) {
                    appendMessage(data.message, data.senderName, data.sentAt, false);
                    scrollToBottom();
                } else {
                    addWidgetUnread();
                }
            };
        }

        // ── Modo AGENTE (ITSupport.aspx — visor de registros) ─────────────────
        if (window.ChatMode === 'agent') {
            chatHub.client.recibirMensajeDeCliente = function (data) {
                refreshCaseEntry(
                    data.caseId, data.clientId, data.clientName, data.message,
                    data.caseId !== selectedCaseId
                );
                if (data.caseId === selectedCaseId) {
                    appendMessage(data.message, data.clientName, data.sentAt, false);
                    scrollToBottom();
                    if ($.connection.hub.state === HUB_CONNECTED)
                        chatHub.server.marcarLeido(selectedCaseId);
                } else {
                    if (typeof showOsNotification === 'function') {
                        showOsNotification(
                            'Mensaje de ' + data.clientName,
                            data.message.substring(0, 80),
                            window.AppRoot + 'it-support'
                        );
                    }
                }
            };

            chatHub.client.mensajeEnviado = function (data) {
                if (data.caseId === selectedCaseId) {
                    var esPropio = (parseInt(data.senderId) === parseInt(window.CurrentUserId));
                    if (!esPropio) {
                        appendMessage(data.message, data.senderName, data.sentAt, true);
                        scrollToBottom();
                    }
                }
            };
        }

        // ── Modo AGENT-WIDGET (interfaz principal del agente) ──────────────────
        // El agente recibe mensajes en la burbuja en cualquier página y responde
        // sin salir de donde está. Patrón Intercom / Drift / Zendesk.
        if (window.ChatMode === 'agent-widget') {

            chatHub.client.recibirMensajeDeCliente = function (data) {
                // ¿Estamos leyendo activamente esta conversación?
                var inThisConv = (data.caseId === selectedCaseId
                               && widgetOpen
                               && widgetInConvView);

                // Actualizar lista de casos siempre (aunque el widget esté cerrado)
                refreshCaseEntry(
                    data.caseId, data.clientId, data.clientName, data.message,
                    !inThisConv
                );

                if (inThisConv) {
                    appendMessage(data.message, data.clientName, data.sentAt, false);
                    scrollToBottom();
                    if ($.connection.hub.state === HUB_CONNECTED)
                        chatHub.server.marcarLeido(selectedCaseId);
                } else {
                    addWidgetUnread(); // badge sobre el FAB
                }
            };

            // Sincronizar respuesta propia en otra pestaña del agente
            chatHub.client.mensajeEnviado = function (data) {
                if (data.caseId === selectedCaseId && widgetOpen && widgetInConvView) {
                    var isOwn = (parseInt(data.senderId) === parseInt(window.CurrentUserId));
                    if (!isOwn) {
                        appendMessage(data.message, data.senderName, data.sentAt, true);
                        scrollToBottom();
                    }
                }
            };
        }

        // ── Conexión + membresía de grupos ────────────────────────────────────
        // joinGroups()         → grupo personal "user-{id}" (todos los modos)
        // registerAsITAgent()  → grupo "it-agents"  (agent y agent-widget)

        var isAgentMode = (window.ChatMode === 'agent' || window.ChatMode === 'agent-widget');

        $.connection.hub.stateChanged(function (change) {
            if (change.newState === HUB_CONNECTED) {
                chatHub.server.joinGroups();
                if (isAgentMode) chatHub.server.registerAsITAgent();
            }
            updateConnectionStatus(change.newState === HUB_CONNECTED);
        });

        // Si el hub ya estaba conectado cuando se cargó este script
        if ($.connection.hub.state === HUB_CONNECTED) {
            chatHub.server.joinGroups();
            if (isAgentMode) chatHub.server.registerAsITAgent();
            updateConnectionStatus(true);
        }
    }

    // ==========================================================================
    // DOMContentLoaded
    // ==========================================================================

    function onDomReady() {
        initSendControls();

        if (window.ChatMode === 'client') {
            loadHistorialCliente();
        } else if (window.ChatMode === 'agent') {
            loadCases();
        }
        // Modos 'widget' y 'agent-widget': carga diferida al abrir el widget
    }

    // ==========================================================================
    // ENVÍO DE MENSAJES
    // ==========================================================================

    function initSendControls() {
        var sendBtn  = document.getElementById('chatSendBtn');
        var msgInput = document.getElementById('chatInput');
        if (!sendBtn || !msgInput) return;

        sendBtn.addEventListener('click', sendMessage);
        msgInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
    }

    function sendMessage() {
        var msgInput = document.getElementById('chatInput');
        if (!msgInput) return;
        var texto = msgInput.value.trim();
        if (!texto) return;

        // Verificar conexión ANTES de mostrar el mensaje — evita burbujas huérfanas
        if ($.connection.hub.state !== HUB_CONNECTED) {
            showSendError('Sin conexión con el servidor. Intenta de nuevo en unos segundos.');
            return;
        }

        msgInput.value = '';

        if (window.ChatMode === 'client' || window.ChatMode === 'widget') {
            var bubble = appendMessage(texto, 'Tú', new Date().toISOString(), true);
            scrollToBottom();
            chatHub.server.enviarMensajeAIT(texto)
                .fail(function () { markMessageFailed(bubble); });

        } else if (window.ChatMode === 'agent' || window.ChatMode === 'agent-widget') {
            if (selectedClientId === null) return;
            var bubble = appendMessage(texto, window.AgentName || 'IT Support', new Date().toISOString(), true);
            scrollToBottom();
            chatHub.server.enviarMensajeACliente(selectedClientId, texto)
                .fail(function () { markMessageFailed(bubble); });
        }
    }

    function showSendError(msg) {
        var c = document.getElementById('chatMessages');
        if (!c) return;
        var el = document.createElement('div');
        el.className = 'chat-send-error';
        el.textContent = msg;
        c.appendChild(el);
        scrollToBottom();
        setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 4000);
    }

    function markMessageFailed(msgDiv) {
        if (!msgDiv) return;
        msgDiv.classList.add('chat-msg-failed');
        var bubble = msgDiv.querySelector('.chat-bubble');
        if (bubble) {
            var errEl = document.createElement('div');
            errEl.className = 'chat-time';
            errEl.style.color = 'var(--error-color, #e53935)';
            errEl.textContent = 'No enviado — intenta de nuevo';
            bubble.appendChild(errEl);
        }
    }

    // ==========================================================================
    // HISTORIAL — MODO CLIENTE / WIDGET
    // ==========================================================================

    function loadHistorialCliente() {
        showLoadingMessages();
        $.ajax({
            type:        'POST',
            url:         window.ChatWebMethodBase + 'GetHistorial',
            data:        '{}',
            contentType: 'application/json; charset=utf-8',
            dataType:    'json',
            success: function (resp) {
                var d = resp.d !== undefined ? resp.d : resp;
                clearMessages();
                if (d && d.success && d.messages && d.messages.length > 0) {
                    d.messages.forEach(function (m) {
                        appendMessage(
                            m.message,
                            m.isClient ? 'Tú' : m.senderName,
                            m.sentAt,
                            m.isClient
                        );
                    });
                    scrollToBottom();
                } else {
                    showEmptyState();
                }
            },
            error: function () { showErrorState(); }
        });
    }

    // ==========================================================================
    // LISTA DE CASOS — MODOS AGENTE / AGENT-WIDGET
    // ==========================================================================

    // Devuelve el contenedor de la lista de casos según el modo activo
    function getClientListEl() {
        return document.getElementById(
            window.ChatMode === 'agent-widget' ? 'widgetClientList' : 'clientList'
        );
    }

    function loadCases() {
        $.ajax({
            type:        'POST',
            url:         window.ChatWebMethodBase + 'GetCases',
            data:        '{}',
            contentType: 'application/json; charset=utf-8',
            dataType:    'json',
            success: function (resp) {
                var d = resp.d !== undefined ? resp.d : resp;
                if (d && d.success) renderCaseList(d.cases || []);
            },
            error: function () {}
        });
    }

    function renderCaseList(cases) {
        var list = getClientListEl();
        if (!list) return;
        list.innerHTML = '';
        if (cases.length === 0) {
            list.innerHTML = '<div class="chat-empty-list">Sin casos abiertos en este cierre</div>';
            return;
        }
        cases.forEach(function (c) { list.appendChild(buildCaseItem(c)); });
    }

    function buildCaseItem(c) {
        var item = document.createElement('div');
        item.className = 'client-item' + (c.caseId === selectedCaseId ? ' active' : '');
        item.dataset.caseId   = c.caseId;
        item.dataset.clientId = c.clientId;

        var avatar = document.createElement('div');
        avatar.className = 'client-avatar';
        avatar.textContent = (c.clientName || 'U').charAt(0).toUpperCase();

        var info = document.createElement('div');
        info.className = 'client-info';

        var nameRow = document.createElement('div');
        nameRow.className = 'client-name-row';

        var name = document.createElement('span');
        name.className = 'client-name';
        name.textContent = c.clientName || 'Usuario';
        nameRow.appendChild(name);

        if (c.unreadCount > 0) {
            var badge = document.createElement('span');
            badge.className = 'client-badge';
            badge.textContent = c.unreadCount > 99 ? '99+' : c.unreadCount;
            nameRow.appendChild(badge);
        }

        var preview = document.createElement('div');
        preview.className = 'client-preview';
        preview.textContent = (c.lastMessage || '').substring(0, 50);

        info.appendChild(nameRow);
        info.appendChild(preview);
        item.appendChild(avatar);
        item.appendChild(info);
        item.addEventListener('click', function () {
            selectCase(c.caseId, c.clientId, c.clientName);
        });
        return item;
    }

    function refreshCaseEntry(caseId, clientId, clientName, message, addBadge) {
        var list     = getClientListEl();
        var existing = list ? list.querySelector('[data-case-id="' + caseId + '"]') : null;

        if (existing) {
            var preview = existing.querySelector('.client-preview');
            if (preview) preview.textContent = message.substring(0, 50);

            if (addBadge) {
                var badge = existing.querySelector('.client-badge');
                if (badge) {
                    var count = parseInt(badge.textContent) || 0;
                    badge.textContent = count + 1 > 99 ? '99+' : String(count + 1);
                } else {
                    var nameRow = existing.querySelector('.client-name-row');
                    if (nameRow) {
                        var nb = document.createElement('span');
                        nb.className = 'client-badge';
                        nb.textContent = '1';
                        nameRow.appendChild(nb);
                    }
                }
            }
            if (list) list.insertBefore(existing, list.firstChild);

        } else {
            var newItem = buildCaseItem({
                caseId:      caseId,
                clientId:    clientId,
                clientName:  clientName,
                lastMessage: message,
                unreadCount: addBadge ? 1 : 0
            });
            if (list) {
                var emptyMsg = list.querySelector('.chat-empty-list');
                if (emptyMsg) emptyMsg.remove();
                list.insertBefore(newItem, list.firstChild);
            }
        }
    }

    function selectCase(caseId, clientId, clientName) {
        selectedCaseId   = caseId;
        selectedClientId = clientId;

        if (window.ChatMode === 'agent-widget') {
            widgetInConvView = true;

            // Cambiar a la vista de conversación
            var caseView = document.getElementById('widgetCaseListView');
            var convView = document.getElementById('widgetConvView');
            if (caseView) caseView.style.display = 'none';
            if (convView) convView.style.display  = 'flex';

            // Mostrar nombre y avatar del cliente en el sub-header
            var nameEl   = document.getElementById('widgetClientName');
            var avatarEl = document.getElementById('widgetConvAvatar');
            if (nameEl)   nameEl.textContent   = clientName;
            if (avatarEl) avatarEl.textContent = clientName.charAt(0).toUpperCase();

            // Quitar badge del item de esta conversación
            var list = getClientListEl();
            if (list) {
                var item = list.querySelector('[data-case-id="' + caseId + '"]');
                if (item) {
                    var badge = item.querySelector('.client-badge');
                    if (badge) badge.remove();
                }
            }

            if ($.connection.hub.state === HUB_CONNECTED)
                chatHub.server.marcarLeido(caseId);

            loadHistorialAgente(clientId);

        } else {
            // Modo 'agent' (ITSupport.aspx — panel completo de dos columnas)
            document.querySelectorAll('.client-item').forEach(function (item) {
                item.classList.toggle('active', parseInt(item.dataset.caseId) === caseId);
            });

            var activeItem = document.querySelector('[data-case-id="' + caseId + '"]');
            if (activeItem) {
                var badge = activeItem.querySelector('.client-badge');
                if (badge) badge.remove();
            }

            var chatPanel    = document.getElementById('chatPanel');
            var noneSelected = document.getElementById('noneSelected');
            if (chatPanel)    chatPanel.style.display = 'flex';
            if (noneSelected) noneSelected.style.display = 'none';

            var clientNameEl = document.getElementById('chatClientName');
            var convAvatarEl = document.getElementById('convAvatar');
            if (clientNameEl) clientNameEl.textContent = clientName;
            if (convAvatarEl) convAvatarEl.textContent = clientName.charAt(0).toUpperCase();

            if ($.connection.hub.state === HUB_CONNECTED)
                chatHub.server.marcarLeido(caseId);

            loadHistorialAgente(clientId);
        }
    }

    function loadHistorialAgente(clientId) {
        clearMessages();
        showLoadingMessages();
        $.ajax({
            type:        'POST',
            url:         window.ChatWebMethodBase + 'GetHistorial',
            data:        JSON.stringify({ clientId: clientId }),
            contentType: 'application/json; charset=utf-8',
            dataType:    'json',
            success: function (resp) {
                var d = resp.d !== undefined ? resp.d : resp;
                clearMessages();
                if (d && d.success && d.messages && d.messages.length > 0) {
                    d.messages.forEach(function (m) {
                        // isClient=true → el cliente escribió (aparece a la izquierda para el agente)
                        // isClient=false → el agente escribió (aparece a la derecha)
                        appendMessage(m.message, m.senderName, m.sentAt, !m.isClient);
                    });
                    scrollToBottom();
                } else {
                    showEmptyState();
                }
            },
            error: function () { showErrorState(); }
        });
    }

    // ==========================================================================
    // HELPERS DE UI
    // ==========================================================================

    function appendMessage(texto, nombre, sentAt, esPropio) {
        var container = document.getElementById('chatMessages');
        if (!container) return;

        var msgDiv = document.createElement('div');
        msgDiv.className = 'chat-msg ' + (esPropio ? 'chat-msg-own' : 'chat-msg-other');

        var bubble = document.createElement('div');
        bubble.className = 'chat-bubble';

        if (!esPropio) {
            var senderEl = document.createElement('div');
            senderEl.className = 'chat-sender';
            senderEl.textContent = nombre;  // textContent — XSS-safe
            bubble.appendChild(senderEl);
        }

        var textEl = document.createElement('div');
        textEl.className = 'chat-text';
        textEl.textContent = texto;  // textContent — XSS-safe
        bubble.appendChild(textEl);

        var timeEl = document.createElement('div');
        timeEl.className = 'chat-time';
        timeEl.textContent = formatTime(sentAt);
        bubble.appendChild(timeEl);

        msgDiv.appendChild(bubble);
        container.appendChild(msgDiv);
        return msgDiv;
    }

    function showLoadingMessages() {
        var c = document.getElementById('chatMessages');
        if (!c) return;
        c.innerHTML = '<div class="chat-loading"><span class="material-icons chat-spin">autorenew</span></div>';
    }

    function clearMessages() {
        var c = document.getElementById('chatMessages');
        if (c) c.innerHTML = '';
    }

    function showEmptyState() {
        var c = document.getElementById('chatMessages');
        if (!c) return;
        c.innerHTML = [
            '<div class="chat-empty-state">',
            '<span class="material-icons">chat_bubble_outline</span>',
            '<p>Sin mensajes aún.<br>¡Escribe el primero!</p>',
            '</div>'
        ].join('');
    }

    function showErrorState() {
        var c = document.getElementById('chatMessages');
        if (!c) return;
        c.innerHTML = '<div class="chat-error"><span class="material-icons">error_outline</span><p>Error al cargar mensajes.</p></div>';
    }

    function scrollToBottom() {
        var c = document.getElementById('chatMessages');
        if (c) c.scrollTop = c.scrollHeight;
    }

    function updateConnectionStatus(connected) {
        var dot  = document.getElementById('chatStatusDot');
        var text = document.getElementById('chatStatusText');
        if (dot)  dot.className    = 'chat-status-dot ' + (connected ? 'chat-status-on' : 'chat-status-off');
        if (text) text.textContent = connected ? 'En línea' : 'Reconectando...';
    }

    function formatTime(isoStr) {
        try {
            return new Date(isoStr).toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
        } catch (e) { return isoStr || ''; }
    }

    // ==========================================================================
    // INICIO
    // ==========================================================================

    initSignalRHandlers();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', onDomReady);
    } else {
        onDomReady();
    }

})();
