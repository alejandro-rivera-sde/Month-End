<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true" CodeBehind="WarehouseManagement.aspx.cs" Inherits="Close_Portal.Pages.Admin.WarehouseManagement" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Gestión de Bodegas - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/WarehouseManagement.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ========== PAGE HEADER ========== -->
    <div class="page-header">
        <div class="page-title">
            <h2 data-translate-key="wm.title">Gestión de Bodegas</h2>
            <p data-translate-key="wm.subtitle">Administra las locaciones y sus asignaciones</p>
        </div>
        <button type="button" class="btn-new" onclick="openModalNew()">
            <span class="material-icons">add_business</span>
            <span data-translate-key="wm.btn_new">Nueva Locación</span>
        </button>
    </div>

    <!-- ========== STATS ROW ========== -->
    <div class="stats-row">
        <div class="stat-card">
            <div class="stat-icon purple">
                <span class="material-icons">warehouse</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="wm.stat.total">Total Locaciones</p>
                <h3><asp:Literal ID="litTotal" runat="server">0</asp:Literal></h3>
            </div>
        </div>
        <div class="stat-card">
            <div class="stat-icon green">
                <span class="material-icons">check_circle</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="wm.stat.active">Activas</p>
                <h3><asp:Literal ID="litActive" runat="server">0</asp:Literal></h3>
            </div>
        </div>
        <div class="stat-card">
            <div class="stat-icon red">
                <span class="material-icons">do_not_disturb_on</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="wm.stat.inactive">Inactivas</p>
                <h3><asp:Literal ID="litInactive" runat="server">0</asp:Literal></h3>
            </div>
        </div>
        <div class="stat-card">
            <div class="stat-icon amber">
                <span class="material-icons">people</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="wm.stat.users">Usuarios Asignados</p>
                <h3><asp:Literal ID="litUsersAssigned" runat="server">0</asp:Literal></h3>
            </div>
        </div>
    </div>

    <!-- ========== TOOLBAR ========== -->
    <div class="toolbar">
        <div class="search-box">
            <span class="material-icons">search</span>
            <input type="text" id="searchInput"
                   placeholder="Buscar locación..."
                   data-translate-key="wm.search.placeholder"
                   oninput="filterTable()" />
        </div>
        <select class="filter-select" id="filterStatus" onchange="filterTable()">
            <option value="" data-translate-key="wm.filter.all_status">Todos los estados</option>
            <option value="Activa"   data-translate-key="wm.status.active">Activa</option>
            <option value="Inactiva" data-translate-key="wm.status.inactive">Inactiva</option>
        </select>
    </div>

    <!-- ========== TABLE ========== -->
    <div class="table-wrapper">
        <table id="warehouseTable">
            <thead>
                <tr>
                    <th data-translate-key="wm.table.location">Locación</th>
                    <th data-translate-key="wm.table.users">Usuarios</th>
                    <th data-translate-key="wm.table.status">Estado</th>
                    <th data-translate-key="wm.table.actions">Acciones</th>
                </tr>
            </thead>
            <tbody>
                <asp:Repeater ID="rptLocations" runat="server">
                    <ItemTemplate>
                        <tr data-status="<%# Eval("StatusLabel") %>">
                            <td>
                                <div class="location-cell">
                                    <div class="location-icon">
                                        <span class="material-icons">place</span>
                                    </div>
                                    <div class="location-info">
                                        <div class="location-name"><%# Eval("LocationName") %></div>
                                        <div class="location-id-tag">#<%# Eval("LocationId") %></div>
                                    </div>
                                </div>
                            </td>
                            <td>
                                <div class="user-count">
                                    <span class="material-icons">person</span>
                                    <%# Eval("UserCount") %>
                                </div>
                            </td>
                            <td>
                                <span class="badge badge-<%# Eval("StatusBadge") %>"><%# Eval("StatusLabel") %></span>
                            </td>
                            <td>
                                <div class="actions">
                                    <button type="button" class="btn-icon edit"
                                            onclick="openModalEdit(<%# Eval("LocationId") %>)"
                                            title="Editar locación">
                                        <span class="material-icons">edit</span>
                                    </button>
                                    <button type="button"
                                            class="btn-icon <%# (bool)Eval("Active") ? "deactivate" : "activate" %>"
                                            onclick="confirmToggleActive(<%# Eval("LocationId") %>, <%# Eval("Active").ToString().ToLower() %>, '<%# Eval("LocationName") %>')"
                                            title='<%# (bool)Eval("Active") ? "Desactivar" : "Activar" %>'>
                                        <span class="material-icons"><%# (bool)Eval("Active") ? "toggle_on" : "toggle_off" %></span>
                                    </button>
                                </div>
                            </td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </tbody>
        </table>

        <asp:Panel ID="pnlEmpty" runat="server" Visible="false" CssClass="empty-state">
            <span class="material-icons">warehouse</span>
            <p data-translate-key="wm.empty">No se encontraron locaciones</p>
        </asp:Panel>
    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script>window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/Admin/WarehouseManagement.aspx/") %>';</script>
    <script src='<%= ResolveUrl("~/Scripts/warehouse_management.js") %>'></script>
</asp:Content>
