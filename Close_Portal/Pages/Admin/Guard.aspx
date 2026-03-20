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
            <p data-translate-key="gd.subtitle">Administra la guardia activa y asigna responsables por departamento</p>
        </div>
        <div class="gd-header-badge" id="guardStatusBadge">
            <span class="material-icons">shield</span>
            <span id="guardStatusText"></span>
        </div>
    </div>

    <!-- ========== GUARDIA ACTUAL ========== -->
    <div class="gd-section">

        <!-- Sin guardia abierta -->
        <div id="gdNoGuard" class="gd-empty-panel" style="display:none;">
            <span class="material-icons gd-empty-icon">shield</span>
            <p data-translate-key="gd.no_guard">No hay ninguna guardia abierta</p>
            <button type="button" class="gd-btn-create" onclick="createGuard()">
                <span class="material-icons">add_circle_outline</span>
                <span data-translate-key="gd.btn.create">Crear nueva guardia</span>
            </button>
        </div>

        <!-- Guardia en curso (slots) -->
        <div id="gdActivePanel" style="display:none;">

            <!-- Cabecera de la guardia -->
            <div class="gd-guard-header">
                <div class="gd-guard-meta">
                    <span class="material-icons">event</span>
                    <span id="gdCreatedInfo"></span>
                </div>
                <div class="gd-guard-actions">
                    <!-- Paso 3: visible solo en borrador con locaciones seleccionadas -->
                    <button type="button" class="gd-btn-confirm-guard" id="gdBtnConfirmGuard"
                            onclick="submitConfirmGuard()" style="display:none;">
                        <span class="material-icons">rocket_launch</span>
                        <span data-translate-key="gd.btn.confirm_guard">Crear guardia</span>
                    </button>
                    <button type="button" class="gd-btn-danger-sm" id="gdBtnRemove"
                            onclick="confirmRemoveGuard()" style="display:none;">
                        <span class="material-icons">delete_outline</span>
                        <span data-translate-key="gd.btn.remove">Eliminar guardia</span>
                    </button>
                </div>
            </div>

            <!-- Banner de inicio programado -->
            <div class="gd-start-status" id="gdStartStatus" style="display:none;">
                <span class="material-icons">schedule</span>
                <span id="gdStartStatusText"></span>
            </div>

            <!-- Banner de cierre estimado (informativo, no afecta el cierre real) -->
            <div class="gd-est-end-status" id="gdEstEndStatus" style="display:none;">
                <span class="material-icons">event_available</span>
                <span id="gdEstEndStatusText"></span>
            </div>

            <!-- Slots por departamento -->
            <div class="gd-spots-grid" id="gdSpotsGrid">
                <!-- Llenado por guard.js -->
            </div>

        </div>

    </div>

    <!-- ========== LOCACIONES INVOLUCRADAS ========== -->
    <div class="gd-section" id="gdLocSection" style="display:none;">
        <div class="gd-section-header">
            <span class="material-icons">warehouse</span>
            <h3 data-translate-key="gd.loc.title">Locaciones en operación</h3>
            <span class="gd-count-badge" id="gdLocCount">—</span>
        </div>
        <p class="gd-loc-subtitle" data-translate-key="gd.loc.subtitle">
            Locaciones definidas al crear esta guardia como parte del cierre programado.
        </p>

        <div class="gd-loc-empty" id="gdLocEmpty" style="display:none;">
            <span class="material-icons">inventory_2</span>
            <p data-translate-key="gd.loc.empty">Sin movimientos registrados aún.</p>
        </div>

        <div class="gd-loc-grid" id="gdLocGrid"></div>
    </div>

    <!-- ========== HISTORIAL ========== -->
    <div class="gd-section gd-history-section">
        <div class="gd-section-header">
            <span class="material-icons">history</span>
            <h3 data-translate-key="gd.history.title">Historial de guardias</h3>
            <span class="gd-count-badge" id="historyCount">—</span>
        </div>

        <div class="gd-empty-schedule" id="emptyHistory" style="display:none;">
            <span class="material-icons">event_busy</span>
            <p data-translate-key="gd.history.empty">Sin guardias finalizadas</p>
        </div>

        <div class="gd-table-wrapper" id="historyTableWrapper" style="display:none;">
            <table class="gd-table" id="historyTable">
                <thead>
                    <tr>
                        <th data-translate-key="gd.table.id">#</th>
                        <th data-translate-key="gd.table.slots">Responsables</th>
                        <th data-translate-key="gd.table.start">Inicio</th>
                        <th data-translate-key="gd.table.end">Fin</th>
                        <th data-translate-key="gd.table.created_by">Creada por</th>
                    </tr>
                </thead>
                <tbody id="historyBody">
                    <!-- Llenado por guard.js -->
                </tbody>
            </table>
        </div>
    </div>

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script src='<%= ResolveUrl("~/Scripts/guard.js") %>'></script>
</asp:Content>
