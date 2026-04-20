using System.ComponentModel.DataAnnotations;

namespace UniSync.Web.ViewModels
{
    public class GroupMemberViewModel
    {
        public int Id { get; set; } // Needed for edit mode

        [Required(ErrorMessage = "Student ID is required")]
        [MaxLength(100)]
        public string StudentIdIdentifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required")]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;
    }
}
