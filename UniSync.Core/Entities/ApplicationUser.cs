using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace UniSync.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;
        public int MaxSupervisionCapacity { get; set; } = 30; // Default threshold
        public string? Biography { get; set; }

        public ICollection<ResearchSubmission> Submissions { get; set; } = new List<ResearchSubmission>();
        public ICollection<DraftSubmission> DraftSubmissions { get; set; } = new List<DraftSubmission>();
        public ICollection<ResearchSubmission> SupervisedSubmissions { get; set; } = new List<ResearchSubmission>();
        public ICollection<LinkRequest> LinkRequests { get; set; } = new List<LinkRequest>();
        public ICollection<SupervisorExpertiseDomain> SupervisorAreas { get; set; } = new List<SupervisorExpertiseDomain>();
        public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
        public ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
        public ICollection<AppNotification> SentNotifications { get; set; } = new List<AppNotification>();
        public ICollection<AppNotification> ReceivedNotifications { get; set; } = new List<AppNotification>();
    }
}
