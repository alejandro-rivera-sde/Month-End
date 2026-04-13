using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Close_Portal.Models {

    public class UserPhoneModel {
        public int PhoneId { get; set; }
        public string Phone { get; set; }
        public string Extension { get; set; }
    }

    public class UserManagementModel {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public List<UserPhoneModel> Phones { get; set; } = new List<UserPhoneModel>();
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

        // Locations - lista separada por coma para filtro JS de toolbar
        public string LocationNames { get; set; } // "Bodega Norte,Muelle 3"

        // Campos calculados para el Repeater
        public string FullName {
            get {
                var parts = new[] { FirstName?.Trim(), LastName?.Trim() };
                var joined = string.Join(" ", System.Array.FindAll(parts, s => !string.IsNullOrEmpty(s)));
                return string.IsNullOrEmpty(joined) ? Email?.Split('@')[0] ?? "" : joined;
            }
        }

        public string Initials {
            get {
                if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
                    return (FirstName[0].ToString() + LastName[0].ToString()).ToUpper();
                if (!string.IsNullOrEmpty(FirstName))
                    return FirstName.Substring(0, Math.Min(2, FirstName.Length)).ToUpper();
                string name = Email?.Split('@')[0] ?? "?";
                return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
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
