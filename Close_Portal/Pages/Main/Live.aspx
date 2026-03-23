<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.Master" AutoEventWireup="true" CodeBehind="Live.aspx.cs" Inherits="Close_Portal.Pages.Main.Dashboard" %>

<asp:Content ID="PageTitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Dashboard - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/live.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContentArea" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ── PAGE HEADER ─────────────────────────────────────── -->
    <div class="page-header">
        <div class="page-title">
            <h2>
                <span class="material-icons db-title-icon">dashboard</span>
                <span data-translate-key="db.title">Dashboard</span>
            </h2>
            <p data-translate-key="db.subtitle">Estado de locaciones en el turno de guardia activo</p>
        </div>
    </div>

    <!-- ── BANNER GUARDIA ──────────────────────────────────── -->
    <div id="dbGuardBanner" class="db-guard-banner db-guard-loading">
        <span class="material-icons db-spin">autorenew</span>
        <span data-translate-key="common.loading">Cargando...</span>
    </div>

    <!-- ── STATS ───────────────────────────────────────────── -->
    <div class="db-stats-row" id="dbStats">
        <div class="db-stat-card" data-filter="all">
            <div class="db-stat-icon db-icon-total">
                <span class="material-icons">location_on</span>
            </div>
            <div class="db-stat-info">
                <div class="db-stat-value" id="dbStatTotal">—</div>
                <div class="db-stat-label" data-translate-key="db.stat.total">Total</div>
            </div>
        </div>
        <div class="db-stat-card" data-filter="Active">
            <div class="db-stat-icon db-icon-active">
                <span class="material-icons">check_circle</span>
            </div>
            <div class="db-stat-info">
                <div class="db-stat-value" id="dbStatActive">—</div>
                <div class="db-stat-label" data-translate-key="db.stat.active">Activas</div>
            </div>
        </div>
        <div class="db-stat-card" data-filter="Pending">
            <div class="db-stat-icon db-icon-pending">
                <span class="material-icons">pending</span>
            </div>
            <div class="db-stat-info">
                <div class="db-stat-value" id="dbStatPending">—</div>
                <div class="db-stat-label" data-translate-key="db.stat.pending">En validación</div>
            </div>
        </div>
        <div class="db-stat-card" data-filter="Rejected">
            <div class="db-stat-icon db-icon-rejected">
                <span class="material-icons">cancel</span>
            </div>
            <div class="db-stat-info">
                <div class="db-stat-value" id="dbStatRejected">—</div>
                <div class="db-stat-label" data-translate-key="db.stat.rejected">Rechazadas</div>
            </div>
        </div>
        <div class="db-stat-card" data-filter="Approved">
            <div class="db-stat-icon db-icon-approved">
                <span class="material-icons">lock</span>
            </div>
            <div class="db-stat-info">
                <div class="db-stat-value" id="dbStatApproved">—</div>
                <div class="db-stat-label" data-translate-key="db.stat.approved">Cerradas</div>
            </div>
        </div>
    </div>

    <!-- ── PANEL PRINCIPAL ─────────────────────────────────── -->
    <div class="db-panel">

        <!-- Filter tabs -->
        <div class="db-filter-tabs">
            <button type="button" class="db-tab active" data-filter="all"      onclick="filterLocations('all')"      data-translate-key="db.filter.all">Todas</button>
            <button type="button" class="db-tab"        data-filter="Active"   onclick="filterLocations('Active')"   data-translate-key="db.filter.active">Activas</button>
            <button type="button" class="db-tab"        data-filter="Pending"  onclick="filterLocations('Pending')"  data-translate-key="db.filter.pending">En validación</button>
            <button type="button" class="db-tab"        data-filter="Rejected" onclick="filterLocations('Rejected')" data-translate-key="db.filter.rejected">Rechazadas</button>
            <button type="button" class="db-tab"        data-filter="Approved" onclick="filterLocations('Approved')" data-translate-key="db.filter.approved">Cerradas</button>
        </div>

        <!-- Location grid -->
        <div id="dbLocationGrid" class="db-location-grid">
            <div class="db-loading">
                <span class="material-icons db-spin">autorenew</span>
                <span data-translate-key="db.loading">Cargando locaciones...</span>
            </div>
        </div>

    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script>
        window.DashboardPageUrl = '<%= ResolveUrl("~/Pages/Main/Live.aspx") %>';
    </script>
    <script src='<%= ResolveUrl("~/Scripts/live.js") %>'></script>
</asp:Content>
