using AngleSharp;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PuppeteerSharp;
using System.Threading;
using Driver.Services;

namespace Driver.Controllers
{
    #region [ MODEL ]
    public class FbJobRaw
    {
        public string id { set; get; }
        public string city_name { set; get; }
        public long created_time { set; get; }
        public string job_status { set; get; }
        public FbJobId employer { set; get; }
        public FbJobText business_or_employer_name { set; get; }
        public FbJobText title { set; get; }
        public FbJobText long_description { set; get; }

        public FbJob ToJob()
        {
            var j = new FbJob();
            j.id = this.id;
            j.city_name = this.city_name;
            j.created_time = this.created_time;
            j.job_status = this.job_status;

            if (this.long_description != null) j.description = this.long_description.text;
            if (this.employer != null) j.publisher_id = this.employer.id;
            if (this.business_or_employer_name != null) j.publisher_name = this.business_or_employer_name.text;
            if (this.title != null) j.title = this.title.text;
            return j;
        }
    }
    public class FbJobNode
    {
        public FbJobRaw node { set; get; }
    }
    public class FbJob
    {
        public string id { set; get; }
        public string title { set; get; }
        public string description { set; get; }
        public string publisher_name { set; get; }
        public string publisher_id { set; get; }
        public string city_name { set; get; }
        public long created_time { set; get; }
        public string job_status { set; get; }
    }
    public class FbJobText { public string text { set; get; } }
    public class FbJobId { public string id { set; get; } }
    #endregion

    [Route("api/[controller]")]
    public class TestController : Controller
    {
        readonly DriverService _driver;
        public TestController(DriverService driver)
        {
            _driver = driver;
        }


        const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.81 Safari/537.36";

        [HttpGet("fb/jobs/puppeteer")]
        public async Task<dynamic> getJobByPuppeteer()
        {
           var a = await _driver.bot_Test();
            a = a.Distinct().ToArray();
            return new { size = a.Length, data = a };
        }

        [HttpGet("fb/jobs/angle")]
        public async Task<dynamic> getJobByAngle()
        {
            try
            {
                string json = await geJobs();
                var jr = JsonConvert.DeserializeObject<FbJobNode[]>(json);
                var js = jr.Select(x => x.node.ToJob()).ToArray();
                return new { ok = true, size = js.Length, data = js };
            }
            catch (Exception e) { }
            return new { ok = false };
        }


        async Task<string> geJobs()
        {
            string url = "https://www.facebook.com/jobs/";

            //string cv = "c_user=100002130364781;datr=O3RnYQTh-aMjQU-jzWD6SjMj;fr=0qLTpVNXLfT1TLK7R.AWXL8X1KY8bqS1csAOHgAdOS39E.BhaSJn.up.AAA.0.0.BhaSJn.AWX8EdvItZU;locale=en_GB;sb=HN9nYfsskSmkz-4ms6ksyZM-;spin=r.1004560766_b.trunk_t.1634276029_s.1_v.2_;wd=1920x947;xs=31%3AW9Yn8Uwgud5fGw%3A2%3A1634276023%3A-1%3A6381%3A%3AAcVWCUXI5W4kVwXLR_xgpY904uWEOSYc5oxgT2lPcw;";
            //
            //var handler = new HttpClientHandler()
            //{
            //    //Proxy = new WebProxy("127.0.0.1", 8888),
            //    //UseProxy = true,
            //};
            //var requester = new DefaultHttpRequester(userAgent);
            //var config = Configuration.Default.WithRequesters(handler)
            //    .With(requester)
            //    .WithDefaultLoader();
            //var context = BrowsingContext.New(config);
            //context.SetCookie(AngleSharp.Dom.Url.Create(url), cv);
            //var doc = await context.OpenAsync(url);

            var cf = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(cf);
            var doc = await context.OpenAsync(url);

            string s = doc.Body.InnerHtml;
            //s = doc.Cookie;
            string p = @":{""edges"":";
            int k = -1;
            k = s.IndexOf(p);
            if (k > 0)
            {
                s = s.Substring(k + p.Length, s.Length - (k + p.Length));
                p = @"],""page_info"":{";
                k = s.IndexOf(p);
                if (k > 0)
                {
                    string json = s.Substring(0, k + 1);
                    return json;
                }
            }

            return "[]";
        }
    }
}
