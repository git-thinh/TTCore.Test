using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Redis
{
    public class RedisHub : Hub
    {
        public Task Send(string message)
        {
            return Clients.All.SendAsync("MESSAGE_REDIS", message);
        }
    }
}
