using System;
using System.ComponentModel.DataAnnotations;

namespace UniSync.Core.Entities
{
    public class GlobalSetting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string Value { get; set; } = null!;

        [MaxLength(100)]
        public string? Category { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        
        public string? LastUpdatedBy { get; set; }
    }
}
