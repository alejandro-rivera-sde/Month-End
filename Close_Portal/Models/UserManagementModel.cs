using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Close_Portal.Models {
    public class UserManagementModel {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string RoleName { get; set; }
        public int RoleId { get; set; }
        public bool Active { get; set; }
        public bool Locked { get; set; }
        public string LoginType { get; set; }

        // Department
        public int? DepartmentId { get; set; }
        public string DepartmentCode { get; set; }
        public string DepartmentName { get; set; }

        // WMS - lista separada por coma para filtros JS
        public string WmsCodes { get; set; }      // "ELP,MTY"
        public string WmsTagsHtml { get; set; }   // "<span class='wms-tag'>ELP</span>..."

        // Campos calculados para el Repeater
        public string Initials {
            get {
                string name = !string.IsNullOrEmpty(Username) ? Username : Email;
                return name.Length >= 2
                    ? name.Substring(0, 2).ToUpper()
                    : name.Substring(0, 1).ToUpper();
            }
        }

        public string RoleBadge {
            get {
                switch (RoleName?.ToLower()) {
                    case "owner": return "owner";
                    case "administrador": return "admin";
                    case "manager": return "manager";
                    case "regular": return "regular";
                    default: return "regular";
                }
            }
        }

        public string StatusLabel {
            get {
                if (Locked) return "Bloqueado";
                if (!Active) return "Inactivo";
                return "Activo";
            }
        }

        public string StatusBadge {
            get {
                if (Locked) return "locked";
                if (!Active) return "inactive";
                return "active";
            }
        }

        public string LoginTypeLabel {
            get { return LoginType == "Google" ? "Google" : "Estándar"; }
        }

        public string LoginIcon {
            get { return LoginType == "Google" ? "g_mobiledata" : "lock"; }
        }
    }
}