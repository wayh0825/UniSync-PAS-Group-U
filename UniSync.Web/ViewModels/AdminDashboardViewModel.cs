using System.Collections.Generic;
using UniSync.Core.Entities;

namespace UniSync.Web.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalSupervisors { get; set; }
        public int ActiveSubmissions { get; set; }
        public int SuccessfulLinks { get; set; }
        
        public int TotalPlatformUsers { get; set; }
        public int TotalExpertiseDomains { get; set; }
        public int LinkRatePercent { get; set; }
        public int SubmissionLoadPercent { get; set; }
        public int StudentRatioPercent { get; set; }
        
        public bool DatabaseConnected { get; set; }
        public string LastHealthCheck { get; set; } = string.Empty;
        public bool WebAppActive { get; set; }
        public bool NotificationServiceActive { get; set; }
        public bool PlagiarismApiActive { get; set; }
        
        public List<string> ApiResponseLabels { get; set; } = new List<string>();
        public List<int> ApiResponseValues { get; set; } = new List<int>();
        public double ApiAveragePerWindow { get; set; }
        
        public List<ResearchSubmission> RecentSubmissions { get; set; } = new List<ResearchSubmission>();
        
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<int> ChartValues { get; set; } = new List<int>();
        
        public IEnumerable<ResearchSubmission> Submissions { get; set; } = new List<ResearchSubmission>();
        public IEnumerable<ExpertiseDomain> ExpertiseDomains { get; set; } = new List<ExpertiseDomain>();
    }
}
