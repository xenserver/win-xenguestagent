using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace XenUpdater
{
    [TestFixture]
    public class UnitTest1
    {
        public class MockXenStoreItem : ACXenStoreItem
        {
            string _value;
            string _path;
            bool _exists = true;
            
            public MockXenStoreItem(string path, string value) {
                this._path = path;
                if (value != null)
                {
                    this._value = value;
                }
                else
                {
                    _exists = false;
                    this._value = "";
                }
            }
            
            public override bool Exists 
            { 
                get 
                { 
                    return _exists; 
                } 
            }
            
            public override string Value 
            { 
                get 
                { 
                    return _value; 
                } 
                set 
                { 
                    _value = value; 
                } 
            }
            
            public override string ValueOrDefault(string def)
            {
                return this.Value;
            }
            
            public override string Path
            {
                get { 
                    return _path;
                } 
            }
            
            public override void Remove()
            {
                _exists = false;
            }
        }

        class MockXenStoreItemFactory : IXenStoreItemFactory
        {
            Dictionary<string, string> mapping;
            
            public MockXenStoreItemFactory(Dictionary<string, string> mapping)
            {
                this.mapping = mapping;
            }
            
            public ACXenStoreItem newXenStoreItem(string StoreLocation)
            {
                return new MockXenStoreItem(StoreLocation, mapping[StoreLocation]);
            }
            
            public void Log(string message) { }
        }

        class MockBranding : IBranding
        {
            Dictionary<string, string> mapping;
            
            public MockBranding(Dictionary<string, string> mapping)
            {
                this.mapping = mapping;
            }
            
            public string GetString(string key)
            {
                if (mapping.ContainsKey(key))
                {
                    return mapping[key];
                }
                else
                {

                    return "BRANDING key " + key + " not implemented";
                }
            }
        }
        
        class MockGetReg : IGetReg
        {
            Dictionary<string, Dictionary<string, string>> mapping;
            
            public MockGetReg(Dictionary<string, Dictionary<string, string>> mapping)
            {
                this.mapping = mapping;
            }
            
            public object GetReg(string key, string name, object def)
            {
                if (mapping.ContainsKey(key) && mapping[key].ContainsKey(name))
                {
                    return mapping[key][name];
                }
                else
                {

                    return def;
                }
            }
        }

        class MockWebClient : IWebClientWrapper
        {
            public bool setUserAgent = false;
            public bool setUserAgentUnitTest = false;
            public bool unknownurl = false;
            public bool urlhasid = false;
            
            public void AddHeader(string header, string value)
            {
                if (header == "User-Agent")
                {
                    setUserAgent=true;
                    if (value == "UnitTest")
                    {
                        setUserAgentUnitTest = true;
                    }
                }
            }
            
            public string DownloadString(string url)
            {
                if (url == "http://127.0.0.1/updates.tsv?id=11111111-1111-1111-1111-111111111111")
                {
                    urlhasid = true;
                }
                else if (url == "http://127.0.0.1/updates.tsv")
                {
                    urlhasid = false;
                }
                else 
                {
                    unknownurl=true;
                }
                return null;
            }
        }

        [Test]
        public void TestCheckUpdateWithUUID()
        {
            // If HKEY_LOCAL_MACHINE\Software\Citrix\XenTools\AutoUpdate Identify == YES
            // Make a call to the specified URL including the UUID
            
            var xsdict = new Dictionary<string,string>();
            var brandingdict = new Dictionary<string, string>();
            var regdict = new Dictionary<string, Dictionary<string,string>>();
            
            brandingdict.Add("BRANDING_updaterURL", "http://127.0.0.1/updates.tsv");
            brandingdict.Add("BRANDING_userAgent", "UnitTest");
            
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/licensed", "1");
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/parameters/enabled", "1");
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/parameters/update_url",null);
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/parameters/allow-driver-install", "NO");
            xsdict.Add("data/xd/present", "0");
            xsdict.Add("vm", "/vm/11111111-1111-1111-1111-111111111111");
            
            regdict.Add("HKEY_LOCAL_MACHINE\\Software\\Citrix\\XenTools\\AutoUpdate",new Dictionary<string,string>());
            regdict["HKEY_LOCAL_MACHINE\\Software\\Citrix\\XenTools\\AutoUpdate"].Add("Identify","YES");
            
            Version nv =  new Version(6,6,0,1);
            var au = new AutoUpdate(new MockXenStoreItemFactory(xsdict),
                                    new MockBranding(brandingdict),
                                    new MockGetReg(regdict));
            au.version = nv;
            MockWebClient wc = new MockWebClient();
            au.CheckForUpdates(wc);
             
            Assert.AreEqual(wc.setUserAgent, true);
            Assert.AreEqual(wc.setUserAgentUnitTest, true);
            Assert.AreEqual(wc.urlhasid, true);
        }

        [Test]
        public void TestCheckUpdateWithoutUUID()
        {
            // If HKEY_LOCAL_MACHINE\Software\Citrix\XenTools\AutoUpdate Identify == NO
            // Make a call to the specified URL without including the UUID
            
            var xsdict = new Dictionary<string, string>();
            var brandingdict = new Dictionary<string, string>();
            var regdict = new Dictionary<string, Dictionary<string, string>>();
            
            brandingdict.Add("BRANDING_updaterURL", "http://127.0.0.1/updates.tsv");
            brandingdict.Add("BRANDING_userAgent", "UnitTest");
            
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/licensed", "1");
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/parameters/enabled", "1");
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/parameters/update_url", null);
            xsdict.Add("/guest_agent_features/Guest_agent_auto_update/parameters/allow-driver-install", "NO");
            xsdict.Add("data/xd/present", "0");
            xsdict.Add("vm", "/vm/11111111-1111-1111-1111-111111111111");
            
            regdict.Add("HKEY_LOCAL_MACHINE\\Software\\Citrix\\XenTools\\AutoUpdate", new Dictionary<string, string>());
            regdict["HKEY_LOCAL_MACHINE\\Software\\Citrix\\XenTools\\AutoUpdate"].Add("Identify", "NO");
            
            Version nv = new Version(6, 6, 0, 1);
            var au = new AutoUpdate(new MockXenStoreItemFactory(xsdict),
                                    new MockBranding(brandingdict),
                                    new MockGetReg(regdict));
            au.version = nv;
            MockWebClient wc = new MockWebClient();
            au.CheckForUpdates(wc);
            
            Assert.AreEqual(wc.setUserAgent, true);
            Assert.AreEqual(wc.setUserAgentUnitTest, true);
            Assert.AreEqual(wc.urlhasid, false);
        }
    }
}
