using System;
using System.Web.UI;

namespace Close_Portal.Controls {

    /// <summary>
    /// Widget flotante de Soporte IT.
    /// Este control es puramente de marcado; toda la lógica está en it_chat.js.
    /// No requiere ningún procesamiento server-side en sí mismo.
    /// </summary>
    public partial class ChatWidget : UserControl {

        protected void Page_Load(object sender, EventArgs e) {
            // Sin lógica de servidor — el widget es completamente client-side.
        }
    }
}
