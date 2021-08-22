using Grpc.Core;
using Helloworld;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ClearScript.Controllers
{
    public class TestController: Controller
    {
        //readonly ISearcher _searcher;
        //public TestController(ISearcher search)
        //{
        //    _searcher = search;
        //}


        [HttpGet("/grpc")]
        public string grpc(string name)
        {
            string s = string.Empty;
            try
            {
                var channel = new Channel("127.0.0.1:101010", ChannelCredentials.Insecure);
                var client = new Greeter.GreeterClient(channel);
                var reply = client.SayHello(new HelloRequest { Name = name });
                s = reply.Message;
                channel.ShutdownAsync().Wait();
            }
            catch (Exception ex)
            {
            }
            return s;
        }
    }
}
