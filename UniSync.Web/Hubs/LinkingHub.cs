using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UniSync.Web.Hubs
{
    [Authorize(Roles = "Supervisor")]
    public class LinkingHub : Hub
    {
    }
}
