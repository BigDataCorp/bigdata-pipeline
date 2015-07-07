using Nancy;
using Nancy.Owin;
using Owin;
using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace BigDataPipeline.Web
{
    public class ApplicationBootstrapper : DefaultNancyBootstrapper
    {
        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger ();
        static Tuple<string, decimal>[] DefaultEmptyHeader = new[] { Tuple.Create ("application/json", 1.0m), Tuple.Create ("text/html", 0.9m), Tuple.Create ("*/*", 0.8m) };
        static Dictionary<string, string> moduleViews = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
        static IAccessControlMapper accessControlContext = null;
        static bool disableAuthentication;

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
                        var a = PrepareFilePath (d.Substring (modulesRoot.Length));//.Split ('/');
                        nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory (a, PrepareFilePath (d.Substring (root.Length))));
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
            disableAuthentication = BigDataPipeline.Core.PipelineService.Instance.SystemOptions.Get ("disableAuth", false);

            // configure nancy            
            StaticConfiguration.CaseSensitive = false;            
            Nancy.Json.JsonSettings.MaxJsonLength = 20 * 1024 * 1024;
            // check if the debugmode flag is enabled
            if (!BigDataPipeline.Core.PipelineService.Instance.SystemOptions.Get ("debugMode", false))
            {
                Nancy.Diagnostics.DiagnosticsHook.Disable (pipelines);
                StaticConfiguration.DisableErrorTraces = true;
                StaticConfiguration.EnableRequestTracing = false;

                // log any errors only as debug 
                pipelines.OnError.AddItemToStartOfPipeline ((ctx, ex) =>
                {
                    logger.Debug (ex);
                    return null;
                });
            }
            else
            {
                StaticConfiguration.DisableErrorTraces = false;
                StaticConfiguration.EnableRequestTracing = true;
                StaticConfiguration.Caching.EnableRuntimeViewDiscovery = true;
                StaticConfiguration.Caching.EnableRuntimeViewUpdates = true;                

                // log any errors as errors
                pipelines.OnError.AddItemToStartOfPipeline ((ctx, ex) =>
                {
                    logger.Error (ex);
                    return null;
                });
            }

#if DEBUG
            StaticConfiguration.DisableErrorTraces = false;
            StaticConfiguration.EnableRequestTracing = true;
            StaticConfiguration.Caching.EnableRuntimeViewDiscovery = true;
            StaticConfiguration.Caching.EnableRuntimeViewUpdates = true;
#endif

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
                }
            });

            // gzip compression
            pipelines.AfterRequest.AddItemToEndOfPipeline (NancyCompressionExtenstion.CheckForCompression);
                        
            // try to enable authentication            
            if (container.TryResolve<IAccessControlMapper> (out accessControlContext) &&
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
                    // note: the default CryptographyConfiguration uses the same salt key
                    CryptographyConfiguration = new Nancy.Cryptography.CryptographyConfiguration (
                        new Nancy.Cryptography.RijndaelEncryptionProvider (new Nancy.Cryptography.PassphraseKeyGenerator ("encryption" + this.GetType ().FullName, Encoding.UTF8.GetBytes ("PipelineSaltProvider"))),
                        new Nancy.Cryptography.DefaultHmacProvider (new Nancy.Cryptography.PassphraseKeyGenerator ("HMAC" + this.GetType ().FullName, Encoding.UTF8.GetBytes ("PipelineSaltProvider"))))
                };

                FormsAuthentication.Enable (pipelines, formsAuthConfiguration);
            }

            // Enable Squishit to bundle our javascript and css resources
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
        /// Lets adjust default container settings.
        /// Add this converter to the default settings of Newtonsoft JSON.NET.
        /// </summary>            
        protected override void ConfigureApplicationContainer (Nancy.TinyIoc.TinyIoCContainer container)
        {
            //base.ConfigureApplicationContainer (container);

            // substitute nancy default assembly registration to a faster selected loading... (5x faster loading...)
            var nancyEngineAssembly = typeof (NancyEngine).Assembly;
            HashSet<string> blackListedAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) { "mscorlib", "vshost", "BigDataPipeline", "NLog", "Newtonsoft.Json", "Topshelf", "Topshelf.Linux", "Topshelf.NLog", "AWSSDK", "Dapper", "Mono.CSharp", "Mono.Security", "NCrontab", "Renci.SshNet", "System.Net.FtpClient", "MongoDB.Bson", "MongoDB.Driver", "System.Data.SQLite", "System.Net.Http.Formatting", "System.Web.Razor", "Microsoft.Owin.Hosting", "Microsoft.Owin", "Owin" };
            container.AutoRegister (AppDomain.CurrentDomain.GetAssemblies ().Where (a => !a.GlobalAssemblyCache && !a.IsDynamic && !blackListedAssemblies.Contains (ParseAssemblyName (a.FullName))), Nancy.TinyIoc.DuplicateImplementationActions.RegisterMultiple, t => t.Assembly != nancyEngineAssembly);

            // register json.net default options
            container.Register<JsonSerializer, CustomJsonSerializer> ();

            //BigDataPipeline.Core.ModuleContainer.Instance.GetTypesOf<NancyModule> ();
        }

        /// <summary>
        /// Allow anonymous access only to the login page... 
        /// All other modules access must be authentication:
        /// 1 - forms authentication
        /// 2 - token authentication in the Header["Authorization"]
        /// 3 - token authentication in the request parameters: <request path>/?token=<token value>
        /// 4 - login and password authentication in the request parameters: <request path>/?login=<login>&password=<password>
        /// </summary>        
        private Response AllResourcesAuthentication (NancyContext ctx)
        {
            // if authenticated, go on...
            if (ctx.CurrentUser != null)
                return null;

            // if login module, go on... (here we can put other routes without authentication)
            if (ctx.Request.Url.Path.IndexOf ("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;

            // search for a session id or token
            if (accessControlContext == null)
                return null;

            // 1. check for token authentication: Header["Authorization"] with the sessionId/token 
            string authToken = ctx.Request.Headers.Authorization;
            if (!String.IsNullOrEmpty (authToken))
            {
                ctx.CurrentUser = accessControlContext.GetUserFromIdentifier (authToken, ctx);
            }

            // 2. check for token authentication: query parameter or form unencoded parameter
            if (ctx.CurrentUser == null)
            {
                authToken = TryGetRequestParameter (ctx, "token");
                if (!String.IsNullOrEmpty (authToken))
                {
                    ctx.CurrentUser = accessControlContext.GetUserFromIdentifier (authToken, ctx);
                }
            }

            // 3. finally, check if login and password were passed as parameters
            if (ctx.CurrentUser == null)
            {
                var password = TryGetRequestParameter (ctx, "password");                
                if (!String.IsNullOrEmpty (password))
                {
                    var login = TryGetRequestParameter (ctx, "login");
                    if (String.IsNullOrEmpty (login))
                        login = TryGetRequestParameter (ctx, "username");
                    if (!String.IsNullOrEmpty (login))
                        authToken = accessControlContext.OpenSession (login, password, TimeSpan.FromMinutes (30));
                    if (!String.IsNullOrEmpty (authToken))
                        ctx.CurrentUser = accessControlContext.GetUserFromIdentifier (authToken, ctx);
                }
            }

            // if authentication is disbled, go on...
            if (disableAuthentication)
                return null;
            // analise if we got an authenticated user            
            return (ctx.CurrentUser == null) ? new Nancy.Responses.HtmlResponse (HttpStatusCode.Unauthorized) : null;
        }

        /// ***********************
        /// Custom Path provider
        /// ***********************
        public class PathProvider : IRootPathProvider
        {
            static string _path = SetRootPath (AppDomain.CurrentDomain.BaseDirectory);//System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, @"site");//

            public string GetRootPath ()
            {
                return _path;
            }

            public static string SetRootPath (string fullPath)
            {
                fullPath = fullPath.Replace ('\\', '/');
                if (fullPath.Length == 0 || fullPath[fullPath.Length - 1] != '/')
                    fullPath += '/';
                if (fullPath.EndsWith ("/bin/", StringComparison.OrdinalIgnoreCase))
                {
                    var p = fullPath.LastIndexOf ("/bin/", StringComparison.OrdinalIgnoreCase);
                    fullPath = fullPath.Substring (0, p + 1);
                }
                _path = fullPath;
                return _path;
            }
        }

        protected override IRootPathProvider RootPathProvider
        {
            get { return new PathProvider (); }
        }

        #region *   JSON serialization options  *

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

                // note: this converter is added to the default settings of Newtonsoft JSON.NET in ConfigureApplicationContainer method
            }
        }

        #endregion

        #region *   Helper methods  *
        /// ***********************
        /// Helper methods
        /// ***********************
        private static string ParseAssemblyName (string name)
        {
            int i = name.IndexOf (',');
            return (i > 0) ? name.Substring (0, i) : name;
        }
        
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

        static string TryGetRequestParameter (NancyContext ctx, string parameter)
        {
            object p;
            if (((DynamicDictionary)ctx.Request.Query).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            if (((DynamicDictionary)ctx.Request.Form).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            if (ctx.Parameters != null && ctx.Parameters is DynamicDictionary &&
                ((DynamicDictionary)ctx.Parameters).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            return null;
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
