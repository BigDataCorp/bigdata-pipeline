using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCaseModule
{
    public class SimpleAction : IActionModule
    {
        /// <summary>
        /// The description of the module.<para/>
        /// This description can contain html markup or Markdown (github) and 
        /// will be displayed in the web interface as a reference for this module usage.
        /// </summary>
        /// <returns></returns>
        public string GetDescription ()
        {
            string filePath = System.IO.Path.Combine ((new System.Uri (System.Reflection.Assembly.GetExecutingAssembly ().CodeBase)).AbsolutePath, "README.md");
            if (System.IO.File.Exists (filePath))
            {
                using (var file = new System.IO.StreamReader (filePath, Encoding.GetEncoding ("ISO-8859-1"), true))
                    return file.ReadToEnd ();                    
            }
            return "";
        }

        /// <summary>
        /// Gets the Descriptions of the parameters that this module uses.<para/>
        /// Must list both the required parameters and the optional parameters.<para/>
        /// This list is also displayed in the web interface as a reference for this module usage.
        /// </summary>
        /// <returns>List of Module Parameters Details</returns>
        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("Parameter1", typeof (string), "First parameter...", true);
        }

        /// <summary>
        /// Executes the module action.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public bool Execute (ISessionContext context)
        {
            var _logger = context.GetLogger ();
            var _options = context.Options;

            try
            {
                _logger.Trace ("Start");

                var inputStream = context.GetInputStream ();
                _logger.Info (inputStream != null ? "Stream found!!!" : "Stream not found");

                Record inputData = inputStream != null ? inputStream.GetStream ().FirstOrDefault () : new Record ();

                string kind = inputData.Get ("kind", "<empty>");
                _logger.Info ("kind = " + kind);
                _logger.Info ("count = " + inputData.Get ("count", "0"));

                // increment counter
                int c = inputData.Get ("count", 0);
                inputData.Set ("count", c + 1);

                // fire other events
                if (inputData.Get ("count", 0) < 2)
                {
                    context.EmitEvent ("this.testCase1", new Record ().Set ("kind", "local").Set ("count", inputData.Get ("count", 0)));
                    context.EmitEvent ("testCase2", new Record ().Set ("kind", "global").Set ("count", inputData.Get ("count", 0)));
                } 

                _logger.Success ("Done");
            }
            catch (Exception ex)
            {
                context.Error = ex.Message;
                _logger.Error (ex);
                return false;
            }

            _logger.Trace ("End");
            return true;
        }
    }
}
