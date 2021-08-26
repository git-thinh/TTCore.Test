using System.Collections.Generic;

namespace Driver.Models
{
    public class oItem
    {
        public string id { set; get; }
        public string name { set; get; }
        public long size { set; get; }
        public long created_time { set; get; }
        public string mime_type { set; get; }
        public IList<string> parents { set; get; }
        public bool shared { get; set; }
        public string content_link { set; get; }
        public string view_link { set; get; }
        public string resource_key { set; get; } //A key needed to access the item via a shared link.
    }
}
