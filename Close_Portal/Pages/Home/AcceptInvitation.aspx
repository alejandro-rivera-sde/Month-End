<%@ Page Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AcceptInvitation.aspx.cs" Inherits="Close_Portal.Pages.AcceptInvitation" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="TitleContent" runat="server">
    Aceptar Invitación - Close Portal
</asp:Content>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <link href='<%= ResolveUrl("~/Styles/AcceptInvitation.css") %>' rel="stylesheet" type="text/css" />
    <link href="https://fonts.googleapis.com/icon?family=Material+Icons" rel="stylesheet" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="ai-wrapper">
        <div class="ai-card">

            <!-- ========== ESTADO: INVÁLIDO ========== -->
            <asp:Panel ID="PanelInvalid" runat="server" Visible="false">
                <div class="ai-header">
                    <div class="ai-header-icon invalid">
                        <span class="material-icons">link_off</span>
                    </div>
                    <h1>Link inválido</h1>
                    <p>Este link de invitación no existe o no es válido.</p>
                </div>
                <div class="ai-body">
                    <p style="color:var(--text-secondary);text-align:center;font-size:14px;margin:0;">
                        Si crees que es un error, contacta a tu administrador.
                    </p>
                </div>
            </asp:Panel>

            <!-- ========== ESTADO: CANCELADO ========== -->
            <asp:Panel ID="PanelCancelled" runat="server" Visible="false">
                <div class="ai-header">
                    <div class="ai-header-icon cancelled">
                        <span class="material-icons">cancel</span>
                    </div>
                    <h1>Invitación cancelada</h1>
                    <p>Esta invitación fue cancelada por el administrador.</p>
                </div>
                <div class="ai-body">
                    <p style="color:var(--text-secondary);text-align:center;font-size:14px;margin:0;">
                        Solicita una nueva invitación si lo necesitas.
                    </p>
                </div>
            </asp:Panel>

            <!-- ========== ESTADO: YA ACEPTADA ========== -->
            <asp:Panel ID="PanelAccepted" runat="server" Visible="false">
                <div class="ai-header">
                    <div class="ai-header-icon accepted">
                        <span class="material-icons">check_circle</span>
                    </div>
                    <h1>Invitación ya aceptada</h1>
                    <p>Tu cuenta ya fue configurada. Inicia sesión normalmente.</p>
                </div>
                <div class="ai-footer">
                    <a href='<%= ResolveUrl("~/Pages/Home/Login.aspx") %>' class="ai-btn-login">
                        <span class="material-icons">login</span>
                        Ir al inicio de sesión
                    </a>
                </div>
            </asp:Panel>

            <!-- ========== ESTADO: PENDIENTE ========== -->
            <asp:Panel ID="PanelPending" runat="server" Visible="false">
                <div class="ai-header">
                    <div class="ai-header-icon pending">
                        <span class="material-icons">mail</span>
                    </div>
                    <h1>Tienes una invitación</h1>
                    <p>Fuiste invitado a Close Portal por <strong><asp:Literal ID="LitInvitedBy" runat="server" /></strong></p>
                </div>

                <div class="ai-body">

                    <!-- Error al aceptar -->
                    <asp:Panel ID="PanelError" runat="server" Visible="false">
                        <div class="ai-error">
                            <span class="material-icons">error_outline</span>
                            <asp:Literal ID="LitError" runat="server" />
                        </div>
                    </asp:Panel>

                    <div class="ai-summary">
                        <div class="ai-summary-row">
                            <span class="material-icons">alternate_email</span>
                            <div>
                                <div class="ai-summary-label">Email</div>
                                <div class="ai-summary-value"><asp:Literal ID="LitEmail" runat="server" /></div>
                            </div>
                        </div>
                        <div class="ai-summary-row">
                            <span class="material-icons">admin_panel_settings</span>
                            <div>
                                <div class="ai-summary-label">Rol asignado</div>
                                <div class="ai-summary-value"><asp:Literal ID="LitRole" runat="server" /></div>
                            </div>
                        </div>
                        <div class="ai-summary-row">
                            <span class="material-icons">warehouse</span>
                            <div>
                                <div class="ai-summary-label">WMS asignados</div>
                                <div class="ai-wms-list"><asp:Literal ID="LitWms" runat="server" /></div>
                            </div>
                        </div>
                    </div>

                    <div class="ai-login-notice">
                        <span class="material-icons">info</span>
                        <span>
                            Al aceptar serás redirigido al inicio de sesión.
                            Usa la cuenta Google <strong><asp:Literal ID="LitEmailNotice" runat="server" /></strong>
                            para completar el registro.
                        </span>
                    </div>

                </div>

                <div class="ai-footer">
                    <asp:Button ID="BtnAccept" runat="server"
                        CssClass="ai-btn-accept"
                        OnClick="BtnAccept_Click"
                        Text="Aceptar e iniciar sesión" />

                    <a href='<%= ResolveUrl("~/Pages/Home/Login.aspx") %>' class="ai-btn-login">
                        <span class="material-icons">arrow_back</span>
                        Cancelar
                    </a>
                </div>
            </asp:Panel>

            <div class="ai-branding">
                Close Portal &mdash; Novamex
            </div>

        </div>
    </div>
</asp:Content>
