using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniSync.Core.Entities
{
    public class DraftSubmission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Student")]
        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser Student { get; set; } = null!;

        [MaxLength(200)]
        public string? Title { get; set; }

        public int? ExpertiseDomainId { get; set; }
        public ExpertiseDomain? ExpertiseDomain { get; set; }

        [MaxLength(2000)]
        public string? ExecutiveSummary { get; set; }

        [MaxLength(2000)]
        public string? ProblemStatement { get; set; }

        [MaxLength(2000)]
        public string? Objectives { get; set; }

        [MaxLength(4000)]
        public string? Methodology { get; set; }

        [MaxLength(2000)]
        public string? ExpectedOutcomes { get; set; }

        [MaxLength(500)]
        public string? TechStack { get; set; }

        [MaxLength(300)]
        public string? Keywords { get; set; }

        [MaxLength(2000)]
        public string? EthicsConsiderations { get; set; }

        public int? TimelineWeeks { get; set; }

        [MaxLength(4000)]
        public string? ReferencesText { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsGroupProject { get; set; } = false;

        [MaxLength(4000)]
        public string? GroupMembersText { get; set; } // JSON or comma-separated for drafting
    }
}
