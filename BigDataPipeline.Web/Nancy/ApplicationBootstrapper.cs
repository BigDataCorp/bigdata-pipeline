using Nancy;
using Nancy.Owin;
using Owin;
using System;
using System.Linq;
using System.Text;
using Nancy.Authentication.Forms;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace BigDataPipeline.Web
{
    public class ApplicationBootstrapper : DefaultNancyBootstrapper
    {
        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger ();
        static Tuple<string, decimal>[] DefaultEmptyHeader = new[] { Tuple.Create ("application/json", 1.0m), Tuple.Create ("text/html", 0.9m), Tuple.Create ("*/*", 0.8m) };
        static Dictionary<string, string> moduleViews = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
        static IUserMapper accessControlContext = null;        

        protected override void ConfigureConventions (Nancy.Conventions.NancyConventions nancyConventions)
        {
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("styles",    @"site/styles"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("fonts",     @"site/fonts"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("scripts",   @"site/scripts"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("images",    @"site/images"));
            
            // add some default header Accept if empty (to provide a degault dynamic content negotiation rule)
            this.Conventions.AcceptHeaderCoercionConventions.Add ((acceptHeaders, ctx) =>
            {
                if (!acceptHeaders.Any ())
                {
                    return DefaultEmptyHeader;
                }
                return acceptHeaders;
            });

            // add content folder for modules, following the convention
            // ./modules/[foldername of the modules]/assets
            // ./modules/[foldername of the modules]/views
            
            var root = this.RootPathProvider.GetRootPath ();
            var modulesRoot = System.IO.Path.Combine (root, "modules");
            if (System.IO.Directory.Exists (modulesRoot))
            {
                foreach (var d in System.IO.Directory.GetDirectories (modulesRoot, "*", System.IO.SearchOption.AllDirectories))
                {
                    var name = GetLastPathPart (d);
                    if (name.Equals ("assets", StringComparison.OrdinalIgnoreCase))
                    {
                        var a = PrepareFilePath (d.Substring (modulesRoot.Length)).Split('/');
                        nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory (String.Join ("/", a), PrepareFilePath (d.Substring (root.Length))));
                    }
                    else if (name.Equals ("views", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var v in System.IO.Directory.EnumerateFiles (d, "*html", System.IO.SearchOption.AllDirectories))
                        {
                            string view = v;
                            int i = view.LastIndexOf ('.');
                            if (i > 0)
                                view = view.Remove (i);
                            moduleViews[PrepareFilePath (view.Substring (d.Length))] = PrepareFilePath (view.Substring (root.Length));
                        }
                    }
                }
            }

            // set custom view location conventions
            this.Conventions.ViewLocationConventions.Add ((viewName, model, context) =>
            {
                return string.Concat ("site/Views/", context.ModuleName, "/", viewName);
            });
            this.Conventions.ViewLocationConventions.Add ((viewName, model, context) =>
            {
                return string.Concat ("site/Views/", viewName);
            });

            this.Conventions.ViewLocationConventions.Add ((viewName, model, context) =>
            {
                string p;
                if (moduleViews.TryGetValue (context.ModuleName + "/" + viewName, out p))
                    return p;
                if (moduleViews.TryGetValue (viewName, out p))
                    return p;
                return string.Concat ("modules/", context.ModulePath, "/Views/", viewName).Replace ("//", "/");
            });

            base.ConfigureConventions (nancyConventions);
        }

        protected override void ApplicationStartup (Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
        {
            Nancy.Diagnostics.DiagnosticsHook.Disable (pipelines);
            StaticConfiguration.CaseSensitive = false;
            StaticConfiguration.DisableErrorTraces = false;
            StaticConfiguration.EnableRequestTracing = false;
            Nancy.Json.JsonSettings.MaxJsonLength = 10 * 1024 * 1024;

            // log any errors
            pipelines.OnError.AddItemToStartOfPipeline ((ctx, ex) =>
            {
                logger.Error (ex);
                return null;
            });

            // some additional response configuration
            pipelines.AfterRequest.AddItemToEndOfPipeline ((ctx) =>
            {
                // CORS Enable
                if (ctx != null && ctx.Response != null && ctx.Response.ContentType != null &&
                    (ctx.Response.ContentType.Contains ("json") || ctx.Response.ContentType.Contains ("xml")))
                {
                    ctx.Response.WithHeader ("Access-Control-Allow-Origin", "*")
                        .WithHeader ("Access-Control-Allow-Methods", "POST,GET")
                        .WithHeader ("Access-Control-Allow-Headers", "Accept, Origin, Content-type");
                    // to improve thethrouput, lets signal the client to close and reopen the connection per request
                    ctx.Response.Headers["Connection"] = "close";
                }
            });

            // gzip compression
            pipelines.AfterRequest.AddItemToEndOfPipeline (NancyCompressionExtenstion.CheckForCompression);
                        
            // try to enable authentication            
            if (container.TryResolve<IUserMapper> (out accessControlContext) &&
                container.CanResolve<IAccessControlFactory> () &&
                container.Resolve<IAccessControlFactory> ().GetAccessControlModule () != null)
            {
                // global authentication after forms authentication
                pipelines.BeforeRequest.AddItemToStartOfPipeline (AllResourcesAuthentication);

                // set forms authentication
                var formsAuthConfiguration = new FormsAuthenticationConfiguration ()
                {
                    RedirectUrl = "~/login",
                    UserMapper = accessControlContext,
                    //RequiresSSL = true,
                    // note: the default CryptographyConfiguration generates a new key every application restart, invalidating the authentication cookie
                    CryptographyConfiguration = new Nancy.Cryptography.CryptographyConfiguration (
                        new Nancy.Cryptography.RijndaelEncryptionProvider (new Nancy.Cryptography.PassphraseKeyGenerator ("encryption" + this.GetType ().FullName, Encoding.UTF8.GetBytes ("PipelineSaltProvider"))),
                        new Nancy.Cryptography.DefaultHmacProvider (new Nancy.Cryptography.PassphraseKeyGenerator ("HMAC" + this.GetType ().FullName, Encoding.UTF8.GetBytes ("PipelineSaltProvider"))))
                };

                FormsAuthentication.Enable (pipelines, formsAuthConfiguration);
            }

            // Squishit
            SquishIt.Framework.Bundle.ConfigureDefaults ().UsePathTranslator (container.Resolve<SquishIt.Framework.IPathTranslator> ());

            base.ApplicationStartup (container, pipelines);
        }

        protected override void RequestStartup (Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines, NancyContext context)
        {            
            base.RequestStartup (container, pipelines, context);
        }

        /// <summary>
        /// This method sets the password for the NancyFx diagnostics page when its is enabled
        /// To enable the diagnostics page: comment the disable call "Nancy.Diagnostics.DiagnosticsHook.Disable (pipelines);" at "ApplicationStartup" method
        /// To access diagnostics page: http://<address-of-your-application>/_Nancy/
        /// </summary>
        protected override Nancy.Diagnostics.DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new Nancy.Diagnostics.DiagnosticsConfiguration { Password = @"password" }; }
        }

        /// <summary>
        /// Allow anonymous access only to the login page... 
        /// All other modules access must be authentication:
        /// 1 - forms authentication
        /// 2 - token authentication in the Header["Authorization"]
        /// 3 - token authentication in the request parameters: <request path>/?token=<token value>
        /// </summary>        
        private Response AllResourcesAuthentication (NancyContext ctx)
        {
            // if authenticated, go on...
            if (ctx.CurrentUser != null)
                return null;
            // if login module, go on...
            if (ctx.Request.Url.Path.IndexOf ("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;

            // check for token authentication: Header["Authorization"] with the session/token guid
            Guid authToken;
            if (ctx.Request.Headers.Authorization != null && Guid.TryParse (ctx.Request.Headers.Authorization, out authToken))
            {
                ctx.CurrentUser = accessControlContext.GetUserFromIdentifier (authToken, ctx);
            }
            // check for token authentication: query parameter or form unencoded parameter
            if (ctx.CurrentUser == null && Guid.TryParse (TryGetRequestParameter (ctx, "token"), out authToken))
            {
                ctx.CurrentUser = accessControlContext.GetUserFromIdentifier (authToken, ctx);
            }

            // analise if we got an authenticated user
            return (ctx.CurrentUser == null) ? new Nancy.Responses.HtmlResponse (HttpStatusCode.Unauthorized) : null;
        }

        static string TryGetRequestParameter (NancyContext ctx, string parameter)
        {
            object p;
            if ((ctx.Request.Query as DynamicDictionary).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            if ((ctx.Request.Form as DynamicDictionary).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            return null;
        }

        /// ***********************
        /// Custom Path provider
        /// ***********************
        public class PathProvider : IRootPathProvider
        {
            static string _path = AppDomain.CurrentDomain.BaseDirectory;//System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, @"site");//

            public string GetRootPath ()
            {
                return _path;
            }

            public static void SetRootPath (string fullPath)
            {
                _path = fullPath;
            }
        }

        protected override IRootPathProvider RootPathProvider
        {
            get { return new PathProvider (); }
        }

        /// ***********************
        /// Custom Json net options
        /// ***********************
        public class CustomJsonSerializer : JsonSerializer
        {
            public static JsonSerializerSettings DefaultNewtonsoftJsonSettings;
            public CustomJsonSerializer ()
            {
                Formatting = Formatting.None;
                MissingMemberHandling = MissingMemberHandling.Ignore;
                NullValueHandling = NullValueHandling.Ignore;
                ObjectCreationHandling = ObjectCreationHandling.Replace;
            }
        }

        // Add this converter to the default settings of Newtonsoft JSON.NET.            
        protected override void ConfigureApplicationContainer (Nancy.TinyIoc.TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer (container);

            container.Register<JsonSerializer, CustomJsonSerializer> ();
        }


        #region *   Helper methods  *
        /// ***********************
        /// Helper methods
        /// ***********************
        static string GetLastPathPart (string path)
        {
            // reduce length to disregard ending '\\' or '/'
            int len = path.Length - 2;
            if (len < 1)
                return String.Empty;
            int pos = path.LastIndexOf ('\\', len);
            if (pos < 0)
                pos = path.LastIndexOf ('/', len);
            if (pos > 0 && pos <= len)
                return path.Substring (pos + 1);
            return String.Empty;
        }

        static string PrepareFilePath (string path)
        {
            return (path ?? "").Replace ('\\', '/').Replace ("//", "/").Trim ('/');
        }

        #endregion
    }

    // https://blog.tommyparnell.com/getting-squishit-to-work-with-nancyfx-and-razor/
    // http://blogs.lessthandot.com/index.php/webdev/serverprogramming/aspnet/squishit-and-nancy/
    public class NancyPathTranslator : SquishIt.Framework.IPathTranslator
    {
        private readonly IRootPathProvider _rootPathProvider;
        private string rootPath;

        public NancyPathTranslator (IRootPathProvider rootPathProvider)
        {
            _rootPathProvider = rootPathProvider;
            rootPath = (_rootPathProvider.GetRootPath ().TrimEnd ('/', '\\') + "/site/").Replace ('\\', '/');
        }

        public string ResolveAppRelativePathToFileSystem (string file)
        {
            // Remove query string
            if (file.IndexOf ('?') != -1)
            {
                file = file.Substring (0, file.IndexOf ('?'));
            }

            return rootPath + file.TrimStart ('~').TrimStart ('/');
        }

        public string ResolveFileSystemPathToAppRelative (string file)
        {
            var root = new Uri (rootPath);
            return root.MakeRelativeUri (new Uri (file, UriKind.RelativeOrAbsolute)).ToString ();
        }
    }

        
}
