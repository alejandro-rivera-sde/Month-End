<%@ Page Language="C#" MasterPageFile="~/DashboardLayout.master" AutoEventWireup="true" CodeBehind="UserManagement.aspx.cs" Inherits="Close_Portal.Pages.Admin.UserManagement" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="PageTitle" runat="server">
    Gestión de Usuarios - Close Portal
</asp:Content>

<asp:Content ID="AdditionalCSS" ContentPlaceHolderID="AdditionalCSS" runat="server">
    <link href='<%= ResolveUrl("~/Styles/UserManagement.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="DashboardContent" ContentPlaceHolderID="DashboardContent" runat="server">

    <!-- ========== PAGE HEADER ========== -->
    <div class="page-header">
        <div class="page-title">
            <h2 data-translate-key="um.title">Gestión de Usuarios</h2>
            <p data-translate-key="um.subtitle">Administra usuarios, roles y accesos a WMS</p>
        </div>
        <button type="button" class="btn-new" onclick="openModalNew()">
            <span class="material-icons">person_add</span>
            <span data-translate-key="um.btn_new">Nuevo Usuario</span>
        </button>
    </div>

    <!-- ========== STATS ROW ========== -->
    <div class="stats-row">
        <div class="stat-card">
            <div class="stat-icon purple">
                <span class="material-icons">group</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="um.stat.total">Total Usuarios</p>
                <h3><asp:Literal ID="litTotalUsers" runat="server">0</asp:Literal></h3>
            </div>
        </div>
        <div class="stat-card">
            <div class="stat-icon green">
                <span class="material-icons">check_circle</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="um.stat.active">Activos</p>
                <h3><asp:Literal ID="litActiveUsers" runat="server">0</asp:Literal></h3>
            </div>
        </div>
        <div class="stat-card">
            <div class="stat-icon red">
                <span class="material-icons">lock</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="um.stat.locked">Bloqueados</p>
                <h3><asp:Literal ID="litLockedUsers" runat="server">0</asp:Literal></h3>
            </div>
        </div>
        <div class="stat-card">
            <div class="stat-icon amber">
                <span class="material-icons">warehouse</span>
            </div>
            <div class="stat-info">
                <p data-translate-key="um.stat.wms">WMS Activos</p>
                <h3><asp:Literal ID="litActiveWms" runat="server">0</asp:Literal></h3>
            </div>
        </div>
    </div>

    <!-- ========== TOOLBAR ========== -->
    <div class="toolbar">
        <div class="search-box">
            <span class="material-icons">search</span>
            <%-- Honeypot: Chrome hace autofill aquí en lugar del buscador --%>
            <input type="text"     id="um-hp-user" name="um-hp-user"     style="display:none" aria-hidden="true" tabindex="-1" />
            <input type="password" id="um-hp-pass" name="um-hp-pass"     style="display:none" aria-hidden="true" tabindex="-1" />
            <input type="search" id="searchInput"
                   name="um-search-filter"
                   autocomplete="off"
                   data-form-type="other"
                   data-translate-key="um.search.placeholder"
                   placeholder="Buscar por nombre, email..."
                   oninput="filterTable()" />
        </div>
        <select class="filter-select" id="filterRole" onchange="filterTable()">
            <option value="" data-translate-key="um.filter.all_roles">Todos los roles</option>
            <option value="4">Owner</option>
            <option value="3">Administrador</option>
            <option value="2">Manager</option>
            <option value="1">Regular</option>
        </select>
        <select class="filter-select" id="filterStatus" onchange="filterTable()">
            <option value="" data-translate-key="um.filter.all_status">Todos los estados</option>
            <option value="Activo" data-translate-key="um.status.active">Activo</option>
            <option value="Bloqueado" data-translate-key="um.status.locked">Bloqueado</option>
            <option value="Inactivo" data-translate-key="um.status.inactive">Inactivo</option>
        </select>
        <select class="filter-select" id="filterWms" onchange="filterTable()">
            <option value="" data-translate-key="um.filter.all_wms">Todos los WMS</option>
            <asp:Repeater ID="rptWmsFilter" runat="server">
                <ItemTemplate>
                    <option value="<%# Eval("WMS_Code") %>"><%# Eval("WMS_Code") %></option>
                </ItemTemplate>
            </asp:Repeater>
        </select>
        <select class="filter-select" id="filterDepartment" onchange="filterTable()">
            <option value="" data-translate-key="um.filter.all_departments">Todos los departamentos</option>
            <asp:Repeater ID="rptDepartmentsFilter" runat="server">
                <ItemTemplate>
                    <option value="<%# Eval("Department_Code") %>"><%# Eval("Department_Code") %> — <%# Eval("Department_Name") %></option>
                </ItemTemplate>
            </asp:Repeater>
        </select>
    </div>

    <!-- ========== TABLE ========== -->
    <div class="table-wrapper">
        <table id="usersTable">
            <thead>
                <tr>
                    <th data-translate-key="um.table.user">Usuario</th>
                    <th data-translate-key="um.table.role">Rol</th>
                    <th data-translate-key="um.table.department">Departamento</th>
                    <th data-translate-key="um.table.wms">WMS / Locaciones</th>
                    <th data-translate-key="um.table.login">Login</th>
                    <th data-translate-key="um.table.status">Estado</th>
                    <th data-translate-key="um.table.actions">Acciones</th>
                </tr>
            </thead>
            <tbody>
                <asp:Repeater ID="rptUsers" runat="server">
                    <ItemTemplate>
                        <tr data-role="<%# Eval("RoleName") %>"
                            data-roleid="<%# Eval("RoleId") %>"
                            data-status="<%# Eval("StatusLabel") %>" 
                            data-wms="<%# Eval("WmsCodes") %>"
                            data-department="<%# Eval("DepartmentCode") %>">
                            <td>
                                <div class="user-cell">
                                    <div class="avatar"><%# Eval("Initials") %></div>
                                    <div>
                                        <div class="user-name-text"><%# Eval("Username") %></div>
                                        <div class="user-email"><%# Eval("Email") %></div>
                                    </div>
                                </div>
                            </td>
                            <td>
                                <span class="badge badge-<%# Eval("RoleBadge") %>"><%# Eval("RoleName") %></span>
                            </td>
                            <td>
                                <%# !string.IsNullOrEmpty(Eval("DepartmentCode")?.ToString())
                                    ? $"<span class='dept-badge'>{Eval("DepartmentCode")}</span><span class='dept-name'> {Eval("DepartmentName")}</span>"
                                    : "<span style='color:var(--text-muted);font-size:11px'>—</span>" %>
                            </td>
                            <td>
                                <div class="wms-tags"><%# Eval("WmsTagsHtml") %></div>
                            </td>
                            <td>
                                <div class="login-type">
                                    <span class="material-icons"><%# Eval("LoginIcon") %></span>
                                    <%# Eval("LoginTypeLabel") %>
                                </div>
                            </td>
                            <td>
                                <span class="badge badge-<%# Eval("StatusBadge") %>"><%# Eval("StatusLabel") %></span>
                            </td>
                            <td>
                                <div class="actions">
                                    <%# (int)Eval("RoleId") < (int)(Session["RoleId"] ?? 0) ? $@"
                                    <button type='button' class='btn-icon edit'
                                            onclick='openModalEdit({Eval("UserId")})'
                                            title='Editar usuario'>
                                        <span class='material-icons'>edit</span>
                                    </button>
                                    <button type='button' class='btn-icon delete'
                                            onclick='confirmToggleActive({Eval("UserId")}, {Eval("Active").ToString().ToLower()})'
                                            title='{((bool)Eval("Active") ? "Desactivar usuario" : "Activar usuario")}'>
                                        <span class='material-icons'>{((bool)Eval("Active") ? "person_off" : "person")}</span>
                                    </button>" : "<span class='um-no-action' title='Sin permisos para editar'><span class='material-icons'>lock</span></span>" %>
                                </div>
                            </td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </tbody>
        </table>

        <asp:Panel ID="pnlEmpty" runat="server" Visible="false" CssClass="empty-state">
            <span class="material-icons">group_off</span>
            <p data-translate-key="um.empty">No se encontraron usuarios</p>
        </asp:Panel>
    </div>

    <!-- ========== MODAL ========== -->
    <div class="um-overlay" id="modalOverlay">
        <div class="um-modal">

            <!-- Header: user info | tabs | close -->
            <div class="um-header">
                <div class="um-header-user" id="userInfoCard" style="display:none">
                    <div class="um-header-avatar" id="modalAvatar"></div>
                    <div class="um-header-info">
                        <div class="um-header-name" id="modalUserName"></div>
                        <div class="um-header-email" id="modalUserEmail"></div>
                    </div>
                </div>
                <span class="um-header-title" id="modalTitle">Editar Usuario</span>

                <div class="um-tab-nav">
                    <button type="button" class="um-tab active" data-tab="general" onclick="switchTab('general')">
                        <span class="material-icons">person</span>
                        <span data-translate-key="um.tab.general">General</span>
                    </button>
                    <button type="button" class="um-tab" data-tab="wms" onclick="switchTab('wms')">
                        <span class="material-icons">warehouse</span>
                        <span data-translate-key="um.tab.wms">WMS</span>
                    </button>
                    <button type="button" class="um-tab" data-tab="locations" onclick="switchTab('locations')">
                        <span class="material-icons">location_on</span>
                        <span data-translate-key="um.tab.locations">Locaciones</span>
                    </button>
                </div>

                <button type="button" class="btn-icon" onclick="closeModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>

                <!-- Right content area — tabs + panels -->
                <div class="um-body-layout"><div class="um-content-area">

                    <!-- Tab: General -->
                    <div class="um-tab-panel active" id="tab-general">

                        <!-- NEW USER MODE fields -->
                        <div id="newUserFields" style="display:none">
                            <div class="um-general-grid">
                                <div class="field-group">
                                    <label for="newEmail" data-translate-key="um.modal.email">Email (@novamex.com)</label>
                                    <input type="email" id="newEmail"
                                           data-translate-key="um.modal.email_placeholder"
                                           placeholder="usuario@novamex.com" autocomplete="off" />
                                </div>
                                <div class="field-group">
                                    <label for="newUsername" data-translate-key="um.modal.username">Username</label>
                                    <input type="text" id="newUsername"
                                           data-translate-key="um.modal.username_placeholder"
                                           placeholder="Nombre de usuario" maxlength="80" autocomplete="off" />
                                    <span class="field-hint" data-translate-key="um.modal.username_hint">Nombre visible dentro del portal</span>
                                </div>
                                <div class="field-group">
                                    <label for="newModalRole" data-translate-key="um.modal.role">Rol</label>
                                    <select id="newModalRole">
                                        <asp:Repeater ID="rptRolesNew" runat="server">
                                            <ItemTemplate>
                                                <option value="<%# Eval("Role_Id") %>"><%# Eval("Role_Name") %></option>
                                            </ItemTemplate>
                                        </asp:Repeater>
                                    </select>
                                </div>
                                <div class="field-group">
                                    <label for="newModalDepartment" data-translate-key="um.modal.department_label">Departamento</label>
                                    <select id="newModalDepartment">
                                        <option value="" data-translate-key="um.modal.department_placeholder">Seleccionar departamento</option>
                                        <asp:Repeater ID="rptDepartmentsNew" runat="server">
                                            <ItemTemplate>
                                                <option value="<%# Eval("Department_Id") %>"><%# Eval("Department_Code") %> — <%# Eval("Department_Name") %></option>
                                            </ItemTemplate>
                                        </asp:Repeater>
                                    </select>
                                </div>
                            </div>
                            <p class="um-new-hint">
                                <span class="material-icons" style="font-size:15px;vertical-align:middle;">g_mobiledata</span>
                                <span data-translate-key="um.modal.new_google_hint">El usuario iniciará sesión con su cuenta Google @novamex.com</span>
                            </p>
                        </div>

                        <!-- EDIT MODE fields -->
                        <div id="editModeFields" style="display:none">
                            <div class="um-general-grid">
                                <div class="field-group">
                                    <label for="editUsername" data-translate-key="um.modal.username">Username</label>
                                    <input type="text" id="editUsername" placeholder="Nombre de usuario"
                                           maxlength="80" autocomplete="off" />
                                    <span class="field-hint" data-translate-key="um.modal.username_hint">
                                        Nombre visible dentro del portal
                                    </span>
                                </div>
                                <div class="field-group">
                                    <label for="modalRole" data-translate-key="um.modal.role">Rol</label>
                                    <select id="modalRole">
                                        <asp:Repeater ID="rptRoles" runat="server">
                                            <ItemTemplate>
                                                <option value="<%# Eval("Role_Id") %>"><%# Eval("Role_Name") %></option>
                                            </ItemTemplate>
                                        </asp:Repeater>
                                    </select>
                                </div>
                                <div class="field-group">
                                    <label for="modalDepartment" data-translate-key="um.modal.department_label">Departamento</label>
                                    <select id="modalDepartment">
                                        <option value="" data-translate-key="um.modal.department_placeholder">Seleccionar departamento</option>
                                        <asp:Repeater ID="rptDepartments" runat="server">
                                            <ItemTemplate>
                                                <option value="<%# Eval("Department_Id") %>"><%# Eval("Department_Code") %> — <%# Eval("Department_Name") %></option>
                                            </ItemTemplate>
                                        </asp:Repeater>
                                    </select>
                                </div>
                                <div class="field-group">
                                    <div id="statusToggles">
                                        <div class="toggle-row" style="margin-bottom:14px">
                                            <label for="modalActive" data-translate-key="um.modal.active">Usuario Activo</label>
                                            <label class="toggle">
                                                <input type="checkbox" id="modalActive" />
                                                <span class="slider"></span>
                                            </label>
                                        </div>
                                        <div class="toggle-row">
                                            <label for="modalLocked" data-translate-key="um.modal.locked_label">Bloqueado</label>
                                            <label class="toggle">
                                                <input type="checkbox" id="modalLocked" />
                                                <span class="slider"></span>
                                            </label>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div id="editExtraFields">
                                <div class="um-pw-section">
                                    <div class="um-section-title" data-translate-key="um.modal.change_password">
                                        Cambiar Contraseña
                                    </div>
                                    <p class="um-pw-hint" data-translate-key="um.modal.pw_optional">
                                        Dejar en blanco para no modificar la contraseña actual.
                                    </p>
                                    <div class="um-general-grid">
                                        <div class="field-group">
                                            <label for="editPassword1" data-translate-key="um.modal.new_password">Nueva Contraseña</label>
                                            <div class="pw-input-wrap">
                                                <%-- type=text en el HTML inicial — Chrome no activa autofill de credenciales.
                                                     Se convierte a password en JS al abrir el modal. --%>
                                                <input type="text" id="editPassword1" autocomplete="new-password"
                                                       placeholder="Nueva contraseña" maxlength="128"
                                                       onkeypress="return validatePwChar(event)"
                                                       onpaste="return false" oncopy="return false" oncut="return false"
                                                       style="display:none" />
                                                <button type="button" class="btn-pw-toggle"
                                                        onclick="togglePwVisibility('editPassword1', this)" tabindex="-1">
                                                    <span class="material-icons">visibility</span>
                                                </button>
                                            </div>
                                            <span class="pw-allowed-hint" data-translate-key="um.modal.pw_chars">
                                                Solo letras (incluye ñ) y: !"#$%&amp;/()=?¡
                                            </span>
                                        </div>
                                        <div class="field-group">
                                            <label for="editPassword2" data-translate-key="um.modal.confirm_password">Confirmar Contraseña</label>
                                            <div class="pw-input-wrap">
                                                <input type="text" id="editPassword2" autocomplete="new-password"
                                                       placeholder="Repite la contraseña" maxlength="128"
                                                       onkeypress="return validatePwChar(event)"
                                                       oninput="checkPwMatch()"
                                                       onpaste="return false" oncopy="return false" oncut="return false"
                                                       style="display:none" />
                                                <button type="button" class="btn-pw-toggle"
                                                        onclick="togglePwVisibility('editPassword2', this)" tabindex="-1">
                                                    <span class="material-icons">visibility</span>
                                                </button>
                                            </div>
                                            <div id="pwMatchMsg" class="pw-match-msg" style="display:none"></div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                    </div>

                    <!-- Tab: WMS -->
                    <div class="um-tab-panel" id="tab-wms">
                        <p class="um-tab-hint" data-translate-key="um.tab.wms_hint">WMS a los que tiene acceso este usuario.</p>
                        <div class="wms-checklist" id="omsChecklist"></div>
                    </div>

                    <!-- Tab: Locaciones -->
                    <div class="um-tab-panel" id="tab-locations">
                        <div class="um-loc-search-wrap">
                            <span class="material-icons">search</span>
                            <input type="text" id="locSearch" class="um-loc-search"
                                   data-translate-key="um.tab.locations_search"
                                   placeholder="Buscar locación..."
                                   oninput="filterLocationChecklist(this.value)" />
                        </div>
                        <p class="um-tab-hint" data-translate-key="um.tab.locations_hint">Locaciones operativas asignadas a este usuario.</p>
                        <div class="wms-checklist" id="locationsChecklist"></div>
                    </div>

                </div>
            </div>

            <!-- Footer -->
            <div class="um-footer">
                <button type="button" class="btn-cancel" onclick="closeModal()"
                        data-translate-key="common.cancel">Cancelar</button>
                <button type="button" class="btn-save" id="btnSave" onclick="saveChanges()"
                        data-translate-key="um.modal.save">Guardar Cambios</button>
            </div>

        </div>
    </div>

    <asp:HiddenField ID="hfUserId" runat="server" Value="0" />

</asp:Content>

<asp:Content ID="AdditionalScripts" ContentPlaceHolderID="AdditionalScripts" runat="server">
    <script>window.PageWebMethodBase = '<%= ResolveUrl("~/Pages/Admin/UserManagement.aspx/") %>';</script>
    <script src='<%= ResolveUrl("~/Scripts/user_management.js") %>'></script>
</asp:Content>
