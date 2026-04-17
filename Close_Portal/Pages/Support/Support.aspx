<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true"
         CodeBehind="Support.aspx.cs" Inherits="Close_Portal.Pages.Support.SupportPage" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Soporte IT - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <div class="chat-page-wrapper">

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
                              data-translate-key="support.message_placeholder"
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
    <%-- ChatMode para esta página dedicada — sobreescribe el valor del widget
         antes de que it_chat.js (cargado desde master) se ejecute. --%>
    <script>
        window.ChatWebMethodBase = '<%= ResolveUrl("~/Pages/Support/Support.aspx/") %>';
        window.ChatMode  = 'client';
        window.AgentName = '';
    </script>
</asp:Content>
