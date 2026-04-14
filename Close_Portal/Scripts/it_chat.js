// =============================================================================
// it_chat.js — Chat IT Support en tiempo real (SignalR 2.x + jQuery)
//
// Modo de operación:
//   window.ChatMode = 'client'  → Support.aspx   (usuario envía a IT)
//   window.ChatMode = 'agent'   → ITSupport.aspx  (agente gestiona conversaciones)
//
// Dependencias cargadas por DashboardLayout.Master (ANTES de este script):
//   - jQuery 3.x
//   - jquery.signalR-2.4.3.min.js
//   - /signalr/hubs (proxy auto-generado por SignalR)
//
// IMPORTANTE: los handlers de SignalR client-side se registran de forma
// síncrona al cargar este script, antes de que hub.start() sea llamado
// por dashboard_layout.js (en DOMContentLoaded). Esto garantiza que no
// se pierda ningún mensaje entre la carga del DOM y la conexión al hub.
// =============================================================================

(function () {
    'use strict';

    // ── Constantes ────────────────────────────────────────────────
    var HUB_CONNECTED = 1;  // $.connection.hub.state cuando está conectado

    // ── Estado ────────────────────────────────────────────────────
    var chatHub            = null;
    var selectedClienteId  = null;  // solo en modo 'agent'

    // ==========================================================================
    // PASO 3 — Registrar handlers del cliente SignalR
    // Debe ejecutarse síncronamente antes de hub.start()
    // ==========================================================================

    function initSignalRHandlers() {
        if (typeof $.connection === 'undefined' || !$.connection.chatHub) return;
        chatHub = $.connection.chatHub;

        // ── Modo CLIENTE: recibe respuestas del agente IT ─────────
        if (window.ChatMode === 'client') {

            chatHub.client.recibirRespuestaIT = function (data) {
                appendMessage(data.mensaje, data.agenteNombre, data.fechaHora, false);
                scrollToBottom();
            };
        }

        // ── Modo AGENTE: recibe mensajes de clientes y sincroniza ─
        if (window.ChatMode === 'agent') {

            // Llega un mensaje nuevo de un cliente
            chatHub.client.recibirMensajeDeCliente = function (data) {
                // Actualizar o crear entrada en la lista lateral
                refreshClientEntry(
                    data.clienteId,
                    data.clienteNombre,
                    data.mensaje,
                    data.fechaHora,
                    data.clienteId !== selectedClienteId  // mostrar badge solo si no es el chat abierto
                );

                // Si el chat de este cliente está abierto, mostrar el mensaje
                if (data.clienteId === selectedClienteId) {
                    appendMessage(data.mensaje, data.clienteNombre, data.fechaHora, false);
                    scrollToBottom();
                    // Marcar como leído en BD
                    if ($.connection.hub.state === HUB_CONNECTED) {
                        chatHub.server.marcarLeido(selectedClienteId);
                    }
                } else {
                    // Notificación de SO para mensajes de otros clientes
                    if (typeof showOsNotification === 'function') {
                        showOsNotification(
                            'Mensaje de ' + data.clienteNombre,
                            data.mensaje.substring(0, 80),
                            window.AppRoot + 'it-support'
                        );
                    }
                }
            };

            // Sincronización: este u otro agente envió una respuesta
            chatHub.client.mensajeEnviado = function (data) {
                if (data.clienteId === selectedClienteId) {
                    var esPropio = (parseInt(data.agenteId) === parseInt(window.CurrentUserId));
                    if (!esPropio) {
                        // Otro agente respondió mientras yo tenía la conversación abierta
                        appendMessage(data.mensaje, data.agenteNombre, data.fechaHora, true);
                        scrollToBottom();
                    }
                    // Si es propio ya se mostró de forma optimista al enviarlo
                }
            };
        }

        // ── Estado de conexión ────────────────────────────────────
        $.connection.hub.stateChanged(function (change) {
            updateConnectionStatus(change.newState === HUB_CONNECTED);
        });

        // Si ya está conectado al registrar (improbable, pero seguro)
        if ($.connection.hub.state === HUB_CONNECTED) {
            updateConnectionStatus(true);
        }
    }

    // ==========================================================================
    // PASO 3 — DOMContentLoaded: inicializar UI y cargar datos
    // ==========================================================================

    function onDomReady() {
        initSendControls();

        if (window.ChatMode === 'client') {
            loadHistorialCliente();
        } else if (window.ChatMode === 'agent') {
            loadClientList();
        }
    }

    // ==========================================================================
    // ENVÍO DE MENSAJES
    // ==========================================================================

    function initSendControls() {
        var sendBtn  = document.getElementById('chatSendBtn');
        var msgInput = document.getElementById('chatInput');
        if (!sendBtn || !msgInput) return;

        sendBtn.addEventListener('click', sendMessage);

        // Enter envía; Shift+Enter hace salto de línea
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
        msgInput.style.height = '';  // reset altura de textarea

        if (window.ChatMode === 'client') {
            // Mostrar el mensaje de forma optimista antes de la confirmación del servidor
            appendMessage(texto, 'Tú', new Date().toISOString(), true);
            scrollToBottom();

            if ($.connection.hub.state === HUB_CONNECTED) {
                chatHub.server.enviarMensajeAIT(texto);
            }

        } else if (window.ChatMode === 'agent') {
            if (selectedClienteId === null) return;

            appendMessage(texto, window.AgentName || 'IT Support', new Date().toISOString(), true);
            scrollToBottom();

            if ($.connection.hub.state === HUB_CONNECTED) {
                chatHub.server.enviarMensajeACliente(selectedClienteId, texto);
            }
        }
    }

    // ==========================================================================
    // HISTORIAL — MODO CLIENTE
    // ==========================================================================

    function loadHistorialCliente() {
        showLoadingMessages();
        $.ajax({
            type:        'POST',
            url:         window.PageWebMethodBase + 'GetHistorial',
            data:        '{}',
            contentType: 'application/json; charset=utf-8',
            dataType:    'json',
            success: function (resp) {
                var d = resp.d !== undefined ? resp.d : resp;
                clearMessages();
                if (d && d.success && d.mensajes && d.mensajes.length > 0) {
                    d.mensajes.forEach(function (m) {
                        appendMessage(
                            m.mensaje,
                            m.esCliente ? 'Tú' : m.emisorNombre,
                            m.fechaHora,
                            m.esCliente
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
            url:         window.PageWebMethodBase + 'GetClientes',
            data:        '{}',
            contentType: 'application/json; charset=utf-8',
            dataType:    'json',
            success: function (resp) {
                var d = resp.d !== undefined ? resp.d : resp;
                if (d && d.success) renderClientList(d.clientes || []);
            },
            error: function () {}
        });
    }

    function renderClientList(clientes) {
        var list = document.getElementById('clientList');
        if (!list) return;
        list.innerHTML = '';

        if (clientes.length === 0) {
            list.innerHTML = '<div class="chat-empty-list">Sin conversaciones activas</div>';
            return;
        }
        clientes.forEach(function (c) {
            list.appendChild(buildClientItem(c));
        });
    }

    function buildClientItem(c) {
        var item = document.createElement('div');
        item.className = 'client-item' + (c.clienteId === selectedClienteId ? ' active' : '');
        item.dataset.clienteId = c.clienteId;

        // Avatar (inicial del nombre)
        var avatar = document.createElement('div');
        avatar.className = 'client-avatar';
        avatar.textContent = (c.clienteNombre || 'U').charAt(0).toUpperCase();

        // Info
        var info = document.createElement('div');
        info.className = 'client-info';

        var nameRow = document.createElement('div');
        nameRow.className = 'client-name-row';

        var name = document.createElement('span');
        name.className = 'client-name';
        name.textContent = c.clienteNombre || 'Usuario';
        nameRow.appendChild(name);

        if (c.mensajesNoLeidos > 0) {
            var badge = document.createElement('span');
            badge.className = 'client-badge';
            badge.textContent = c.mensajesNoLeidos > 99 ? '99+' : c.mensajesNoLeidos;
            nameRow.appendChild(badge);
        }

        var preview = document.createElement('div');
        preview.className = 'client-preview';
        preview.textContent = (c.ultimoMensaje || '').substring(0, 50);

        info.appendChild(nameRow);
        info.appendChild(preview);
        item.appendChild(avatar);
        item.appendChild(info);

        item.addEventListener('click', function () {
            selectClient(c.clienteId, c.clienteNombre);
        });
        return item;
    }

    // Actualiza (o crea) la entrada en la lista lateral cuando llega un mensaje en tiempo real
    function refreshClientEntry(clienteId, clienteNombre, mensaje, fechaHora, addBadge) {
        var list     = document.getElementById('clientList');
        var existing = list ? list.querySelector('[data-cliente-id="' + clienteId + '"]') : null;

        if (existing) {
            var preview = existing.querySelector('.client-preview');
            if (preview) preview.textContent = mensaje.substring(0, 50);

            if (addBadge) {
                var badge = existing.querySelector('.client-badge');
                if (badge) {
                    var count = parseInt(badge.textContent) || 0;
                    badge.textContent = count + 1 > 99 ? '99+' : String(count + 1);
                } else {
                    var nameRow = existing.querySelector('.client-name-row');
                    if (nameRow) {
                        var newBadge = document.createElement('span');
                        newBadge.className = 'client-badge';
                        newBadge.textContent = '1';
                        nameRow.appendChild(newBadge);
                    }
                }
            }
            // Mover al principio de la lista (conversación más reciente arriba)
            if (list) list.insertBefore(existing, list.firstChild);

        } else {
            // Cliente nuevo que no estaba en la lista
            var newItem = buildClientItem({
                clienteId:        clienteId,
                clienteNombre:    clienteNombre,
                ultimoMensaje:    mensaje,
                mensajesNoLeidos: addBadge ? 1 : 0
            });
            if (list) {
                var emptyMsg = list.querySelector('.chat-empty-list');
                if (emptyMsg) emptyMsg.remove();
                list.insertBefore(newItem, list.firstChild);
            }
        }
    }

    // Seleccionar un cliente para ver su conversación
    function selectClient(clienteId, clienteNombre) {
        selectedClienteId = clienteId;

        // Marcar activo en la lista
        document.querySelectorAll('.client-item').forEach(function (item) {
            item.classList.toggle('active', parseInt(item.dataset.clienteId) === clienteId);
        });

        // Quitar badge del cliente seleccionado
        var activeItem = document.querySelector('[data-cliente-id="' + clienteId + '"]');
        if (activeItem) {
            var badge = activeItem.querySelector('.client-badge');
            if (badge) badge.remove();
        }

        // Mostrar panel de conversación
        var chatPanel    = document.getElementById('chatPanel');
        var noneSelected = document.getElementById('noneSelected');
        if (chatPanel)    chatPanel.style.display = 'flex';
        if (noneSelected) noneSelected.style.display = 'none';

        // Actualizar header de la conversación
        var clientNameEl  = document.getElementById('chatClientName');
        var convAvatarEl  = document.getElementById('convAvatar');
        if (clientNameEl) clientNameEl.textContent = clienteNombre;
        if (convAvatarEl) convAvatarEl.textContent = clienteNombre.charAt(0).toUpperCase();

        // Marcar mensajes como leídos en BD
        if ($.connection.hub.state === HUB_CONNECTED) {
            chatHub.server.marcarLeido(clienteId);
        }

        // Cargar historial del cliente seleccionado
        loadHistorialAgente(clienteId);
    }

    function loadHistorialAgente(clienteId) {
        clearMessages();
        showLoadingMessages();
        $.ajax({
            type:        'POST',
            url:         window.PageWebMethodBase + 'GetHistorial',
            data:        JSON.stringify({ clienteId: clienteId }),
            contentType: 'application/json; charset=utf-8',
            dataType:    'json',
            success: function (resp) {
                var d = resp.d !== undefined ? resp.d : resp;
                clearMessages();
                if (d && d.success && d.mensajes && d.mensajes.length > 0) {
                    d.mensajes.forEach(function (m) {
                        // esCliente=true → mensaje del usuario; false → respuesta del agente
                        appendMessage(
                            m.mensaje,
                            m.emisorNombre,
                            m.fechaHora,
                            !m.esCliente  // "propio" para el agente = mensajes de agente
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
    // HELPERS DE UI
    // ==========================================================================

    function appendMessage(texto, nombre, fechaHora, esPropio) {
        var container = document.getElementById('chatMessages');
        if (!container) return;

        var msgDiv = document.createElement('div');
        msgDiv.className = 'chat-msg ' + (esPropio ? 'chat-msg-own' : 'chat-msg-other');

        var bubble = document.createElement('div');
        bubble.className = 'chat-bubble';

        // Nombre del emisor (solo en mensajes del otro lado)
        if (!esPropio) {
            var senderEl = document.createElement('div');
            senderEl.className = 'chat-sender';
            senderEl.textContent = nombre;  // textContent previene XSS
            bubble.appendChild(senderEl);
        }

        // Texto del mensaje — textContent para XSS-safe rendering
        var textEl = document.createElement('div');
        textEl.className = 'chat-text';
        textEl.textContent = texto;
        bubble.appendChild(textEl);

        // Timestamp
        var timeEl = document.createElement('div');
        timeEl.className = 'chat-time';
        timeEl.textContent = formatTime(fechaHora);
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
        if (dot)  dot.className  = 'chat-status-dot ' + (connected ? 'chat-status-on' : 'chat-status-off');
        if (text) text.textContent = connected ? 'Conectado' : 'Reconectando...';
    }

    function formatTime(isoStr) {
        try {
            var d = new Date(isoStr);
            return d.toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
        } catch (e) { return isoStr || ''; }
    }

    // ==========================================================================
    // INICIO
    // Los handlers de SignalR se registran de forma síncrona al cargar el script,
    // antes de que hub.start() sea invocado por dashboard_layout.js.
    // ==========================================================================

    initSignalRHandlers();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', onDomReady);
    } else {
        onDomReady();
    }

})();
