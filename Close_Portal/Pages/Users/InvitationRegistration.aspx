<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true" CodeBehind="InvitationRegistration.aspx.cs" Inherits="Close_Portal.Pages.InvitationRegistration" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Alta con Invitación - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/InvitationRegistration.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ========== PAGE HEADER ========== -->
    <div class="page-header">
        <div class="page-title">
            <h2>
                <span class="material-icons inv-title-icon">mail_outline</span>
                <span data-translate-key="inv.title">Alta con Invitación</span>
            </h2>
            <p data-translate-key="inv.subtitle">Envía un link de invitación para registrar un nuevo usuario</p>
        </div>
    </div>

    <!-- ========== MAIN GRID ========== -->
    <div class="inv-grid">

        <!-- ====== COLUMNA IZQUIERDA: FORMULARIO ====== -->
        <section class="inv-panel">
            <div class="inv-panel-header">
                <span class="material-icons">person_add</span>
                <h3 data-translate-key="inv.form.title">Nueva invitación</h3>
            </div>
            <div class="inv-panel-body">

                <!-- Email -->
                <div class="inv-field-group">
                    <label>
                        <span data-translate-key="inv.email.label">Email del invitado</span>
                        <span class="inv-required">*</span>
                    </label>
                    <div class="inv-input-wrapper">
                        <span class="material-icons inv-input-icon">alternate_email</span>
                        <input type="email" id="invEmail"
                               class="inv-input has-icon"
                               data-translate-key="inv.email.placeholder"
                               data-translate-attr="placeholder"
                               placeholder="usuario@novamex.com"
                               maxlength="150" />
                    </div>
                    <span class="inv-field-hint" data-translate-key="inv.email.hint">
                        El usuario recibirá el link en este correo
                    </span>
                    <div class="inv-field-error" id="invEmailError"></div>
                </div>

                <!-- Rol -->
                <div class="inv-field-group">
                    <label>
                        <span data-translate-key="inv.role.label">Rol que tendrá</span>
                        <span class="inv-required">*</span>
                    </label>
                    <div class="inv-input-wrapper">
                        <span class="material-icons inv-input-icon">admin_panel_settings</span>
                        <select id="invRole" class="inv-input has-icon">
                            <option value="" data-translate-key="inv.role.placeholder">-- Selecciona un rol --</option>
                        </select>
                    </div>
                    <div class="inv-field-error" id="invRoleError"></div>
                </div>

                <!-- OMS -->
                <div class="inv-field-group">
                    <label>
                        <span data-translate-key="inv.oms.label">OMS que tendrá acceso</span>
                        <span class="inv-required">*</span>
                    </label>
                    <div class="inv-oms-checklist" id="invOmsChecklist">
                        <div class="inv-loading">
                            <span class="material-icons inv-spin">autorenew</span>
                            <span data-translate-key="inv.oms.loading">Cargando OMS...</span>
                        </div>
                    </div>
                    <div class="inv-field-error" id="invOmsError"></div>
                </div>

                <!-- Footer del form -->
                <div class="inv-form-footer">
                    <div class="inv-form-message" id="invFormMessage" style="display:none;"></div>
                    <button type="button" class="inv-btn-send" id="btnSendInvitation" onclick="sendInvitation()">
                        <span class="material-icons">send</span>
                        <span data-translate-key="inv.btn.send">Enviar invitación</span>
                    </button>
                </div>

            </div>
        </section>

        <!-- ====== COLUMNA DERECHA: INVITACIONES ENVIADAS ====== -->
        <section class="inv-panel">
            <div class="inv-panel-header">
                <span class="material-icons">list_alt</span>
                <h3 data-translate-key="inv.list.title">Invitaciones enviadas</h3>
                <span class="inv-count-badge" id="invCount">—</span>
                <button type="button" class="inv-btn-refresh" onclick="loadInvitations()" title="Actualizar">
                    <span class="material-icons">refresh</span>
                </button>
            </div>
            <div class="inv-panel-body inv-list-body">

                <!-- Filtros de estado -->
                <div class="inv-filter-tabs" id="invFilterTabs">
                    <button type="button" class="inv-tab active"  data-filter="all"       onclick="filterInvitations('all')"       data-translate-key="inv.filter.all">Todas</button>
                    <button type="button" class="inv-tab"         data-filter="pending"   onclick="filterInvitations('pending')"   data-translate-key="inv.filter.pending">Pendientes</button>
                    <button type="button" class="inv-tab"         data-filter="accepted"  onclick="filterInvitations('accepted')"  data-translate-key="inv.filter.accepted">Aceptadas</button>
                    <button type="button" class="inv-tab"         data-filter="cancelled" onclick="filterInvitations('cancelled')" data-translate-key="inv.filter.cancelled">Canceladas</button>
                </div>

                <!-- Lista -->
                <div id="invitationsList">
                    <div class="inv-loading">
                        <span class="material-icons inv-spin">autorenew</span>
                        <span data-translate-key="inv.list.loading">Cargando invitaciones...</span>
                    </div>
                </div>

            </div>
        </section>

    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script src='<%= ResolveUrl("~/Scripts/invitation_registration.js") %>'></script>
</asp:Content>
