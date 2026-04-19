using System.Threading.Tasks;
using UniSync.Core.Entities;
using UniSync.Core.Enums;

namespace UniSync.Web.Services
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string recipientId, string title, string message, AlertCategory category, string? actionUrl = null, string? senderId = null);
        Task SendBroadcastAsync(string roleName, string title, string message, AlertCategory category);
    }
}
