using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.Core
{
    public class AppDomainHelper
    {
        /// <summary>
        /// Creates a new AppDomain.
        /// </summary>        
        /// <param name="enableShadowCopy">If true, enables shadow copying within the AppDomain.</param>
        /// <param name="configFile">The app.config or web.config file for the new AppDomain. If null will use the current one.</param>
        /// <param name="applicationBaseDirectory">The application base directory for the new AppDomain. If null will use the current one.</param>        
        /// <returns>The new AppDomain.</returns>
        public static AppDomain CreateAppDomain (bool enableShadowCopy, string configFile = null, string applicationBaseDirectory = null)
        {
            var baseDirectory = applicationBaseDirectory ?? AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            
            AppDomainSetup appDomainSetup = new AppDomainSetup ()
            {
                //ApplicationName = String.Empty,
                ApplicationBase = baseDirectory  
            };

            if (!String.IsNullOrEmpty (configFile))
            {
                // lets use the application app.config or web.config file
                appDomainSetup.ConfigurationFile = configFile;
            }

            if (enableShadowCopy)
            {
                var cachePath = System.IO.Path.Combine (baseDirectory, "ShadowCopyCache");
                if (!System.IO.Directory.Exists (cachePath))
                {
                    System.IO.Directory.CreateDirectory (cachePath);
                }

                appDomainSetup.CachePath = cachePath;
                appDomainSetup.ShadowCopyFiles = @"true";
                appDomainSetup.ShadowCopyDirectories = String.Join (";", new List<string>
                {
                    baseDirectory,
                    System.IO.Path.Combine (baseDirectory, "modules")
                });
            }


            return AppDomain.CreateDomain (appDomainSetup.ApplicationName, AppDomain.CurrentDomain.Evidence, appDomainSetup);
        }

        /// <summary>
        /// Creates a remote instance of a type within another AppDomain.
        /// </summary>
        /// <remarks>
        /// This method first uses <see cref="AppDomain.CreateInstanceAndUnwrap(string, string)" /> to
        /// try create an instance of the type.  If that fails, it uses <see cref="AppDomain.CreateInstanceFromAndUnwrap(string, string)" />
        /// to load the assembly that contains the type into the AppDomain then create an instance of the type.
        /// </remarks>
        /// <param name="appDomain">The AppDomain in which to create the instance.</param>
        /// <param name="type">The type to instantiate.</param>
        /// <param name="args">The constructor arguments for the type.</param>
        /// <returns>The remote instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="appDomain"/> or <paramref name="type"/>
        /// is null.</exception>
        public static object CreateRemoteInstance (AppDomain appDomain, Type type, params object[] args)
        {
            if (appDomain == null)
                throw new ArgumentNullException ("appDomain");
            if (type == null)
                throw new ArgumentNullException ("type");

            var assembly = type.Assembly;
            return appDomain.CreateInstanceAndUnwrap (assembly.FullName, type.FullName, false,
                    BindingFlags.Default, null, args, null, null, null);
        }

        /// <summary>
        /// Disposes the specified app domain.
        /// </summary>
        /// <param name="appDomain">The app domain.</param>
        public static void Dispose (AppDomain appDomain)
        {
            AppDomain.Unload (appDomain);
        }
    }
}
