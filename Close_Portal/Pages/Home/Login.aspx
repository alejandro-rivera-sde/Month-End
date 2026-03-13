<%@ Page Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Close_Portal.Pages.Login" Async="true" %>

<asp:Content ID="TitleContent" ContentPlaceHolderID="TitleContent" runat="server">
    Login - Close Portal
</asp:Content>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://accounts.google.com/gsi/client" async="async" defer="defer"></script>
    <link href='<%= ResolveUrl("~/Styles/Login.css") %>' rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="login-page-wrapper">
        <div class="login-card">
            <!-- Header -->
            <div class="login-header-section">
                <h1>Close Portal</h1>
                <p data-translate-key="login.subtitle">Sistema de Gestión de Cierre de Operaciones</p>
            </div>
            
            <!-- Tab Toggle (siempre visible arriba) -->
            <div class="login-tabs-section">
                <button type="button" class="tab-btn" id="toggleLoginMode">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
                        <path d="M8 8a3 3 0 100-6 3 3 0 000 6zm2-3a2 2 0 11-4 0 2 2 0 014 0zm4 8c0 1-1 1-1 1H3s-1 0-1-1 1-4 6-4 6 3 6 4zm-1-.004c-.001-.246-.154-.986-.832-1.664C11.516 10.68 10.289 10 8 10c-2.29 0-3.516.68-4.168 1.332-.678.678-.83 1.418-.832 1.664h10z"/>
                    </svg>
                    <span id="tabText">Login</span>
                </button>
            </div>
            
            <!-- Login Form Section -->
            <div class="login-form-section">
                
                <!-- ========== GOOGLE LOGIN (Por defecto visible) ========== -->
                <div id="googleLoginSection">
                    <div class="google-container" id="google-btn-container"></div>
                </div>
                
                <!-- ========== STANDARD LOGIN (Oculto por defecto) ========== -->
                <div id="standardLoginSection" style="display: none;">
                    <div class="form-field">
                        <label for="email">Email</label>
                        <input 
                            type="email" 
                            id="emailStandard" 
                            placeholder="email@novamex.com"
                        />
                    </div>
                    
                    <div class="form-field">
                        <label for="password">Contraseña</label>
                        <input 
                            type="password" 
                            id="passwordStandard" 
                            placeholder="Tu contraseña"
                        />
                    </div>
                    
                    <button type="button" class="btn-submit" id="btnStandardLogin">
                        Iniciar Sesión
                    </button>
                </div>
                
                <!-- Loading & Messages -->
                <div class="loading-indicator" id="loading"></div>
                <div class="error-message" id="errorMessage"></div>
                <div class="success-message" id="successMessage"></div>
                
                <!-- Footer -->
                <div class="login-footer">
                    <p>¿Necesitas una cuenta? <a href="mailto:admin@novamex.com">Contacta a tu administrador</a></p>
                </div>
            </div>
        </div>
    </div>
    <input type="hidden" id="pendingInvToken" value="<%= PendingInvToken %>" />
</asp:Content>

<asp:Content ID="ScriptsContent" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script src='<%= ResolveUrl("~/Scripts/login.js") %>'></script>
</asp:Content>
