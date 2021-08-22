using ClearScript.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClearScript.Controllers
{
    public class TestController: Controller
    {
        readonly ISearcher _searcher;
        public TestController(ISearcher search)
        {
            _searcher = search;
        }

        [HttpGet("/fetch")]
        public string fetch(string url)
        {
            string s = string.Empty;
            s = _searcher.Test(url);
            return s;
        }
    }
}
