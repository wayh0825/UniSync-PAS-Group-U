using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Web.Hubs;
using UniSync.Core.Enums;
using System;

namespace UniSync.Web.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<SignalHub> _hubContext;

        public NotificationService(ApplicationDbContext context, IHubContext<SignalHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task SendNotificationAsync(string recipientId, string title, string message, AlertCategory category, string? actionUrl = null, string? senderId = null)
        {
            // 1. Save to Database
            var notification = new AppNotification
            {
                RecipientId = recipientId,
                SenderId = senderId,
                Title = title,
                Message = message,
                Type = category,
                ActionUrl = actionUrl,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.AppNotifications.Add(notification);
            await _context.SaveChangesAsync();

            // 2. Broadcast via SignalR to the specific user group (UserId)
            await _hubContext.Clients.Group(recipientId).SendAsync("ReceiveSignal", category.ToString(), title);
        }

        public async Task SendBroadcastAsync(string roleName, string title, string message, AlertCategory category)
        {
            // For now, we broadcast to the SignalR role group
            // In a real scenario, we might also create individual Persistent notifications in DB
            await _hubContext.Clients.Group(roleName).SendAsync("ReceiveSignal", "Broadcast", title);
        }
    }
}
