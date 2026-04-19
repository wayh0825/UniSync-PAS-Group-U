using System;
using System.Collections.Generic;
using UniSync.Core.Entities;

namespace UniSync.Web.ViewModels
{
    public class StudentSubmissionsTrackerViewModel
    {
        public List<ResearchSubmission> Submissions { get; set; } = new();
        public ResearchSubmission? SelectedSubmission { get; set; }

        public int PendingCount { get; set; }
        public int ReviewCount { get; set; }
        public int LinkedCount { get; set; }
        public int CompletedCount { get; set; }

        public int ProgressPercent { get; set; }
        public string ReviewLabel { get; set; } = "No Submission";
        public string ReviewState { get; set; } = "No Submission";

        public string SubmittedDateLabel { get; set; } = "N/A";
        public string ReviewDateLabel { get; set; } = "Pending";
        public string BoardDateLabel { get; set; } = "Pending";
        public string FinalDateLabel { get; set; } = "Pending";

        public bool IsSubmittedCompleted { get; set; }
        public bool IsReviewCompleted { get; set; }
        public bool IsReviewActive { get; set; }
        public bool IsBoardCompleted { get; set; }
        public bool IsFinalCompleted { get; set; }
    }
}
