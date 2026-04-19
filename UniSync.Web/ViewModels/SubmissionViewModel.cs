using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UniSync.Web.ViewModels
{
    public class SubmissionSubmissionViewModel
    {
        public int? DraftId { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        public int? ExpertiseDomainId { get; set; }

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

        [Range(1, 260)]
        public int? TimelineWeeks { get; set; }

        [MaxLength(4000)]
        public string? ReferencesText { get; set; }

        public bool AgreeBlindRule { get; set; }
        public DateTime? LastSavedAt { get; set; }

        public bool IsGroupProject { get; set; }
        public List<GroupMemberViewModel> GroupMembers { get; set; } = new List<GroupMemberViewModel>();
    }
}
