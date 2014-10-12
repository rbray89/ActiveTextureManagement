using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ActiveTextureManagement
{
    class CustomDatabaseLoader : LoadingSystem
    {

        public CustomDatabaseLoader() : base()
        {

        }

        public override bool IsReady()
        {
            return true;// base.IsReady();    
        }

        public override float ProgressFraction()
        {
            return 1;// base.ProgressFraction();    
        }

        public override string ProgressTitle()
        {
            return "";// base.ProgressTitle();  
        }
        public override void StartLoad()
        {
            //base.StartLoad();  
        }

    }
}
