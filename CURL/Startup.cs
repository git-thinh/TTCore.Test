using HtmlAgilityPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace CURL
{
    public class Startup
    {
        readonly static V8ScriptEngine _v8Engine;
        static Startup()
        {
            _v8Engine = new V8ScriptEngine(V8ScriptEngineFlags.DisableGlobalMembers);
            _v8Engine.AddCOMType("XMLHttpRequest", "MSXML2.XMLHTTP");
            _v8Engine.Execute(@"
                function Curl(url) {
                    try {
                        var xhr = new XMLHttpRequest();
                        xhr.open('GET', url, false);
                        xhr.send();
                        if (xhr.status == 200)
                            return xhr.responseText;
                    } catch(e){}
                    return '';
                }
            ");
        }

        IConfiguration _configuration { get; }
        IWebHostEnvironment _environment { get; }
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _environment = env;
        }
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    string s = string.Empty;
                    string url = context.Request.Query["url"];
                    try
                    {
                        if (!string.IsNullOrEmpty(url))
                            s = _v8Engine.Script.Curl(url);
                        if (!string.IsNullOrEmpty(s))
                        {
                            string sels = context.Request.Query["sels"];
                            s = clearHtmlTag(s, sels);
                        }
                    }
                    catch { }
                    context.Response.ContentType = "text/plain; charset=UTF-8";
                    await context.Response.WriteAsync(s);
                });
            });
        }

        string clearHtmlTag(string html, string sels)
        {
            string s = html;
            if (!string.IsNullOrEmpty(html))
            {
                if (!string.IsNullOrEmpty(sels))
                {
                    try
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
                    catch { }
                }
            }
            return s;
        }
    }
}
