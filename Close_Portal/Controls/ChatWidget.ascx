<%@ Control Language="C#" AutoEventWireup="true"
    CodeBehind="ChatWidget.ascx.cs"
    Inherits="Close_Portal.Controls.ChatWidget" %>
<%--
    ChatWidget.ascx — Widget flotante de Soporte IT

    CÓMO INCLUIR EN UNA PÁGINA:
      1. Registrar el control al inicio del .aspx:
            <%@ Register Src="~/Controls/ChatWidget.ascx"
                         TagPrefix="uc" TagName="ChatWidget" %>

      2. Colocar el control al final de DashboardContent:
            <uc:ChatWidget ID="ChatWidget1" runat="server" />

      3. En AdditionalCSS, añadir el CSS del chat:
            <link href='<%= ResolveUrl("~/Styles/ITChat.css") %>'
                  rel="stylesheet" type="text/css" />

      4. En AdditionalScripts, configurar e iniciar el widget:
            <script>
                window.ChatMode          = 'widget';
                window.ChatWebMethodBase = '<%= ResolveUrl("~/Pages/Support/Support.aspx/") %>';
            </script>
            <script src='<%= ResolveUrl("~/Scripts/it_chat.js") %>'></script>

    NOTA DE ARQUITECTURA:
      - Este control solo contiene el marcado HTML.
      - Los scripts se cargan desde AdditionalScripts (dentro de ScriptsContent),
        que garantiza que jQuery y SignalR ya están disponibles.
      - El control se posiciona con position:fixed en la esquina inferior derecha,
        por lo que no interfiere con el layout de la página anfitriona.
--%>

<!-- ─── Botón FAB (Floating Action Button) ──────────────────────── -->
<button type="button" class="chat-fab" id="chatWidgetBtn"
        onclick="toggleChatWidget()" title="Soporte IT">
    <span class="material-icons" id="chatFabIcon">support_agent</span>
    <span class="chat-fab-badge" id="chatWidgetBadge" style="display:none;">0</span>
</button>

<!-- ─── Panel flotante del chat ─────────────────────────────────── -->
<!--
    display:none al inicio. it_chat.js lo abre/cierra con toggleChatWidget() / closeChatWidget().
    La animación CSS widgetSlideIn se aplica al mostrarse vía JS.
-->
<div id="chatWidgetPanel" class="chat-widget-panel" style="display:none;">

    <!-- Header con accent color -->
    <div class="chat-widget-header">
        <div class="chat-widget-header-left">
            <div class="chat-widget-avatar">
                <span class="material-icons">support_agent</span>
            </div>
            <div>
                <div class="chat-widget-title">Soporte IT</div>
                <!-- Indicador de conexión SignalR -->
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

    <!-- Área de mensajes (scrollable, llenada por it_chat.js) -->
    <div class="chat-messages" id="chatMessages">
        <!-- Estado inicial: invitar al usuario a escribir -->
        <div class="chat-empty-state">
            <span class="material-icons">chat_bubble_outline</span>
            <p>¿Tienes alguna consulta?<br>El equipo de IT te responderá.</p>
        </div>
    </div>

    <!-- Barra de entrada -->
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

</div>
