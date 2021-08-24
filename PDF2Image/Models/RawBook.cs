using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PDF2Image.Models
{
    public class RawBook
    {
        public int Page { set; get; }
        public string Title { set; get; }
        public string Author { set; get; }
        public string Subject { set; get; }
        public string Keywords { set; get; }
        public string Creator { set; get; }
        public string Producer { set; get; }
        public string CreationDate { set; get; }
        public string ModDate { set; get; }
    }
}
