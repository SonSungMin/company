using P72B.Core.BackendService.App_Start;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Security;
using System.Web.SessionState;

namespace P72B.Core.BackendService
{
    public class Global : HHI.ServiceModel.Standard.Web.GlobalHttpApplication
    {
        protected override void Application_Start()
        {
            base.Application_Start();

            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
