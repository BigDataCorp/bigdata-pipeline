using BigDataPipeline.Core.Interfaces;
using BigDataPipeline.Web.Models;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BigDataPipeline.Web
{
    public interface IAccessControlFactory
    {
        IAccessControlModule GetAccessControlModule ();
    }

    public class AccessControlFactory : IAccessControlFactory
    {
        public IAccessControlModule GetAccessControlModule ()
        {
            return BigDataPipeline.Core.PipelineService.Instance.GetAccessControlModule ();
        }
    }
}
