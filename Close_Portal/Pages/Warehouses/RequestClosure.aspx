<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.Master" AutoEventWireup="true" CodeBehind="RequestClosure.aspx.cs" Inherits="Close_Portal.Pages.RequestClosure" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Solicitar Cierre - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/RequestClosure.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <div class="page-header">
        <div class="page-title">
            <h2>
                <span class="material-icons rc-title-icon">lock</span>
                <span data-translate-key="rc.title">Solicitar Cierre</span>
            </h2>
            <p data-translate-key="rc.subtitle">Envía una solicitud de cierre de operaciones al Manager de tu locación</p>
        </div>
    </div>

    <!-- ── GRID: formulario izquierda + historial derecha ── -->
    <div class="rc-grid">

        <!-- ====== COLUMNA IZQUIERDA: FORMULARIO ====== -->
        <section class="rc-panel">
            <div class="rc-panel-header">
                <span class="material-icons">edit_note</span>
                <h3 data-translate-key="rc.form.title">Nueva solicitud</h3>
            </div>
            <div class="rc-panel-body">

                <!-- Locación -->
                <div class="rc-field-group">
                    <label>
                        <span data-translate-key="rc.location.label">Locación</span>
                        <span class="rc-required">*</span>
                    </label>
                    <div class="rc-input-wrapper">
                        <span class="material-icons rc-input-icon">location_on</span>
                        <select id="rcLocation" class="rc-input has-icon">
                            <option value="" data-translate-key="rc.location.loading">-- Cargando locaciones... --</option>
                        </select>
                    </div>
                    <span class="rc-field-hint" data-translate-key="rc.location.hint">Solo se muestran tus locaciones asignadas</span>
                    <div class="rc-field-error" id="rcLocationError"></div>
                </div>

                <!-- Manager info -->
                <div class="rc-manager-card" id="rcManagerCard" style="display:none;">
                    <span class="material-icons">person</span>
                    <div>
                        <div class="rc-manager-label" data-translate-key="rc.manager.label">Manager asignado</div>
                        <div class="rc-manager-name" id="rcManagerName">—</div>
                        <div class="rc-manager-email" id="rcManagerEmail">—</div>
                    </div>
                </div>

                <div class="rc-no-manager" id="rcNoManager" style="display:none;">
                    <span class="material-icons">warning_amber</span>
                    <span data-translate-key="rc.manager.none">Esta locación no tiene un Manager asignado. Contacta a tu Administrador.</span>
                </div>

                <!-- Notas -->
                <div class="rc-field-group">
                    <label>
                        <span data-translate-key="rc.notes.label">Notas adicionales</span>
                        <span class="rc-optional" data-translate-key="rc.notes.optional">(opcional)</span>
                    </label>
                    <textarea id="rcNotes" class="rc-textarea"
                              data-translate-key="rc.notes.placeholder"
                              data-translate-attr="placeholder"
                              placeholder="Describe el motivo del cierre, turno, incidencias relevantes..."
                              maxlength="500" rows="4"></textarea>
                    <span class="rc-field-hint rc-char-count">
                        <span id="rcCharCount">0</span>
                        <span data-translate-key="rc.notes.char_count">/ 500 caracteres</span>
                    </span>
                </div>

                <!-- Footer -->
                <div class="rc-form-footer">
                    <div class="rc-form-message" id="rcFormMessage" style="display:none;"></div>
                    <button type="button" class="rc-btn-send" id="rcBtnSend" onclick="sendRequest()" disabled>
                        <span class="material-icons">send</span>
                        <span data-translate-key="rc.btn.send">Enviar solicitud</span>
                    </button>
                </div>

            </div>
        </section>

        <!-- ====== COLUMNA DERECHA: HISTORIAL ====== -->
        <section class="rc-panel">
            <div class="rc-panel-header">
                <span class="material-icons">history</span>
                <h3 data-translate-key="rc.history.title">Mis solicitudes</h3>
                <span class="rc-count-badge" id="rcCount">—</span>
                <button type="button" class="rc-btn-refresh" onclick="loadHistory()"
                        data-translate-key="rc.btn.refresh" data-translate-attr="title"
                        title="Actualizar">
                    <span class="material-icons">refresh</span>
                </button>
            </div>

            <!-- Filtros -->
            <div class="rc-filter-tabs">
                <button type="button" class="rc-tab active" data-filter="all"      onclick="filterHistory('all')"      data-translate-key="rc.filter.all">Todas</button>
                <button type="button" class="rc-tab"        data-filter="Pending"  onclick="filterHistory('Pending')"  data-translate-key="rc.filter.pending">Pendientes</button>
                <button type="button" class="rc-tab"        data-filter="Approved" onclick="filterHistory('Approved')" data-translate-key="rc.filter.approved">Aprobadas</button>
                <button type="button" class="rc-tab"        data-filter="Rejected" onclick="filterHistory('Rejected')" data-translate-key="rc.filter.rejected">Rechazadas</button>
            </div>

            <div class="rc-panel-body rc-list-body">
                <div id="rcHistoryList">
                    <div class="rc-loading">
                        <span class="material-icons rc-spin">autorenew</span>
                        <span data-translate-key="rc.history.loading">Cargando historial...</span>
                    </div>
                </div>
            </div>
        </section>

    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script>
        window.CurrentUserId = '<%= Session["UserId"] %>';
    </script>
    <script src='<%= ResolveUrl("~/Scripts/request_closure.js") %>'></script>
</asp:Content>
