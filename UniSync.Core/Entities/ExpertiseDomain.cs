using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UniSync.Core.Entities
{
    public class ExpertiseDomain
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        public ICollection<ResearchSubmission> Submissions { get; set; } = new List<ResearchSubmission>();
        public ICollection<SupervisorExpertiseDomain> SupervisorAreas { get; set; } = new List<SupervisorExpertiseDomain>();
    }

    public class SupervisorExpertiseDomain
    {
        [Key]
        public int Id { get; set; }

        public string SupervisorId { get; set; } = null!;
        public ApplicationUser Supervisor { get; set; } = null!;

        public int ExpertiseDomainId { get; set; }
        public ExpertiseDomain ExpertiseDomain { get; set; } = null!;
    }
}
