using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UniSync.Web.ViewModels
{
    public class ProfileViewModel
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "University Email")]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public string Initials { get; set; } = "U";
        public bool IsStudent { get; set; }
        public bool IsSupervisor { get; set; }
        
        // Supervisor Specifics
        public string? Biography { get; set; }
        public int MaxSupervisionCapacity { get; set; }

        public IList<string> ExpertiseAreas { get; set; } = new List<string>();
        public int SupervisorLinkedCount { get; set; }
        public int SupervisorApprovedCount { get; set; }
        public int SupervisorInProgressCount { get; set; }
        public int SupervisorCompletedCount { get; set; }
        public int SupervisorPendingDecisionCount { get; set; }
        public int SupervisorUnreadMessages { get; set; }
        public IList<SupervisorProfileProjectItemViewModel> RecentSupervisoredSubmissions { get; set; } = new List<SupervisorProfileProjectItemViewModel>();

        public IList<ProfileMetricViewModel> Metrics { get; set; } = new List<ProfileMetricViewModel>();
    }

    public class SupervisorProfileProjectItemViewModel
    {
        public int SubmissionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ExpertiseDomainName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare(nameof(NewPassword), ErrorMessage = "Password confirmation does not link.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ProfileMetricViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = "0";
        public string Icon { get; set; } = "insights";
        public string SurfaceClass { get; set; } = "bg-slate-50 text-slate-700 border-slate-100";
    }
}