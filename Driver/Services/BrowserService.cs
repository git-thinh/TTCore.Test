using Driver.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Blogger.v3;
using Google.Apis.Blogger.v3.Data;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using GooService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Driver.Services
{
    public class BrowserService : BackgroundService
    {
        readonly string ServiceName = nameof(DriverService);
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        readonly IWebHostEnvironment _environment;
        public BrowserService(ILoggerFactory loggerFactory,
            IWebHostEnvironment env,
            IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _configuration = configuration;
            _environment = env;
        }

        static Browser browser = null;
        static PuppeteerSharp.Page page = null;
        static int k = 0;

        async public Task<string[]> bot_Test() {
            k++;
            await page.EvaluateExpressionAsync("window.scrollTo(0,0);window.scrollTo(0, document.body.scrollHeight);");

            string s = await page.EvaluateExpressionAsync<string>(@"
var a = [], p='/jobs/job-opening/',
    es = document.querySelectorAll('a');
for (var i = 0; i < es.length; i++) {
    var l = es[i].getAttribute('href');
    if (l && l.indexOf(p) != -1){
        var u = l.split('?')[0].substr(p.length);
        if(u.endsWith('/')) u = u.substr(0,u.length-1);
        a.push(u);
    }
}; a.join('^');");
            var a = s.Split('^').Select(x => x.Trim()).ToArray();
            return a;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{ServiceName} is starting.");
            stoppingToken.Register(() => _logger.LogInformation($"{ServiceName} background task is stopping."));

            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    if (_isRunning == false)
            //    {
            //        _isRunning = true;

            //        _isRunning = false;
            //    }
            //    await Task.Delay(500, stoppingToken);
            //}

            string url = "https://www.facebook.com/jobs/";
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            
            browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            page = await browser.NewPageAsync();

            ////await page.SetUserAgentAsync(USER_AGENT);
            //await page.SetRequestInterceptionAsync(true);
            //page.Request += async (sender, args) =>
            //{
            //    string url_ = args.Request.Url;
            //    if (url_.Contains("/api/graphql"))
            //    {

            //    }
            //    //if (args.Request.ResourceType == ResourceType.Image) await args.Request.AbortAsync();
            //    else await args.Request.ContinueAsync();
            //};

            //page.Response += async (object sender, ResponseCreatedEventArgs e) =>
            //{
            //    if (e.Response.Url.Contains("/api/graphql"))
            //    {
            //        string s = await e.Response.TextAsync();
            //    }
            //};

            //await page.SetViewportAsync(new ViewPortOptions() { Width = 1024, Height = 500 });
            var r = await page.GoToAsync(url, 60000 * 3);
            string s = await r.TextAsync().ConfigureAwait(false);

            k++;
            await page.EvaluateExpressionAsync("window.scrollTo(0, document.body.scrollHeight);");

            await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogDebug($"{ServiceName} is stopping.");
        }


    }
}
