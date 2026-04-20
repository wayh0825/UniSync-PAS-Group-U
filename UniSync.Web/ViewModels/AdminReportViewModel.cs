using System;
using System.Collections.Generic;

namespace UniSync.Web.ViewModels
{
    public class AdminReportViewModel
    {
        public DateTime GeneratedAtUtc { get; set; }

        public int TotalUsers { get; set; }
        public int StudentCount { get; set; }
        public int SupervisorCount { get; set; }
        public int AdminCount { get; set; }
        public int LeaderCount { get; set; }

        public int TotalSubmissions { get; set; }
        public int PendingSubmissions { get; set; }
        public int LinkedSubmissions { get; set; }
        public double LinkRatePercent { get; set; }

        public int ExpertiseDomainsCount { get; set; }
        public int LinkInterestEvents { get; set; }

        public List<AdminReportAreaRowViewModel> TopExpertiseDomains { get; set; } = new();
    }

    public class AdminReportAreaRowViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int SubmissionCount { get; set; }
    }
}
