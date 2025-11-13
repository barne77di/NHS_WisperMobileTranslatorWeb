using System.ComponentModel.DataAnnotations;

namespace WhisperTranslator.Web.Models
{
    public class AdminUserListItem
    {
        public string Id { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool IsAdmin { get; set; }
        public bool IsUser { get; set; }
    }

    public class AdminUserEditVm
    {
        public string? Id { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [DataType(DataType.Password)]
        public string? Password { get; set; } // optional on edit

        public bool RoleAdmin { get; set; }
        public bool RoleUser { get; set; }
    }
}
