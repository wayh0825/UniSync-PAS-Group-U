using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniSync.Core.Entities
{
    public enum ReallocationStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public class ReallocationRequest
    {
        [Key]
        public int Id { get; set; }

        public int ResearchSubmissionId { get; set; }
        [ForeignKey("ResearchSubmissionId")]
        public ResearchSubmission Submission { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        public ReallocationStatus Status { get; set; } = ReallocationStatus.Pending;

        public string? ResponseNote { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        [Required]
        public string RequestedById { get; set; } = string.Empty;
        [ForeignKey("RequestedById")]
        public ApplicationUser RequestedBy { get; set; } = null!;
    }
}
