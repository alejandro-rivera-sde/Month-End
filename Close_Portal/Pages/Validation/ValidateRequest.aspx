<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.Master" AutoEventWireup="true" CodeBehind="ValidateRequest.aspx.cs" Inherits="Close_Portal.Pages.ValidateRequest" %>
<%@ Register Src="~/Controls/ChatWidget.ascx" TagPrefix="uc" TagName="ChatWidget" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Validar Solicitudes - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/ValidateRequest.css") %>' rel="stylesheet" type="text/css" />
    <link href='<%= ResolveUrl("~/Styles/ITChat.css") %>'         rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <div class="page-header">
        <button type="button" class="vr-btn-closed" onclick="openClosedPanel()">
            <span class="material-icons">lock</span>
            <span data-translate-key="vr.btn.closed">Solicitudes Cerradas</span>
        </button>
    </div>

    <!-- ── PANEL PRINCIPAL ── -->
    <div class="vr-panel">

        <!-- Header con filtros y contador -->
        <div class="vr-panel-header">
            <span class="material-icons">inbox</span>
            <h3 data-translate-key="vr.panel.title">Solicitudes de cierre</h3>
            <span class="vr-count-badge" id="vrCount">—</span>
            <div class="vr-header-actions">
                <select id="vrLocationFilter" class="vr-filter-select" onchange="applyFilters()">
                    <option value="" data-translate-key="vr.filter.all_locations">Todas las locaciones</option>
                </select>
            </div>
        </div>

        <!-- Tabs de estado -->
        <div class="vr-filter-tabs">
            <button type="button" class="vr-tab active" data-filter="Pending" onclick="setStatusFilter('Pending')">
                <span class="material-icons">schedule</span>
                <span data-translate-key="vr.tab.pending">Pendientes</span>
                <span class="vr-tab-count" id="vrCountPending">0</span>
            </button>
            <button type="button" class="vr-tab" data-filter="Approved" onclick="setStatusFilter('Approved')">
                <span class="material-icons">check_circle</span>
                <span data-translate-key="vr.tab.approved">Aprobadas</span>
                <span class="vr-tab-count" id="vrCountApproved">0</span>
            </button>
            <button type="button" class="vr-tab" data-filter="Rejected" onclick="setStatusFilter('Rejected')">
                <span class="material-icons">cancel</span>
                <span data-translate-key="vr.tab.rejected">Rechazadas</span>
                <span class="vr-tab-count" id="vrCountRejected">0</span>
            </button>
        </div>

        <!-- Lista de solicitudes -->
        <div id="vrRequestList">
            <div class="vr-loading">
                <span class="material-icons vr-spin">autorenew</span>
                <span data-translate-key="vr.loading">Cargando solicitudes...</span>
            </div>
        </div>

    </div>

    <!-- Widget de Soporte IT — aparece como botón flotante en esquina inferior derecha -->
    <uc:ChatWidget ID="ChatWidget1" runat="server" />

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script>window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/Validation/ValidateRequest.aspx/") %>';</script>
    <script src='<%= ResolveUrl("~/Scripts/validate_request.js") %>'></script>
    <!-- Chat widget — ChatWebMethodBase apunta al WebMethod del chat (Support.aspx),
         distinto del PageWebMethodBase de esta página. -->
    <script>
        window.ChatMode          = 'widget';
        window.ChatWebMethodBase = '<%= ResolveUrl("~/Pages/Support/Support.aspx/") %>';
    </script>
    <script src='<%= ResolveUrl("~/Scripts/it_chat.js") %>'></script>
</asp:Content>
