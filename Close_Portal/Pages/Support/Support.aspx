<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true"
         CodeBehind="Support.aspx.cs" Inherits="Close_Portal.Pages.Support.SupportPage" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Soporte IT - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/ITChat.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <div class="chat-page-wrapper">

        <!-- ========== PAGE HEADER ========== -->
        <div class="chat-page-header">
            <div class="chat-page-title">
                <span class="material-icons">support_agent</span>
                <h2>Soporte IT</h2>
            </div>
            <!-- Indicador de conexión en tiempo real -->
            <div class="chat-connection-status">
                <span class="chat-status-dot chat-status-off" id="chatStatusDot"></span>
                <span id="chatStatusText">Conectando...</span>
            </div>
        </div>

        <!-- ========== PANEL DE CHAT ========== -->
        <!--
            El chat está embebido en esta misma página ASPX.
            No hay popups ni ventanas externas.
        -->
        <div class="chat-client-wrapper">

            <!-- Área de mensajes (scrollable) -->
            <div class="chat-messages" id="chatMessages">
                <!-- Llenado por it_chat.js vía WebMethod + SignalR -->
                <div class="chat-loading">
                    <span class="material-icons chat-spin">autorenew</span>
                </div>
            </div>

            <!-- Barra de entrada -->
            <div class="chat-input-bar">
                <div class="chat-input-wrap">
                    <textarea id="chatInput"
                              class="chat-input"
                              rows="1"
                              placeholder="Escribe tu mensaje... (Enter para enviar, Shift+Enter para salto de línea)"
                              maxlength="2000"></textarea>
                </div>
                <button type="button" class="chat-send-btn" id="chatSendBtn" title="Enviar mensaje">
                    <span class="material-icons">send</span>
                </button>
            </div>

        </div>
    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <!--
        PageWebMethodBase: URL base para llamadas AJAX a WebMethods de esta página.
        ChatMode: indica al script que opera en modo cliente (usuario → IT).
        AgentName: no usado en modo cliente, pero requerido por el script compartido.

        SEGURIDAD: el usuario real se lee siempre desde Session en el servidor.
        Estas variables solo controlan la UI.
    -->
    <script>
        window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/Support/Support.aspx/") %>';
        window.ChatMode  = 'client';
        window.AgentName = '';  // no aplica en modo cliente
    </script>
    <script src='<%= ResolveUrl("~/Scripts/it_chat.js") %>'></script>
</asp:Content>
