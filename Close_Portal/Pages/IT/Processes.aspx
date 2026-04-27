<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true" CodeBehind="Processes.aspx.cs" Inherits="Close_Portal.Pages.IT.Processes" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Procesos - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/Processes.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ========== PAGE HEADER ========== -->
    <div class="page-header">
        <div class="pr-header-badge" id="prStatusBadge">
            <span class="material-icons">circle</span>
            <span id="prStatusText" data-translate-key="common.loading">Cargando...</span>
        </div>
    </div>

    <!-- ========== PROCESOS ========== -->
    <div class="pr-section">
        <div class="pr-section-header">
            <span class="material-icons">settings_suggest</span>
            <h3 data-translate-key="pr.section_title">Procesos del sistema</h3>
        </div>
        <p class="pr-section-desc" data-translate-key="pr.section_desc">
            Activa o desactiva cada proceso. El estado se reinicia al reiniciar el servidor.
        </p>

        <div class="pr-table-wrap">
            <table class="pr-table">
                <thead>
                    <tr>
                        <th data-translate-key="pr.table.process">Proceso</th>
                        <th data-translate-key="pr.table.status">Estado</th>
                        <th data-translate-key="pr.table.actions">Acciones</th>
                    </tr>
                </thead>
                <tbody>
                    <!-- ── Confirmación de cierre ── -->
                    <tr id="prRowConfirmacionCierre">
                        <td class="pr-process-cell">
                            <div class="pr-process-icon">
                                <span class="material-icons">mark_email_unread</span>
                            </div>
                            <div class="pr-process-info">
                                <div class="pr-process-name" data-translate-key="pr.confirmacion_cierre.name">
                                    Confirmación de cierre
                                </div>
                                <div class="pr-process-desc" data-translate-key="pr.confirmacion_cierre.desc">
                                    Envía un correo al finalizar la guardia con el año y periodo del cierre.
                                </div>
                                <!-- Destinatario -->
                                <div class="pr-recipient-wrap" id="prRecipientWrap">
                                    <span class="material-icons pr-recipient-icon">mail_outline</span>
                                    <input type="email" id="prRecipientConfirmacionCierre"
                                           class="pr-recipient-input"
                                           data-translate-key-placeholder="pr.recipient_placeholder"
                                           placeholder="destinatario@dominio.com"
                                           oninput="debounceSetRecipient('ConfirmacionCierre', this.value)" />
                                    <span class="material-icons pr-recipient-ok" id="prRecipientOkIcon" style="display:none;">check_circle</span>
                                </div>
                            </div>
                        </td>
                        <td class="pr-status-cell">
                            <label class="pr-toggle">
                                <input type="checkbox" id="prToggleConfirmacionCierre"
                                       onchange="setProcessEnabled('ConfirmacionCierre', this.checked)" />
                                <span class="pr-toggle-track">
                                    <span class="pr-toggle-thumb"></span>
                                </span>
                                <span class="pr-toggle-label" id="prToggleConfirmacionCierreText"
                                      data-translate-key="pr.toggle_off">Inactivo</span>
                            </label>
                        </td>
                        <td class="pr-actions-cell">
                            <button type="button" class="pr-btn-test"
                                    onclick="testProcess('ConfirmacionCierre')">
                                <span class="material-icons">send</span>
                                <span data-translate-key="pr.btn_test">Test</span>
                            </button>
                        </td>
                    </tr>
                </tbody>
            </table>
        </div>
    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script>window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/IT/Processes.aspx/") %>';</script>
    <script src='<%= ResolveUrl("~/Scripts/processes.js") %>'></script>
</asp:Content>
