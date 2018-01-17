using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using xenwinsvc;

namespace XenWinSvcTester
{
    /// <summary>
    /// Class to mock the Xenstore item
    /// </summary>
    public class MockXenStoreItem : AXenStoreItem
    {
        private string _path, _value;
        private bool _exist = true;
        /// <summary>
        /// The session this XenstoreItem belongs to
        /// </summary>
        MockWmiSession session = null;
        /// <summary>
        /// Get all the children path of this XenstoreItem
        /// </summary>
        public override string[] children
        {
            get
            {
                return session.children(_path);
            }

        }

        public override WmiWatchListener Watch(System.Management.EventArrivedEventHandler handler)
        {
            return null;
        }
        /// <summary>
        /// Get/Set value
        /// </summary>
        public override string value
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
        /// <summary>
        /// Whether exists
        /// </summary>
        /// <returns></returns>
        public override bool Exists()
        {
            return _exist;
        }
        /// <summary>
        /// Delete the Xenstore Item
        /// </summary>
        public override void Remove()
        {
            _exist = false;
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">item path</param>
        /// <param name="value">the value it store</param>
        /// <param name="session">session this xenstore item belongs to</param>
        public MockXenStoreItem(string path, string value, MockWmiSession session)
        {
            _path = path;
            _value = value;
            _exist = true;
            this.session = session;
        }
        public MockXenStoreItem(string path, MockWmiSession session)
            : this(path, null, session)
        {

        }
    }
    /// <summary>
    /// Mock the sessoin object
    /// </summary>
    public class MockWmiSession : AWmiSession
    {
        /// <summary>
        /// Mock the xenstore items, mapping from path to xenstore item
        /// </summary>
        Dictionary<string, MockXenStoreItem> mockXenstoreItems;
        public MockWmiSession()
        {
            mockXenstoreItems = new Dictionary<string, MockXenStoreItem>();
        }
        /// <summary>
        /// Get the Xenstore Item from path, create new one if not exist
        /// </summary>
        /// <param name="path">path of the item</param>
        /// <returns>the target item</returns>
        public override AXenStoreItem GetXenStoreItem(string path)
        {
            if (!mockXenstoreItems.ContainsKey(path))
            {
                MockXenStoreItem item = new MockXenStoreItem(path, this);
                mockXenstoreItems.Add(path, item);
            }
            return mockXenstoreItems[path];
        }
        /// <summary>
        /// Get the Xenstore Item from path, create new one if not exist
        /// </summary>
        /// <param name="path">path of the item</param>
        /// <param name="value">the new value of the item, if exist, then override the value</param>
        /// <returns>the target item</returns>
        public AXenStoreItem GetXenStoreItem(string path, string value)
        {
            if (!mockXenstoreItems.ContainsKey(path))
            {
                MockXenStoreItem item = new MockXenStoreItem(path, this);
                mockXenstoreItems.Add(path, item);
            }
            mockXenstoreItems[path].value = value;
            return mockXenstoreItems[path];
        }

        public override void StartTransaction()
        {
        }

        public override void AbortTransaction()
        {
        }

        public override void CommitTransaction()
        {
        }
        /// <summary>
        /// Whether subPath is the sub path of the path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="subPath"></param>
        /// <returns></returns>
        private bool bSubKey(string path, string subPath)
        {
            const string SEP = "/";
            if (null == path || null == subPath) return false;
            if (path.Equals(subPath)) return false;
            if (!subPath.StartsWith(path)) return false;
            int length = subPath.Length - path.Length;
            string reminding = subPath.Substring(path.Length + 1);
            return !reminding.Contains(SEP);

        }
        /// <summary>
        /// Return all children of path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string[] children(string path)
        {
            List<string> ch = new List<string>();
            foreach (string key in mockXenstoreItems.Keys)
            {
                if (bSubKey(path, key))
                {
                    ch.Add(key);
                }
            }
            return ch.ToArray();
        }
    }
}
