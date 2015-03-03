using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline
{
    public class ConsoleUtils
    {
        public static readonly CultureInfo cultureUS = new CultureInfo ("en-US");
        public static readonly CultureInfo cultureBR = new CultureInfo ("pt-BR");

        public static FlexibleOptions ProgramOptions { get; private set; }

        internal static void DefaultProgramInitialization ()
        {
            // set culture info
            // net45 or higher
            CultureInfo.DefaultThreadCurrentCulture = cultureUS;
            CultureInfo.DefaultThreadCurrentUICulture = cultureUS;

            // some additional configuration
            // http://stackoverflow.com/questions/8971210/how-to-turn-off-the-automatic-proxy-detection-in-the-amazons3-object
            System.Net.WebRequest.DefaultWebProxy = null;

            // more concurrent connections to the same IP (avoid throttling) and other tuning
            // http://blogs.msdn.com/b/jpsanders/archive/2009/05/20/understanding-maxservicepointidletime-and-defaultconnectionlimit.aspx
            System.Net.ServicePointManager.DefaultConnectionLimit = 1024; // more concurrent connections to the same IP (avoid throttling)
            System.Net.ServicePointManager.MaxServicePointIdleTime = 20 * 1000; // release unused connections sooner (20 seconds)
            // since mono blocks all non intalled SSL root certificate, lets disable it!
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        static string _logFileName;
        static string _logLevel;

        /// <summary>
        /// Log initialization.
        /// </summary>
        internal static void InitializeLog (string logFileName = null, string logLevel = null)
        {
            // default parameters initialization from config file
            if (logFileName == null)
                logFileName = SimpleHelpers.ConfigManager.Get<string> ("logFilename", "${basedir}/log/" + typeof (Program).Namespace + ".log");
            if (logLevel == null)
                logLevel = SimpleHelpers.ConfigManager.Get ("logLevel", "Info");

            // check if log was initialized with same options
            if (_logFileName == logFileName && _logLevel == logLevel) 
                return;

            // save current log configuration
            _logFileName = logFileName;
            _logLevel = logLevel;

            // try to parse loglevel
            LogLevel currentLogLevel;
            try { currentLogLevel = LogLevel.FromString (logLevel); }
            catch { currentLogLevel = LogLevel.Info; }

            // prepare log configuration
            var config = new NLog.Config.LoggingConfiguration ();

            // console output
            var consoleTarget = new NLog.Targets.ColoredConsoleTarget ();
            consoleTarget.Layout = "${longdate}\t${callsite}\t${level}\t${message}\t${onexception: \\:[Exception] ${exception:format=tostring}}";

            config.AddTarget ("console", consoleTarget);

            var rule1 = new NLog.Config.LoggingRule ("*", LogLevel.Trace, consoleTarget);
            config.LoggingRules.Add (rule1);


            // file output
            var fileTarget = new NLog.Targets.FileTarget ();
            fileTarget.FileName = "${basedir}/log/" + typeof (Program).Namespace + ".log";
            fileTarget.Layout = "${longdate}\t${callsite}\t${level}\t\"${message}${onexception: \t [Exception] ${exception:format=tostring}}\"";
            fileTarget.ConcurrentWrites = true;
            fileTarget.AutoFlush = true;
            fileTarget.KeepFileOpen = true;
            fileTarget.DeleteOldFileOnStartup = false;
            fileTarget.ArchiveAboveSize = 2 * 1024 * 1024;  // 2 Mb
            fileTarget.MaxArchiveFiles = 10;
            fileTarget.ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Date;
            fileTarget.ArchiveDateFormat = "yyyyMMdd_HHmmss";

            // set file output to be async
            var wrapper = new NLog.Targets.Wrappers.AsyncTargetWrapper (fileTarget);

            config.AddTarget ("file", wrapper);

            // configure log from configuration file
            SimpleHelpers.ConfigManager.AddNonExistingKeys = true;
            fileTarget.FileName = logFileName;
            var rule2 = new NLog.Config.LoggingRule ("*", currentLogLevel, fileTarget);
            config.LoggingRules.Add (rule2);

            // set configuration options
            LogManager.Configuration = config;
        }

        /// <summary>
        /// Execute some housekeeping and closes the application.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        internal static void CloseApplication (int exitCode, bool exitApplication)
        {
            System.Threading.Thread.Sleep (0);
            // log error code and close log
            Console.WriteLine ("ExitCode = " + exitCode.ToString ());
            if (exitCode == 0)
                LogManager.GetCurrentClassLogger ().Info ("ExitCode", exitCode.ToString ());
            else
                LogManager.GetCurrentClassLogger ().Error ("ExitCode", exitCode.ToString ());
            LogManager.Flush ();
            // force garbage collector run
            // usefull for clearing COM interfaces or any other similar resource
            GC.Collect ();
            GC.WaitForPendingFinalizers ();
            System.Threading.Thread.Sleep (0);

            // set exit code and exit
            System.Environment.ExitCode = exitCode;
            if (exitApplication) 
                System.Environment.Exit (exitCode);
        }

        internal static FlexibleOptions CheckCommandLineParams (string[] args, bool thrownOnError)
        {
            return CheckCommandLineParams (args, null, thrownOnError);
        }

        /// <summary>
        /// Checks the command line params.
        /// </summary>
        /// <param name="args">The args.</param>
        internal static FlexibleOptions CheckCommandLineParams (string[] args, IDictionary<string, string> programOptions, bool thrownOnError)
        {
            FlexibleOptions mergedOptions = null;
            FlexibleOptions argsOptions = new FlexibleOptions ();
            FlexibleOptions localOptions = new FlexibleOptions ();
            FlexibleOptions externalLoadedOptions = null;

            var option_set = new Mono.Options.OptionSet ();
            option_set.Add ("?|help|h", "Prints out the options.", opt => show_help (opt, option_set))
                .Add ("logFilename=", "Log filename. Default value is: " + "${basedir}/log/" + typeof (Program).Namespace + ".log", opt => argsOptions.Set ("WebConfigurationFile", opt))
                .Add ("logLevel=", "Log level (Fatal, Error, Warn, Info, Debug, Trace, Off). Default value is Info", opt => argsOptions.Set ("logLevel", opt))
                .Add ("config=|webConfigurationFile=|S3ConfigurationPath=", "Address to a downloadable configuration file with json configuration options (default=[empty]).", opt => argsOptions.Set ("config", opt));

            // To add custom options just folow the example:            
            // option_set.Add ("t=|threads=", "Number of threads for the queue processing (default=1).", opt => ... );
            // option_set.Add ("q|queue", "Start processing queue.", opt => ...)

            try
            {
                // set custom options
                if (programOptions != null)
                {
                    foreach (var o in programOptions)
                    {
                        if (!option_set.Contains (o.Key))
                        {
                            string key = o.Key;
                            option_set.Add (o.Key + "=", o.Value, opt => argsOptions.Set (key, opt));
                        }
                    }
                }

                // parse local configuration file
                // display the options listed in the configuration file                
                foreach (var o in SimpleHelpers.ConfigManager.GetAll ())
                {
                    localOptions.Set (o.Key, o.Value);
                    if (!option_set.Contains (o.Key))
                    {
                        string key = o.Key;
                        if (key.IndexOf (':') >= 0 || key.IndexOf ('=') >= 0 || key.IndexOf ('|') >= 0) continue;
                        option_set.Add (o.Key + "=", "Option " + o.Key, opt => argsOptions.Set (key, opt));
                    }
                }

                // parse console arguments
                option_set.Parse (args);

                // adjust alias for web hosted configuration file
                if (String.IsNullOrEmpty (localOptions.Get ("config")))
                    localOptions.Set ("config", localOptions.Get ("S3ConfigurationPath", localOptions.Get ("webConfigurationFile")));

                // merge arguments with app.config options. Priority: arguments > app.config
                mergedOptions = FlexibleOptions.Merge (localOptions, argsOptions);

                // load and parse web hosted configuration file (priority order: argsOptions > localOptions)
                string externalConfigFile = mergedOptions.Get ("config", "");
                bool configAbortOnError = mergedOptions.Get ("configAbortOnError", true);
                if (!String.IsNullOrWhiteSpace (externalConfigFile))
                {
                    foreach (var file in externalConfigFile.Trim(' ', '\'', '"', '[', ']').Split (',', ';'))
                    {
                        LogManager.GetCurrentClassLogger ().Info ("Loading configuration file from {0} ...", externalConfigFile);
                        externalLoadedOptions = FlexibleOptions.Merge (externalLoadedOptions, LoadWebConfigurationFile (file.Trim (' ', '\'', '"'), configAbortOnError));
                    }
                }
            }
            catch (Mono.Options.OptionException ex)
            {
                show_help ("Error - usage is:", option_set, true);
                if (thrownOnError)
                    throw;
                LogManager.GetCurrentClassLogger ().Error (ex);
            }
            catch (Exception ex)
            {
                if (thrownOnError)
                    throw;
                LogManager.GetCurrentClassLogger ().Error (ex);
            }

            // merge options with the following priority:
            // 1. console arguments
            // 2. web configuration file
            // 3. local configuration file (app.config or web.config)
            mergedOptions = FlexibleOptions.Merge (externalLoadedOptions, mergedOptions);

            // reinitialize log options if different from local configuration file
            InitializeLog (mergedOptions.Get ("logFilename"), mergedOptions.Get ("logLevel", "Info"));

            // return final merged options
            ProgramOptions = mergedOptions;
            return mergedOptions;
        }

        private static FlexibleOptions LoadExtenalConfigurationFile (string filePath, bool thrownOnError)
        {
            if (filePath.StartsWith ("http", StringComparison.OrdinalIgnoreCase))
            {
                return LoadWebConfigurationFile (filePath, thrownOnError);
            }
            else
            {
                return LoadFileSystemConfigurationFile (filePath, thrownOnError);
            }           
        }

        private static FlexibleOptions LoadWebConfigurationFile (string filePath, bool thrownOnError)
        {
            var options = new FlexibleOptions ();
            using (WebClient client = new WebClient ())
            {
                try
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse (client.DownloadString (filePath));
                    foreach (var i in json)
                    {
                        options.Set (i.Key, i.Value.ToString (Newtonsoft.Json.Formatting.None));
                    }
                }
                catch (Exception ex)
                {
                    if (thrownOnError)
                        throw;
                    LogManager.GetCurrentClassLogger ().Error (ex);
                }
            }
            return options;
        }

        private static FlexibleOptions LoadFileSystemConfigurationFile (string filePath, bool thrownOnError)
        {
            var options = new FlexibleOptions ();
            using (WebClient client = new WebClient ())
            {
                try
                {
                    string text;
                    using (var file = new System.IO.StreamReader (filePath, Encoding.GetEncoding("IDO-8859-1"), true))
                    {
                        text = file.ReadToEnd ();
                    }
                    var json = Newtonsoft.Json.Linq.JObject.Parse (text);
                    foreach (var i in json)
                    {
                        options.Set (i.Key, i.Value.ToString (Newtonsoft.Json.Formatting.None));
                    }
                }
                catch (Exception ex)
                {
                    if (thrownOnError)
                        throw;
                    LogManager.GetCurrentClassLogger ().Error (ex);
                }
            }
            return options;
        }

        private static void show_help (string message, Mono.Options.OptionSet option_set, bool isError = false)
        {
            if (message == null) return;
            if (isError)
            {
                Console.Error.WriteLine (message);
                option_set.WriteOptionDescriptions (Console.Error);
            }
            else
            {
                Console.WriteLine (message);
                option_set.WriteOptionDescriptions (Console.Out);
            }
            CloseApplication (-40, true);
        }

        public static void DisplayHeader (params string[] messages)
        {
            Console.WriteLine ("##########################################");
            
            Console.WriteLine ("#  {0}", DateTime.Now.ToString ("yyyy/MM/dd HH:mm:ss"));
            
            if (messages == null)
            {
                Console.WriteLine ("#  ");
            }
            else
            {
                foreach (var msg in messages)
                {
                    Console.Write ("#  ");
                    Console.WriteLine (msg ?? "");
                }
            }            

            Console.WriteLine ("##########################################");
            Console.WriteLine ();
        }

        public static void WaitForAnyKey ()
        {
            WaitForAnyKey ("Press any key to continue...");
        }

        public static void WaitForAnyKey (string message)
        {
            Console.WriteLine (message);
            Console.ReadKey ();
        }

        public static string GetUserInput (string message)
        {
            message = (message ?? String.Empty).Trim ();
            Console.WriteLine (message);
            Console.Write ("> ");
            return Console.ReadLine ();
        }

        public static IEnumerable<string> GetUserInputAsList (string message)
        {
            message = (message ?? String.Empty).Trim ();
            Console.WriteLine (message + " (enter an empty line to stop)");
            Console.Write ("> ");
            var txt = Console.ReadLine ();
            while (!String.IsNullOrEmpty (txt))
            {
                yield return txt;
                Console.Write ("> ");
                txt = Console.ReadLine ();
            }
        }

        public static char GetUserInputKey (string message = null)
        {
            message = (message ?? "Press any key to continue...").Trim ();
            Console.WriteLine (message);
            Console.Write ("> ");
            return Console.ReadKey (false).KeyChar;
        }

        public static bool GetUserInputAsBool (string message)
        {
            bool done = false;
            while (!done)
            {
                // show message
                var res = GetUserInputKey (message + " (Y/N)");
                // treat input
                if (res == 'y' || res == 'Y')
                    return true;
                if (res == 'N' || res == 'n')
                    return false;
            }
            return false;
        }

        public static int GetUserInputAsInt (string message)
        {
            int value = 0;
            bool done = false;
            while (!done)
            {
                // show message
                var res = GetUserInput (message + " (integer)").Trim ();
                // treat input
                if (int.TryParse (res, out value))
                    break;
            }
            return value;
        }

        public static double GetUserInputAsDouble (string message)
        {
            double value = 0;
            bool done = false;
            while (!done)
            {
                // show message
                var res = GetUserInput (message + " (number)").Trim ();
                // treat input
                if (double.TryParse (res, out value))
                    break;
            }
            return value;
        }
    }
}
