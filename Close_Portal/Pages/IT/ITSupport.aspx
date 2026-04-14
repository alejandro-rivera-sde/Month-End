<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true"
         CodeBehind="ITSupport.aspx.cs" Inherits="Close_Portal.Pages.IT.ITSupportPage" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    IT Support Chat - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/ITChat.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <div class="chat-page-wrapper">

        <!-- ========== PAGE HEADER ========== -->
        <div class="chat-page-header">
            <div class="chat-page-title">
                <span class="material-icons">headset_mic</span>
                <h2>IT Support Chat</h2>
            </div>
            <!-- Indicador de conexión -->
            <div class="chat-connection-status">
                <span class="chat-status-dot chat-status-off" id="chatStatusDot"></span>
                <span id="chatStatusText">Conectando...</span>
            </div>
        </div>

        <!-- ========== PANEL PRINCIPAL (dos columnas) ========== -->
        <!--
            Toda la UI está embebida en esta página ASPX, sin popups ni iframes.
            Columna izquierda: lista de clientes con mensajes pendientes.
            Columna derecha:  conversación seleccionada + campo de respuesta.
        -->
        <div class="chat-agent-wrapper">

            <!-- ── COLUMNA IZQUIERDA: lista de clientes ── -->
            <div class="chat-client-list-panel">
                <div class="chat-list-header">Conversaciones</div>
                <div class="chat-client-list" id="clientList">
                    <!-- Llenado por it_chat.js vía WebMethod + SignalR -->
                    <div class="chat-empty-list">Cargando...</div>
                </div>
            </div>

            <!-- ── COLUMNA DERECHA: conversación activa ── -->
            <div class="chat-conversation-panel">

                <!-- Estado: ningún cliente seleccionado -->
                <div class="chat-none-selected" id="noneSelected">
                    <span class="material-icons">forum</span>
                    <p>Selecciona una conversación<br>para comenzar a responder</p>
                </div>

                <!-- Panel de chat (oculto hasta seleccionar un cliente) -->
                <div id="chatPanel" style="display:none; flex-direction:column; flex:1; min-height:0;">

                    <!-- Header de la conversación con el cliente seleccionado -->
                    <div class="chat-conv-header">
                        <div class="chat-conv-avatar" id="convAvatar">?</div>
                        <span class="chat-conv-client-name" id="chatClientName">Cliente</span>
                    </div>

                    <!-- Mensajes -->
                    <div class="chat-messages" id="chatMessages">
                        <!-- Llenado por it_chat.js -->
                    </div>

                    <!-- Campo de respuesta del agente -->
                    <div class="chat-input-bar">
                        <div class="chat-input-wrap">
                            <textarea id="chatInput"
                                      class="chat-input"
                                      rows="1"
                                      placeholder="Responde al cliente... (Enter para enviar, Shift+Enter para nueva línea)"
                                      maxlength="2000"></textarea>
                        </div>
                        <button type="button" class="chat-send-btn" id="chatSendBtn" title="Enviar respuesta">
                            <span class="material-icons">send</span>
                        </button>
                    </div>

                </div>
            </div>

        </div>
    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <!--
        PageWebMethodBase: base para WebMethods de esta página.
        ChatMode: 'agent' activa la lógica de lista de clientes y respuesta en it_chat.js.
        AgentName: nombre del agente actual para mostrar en mensajes propios.

        SEGURIDAD: el RoleId y la autorización se validan en ITSupportPage.cs.
        Estas variables solo controlan la UI.
    -->
    <script>
        window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/IT/ITSupport.aspx/") %>';
        window.ChatMode  = 'agent';
        window.AgentName = '<%= System.Web.HttpUtility.JavaScriptStringEncode(Session["FullName"]?.ToString() ?? "IT Support") %>';
    </script>
    <script src='<%= ResolveUrl("~/Scripts/it_chat.js") %>'></script>
</asp:Content>
