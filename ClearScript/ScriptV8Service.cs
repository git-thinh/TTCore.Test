using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ClearScript.Services
{
    public interface ISearcher
    {
        T[] Search<T>(string linqDynamicWhere);
        bool SetItem<T>(string key, T item);
        bool SetItem(string model, string key, string itemJson);
        T GetByKey<T>(string key);
        //bool SetKey<T>(string key, T item);
        bool Remove(string key);
        string Test(string url);
    }

    public class ScriptV8Service : ISearcher
    {
        readonly V8ScriptEngine _v8Engine;
        readonly IConfiguration _configuration;
        readonly ILogger<ScriptV8Service> _logger;
        readonly ConcurrentDictionary<string, int> _indexs;
        public ScriptV8Service(
            ILogger<ScriptV8Service> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _indexs = new ConcurrentDictionary<string, int>() { };
            _v8Engine = new V8ScriptEngine(V8ScriptEngineFlags.DisableGlobalMembers);
            //_engineJsV8.Execute("__CACHES = [];");
            registerFetch();
            //Test("");
        }

        void registerFetch() {
            _v8Engine.AddCOMType("XMLHttpRequest", "MSXML2.XMLHTTP");
            _v8Engine.Execute(@"
                function get(url) {
                    var xhr = new XMLHttpRequest();
                    xhr.open('GET', url, false);
                    xhr.send();
                    if (xhr.status == 200)
                        return xhr.responseText;
                    throw new Error('Request failed: ' + xhr.status);
                }
            ");
        }

        public string Test(string url)
        {
            string s = string.Empty;
            if (!string.IsNullOrEmpty(url))
                s = _v8Engine.Script.get(url);
            return s;
        }

        string _toModel(Type type)
        {
            string model = type.Name.ToLower();
            if (model.EndsWith("entity")) model = model.Substring(0, model.Length - "entity".Length);
            return model;
        }

        public bool Remove(string key)
        {
            return false;
        }

        public T[] Search<T>(string linqDynamicWhere)
        {
            _logger.LogInformation("MemoryCacher.Search: linqDynamicWhere = " + linqDynamicWhere);

            var ls = new List<T>();
            string s = @"
                var arr = [];
                for(var i = 0; i < __CACHES.length; i++) {
                    var it = __CACHES[i]; 
                    try{ if(" + linqDynamicWhere + @") arr.push(i); }catch(e){ }
                }
                arr;
            ";
            var lsIndex = (IList)_v8Engine.Evaluate(s);
            var indexs = lsIndex.Cast<int>().ToArray();
            if (indexs.Length > 0)
            {
                var caches = _v8Engine.Script.__CACHES;
                for (int i = 0; i < indexs.Length; i++)
                {
                    int index = indexs[i];
                    var it = caches[index];
                    string json = JsonConvert.SerializeObject(it);
                    var obj = JsonConvert.DeserializeObject<T>(json);
                    ls.Add(obj);
                }
            }
            return ls.ToArray();
        }

        public bool SetItem<T>(string key, T item)
        {
            if (!string.IsNullOrEmpty(key) && item != null)
            {
                string itemJson = JsonConvert.SerializeObject(item);
                string model = _toModel(item.GetType());
                return SetItem(model, key, itemJson);
            }
            return true;
        }

        public bool SetItem(string model, string key, string itemJson)
        {
            _v8Engine.Execute("try{ __CACHES.push(" + itemJson + "); }catch(e){ }");
            int index = _v8Engine.Script.__CACHES.length - 1;
            _indexs.TryAdd(key, index);
            //_redis.Update(model, key, itemJson);
            return true;
        }

        public T GetByKey<T>(string key)
        {
            int index = -1;
            _indexs.TryGetValue(key, out index);
            if (index > -1)
            {
                var it = _v8Engine.Script.__CACHES[index];
                string json = JsonConvert.SerializeObject(it);
                var obj = JsonConvert.DeserializeObject<T>(json);
                return obj;
            }
            return default(T);
        }
    }
}
