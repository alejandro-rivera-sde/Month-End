<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true" CodeBehind="Guard.aspx.cs" Inherits="Close_Portal.Pages.Admin.Guard" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Guardia - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/Guard.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ========== PAGE HEADER ========== -->
    <div class="page-header">
        <div class="page-title">
            <h2>
                <span class="material-icons gd-title-icon">security</span>
                <span data-translate-key="gd.title">Gestión de Guardia</span>
            </h2>
            <p data-translate-key="gd.subtitle">Asigna turnos de guardia a los Owners y gestiona alertas activas</p>
        </div>
        <!-- Badge de estado de guardia activa (actualizado por JS) -->
        <div class="gd-header-badge" id="guardStatusBadge">
            <span class="material-icons">shield</span>
            <span id="guardStatusText" data-translate-key="gd.badge.loading">Cargando...</span>
        </div>
    </div>

    <!-- ========== MAIN GRID ========== -->
    <div class="gd-grid">

        <!-- ====== COLUMNA IZQUIERDA: OWNERS ====== -->
        <section class="gd-panel">
            <div class="gd-panel-header">
                <span class="material-icons">group</span>
                <h3 data-translate-key="gd.owners.title">Owners disponibles</h3>
                <span class="gd-count-badge" id="ownersCount">—</span>
            </div>
            <div class="gd-panel-body">
                <!-- Buscador -->
                <div class="gd-search-box">
                    <span class="material-icons">search</span>
                    <input type="text" id="ownerSearch"
                           data-translate-key="gd.owners.search"
                           placeholder="Buscar owner..."
                           oninput="filterOwners()" />
                </div>
                <!-- Lista de owners — llenada por JS -->
                <div class="gd-owners-list" id="ownersList">
                    <div class="gd-loading">
                        <span class="material-icons gd-spin">autorenew</span>
                        <span data-translate-key="gd.owners.loading">Cargando owners...</span>
                    </div>
                </div>
            </div>
        </section>

        <!-- ====== COLUMNA DERECHA: TURNOS ====== -->
        <section class="gd-panel">
            <div class="gd-panel-header">
                <span class="material-icons">calendar_month</span>
                <h3 data-translate-key="gd.schedule.title">Turnos programados</h3>
                <span class="gd-count-badge" id="scheduleCount">—</span>
            </div>
            <div class="gd-panel-body">

                <!-- Estado vacío -->
                <div class="gd-empty-schedule" id="emptySchedule" style="display:none;">
                    <span class="material-icons">event_busy</span>
                    <p data-translate-key="gd.schedule.empty">No hay turnos activos ni programados</p>
                </div>

                <!-- Tabla de turnos — tbody llenado por JS -->
                <div class="gd-table-wrapper" id="scheduleTableWrapper">
                    <table class="gd-table" id="scheduleTable">
                        <thead>
                            <tr>
                                <th data-translate-key="gd.table.owner">Owner</th>
                                <th data-translate-key="gd.table.start">Inicio</th>
                                <th data-translate-key="gd.table.end">Fin</th>
                                <th data-translate-key="gd.table.assigned_by">Asignado por</th>
                                <th data-translate-key="gd.table.status">Estado</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody id="scheduleBody">
                            <!-- Llenado por guard.js -->
                        </tbody>
                    </table>
                </div>

            </div>
        </section>

    </div>
    <!-- /gd-grid -->

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script src='<%= ResolveUrl("~/Scripts/guard.js") %>'></script>
</asp:Content>
