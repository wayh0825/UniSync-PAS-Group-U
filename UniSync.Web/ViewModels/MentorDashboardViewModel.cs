using System.Collections.Generic;
using UniSync.Core.Entities;

namespace UniSync.Web.ViewModels
{
    public class SupervisorDashboardViewModel
    {
        public string SupervisorName { get; set; } = "Supervisor";
        public List<ExpertiseDomain> ExpertiseDomains { get; set; } = new();
        public HashSet<int> SelectedAreaIds { get; set; } = new();
        public List<ResearchSubmission> RecommendedProjects { get; set; } = new();
        public int AvailableProjectsCount { get; set; }
        public int MyLinksCount { get; set; }
        public int ApprovedCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public int PendingChangesCount { get; set; }
        public int UnreadMessagesCount { get; set; }
    }
}
