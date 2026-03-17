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
            <p data-translate-key="um.subtitle">Administra usuarios, roles y accesos a OMS</p>
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
            <input type="text" id="searchInput" 
                   data-translate-key="um.search.placeholder"
                   placeholder="Buscar por nombre, email..." 
                   oninput="filterTable()" />
        </div>
        <select class="filter-select" id="filterRole" onchange="filterTable()">
            <option value="" data-translate-key="um.filter.all_roles">Todos los roles</option>
            <option value="Owner">Owner</option>
            <option value="Administrador">Administrador</option>
            <option value="Manager">Manager</option>
            <option value="Regular">Regular</option>
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
                                    <button type="button" class="btn-icon edit" 
                                            onclick="openModalEdit(<%# Eval("UserId") %>)" 
                                            title="Editar usuario">
                                        <span class="material-icons">edit</span>
                                    </button>
                                    <button type="button" class="btn-icon delete" 
                                            onclick="confirmToggleActive(<%# Eval("UserId") %>, <%# Eval("Active").ToString().ToLower() %>)"
                                            title='<%# (bool)Eval("Active") ? "Desactivar usuario" : "Activar usuario" %>'>
                                        <span class="material-icons"><%# (bool)Eval("Active") ? "person_off" : "person" %></span>
                                    </button>
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
            <div class="um-header">
                <h3 id="modalTitle" data-translate-key="um.modal.edit_title">Editar Usuario</h3>
                <button type="button" class="btn-icon" onclick="closeModal()">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="um-body">

                <!-- ── COLUMNA IZQUIERDA: Rol / Estado / OMS / Locaciones ── -->
                <div class="um-col-left">

                    <div class="user-info-card" id="userInfoCard">
                        <div class="avatar" id="modalAvatar"></div>
                        <div>
                            <div class="user-name-text" id="modalUserName"></div>
                            <div class="user-email" id="modalUserEmail"></div>
                        </div>
                    </div>

                    <div id="newUserFields" style="display:none">
                        <div class="field-group">
                            <label data-translate-key="um.modal.email">Email (@novamex.com)</label>
                            <input type="email" id="newEmail" placeholder="usuario@novamex.com" />
                        </div>
                        <div class="field-group">
                            <label data-translate-key="um.modal.username">Username</label>
                            <input type="text" id="newUsername" placeholder="Nombre de usuario" />
                        </div>
                        <div class="field-group">
                            <label data-translate-key="um.modal.login_type">Tipo de Login</label>
                            <select id="newLoginType" onchange="togglePasswordField()">
                                <option value="Standard" data-translate-key="um.modal.login_standard">Estándar (contraseña)</option>
                                <option value="Google" data-translate-key="um.modal.login_google">Google OAuth</option>
                            </select>
                        </div>
                        <div class="field-group" id="passwordField">
                            <label data-translate-key="um.modal.password">Contraseña</label>
                            <input type="password" id="newPassword" placeholder="Contraseña temporal" />
                        </div>
                    </div>

                    <div class="field-group">
                        <label data-translate-key="um.modal.role">Rol</label>
                        <select id="modalRole">
                            <asp:Repeater ID="rptRoles" runat="server">
                                <ItemTemplate>
                                    <option value="<%# Eval("Role_Id") %>"><%# Eval("Role_Name") %></option>
                                </ItemTemplate>
                            </asp:Repeater>
                        </select>
                    </div>

                    <div class="field-group">
                        <label data-translate-key="um.modal.department_label">Departamento</label>
                        <select id="modalDepartment">
                            <option value="" data-translate-key="um.modal.department_placeholder">Seleccionar departamento</option>
                            <asp:Repeater ID="rptDepartments" runat="server">
                                <ItemTemplate>
                                    <option value="<%# Eval("Department_Id") %>"><%# Eval("Department_Code") %> — <%# Eval("Department_Name") %></option>
                                </ItemTemplate>
                            </asp:Repeater>
                        </select>
                    </div>

                    <div id="statusToggles">
                        <div class="field-group">
                            <div class="toggle-row">
                                <label data-translate-key="um.modal.active">Usuario Activo</label>
                                <label class="toggle">
                                    <input type="checkbox" id="modalActive" />
                                    <span class="slider"></span>
                                </label>
                            </div>
                        </div>
                        <div class="field-group">
                            <div class="toggle-row">
                                <label data-translate-key="um.modal.locked_label">Bloqueado</label>
                                <label class="toggle">
                                    <input type="checkbox" id="modalLocked" />
                                    <span class="slider"></span>
                                </label>
                            </div>
                        </div>
                    </div>

                    <!-- OMS: scope de visibilidad del usuario -->
                    <div class="field-divider" data-translate-key="um.modal.oms">OMS Asignados</div>
                    <div class="wms-checklist" id="omsChecklist"></div>

                    <!-- Locaciones: asignación operativa, filtrada por OMS seleccionados -->
                    <div class="field-divider" data-translate-key="um.modal.locations">Locaciones Operativas</div>
                    <div class="wms-checklist" id="locationsChecklist"></div>

                </div>

                <!-- ── COLUMNA DERECHA: Username + Contraseña ── -->
                <div class="um-col-right" id="editExtraFields" style="display:none">

                    <div class="um-col-right-title" data-translate-key="um.modal.account_info">
                        Información de Cuenta
                    </div>

                    <div class="field-group">
                        <label data-translate-key="um.modal.username">Username</label>
                        <input type="text" id="editUsername" placeholder="Nombre de usuario"
                               maxlength="80" autocomplete="off" />
                        <span class="field-hint" data-translate-key="um.modal.username_hint">
                            Nombre visible dentro del portal
                        </span>
                    </div>

                    <div class="um-pw-section">
                        <div class="um-col-right-title" data-translate-key="um.modal.change_password">
                            Cambiar Contraseña
                        </div>
                        <p class="um-pw-hint" data-translate-key="um.modal.pw_optional">
                            Dejar en blanco para no modificar la contraseña actual.
                        </p>

                        <div class="field-group">
                            <label data-translate-key="um.modal.new_password">Nueva Contraseña</label>
                            <div class="pw-input-wrap">
                                <input type="password" id="editPassword1"
                                       autocomplete="off"
                                       placeholder="Nueva contraseña"
                                       maxlength="128"
                                       onkeypress="return validatePwChar(event)"
                                       onpaste="return false"
                                       oncopy="return false"
                                       oncut="return false" />
                                <button type="button" class="btn-pw-toggle"
                                        onclick="togglePwVisibility('editPassword1', this)"
                                        tabindex="-1">
                                    <span class="material-icons">visibility</span>
                                </button>
                            </div>
                            <span class="pw-allowed-hint" data-translate-key="um.modal.pw_chars">
                                Solo letras (incluye ñ) y: !"#$%&amp;/()=?¡
                            </span>
                        </div>

                        <div class="field-group">
                            <label data-translate-key="um.modal.confirm_password">Confirmar Contraseña</label>
                            <div class="pw-input-wrap">
                                <input type="password" id="editPassword2"
                                       autocomplete="off"
                                       placeholder="Repite la contraseña"
                                       maxlength="128"
                                       onkeypress="return validatePwChar(event)"
                                       oninput="checkPwMatch()"
                                       onpaste="return false"
                                       oncopy="return false"
                                       oncut="return false" />
                                <button type="button" class="btn-pw-toggle"
                                        onclick="togglePwVisibility('editPassword2', this)"
                                        tabindex="-1">
                                    <span class="material-icons">visibility</span>
                                </button>
                            </div>
                            <div id="pwMatchMsg" class="pw-match-msg" style="display:none"></div>
                        </div>
                    </div>

                </div>

            </div>
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
    <script src='<%= ResolveUrl("~/Scripts/user_management.js") %>'></script>
</asp:Content>
