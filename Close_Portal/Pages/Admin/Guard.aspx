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

    <!-- ========== SIN GUARDIA ABIERTA ========== -->
    <div class="gd-section" id="gdNoGuard" style="display:none;">
        <div class="gd-empty-panel">
            <span class="material-icons gd-empty-icon">shield</span>
            <p data-translate-key="gd.no_guard">No hay ninguna guardia abierta</p>
            <button type="button" class="gd-btn-create" id="gdBtnCreate"
                    onclick="createGuard()" style="display:none;">
                <span class="material-icons">add_circle_outline</span>
                <span data-translate-key="gd.btn.create">Crear nueva guardia</span>
            </button>
        </div>
    </div>

    <!-- ========== CARRUSEL DE PASOS (visible cuando hay guardia) ========== -->
    <div class="gd-section gd-carousel-section" id="gdCarousel" style="display:none;">

        <!-- Cabecera de guardia: meta + acciones -->
        <div class="gd-guard-header">
            <div class="gd-guard-meta">
                <span class="material-icons">event</span>
                <span id="gdCreatedInfo"></span>
            </div>
            <div class="gd-guard-actions">
                <button type="button" class="gd-btn-danger-sm" id="gdBtnRemove"
                        onclick="confirmRemoveGuard()" style="display:none;">
                    <span class="material-icons">delete_outline</span>
                    <span data-translate-key="gd.btn.remove">Eliminar guardia</span>
                </button>
            </div>
        </div>

        <!-- Banners de estado -->
        <div class="gd-start-status" id="gdStartStatus" style="display:none;">
            <span class="material-icons">schedule</span>
            <span id="gdStartStatusText"></span>
        </div>
        <div class="gd-est-end-status" id="gdEstEndStatus" style="display:none;">
            <span class="material-icons">event_available</span>
            <span id="gdEstEndStatusText"></span>
        </div>

        <!-- ── Pestañas de navegación ── -->
        <div class="gd-tabs">
            <button type="button" class="gd-tab gd-tab-active" data-tab="0" onclick="gdSwitchTab(0)">
                <span class="material-icons">manage_accounts</span>
                <span data-translate-key="gd.tab.responsible">Persona responsable</span>
            </button>
            <button type="button" class="gd-tab" data-tab="1" onclick="gdSwitchTab(1)">
                <span class="material-icons">warehouse</span>
                <span data-translate-key="gd.tab.locations">Locaciones involucradas</span>
            </button>
            <button type="button" class="gd-tab" data-tab="2" onclick="gdSwitchTab(2)">
                <span class="material-icons">rocket_launch</span>
                <span data-translate-key="gd.tab.creation">Creación de guardia</span>
            </button>
            <button type="button" class="gd-tab gd-tab-close" id="gdTab3" onclick="gdSwitchTab(3)"
                    style="display:none;">
                <span class="material-icons">lock</span>
                <span data-translate-key="gd.tab.close">Finalizar guardia</span>
            </button>
        </div>

        <!-- ── Panel 0: Persona responsable (spots) ── -->
        <div class="gd-tab-panel gd-tab-panel-active" id="gdTabPanel0">
            <div class="gd-spots-grid" id="gdSpotsGrid">
                <!-- Llenado por guard.js -->
            </div>
        </div>

        <!-- ── Panel 1: Locaciones involucradas ── -->
        <div class="gd-tab-panel" id="gdTabPanel1">
            <div class="gd-loc-count-row">
                <span class="gd-count-badge" id="gdLocCount">—</span>
                <p class="gd-loc-subtitle" data-translate-key="gd.loc.subtitle">
                    Locaciones definidas para este cierre programado.
                </p>
            </div>
            <div class="gd-loc-empty" id="gdLocEmpty" style="display:none;">
                <span class="material-icons">inventory_2</span>
                <p data-translate-key="gd.loc.empty">Sin movimientos registrados aún.</p>
            </div>
            <div class="gd-loc-grid" id="gdLocGrid"></div>
        </div>

        <!-- ── Panel 2: Creación de guardia ── -->
        <div class="gd-tab-panel" id="gdTabPanel2">

            <!-- Pendiente de locaciones -->
            <div id="gdPendingLocPanel" class="gd-step-panel" style="display:none;">
                <div class="gd-step-icon-wrap gd-step-icon-muted">
                    <span class="material-icons">hourglass_empty</span>
                </div>
                <h4 data-translate-key="gd.step.pending.title">Pendiente de locaciones</h4>
                <p data-translate-key="gd.step.pending.desc">
                    Ve a la pestaña <strong>Locaciones involucradas</strong> y selecciona
                    al menos una locación para poder crear la guardia.
                </p>
            </div>

            <!-- Listo para crear -->
            <div id="gdCreationPanel" class="gd-step-panel" style="display:none;">
                <div class="gd-step-icon-wrap gd-step-icon-amber">
                    <span class="material-icons">rocket_launch</span>
                </div>
                <h4 data-translate-key="gd.step.create.title">Crear guardia</h4>
                <p data-translate-key="gd.step.create.desc">
                    Las locaciones han sido seleccionadas. Confirma para activar la guardia
                    y que los responsables puedan comenzar a registrar.
                </p>
                <button type="button" class="gd-btn-confirm-guard" id="gdBtnConfirmGuard"
                        onclick="submitConfirmGuard()">
                    <span class="material-icons">rocket_launch</span>
                    <span data-translate-key="gd.btn.confirm_guard">Crear guardia</span>
                </button>
            </div>

            <!-- Guardia en curso -->
            <div id="gdProgressPanel" class="gd-step-panel" style="display:none;">
                <div class="gd-step-icon-wrap gd-step-icon-green">
                    <span class="material-icons">pending_actions</span>
                </div>
                <h4 data-translate-key="gd.step.progress.title">Guardia en curso</h4>
                <p data-translate-key="gd.step.progress.desc">
                    La guardia está activa. El cierre estará disponible cuando
                    todas las locaciones involucradas hayan sido procesadas.
                </p>
                <div class="gd-progress-locs" id="gdProgressLocs"></div>
            </div>

            <!-- Guardia ya finalizada -->
            <div id="gdFinishedPanel" class="gd-step-panel" style="display:none;">
                <div class="gd-step-icon-wrap gd-step-icon-muted">
                    <span class="material-icons">check_circle</span>
                </div>
                <h4 data-translate-key="gd.step.finished.title">Guardia finalizada</h4>
                <p data-translate-key="gd.step.finished.desc">
                    Esta guardia ha sido cerrada exitosamente. Consulta el historial para más detalles.
                </p>
            </div>

        </div>
        <!-- /Panel 2 -->

        <!-- ── Panel 3: Finalizar guardia (visible solo cuando todas las locs. cerraron) ── -->
        <div class="gd-tab-panel" id="gdTabPanel3">
            <div id="gdClosurePanel" class="gd-step-panel">
                <div class="gd-step-icon-wrap gd-step-icon-red">
                    <span class="material-icons">lock</span>
                </div>
                <h4 data-translate-key="gd.step.close.title">Finalizar guardia</h4>
                <p data-translate-key="gd.step.close.desc">
                    Todas las locaciones han sido procesadas. Confirma que las operaciones de
                    <strong>AR</strong> y <strong>CS</strong> han concluido para cerrar esta guardia.
                </p>
                <div class="gd-closure-checklist">
                    <label class="gd-closure-check-item">
                        <input type="checkbox" id="gdCheckAR" onchange="gdUpdateCloseBtn()" />
                        <span class="material-icons gd-closure-icon">business</span>
                        <span>Operaciones <strong>AR</strong> finalizadas</span>
                    </label>
                    <label class="gd-closure-check-item">
                        <input type="checkbox" id="gdCheckCS" onchange="gdUpdateCloseBtn()" />
                        <span class="material-icons gd-closure-icon">local_shipping</span>
                        <span>Operaciones <strong>CS</strong> finalizadas</span>
                    </label>
                </div>
                <button type="button" class="gd-btn-close-guard" id="gdBtnCloseGuard"
                        onclick="confirmCloseGuard()" disabled style="display:none;">
                    <span class="material-icons">lock</span>
                    <span data-translate-key="gd.btn.close_guard">Confirmar cierre de guardia</span>
                </button>
            </div>
        </div>
        <!-- /Panel 3 -->

    </div>
    <!-- /gdCarousel -->

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
    <script>window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/Admin/Guard.aspx/") %>';</script>
    <script src='<%= ResolveUrl("~/Scripts/guard.js") %>'></script>
</asp:Content>
