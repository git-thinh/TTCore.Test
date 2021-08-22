using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Redis
{
    public class RedisSetting
    {
        public string ConnectString { set; get; }
        public int PortMaster { set; get; }
        public int PortRead1 { set; get; }
        public int PortRead2 { set; get; }
        public int PortRead3 { set; get; }
    }
}
