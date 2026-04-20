using System;
using System.Collections.Generic;
using UniSync.Core.Enums;
using UniSync.Core.Entities;

namespace UniSync.Web.ViewModels
{
    public class SubmissionLinkViewModel
    {
        public int SubmissionId { get; set; }
        public string AnonymousId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ExpertiseDomainName { get; set; } = string.Empty;
        public DateTime SubmissionDate { get; set; }
        public string ExecutiveSummary { get; set; } = string.Empty;
        public string TechStack { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public int LinkScore { get; set; }
        public int InterestCount { get; set; }
        public bool HasExpressedInterest { get; set; }
        public string IconEmoji { get; set; } = "📋";
        public string StatusBadge { get; set; } = "NEW PROPOSAL";
    }

    public class AvailableProjectsViewModel
    {
        public List<SubmissionLinkViewModel> Submissions { get; set; } = new();
        public int TotalSubmissionCount { get; set; }
        public int LinkedSubmissionCount { get; set; }
        public List<(string AreaName, int Count)> SubmissionsByArea { get; set; } = new();
    }

    public class SubmissionDetailViewModel : SubmissionLinkViewModel
    {
        public string ProblemStatement { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public string Methodology { get; set; } = string.Empty;
        public string ExpectedOutcomes { get; set; } = string.Empty;
        public string EthicsConsiderations { get; set; } = string.Empty;
        public string ReferencesText { get; set; } = string.Empty;
        public int TimelineWeeks { get; set; }
        public List<string> LinkInsights { get; set; } = new();
    }

    public class SupervisorProfileSummaryViewModel
    {
        public string SupervisorId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public List<string> ExpertiseDomains { get; set; } = new();
        public int TotalSupervisoredSubmissions { get; set; }
        public int AverageLinkScore { get; set; }
        public int TotalInterestsExpressed { get; set; }
    }

    public class LinkedProjectDetailViewModel
    {
        public int SubmissionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ExpertiseDomainName { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string StudentInitials { get; set; } = string.Empty;
        public DateTime LinkedDate { get; set; }
        public int LinkScore { get; set; }
        public string ExecutiveSummary { get; set; } = string.Empty;
        public string TechStack { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public DateTime SubmissionDate { get; set; }
    }

    public class SupervisorLinkedProjectsPageViewModel
    {
        public string Search { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = "all";
        public string ProgressFilter { get; set; } = "all";
        public string SortBy { get; set; } = "newest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;

        public int TotalCount { get; set; }
        public int FilteredCount { get; set; }
        public int ActiveCount { get; set; }
        public int AttentionCount { get; set; }
        public int CompletedCount { get; set; }
        public int TotalPages { get; set; }

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.Equals(StatusFilter, "all", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(ProgressFilter, "all", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(SortBy, "newest", StringComparison.OrdinalIgnoreCase);

        public List<SupervisorLinkedProjectCardViewModel> Projects { get; set; } = new();
    }

    public class SupervisorLinkedProjectCardViewModel
    {
        public int SubmissionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ExpertiseDomainName { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string StudentInitial { get; set; } = "S";
        public SubmissionStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string StatusClass { get; set; } = "bg-slate-100 text-slate-700";
        public int ProgressPercent { get; set; }
        public string ProgressLabel { get; set; } = "Open";
        public DateTime SubmissionDate { get; set; }
        public DateTime LinkedDate { get; set; }
        public string ExecutiveSummary { get; set; } = string.Empty;
        public string TechStack { get; set; } = string.Empty;
        public bool IsGroupProject { get; set; }
        public List<SubmissionGroupMember> GroupMembers { get; set; } = new();
    }
}
