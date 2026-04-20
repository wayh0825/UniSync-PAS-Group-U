using System;
using System.Threading.Tasks;
using UniSync.Core.Data;
using UniSync.Core.Entities;

namespace UniSync.Web.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _context;

        public AuditLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string action, string actorEmail, string details, string severity = "Information", string? ipAddress = null)
        {
            var log = new AuditLog
            {
                Action = action,
                ActorEmail = actorEmail,
                Details = details,
                Severity = severity,
                IPAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
