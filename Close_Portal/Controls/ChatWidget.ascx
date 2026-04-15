<%@ Control Language="C#" AutoEventWireup="true"
    CodeBehind="ChatWidget.ascx.cs"
    Inherits="Close_Portal.Controls.ChatWidget" %>
<%--
    ChatWidget.ascx — Widget flotante de Soporte IT (interfaz principal)

    Este control se carga desde DashboardLayout.Master y aparece en TODAS las
    páginas del dashboard excepto Support.aspx e ITSupport.aspx.

    Modos (window.ChatMode — resuelto por código de servidor):
      'widget'       → usuario regular: conversa directamente con el agente IT
      'agent-widget' → agente IT:       lista de casos activos + respuesta inline

    El agente IT recibe mensajes en tiempo real desde cualquier página y puede
    responder sin salir de donde está (patrón Intercom / Drift / Zendesk).

    Dependencias cargadas por DashboardLayout.Master (antes de it_chat.js):
      jQuery · jquery.signalR · /signalr/hubs · ITChat.css
--%>

<%-- Configuración de modo — se asigna al cargar la página, antes de it_chat.js --%>
<script>
    window.ChatMode          = '<%=ChatModeJs%>';
    window.ChatWebMethodBase = '<%=WebMethodBaseJs%>';
    window.AgentName         = '<%=AgentNameJs%>';
</script>

<!-- ─── Botón FAB (Floating Action Button) ──────────────────────── -->
<button type="button" class="chat-fab" id="chatWidgetBtn"
        onclick="toggleChatWidget()"
        title="<%=IsAgent ? "IT Support — Conversaciones" : "Soporte IT"%>">
    <span class="material-icons" id="chatFabIcon">
        <%=IsAgent ? "mark_chat_unread" : "support_agent"%>
    </span>
    <span class="chat-fab-badge" id="chatWidgetBadge" style="display:none;">0</span>
</button>

<!-- ─── Panel flotante del chat ─────────────────────────────────── -->
<div id="chatWidgetPanel" class="chat-widget-panel" style="display:none;">

    <!-- Header — siempre visible -->
    <div class="chat-widget-header">
        <div class="chat-widget-header-left">
            <div class="chat-widget-avatar">
                <span class="material-icons">
                    <%=IsAgent ? "mark_chat_unread" : "support_agent"%>
                </span>
            </div>
            <div>
                <div class="chat-widget-title">
                    <%=IsAgent ? "IT Support" : "Soporte IT"%>
                </div>
                <div class="chat-widget-status">
                    <span class="chat-status-dot chat-status-off" id="chatStatusDot"></span>
                    <span id="chatStatusText" class="chat-widget-status-text">Conectando...</span>
                </div>
            </div>
        </div>
        <button type="button" class="chat-widget-close-btn"
                onclick="closeChatWidget()" title="Cerrar">
            <span class="material-icons">close</span>
        </button>
    </div>

<% if (IsAgent) { %>

    <!-- ══ MODO AGENTE-WIDGET ══════════════════════════════════════════════ -->

    <!-- Vista A: Lista de casos activos (predeterminada) -->
    <div id="widgetCaseListView" class="widget-view-cases">
        <div class="widget-cases-header">Conversaciones activas</div>
        <div id="widgetClientList" class="chat-client-list">
            <!-- Llenado por it_chat.js al abrir el widget -->
            <div class="chat-empty-list">Cargando...</div>
        </div>
    </div>

    <!-- Vista B: Conversación individual (visible al seleccionar un caso) -->
    <div id="widgetConvView" class="widget-view-conv" style="display:none;">

        <!-- Sub-header: botón de volver + nombre del cliente -->
        <div class="widget-conv-subheader">
            <button type="button" class="widget-back-btn"
                    onclick="widgetGoBack()" title="Volver a conversaciones">
                <span class="material-icons">arrow_back</span>
            </button>
            <div class="chat-conv-avatar" id="widgetConvAvatar">?</div>
            <span class="widget-conv-client-name" id="widgetClientName">Cliente</span>
        </div>

        <!-- Mensajes de la conversación -->
        <div class="chat-messages" id="chatMessages"></div>

        <!-- Campo de respuesta -->
        <div class="chat-input-bar">
            <div class="chat-input-wrap">
                <textarea id="chatInput"
                          class="chat-input"
                          rows="1"
                          placeholder="Responde al cliente... (Enter para enviar)"
                          maxlength="2000"></textarea>
            </div>
            <button type="button" class="chat-send-btn" id="chatSendBtn" title="Enviar respuesta">
                <span class="material-icons">send</span>
            </button>
        </div>
    </div>

<% } else { %>

    <!-- ══ MODO USUARIO-WIDGET ═════════════════════════════════════════════ -->

    <!-- Conversación única con IT Support -->
    <div class="chat-messages" id="chatMessages">
        <div class="chat-empty-state">
            <span class="material-icons">chat_bubble_outline</span>
            <p>¿Tienes alguna consulta?<br>El equipo de IT te responderá.</p>
        </div>
    </div>

    <div class="chat-input-bar">
        <div class="chat-input-wrap">
            <textarea id="chatInput"
                      class="chat-input"
                      rows="1"
                      placeholder="Escribe tu mensaje... (Enter para enviar)"
                      maxlength="2000"></textarea>
        </div>
        <button type="button" class="chat-send-btn" id="chatSendBtn" title="Enviar">
            <span class="material-icons">send</span>
        </button>
    </div>

<% } %>

</div>
