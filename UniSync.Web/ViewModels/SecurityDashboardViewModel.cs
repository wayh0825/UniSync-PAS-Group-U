using System;
using System.Collections.Generic;
using UniSync.Core.Entities;

namespace UniSync.Web.ViewModels
{
    public class SecurityDashboardViewModel
    {
        public bool TwoFactorForced { get; set; }
        public int SessionTimeoutMinutes { get; set; }
        public bool PasswordComplexityEnabled { get; set; }
        public bool MaintenanceModeEnabled { get; set; }
        public string MaintModeSummary { get; set; } = string.Empty;
        public string DataEncyptionSummary { get; set; } = string.Empty;
        public List<SystemApiKey> ApiKeys { get; set; } = new List<SystemApiKey>();
    }
}
