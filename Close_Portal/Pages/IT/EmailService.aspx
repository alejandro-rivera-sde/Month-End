<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true" CodeBehind="EmailService.aspx.cs" Inherits="Close_Portal.Pages.IT.EmailServicePage" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Email Service - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/EmailService.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ========== PAGE HEADER ========== -->
    <div class="page-header">
        <div class="page-title">
            <h2>
                <span class="material-icons es-title-icon">mail</span>
                <span data-translate-key="email.title">Email Service</span>
            </h2>
            <p data-translate-key="email.subtitle">Administración de notificaciones, grupos de correo y alertas del sistema</p>
        </div>
        <div class="es-header-badge" id="esStatusBadge">
            <span class="material-icons">circle</span>
            <span id="esStatusText" data-translate-key="common.loading">Cargando...</span>
        </div>
    </div>

    <!-- ========== CONTROL DEL SERVICIO ========== -->
    <div class="es-section">
        <div class="es-section-header">
            <span class="material-icons">power_settings_new</span>
            <h3 data-translate-key="email.service_control">Control del servicio</h3>
        </div>

        <div class="es-controls-grid">

            <!-- Toggle principal -->
            <div class="es-control-card" id="esMainCard">
                <div class="es-control-info">
                    <div class="es-control-title" data-translate-key="email.global_notif">Notificaciones globales</div>
                    <div class="es-control-desc" data-translate-key="email.global_notif_desc">Activa o desactiva el envío de todos los correos del sistema.</div>
                    <div class="es-control-warning">
                        <span class="material-icons">info</span>
                        <span data-translate-key="email.state_warning">El estado se reinicia al reiniciar el servidor.</span>
                    </div>
                </div>
                <label class="es-toggle" id="esMainToggleLabel">
                    <input type="checkbox" id="esMainToggle" onchange="toggleNotifications(this.checked)" />
                    <span class="es-toggle-track">
                        <span class="es-toggle-thumb"></span>
                    </span>
                    <span class="es-toggle-label" id="esMainToggleText" data-translate-key="email.toggle_off">Desactivado</span>
                </label>
            </div>

            <!-- Toggle modo prueba -->
            <div class="es-control-card" id="esTestCard">
                <div class="es-control-info">
                    <div class="es-control-title" data-translate-key="email.test_mode">Modo prueba</div>
                    <div class="es-control-desc" data-translate-key="email.test_mode_desc">Redirige todos los correos a un único destinatario de prueba, ignorando los grupos reales.</div>
                </div>
                <div class="es-test-row">
                    <label class="es-toggle">
                        <input type="checkbox" id="esTestToggle" onchange="toggleTestMode(this.checked)" />
                        <span class="es-toggle-track">
                            <span class="es-toggle-thumb"></span>
                        </span>
                        <span class="es-toggle-label" id="esTestToggleText" data-translate-key="email.toggle_off">Desactivado</span>
                    </label>
                    <div class="es-test-email-wrap" id="esTestEmailWrap" style="display:none;">
                        <input type="email" id="esTestEmail" class="es-input"
                               placeholder="correo@dominio.com"
                               data-translate-key="email.test_email_placeholder"
                               oninput="debounceSaveTestEmail(this.value)" />
                        <span class="material-icons es-input-icon" id="esTestEmailIcon" style="display:none;">check_circle</span>
                    </div>
                </div>
            </div>

        </div>
    </div>

    <!-- ========== GRUPOS DE CORREO ========== -->
    <div class="es-section">
        <div class="es-section-header">
            <span class="material-icons">group</span>
            <h3 data-translate-key="email.groups">Grupos de correo</h3>
        </div>
        <p class="es-section-desc" data-translate-key="email.groups_desc">Grupos configurados en el servidor. Para modificarlos, actualiza los AppSettings en el servidor.</p>

        <div class="es-groups-grid" id="esGroupsGrid">
            <!-- Llenado por email_service.js -->
            <div class="es-loading">
                <span class="material-icons es-spin">autorenew</span>
                <span data-translate-key="email.groups_loading">Cargando grupos...</span>
            </div>
        </div>
    </div>

    <!-- ========== ALERTAS ========== -->
    <div class="es-section">
        <div class="es-section-header">
            <span class="material-icons">notifications</span>
            <h3 data-translate-key="email.alerts">Alertas</h3>
            <div class="es-alert-bulk">
                <button type="button" class="es-btn-sm" onclick="setBulkAlerts(true)">
                    <span class="material-icons">done_all</span>
                    <span data-translate-key="email.alerts_enable_all">Activar todas</span>
                </button>
                <button type="button" class="es-btn-sm es-btn-sm-muted" onclick="setBulkAlerts(false)">
                    <span class="material-icons">remove_done</span>
                    <span data-translate-key="email.alerts_disable_all">Desactivar todas</span>
                </button>
            </div>
        </div>

        <div class="es-alerts-table-wrap">
            <table class="es-alerts-table">
                <thead>
                    <tr>
                        <th data-translate-key="email.table_event">Evento</th>
                        <th data-translate-key="email.table_recipient">Destinatario</th>
                        <th data-translate-key="email.table_status">Estado</th>
                        <th data-translate-key="email.table_test">Prueba</th>
                    </tr>
                </thead>
                <tbody id="esAlertsBody">
                    <!-- Llenado por email_service.js -->
                </tbody>
            </table>
        </div>
    </div>

    <!-- ========== CONFIGURACIÓN SMTP ========== -->
    <div class="es-section">
        <div class="es-section-header">
            <span class="material-icons">dns</span>
            <h3 data-translate-key="email.smtp_config">Configuración SMTP</h3>
        </div>
        <div class="es-smtp-grid" id="esSmtpGrid">
            <!-- Llenado por email_service.js -->
        </div>
    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script>window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/IT/EmailService.aspx/") %>';</script>
    <script src='<%= ResolveUrl("~/Scripts/email_service.js") %>'></script>
</asp:Content>
