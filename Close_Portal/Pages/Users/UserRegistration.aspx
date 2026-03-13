<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true" CodeBehind="UserRegistration.aspx.cs" Inherits="Close_Portal.Pages.UserRegistration" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Alta de Usuario - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/UserManagement.css") %>' rel="stylesheet" type="text/css" />
    <link href='<%= ResolveUrl("~/Styles/UserRegistration.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ========== PAGE HEADER ========== -->
    <div class="page-header">
        <div class="page-title">
            <h2 data-translate-key="ur.title">Alta de Usuario</h2>
            <p data-translate-key="ur.subtitle">Registra un nuevo usuario y asigna su rol y OMS</p>
        </div>
    </div>

    <!-- ========== FORM CARD ========== -->
    <div class="ur-card">

        <div class="ur-card-header">
            <span class="material-icons">person_add</span>
            <span data-translate-key="ur.card_header">Información del nuevo usuario</span>
        </div>

        <!-- Dos columnas -->
        <div class="ur-body">

            <!-- ── COLUMNA IZQUIERDA: datos del usuario ── -->
            <div class="ur-col-left">

                <!-- Email -->
                <div class="ur-field-group">
                    <label>
                        <span data-translate-key="ur.email.label">Email</span>
                        <span class="ur-required">*</span>
                    </label>
                    <div class="ur-input-wrapper">
                        <span class="material-icons ur-input-icon">alternate_email</span>
                        <input type="email" id="newEmail" placeholder="usuario@novamex.com"
                               class="ur-input has-icon" maxlength="100" />
                    </div>
                    <span class="ur-field-hint" data-translate-key="ur.email.hint">
                        El usuario iniciará sesión con su cuenta Google @novamex.com
                    </span>
                </div>

                <!-- Rol -->
                <div class="ur-field-group">
                    <label>
                        <span data-translate-key="ur.role.label">Rol</span>
                        <span class="ur-required">*</span>
                    </label>
                    <div class="ur-input-wrapper">
                        <span class="material-icons ur-input-icon">admin_panel_settings</span>
                        <select id="ddlRole" class="ur-input has-icon">
                            <option value="" data-translate-key="ur.role.placeholder">-- Selecciona un rol --</option>
                        </select>
                    </div>
                    <span class="ur-field-hint" id="roleHint"></span>
                </div>

                <!-- Mensaje y botones -->
                <div id="formMessage" class="ur-form-message" style="display:none;"></div>

                <div class="ur-footer-actions">
                    <button type="button" class="ur-btn-cancel"
                            data-translate-key="common.cancel"
                            onclick="window.location='<%= ResolveUrl("~/Pages/Admin/UserManagement.aspx") %>'">
                        Cancelar
                    </button>
                    <button type="button" class="ur-btn-save" id="btnCreate" onclick="createUser()">
                        <span class="material-icons">person_add</span>
                        <span data-translate-key="ur.btn_create">Crear Usuario</span>
                    </button>
                </div>

            </div>

            <!-- ── COLUMNA DERECHA: OMS checklist ── -->
            <div class="ur-col-right">

                <div class="ur-oms-header">
                    <span class="material-icons">hub</span>
                    <span data-translate-key="ur.oms.divider">OMS disponibles para asignar</span>
                </div>

                <div class="ur-oms-checklist" id="omsChecklist">
                    <div class="ur-loading">
                        <span class="material-icons ur-spin">sync</span>
                        <span data-translate-key="ur.oms.loading">Cargando OMS disponibles...</span>
                    </div>
                </div>

            </div>

        </div><!-- /ur-body -->

    </div><!-- /ur-card -->

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script src='<%= ResolveUrl("~/Scripts/user_registration.js") %>'></script>
</asp:Content>
