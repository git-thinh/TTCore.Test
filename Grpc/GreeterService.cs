using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Helloworld;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grpc
{
    public class GreeterImpl : Greeter.GreeterBase
    {
        // Server side handler of the SayHello RPC
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
        }
    }

    public class GreeterService : BackgroundService
    {
        readonly ILogger _logger;
        readonly string ServiceName = nameof(GreeterService);
        public GreeterService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(GetType());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{ServiceName} is starting.");
            stoppingToken.Register(() => _logger.LogInformation($"{ServiceName} background task is stopping."));

            var server = new Server()
            {
                Services = { Helloworld.Greeter.BindService(new GreeterImpl()) },
                Ports = { new ServerPort("localhost", 101010, ServerCredentials.Insecure) }
            };
            server.Start();

            await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogDebug($"{ServiceName} is stopping.");
        }
    }
}