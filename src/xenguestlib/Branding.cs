using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using BrandSupport;


namespace xenwinsvc
{
    public class Branding
    {
        private static BrandingControl instance;

        public static BrandingControl Instance
        {
            get
            {
                if (instance == null)
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    string brandsatpath = Path.GetDirectoryName(assembly.Location) + "\\Branding\\brandsat.dll";
                    instance = new BrandingControl(brandsatpath);
                }

                return instance;
            }
        }
    }
}
