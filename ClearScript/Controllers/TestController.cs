using ClearScript.Services;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Text;

namespace ClearScript.Controllers
{
    public class TestController : Controller
    {
        const string _URL = "https://vnexpress.net/";
        readonly ISearcher _searcher;
        public TestController(ISearcher search)
        {
            _searcher = search;
        }

        [HttpGet("/fetch")]
        public IActionResult fetch(string url)
        {
            if (string.IsNullOrEmpty(url)) url = _URL;
            string s = string.Empty;
            s = _searcher.Test(url);
            return Content(s, "text/html", Encoding.UTF8);
        }

        [HttpGet("/clear")]
        public IActionResult clear(string url, string sels)
        {
            if (string.IsNullOrEmpty(url)) url = _URL;
            string s = string.Empty;
            s = _searcher.Test(url);
            s = clearHtmlTag(s, sels);
            return Content(s, "text/html", Encoding.UTF8);
        }

        string clearHtmlTag(string html, string sels)
        {
            string s = html;
            if (!string.IsNullOrEmpty(html))
            {
                if (!string.IsNullOrEmpty(sels))
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var a = sels.Split('|');
                    for (int i = 0; i < a.Length; i++)
                    {
                        var nodes = doc.QuerySelectorAll(a[i]).ToArray();
                        if (nodes.Length > 0)
                        {
                            foreach (var node in nodes)
                            {
                                node.ParentNode.RemoveChild(node);
                                //node.InnerHtml = string.Empty;
                            }
                        }
                    }
                    s = doc.DocumentNode.OuterHtml;
                }
            }
            return s;
        }
    }
}
