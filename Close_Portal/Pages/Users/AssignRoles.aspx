<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.Master" AutoEventWireup="true" CodeBehind="AssignRoles.aspx.cs" Inherits="Close_Portal.Pages.AssignRoles" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Asignar Roles - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/AssignRoles.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <div class="page-header">
        <div class="page-title">
            <h2>
                <span class="material-icons ar-title-icon">admin_panel_settings</span>
                <span data-translate-key="ar.title">Asignar Roles</span>
            </h2>
            <p data-translate-key="ar.subtitle">Gestiona los roles de los usuarios dentro de tu alcance</p>
        </div>
        <div class="ar-search-wrapper">
            <span class="material-icons ar-search-icon">search</span>
            <input type="text" id="arSearch" class="ar-search"
                   data-translate-key="ar.search.placeholder"
                   data-translate-attr="placeholder"
                   placeholder="Buscar por nombre o email..."
                   oninput="filterUsers(this.value)" />
        </div>
    </div>

    <div class="ar-filter-tabs">
        <button type="button" class="ar-tab active" data-filter="all"  onclick="filterByRole('all')" data-translate-key="ar.filter.all">Todos</button>
        <button type="button" class="ar-tab"        data-filter="3"    onclick="filterByRole('3')"   data-translate-key="ar.filter.admin">Administrador</button>
        <button type="button" class="ar-tab"        data-filter="2"    onclick="filterByRole('2')"   data-translate-key="ar.filter.manager">Manager</button>
        <button type="button" class="ar-tab"        data-filter="1"    onclick="filterByRole('1')"   data-translate-key="ar.filter.regular">Regular</button>
    </div>

    <div id="arUserList" class="ar-user-list">
        <div class="ar-loading">
            <span class="material-icons ar-spin">autorenew</span>
            <span data-translate-key="common.loading">Cargando...</span>
        </div>
    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script src='<%= ResolveUrl("~/Scripts/assign_roles.js") %>'></script>
</asp:Content>
