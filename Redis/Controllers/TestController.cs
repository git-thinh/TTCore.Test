using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Redis.Controllers
{
    public class TestController : Controller
    {
        readonly RedisService _redis;
        public TestController(RedisService redis)
        {
            _redis = redis;
        }

        [HttpGet("/keys/{db}")]
        public string[] keys(int db = 0)
        {
            try
            {
                var server = _redis.GetServer(REDIS_TYPE.WRITE);
                var a = server.Keys(db).Select(x => x.ToString()).ToArray();
                return a;
            }
            catch { }
            return new string[] { };
        }

        [HttpPost("/update/{db}/{key}/{value}")]
        public dynamic update(string key, string value, int db = 0)
        {
            try
            {
                key = key.ToLower();
                var dbw = _redis.GetDB(REDIS_TYPE.WRITE, 1);
                dbw.StringSet(key, value);

                var dbr = _redis.GetDB(REDIS_TYPE.READ1, 1);
                string v = dbr.StringGet(key);
                return new { Db = db, Key = key, Input = value, Output = v };
            }
            catch { }
            return new { };
        }
    }
}
