using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Redis
{
    public enum REDIS_TYPE
    {
        WRITE,
        PUBSUB,
        READ1,
        READ2,
        READ3
    }

    public class RedisService : BackgroundService
    {
        readonly IHubContext<RedisHub> _appHubContext;
        readonly ILogger _logger;
        readonly string ServiceName = nameof(RedisService);

        readonly RedisSetting _redisSetting;
        readonly ConnectionMultiplexer _conMaster = null;
        readonly ConnectionMultiplexer _conRead1 = null;
        readonly ConnectionMultiplexer _conRead2 = null;
        readonly ConnectionMultiplexer _conRead3 = null;

        public IDatabase GetDB(REDIS_TYPE type, int dbIndex)
        {
            IDatabase db = null;
            switch (type)
            {
                case REDIS_TYPE.WRITE:
                case REDIS_TYPE.PUBSUB:
                    db = _conMaster.GetDatabase(dbIndex);
                    break;
                case REDIS_TYPE.READ1:
                    db = _conRead1.GetDatabase(dbIndex);
                    break;
                case REDIS_TYPE.READ2:
                    db = _conRead2.GetDatabase(dbIndex);
                    break;
                case REDIS_TYPE.READ3:
                    db = _conRead3.GetDatabase(dbIndex);
                    break;
            }
            return db;
        }

        public IServer GetServer(REDIS_TYPE type)
        {
            EndPoint[] eps = null;
            IServer server = null;
            switch (type)
            {
                case REDIS_TYPE.WRITE:
                case REDIS_TYPE.PUBSUB:
                    eps = _conMaster.GetEndPoints();
                    if(eps.Length > 0) server = _conMaster.GetServer(eps[0]);
                    break;
                case REDIS_TYPE.READ1:
                    eps = _conRead1.GetEndPoints();
                    if (eps.Length > 0) server = _conRead1.GetServer(eps[0]);
                    break;
                case REDIS_TYPE.READ2:
                    eps = _conRead2.GetEndPoints();
                    if (eps.Length > 0) server = _conRead2.GetServer(eps[0]);
                    break;
                case REDIS_TYPE.READ3:
                    eps = _conRead3.GetEndPoints();
                    if (eps.Length > 0) server = _conRead3.GetServer(eps[0]);
                    break;
            }
            return server;            
        }

        public ISubscriber GetSubscriber(REDIS_TYPE type)
        {
            ISubscriber sub = null;
            switch (type)
            {
                case REDIS_TYPE.WRITE:
                case REDIS_TYPE.PUBSUB:
                    sub = _conMaster.GetSubscriber();
                    break;
                case REDIS_TYPE.READ1:
                    sub = _conRead1.GetSubscriber();
                    break;
                case REDIS_TYPE.READ2:
                    sub = _conRead2.GetSubscriber();
                    break;
                case REDIS_TYPE.READ3:
                    sub = _conRead3.GetSubscriber();
                    break;
            }
            return sub;            
        }

        public RedisService(
            RedisSetting redisSetting,
            IHubContext<RedisHub> hubContext,
            ILoggerFactory loggerFactory)
        {
            _redisSetting = redisSetting;
            _appHubContext = hubContext;
            _logger = loggerFactory.CreateLogger(GetType());

            string strMaster = string.Format(_redisSetting.ConnectString, _redisSetting.PortMaster);
            _conMaster = ConnectionMultiplexer.Connect(strMaster);
            string strRead1 = string.Format(_redisSetting.ConnectString, _redisSetting.PortRead1);
            _conRead1 = ConnectionMultiplexer.Connect(strRead1);
            string strRead2 = string.Format(_redisSetting.ConnectString, _redisSetting.PortRead2);
            _conRead2 = ConnectionMultiplexer.Connect(strRead2);
            string strRead3 = string.Format(_redisSetting.ConnectString, _redisSetting.PortRead3);
            _conRead3 = ConnectionMultiplexer.Connect(strRead3);

            //var config = new ConfigurationOptions
            //{
            //    AbortOnConnectFail = false
            //};
            //config.EndPoints.Add(IPAddress.Loopback, 0);
            //config.SetDefaultPorts();
            //connection = ConnectionMultiplexer.Connect(config);
            //connection.ConnectionFailed += (_, e) =>
            //{
            //    Console.WriteLine("Connection to Redis failed.");
            //};
            //if (!connection.IsConnected)
            //{
            //    Console.WriteLine("Did not connect to Redis.");
            //}
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{ServiceName} is starting.");
            stoppingToken.Register(() => _logger.LogInformation($"{ServiceName} background task is stopping."));

            _subcriberNotify(REDIS_TYPE.PUBSUB);
            //_subcriberNotify(REDIS_TYPE.READ1);

            await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogDebug($"{ServiceName} is stopping.");
        }

        void _subcriberNotify(REDIS_TYPE type)
        {
            // https://redis.io/topics/notifications
            // $ redis-cli      > config set notify-keyspace-events KEA
            // $ redis-cli--csv > psubscribe '__key*__:*' "__key*__:*" "__keyevent@*__:*"

            try
            {
                int len = "__keyevent@".Length;
                var sub = GetSubscriber(type);
                var subChannel = sub.Subscribe("__keyevent@*__:*");
                subChannel.OnMessage(async (msg) =>
                {
                    string s = msg.ToString().Substring(len), m = msg.Message.ToString();
                    await _appHubContext.Clients.All.SendAsync("MESSAGE_REDIS", s);
                    //await _appHubContext.Clients.Group(HubsConstants.APP_HUB).ReceivePost(notification);
                });
            }
            catch(Exception ex) { 
            }
        }
    }
}