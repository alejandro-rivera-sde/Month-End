// =============================================================================
// it_chat.js — Chat IT Support en tiempo real (SignalR 2.x + jQuery)
//
// Modos de operación (window.ChatMode):
//   'client'  → Support.aspx    — página dedicada, el usuario chatea con IT
//   'agent'   → ITSupport.aspx  — panel del agente, gestiona conversaciones
//   'widget'  → ChatWidget.ascx — widget flotante embebido en páginas existentes
//
// Variable de configuración:
//   window.ChatWebMethodBase — URL base para WebMethods del chat
//     Ejemplo: ResolveUrl("~/Pages/Support/Support.aspx/")
//   window.AgentName — nombre del agente (solo modo 'agent')
//
// Dependencias (cargadas por DashboardLayout.Master ANTES que este script):
//   - jQuery 3.x
//   - jquery.signalR-2.4.3.min.js
//   - /signalr/hubs  (proxy auto-generado)
//
// IMPORTANTE: initSignalRHandlers() se ejecuta de forma síncrona al cargar
// el script, antes de que hub.start() sea invocado por dashboard_layout.js.
// Esto garantiza que ningún mensaje se pierda entre la carga y la conexión.
// =============================================================================

(function () {
    'use strict';

    var HUB_CONNECTED    = 1;   // $.connection.hub.state cuando está activo
    var chatHub          = null;
    var selectedClientId = null; // solo en modo 'agent'

    // ── Estado del widget (solo modo 'widget') ────────────────────
    var widgetOpen   = false;
    var widgetLoaded = false;  // historial cargado al menos una vez
    var widgetUnread = 0;

    // ==========================================================================
    // FUNCIONES PÚBLICAS DEL WIDGET
    // Expuestas globalmente porque los onclick del .ascx las llaman directamente.
    // ==========================================================================

    window.toggleChatWidget = function () {
        if (widgetOpen) { closeChatWidgetInternal(); }
        else            { openChatWidgetInternal();  }
    };

    window.closeChatWidget = function () {
        closeChatWidgetInternal();
    };

    function openChatWidgetInternal() {
        widgetOpen = true;
        var panel = document.getElementById('chatWidgetPanel');
        var icon  = document.getElementById('chatFabIcon');
        if (panel) {
            panel.style.display = 'flex';
            // Forzar re-trigger de la animación CSS
            panel.classList.remove('widget-anim');
            void panel.offsetWidth;
            panel.classList.add('widget-anim');
        }
        if (icon) icon.textContent = 'close';

        clearWidgetBadge();

        // Historial: carga diferida — solo la primera vez que se abre
        if (!widgetLoaded) {
            widgetLoaded = true;
            loadHistorialCliente();
        } else {
            scrollToBottom();
        }
    }

    function closeChatWidgetInternal() {
        widgetOpen = false;
        var panel = document.getElementById('chatWidgetPanel');
        var icon  = document.getElementById('chatFabIcon');
        if (panel) panel.style.display = 'none';
        if (icon)  icon.textContent = 'support_agent';
    }

    function clearWidgetBadge() {
        widgetUnread = 0;
        var badge = document.getElementById('chatWidgetBadge');
        if (badge) badge.style.display = 'none';
    }

    function addWidgetUnread() {
        widgetUnread++;
        var badge = document.getElementById('chatWidgetBadge');
        if (badge) {
            badge.textContent = widgetUnread > 99 ? '99+' : String(widgetUnread);
            badge.style.display = 'block';
        }
    }

    // ==========================================================================
    // PASO 3 — Registrar handlers del cliente SignalR
    // Se ejecuta síncronamente, antes de hub.start()
    // ==========================================================================

    function initSignalRHandlers() {
        if (typeof $.connection === 'undefined' || !$.connection.chatHub) return;
        chatHub = $.connection.chatHub;

        // ── Modo CLIENTE o WIDGET: recibe respuestas del agente IT ───

        if (window.ChatMode === 'client') {
            chatHub.client.recibirRespuestaIT = function (data) {
                appendMessage(data.message, data.senderName, data.sentAt, false);
                scrollToBottom();
            };
        }

        if (window.ChatMode === 'widget') {
            chatHub.client.recibirRespuestaIT = function (data) {
                if (widgetOpen) {
                    appendMessage(data.message, data.senderName, data.sentAt, false);
                    scrollToBottom();
                } else {
                    // Widget cerrado: mostrar badge en el botón FAB
                    addWidgetUnread();
                }
            };
        }

        // ── Modo AGENTE: recibe mensajes de clientes y sincroniza ────

        if (window.ChatMode === 'agent') {

            chatHub.client.recibirMensajeDeCliente = function (data) {
                refreshClientEntry(
                    data.clientId,
                    data.clientName,
                    data.message,
                    data.sentAt,
                    data.clientId !== selectedClientId
                );

                if (data.clientId === selectedClientId) {
                    appendMessage(data.message, data.clientName, data.sentAt, false);
                    scrollToBottom();
                    if ($.connection.hub.state === HUB_CONNECTED) {
                        chatHub.server.marcarLeido(selectedClientId);
                    }
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
                if (data.clientId === selectedClientId) {
                    var esPropio = (parseInt(data.senderId) === parseInt(window.CurrentUserId));
                    if (!esPropio) {
                        appendMessage(data.message, data.senderName, data.sentAt, true);
                        scrollToBottom();
                    }
                }
            };
        }

        // ── Estado de conexión + garantía de membresía de grupo ───────
        // chatHub.server.joinGroups() es el mecanismo principal para unirse
        // a "it-agents" / "user-{id}", porque OnConnected() en el servidor
        // puede ejecutarse antes de que la sesión OWIN esté disponible.

        $.connection.hub.stateChanged(function (change) {
            if (change.newState === HUB_CONNECTED) {
                chatHub.server.joinGroups();
            }
            updateConnectionStatus(change.newState === HUB_CONNECTED);
        });

        if ($.connection.hub.state === HUB_CONNECTED) {
            chatHub.server.joinGroups();
            updateConnectionStatus(true);
        }
    }

    // ==========================================================================
    // DOMContentLoaded — inicializar UI
    // ==========================================================================

    function onDomReady() {
        initSendControls();

        if (window.ChatMode === 'client') {
            loadHistorialCliente();
        } else if (window.ChatMode === 'agent') {
            loadClientList();
        }
        // En modo 'widget' la carga es diferida (openChatWidgetInternal)
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
        msgInput.value = '';

        if (window.ChatMode === 'client' || window.ChatMode === 'widget') {
            appendMessage(texto, 'Tú', new Date().toISOString(), true);
            scrollToBottom();
            if ($.connection.hub.state === HUB_CONNECTED) {
                chatHub.server.enviarMensajeAIT(texto);
            }

        } else if (window.ChatMode === 'agent') {
            if (selectedClientId === null) return;
            appendMessage(texto, window.AgentName || 'IT Support', new Date().toISOString(), true);
            scrollToBottom();
            if ($.connection.hub.state === HUB_CONNECTED) {
                chatHub.server.enviarMensajeACliente(selectedClientId, texto);
            }
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
    // LISTA DE CLIENTES — MODO AGENTE
    // ==========================================================================

    function loadClientList() {
        $.ajax({
            type:        'POST',
            url:         window.ChatWebMethodBase + 'GetClientes',
            data:        '{}',
            contentType: 'application/json; charset=utf-8',
            dataType:    'json',
            success: function (resp) {
                var d = resp.d !== undefined ? resp.d : resp;
                if (d && d.success) renderClientList(d.clients || []);
            },
            error: function () {}
        });
    }

    function renderClientList(clients) {
        var list = document.getElementById('clientList');
        if (!list) return;
        list.innerHTML = '';
        if (clients.length === 0) {
            list.innerHTML = '<div class="chat-empty-list">Sin conversaciones activas</div>';
            return;
        }
        clients.forEach(function (c) { list.appendChild(buildClientItem(c)); });
    }

    function buildClientItem(c) {
        var item = document.createElement('div');
        item.className = 'client-item' + (c.clientId === selectedClientId ? ' active' : '');
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
        item.addEventListener('click', function () { selectClient(c.clientId, c.clientName); });
        return item;
    }

    function refreshClientEntry(clientId, clientName, message, sentAt, addBadge) {
        var list     = document.getElementById('clientList');
        var existing = list ? list.querySelector('[data-client-id="' + clientId + '"]') : null;

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
            var newItem = buildClientItem({
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

    function selectClient(clientId, clientName) {
        selectedClientId = clientId;

        document.querySelectorAll('.client-item').forEach(function (item) {
            item.classList.toggle('active', parseInt(item.dataset.clientId) === clientId);
        });

        var activeItem = document.querySelector('[data-client-id="' + clientId + '"]');
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

        if ($.connection.hub.state === HUB_CONNECTED) {
            chatHub.server.marcarLeido(clientId);
        }
        loadHistorialAgente(clientId);
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
            senderEl.textContent = nombre;  // textContent — inmune a XSS
            bubble.appendChild(senderEl);
        }

        var textEl = document.createElement('div');
        textEl.className = 'chat-text';
        textEl.textContent = texto;  // textContent — inmune a XSS
        bubble.appendChild(textEl);

        var timeEl = document.createElement('div');
        timeEl.className = 'chat-time';
        timeEl.textContent = formatTime(sentAt);
        bubble.appendChild(timeEl);

        msgDiv.appendChild(bubble);
        container.appendChild(msgDiv);
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
        if (dot)  dot.className   = 'chat-status-dot ' + (connected ? 'chat-status-on' : 'chat-status-off');
        if (text) text.textContent = connected ? 'En línea' : 'Reconectando...';
    }

    function formatTime(isoStr) {
        try {
            return new Date(isoStr).toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
        } catch (e) { return isoStr || ''; }
    }

    // ==========================================================================
    // INICIO — síncrono al cargar el script
    // ==========================================================================

    initSignalRHandlers();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', onDomReady);
    } else {
        onDomReady();
    }

})();
