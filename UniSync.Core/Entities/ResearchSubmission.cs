using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniSync.Core.Enums;

namespace UniSync.Core.Entities
{
    public class ResearchSubmission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string ExecutiveSummary { get; set; } = null!;

        [MaxLength(2000)]
        public string? ProblemStatement { get; set; }

        [MaxLength(2000)]
        public string? Objectives { get; set; }

        [MaxLength(4000)]
        public string? Methodology { get; set; }

        [MaxLength(2000)]
        public string? ExpectedOutcomes { get; set; }

        [Required]
        [MaxLength(500)]
        public string TechStack { get; set; } = null!;

        [MaxLength(300)]
        public string? Keywords { get; set; }

        [MaxLength(2000)]
        public string? EthicsConsiderations { get; set; }

        public int? TimelineWeeks { get; set; }

        [MaxLength(4000)]
        public string? ReferencesText { get; set; }

        public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

        public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;

        [MaxLength(2000)]
        public string? SupervisorFeedback { get; set; }

        public DateTime? FeedbackDate { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public double? MatchScore { get; set; }

        [Required]
        [ForeignKey("ExpertiseDomain")]
        public int ExpertiseDomainId { get; set; }
        public ExpertiseDomain ExpertiseDomain { get; set; } = null!;

        [Required]
        [ForeignKey("Submitter")]
        public string SubmitterId { get; set; } = null!;
        public ApplicationUser Submitter { get; set; } = null!;

        [ForeignKey("LinkedSupervisor")]
        public string? LinkedSupervisorId { get; set; }
        public ApplicationUser? LinkedSupervisor { get; set; }

        public ICollection<LinkRequest> LinkRequests { get; set; } = new List<LinkRequest>();

        public bool IsGroupProject { get; set; } = false;

        public ICollection<SubmissionGroupMember> GroupMembers { get; set; } = new List<SubmissionGroupMember>();
    }
}
