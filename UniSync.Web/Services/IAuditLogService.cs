using System.Threading.Tasks;
using UniSync.Core.Entities;

namespace UniSync.Web.Services
{
    public interface IAuditLogService
    {
        Task LogAsync(string action, string actorEmail, string details, string severity = "Information", string? ipAddress = null);
    }
}
