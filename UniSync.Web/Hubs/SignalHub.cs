using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace UniSync.Web.Hubs
{
    [Authorize]
    public class SignalHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // Join a group named after the UserID for private notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, Context.UserIdentifier ?? "anonymous");
            await base.OnConnectedAsync();
        }

        public async Task JoinRoleGroup(string roleName)
        {
            if (Context.User?.IsInRole(roleName) == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, roleName);
            }
        }
    }
}
