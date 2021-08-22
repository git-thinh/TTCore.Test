using Microsoft.AspNetCore.SignalR;
using PDF2Image.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PDF2Image.Hubs
{
    public class ImageHub : Hub
    {
        public Task SendAsync(ImageMessage file)
        {
            return Clients.All.SendAsync("IMAGE_MESSAGE", file);
        }
    }
}