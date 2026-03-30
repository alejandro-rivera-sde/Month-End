using Close_Portal.Core;
using System;

namespace Close_Portal.Pages.IT {
    public partial class Processes : SecurePage {
        protected override int RequiredRoleId => RoleLevel.Owner;
        protected void Page_Load(object sender, EventArgs e) { }
    }
}