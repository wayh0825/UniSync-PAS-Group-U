using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniSync.Core.Entities
{
    public class SubmissionGroupMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string StudentIdIdentifier { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        [EmailAddress]
        public string Email { get; set; } = null!;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public UniSync.Core.Enums.InvitationStatus Status { get; set; } = UniSync.Core.Enums.InvitationStatus.Pending;

        [Required]
        [ForeignKey("ResearchSubmission")]
        public int ResearchSubmissionId { get; set; }
        public ResearchSubmission ResearchSubmission { get; set; } = null!;
    }
}
