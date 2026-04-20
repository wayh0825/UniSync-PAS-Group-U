using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniSync.Core.Entities
{
    public class LinkRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Submission")]
        public int SubmissionId { get; set; }
        public ResearchSubmission Submission { get; set; } = null!;

        [Required]
        [ForeignKey("Supervisor")]
        public string SupervisorId { get; set; } = null!;
        public ApplicationUser Supervisor { get; set; } = null!;

        public DateTime ExpressedAt { get; set; } = DateTime.UtcNow;
    }
}
