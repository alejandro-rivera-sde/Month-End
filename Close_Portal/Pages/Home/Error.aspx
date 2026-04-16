<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="Close_Portal.Pages.Error" %>
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title><%: ErrorTitle %> - Close Portal</title>
    <link href="https://fonts.googleapis.com/icon?family=Material+Icons" rel="stylesheet" />
    <link href='<%= ResolveUrl("~/Styles/Error.css") %>' rel="stylesheet" type="text/css" />
</head>
<body>
    <div class="err-card">

        <div class="err-logo">Close Portal</div>

        <div class="err-icon-wrap">
            <span class="material-icons"><%: ErrorIcon %></span>
        </div>

        <h1 class="err-title"><%: ErrorTitle %></h1>
        <p class="err-message"><%: ErrorMessage %></p>

        <a href='<%= ResolveUrl("~/Pages/Home/Logout.aspx") %>' class="err-btn">
            <span class="material-icons">logout</span>
            <span data-translate-key="common.logout">Cerrar sesión</span>
        </a>

        <p class="err-countdown" id="errCountdown">
            <span data-translate-key="error.countdown_prefix">Cerrando sesión en</span>
            <span id="errSec">8</span>
            <span data-translate-key="error.countdown_suffix">segundos...</span>
        </p>

    </div>

    <script src='<%= ResolveUrl("~/Scripts/translations.js") %>'></script>
    <script src='<%= ResolveUrl("~/Scripts/i18n.js") %>'></script>

    <script>
        // Auto-redirect a Logout para que limpie la sesión correctamente
        var sec = 8;
        var logoutUrl = '<%= ResolveUrl("~/Pages/Home/Logout.aspx") %>';

        var timer = setInterval(function () {
            sec--;
            var el = document.getElementById('errSec');
            if (el) el.textContent = sec;
            if (sec <= 0) {
                clearInterval(timer);
                window.location.href = logoutUrl;
            }
        }, 1000);
    </script>
</body>
</html>
