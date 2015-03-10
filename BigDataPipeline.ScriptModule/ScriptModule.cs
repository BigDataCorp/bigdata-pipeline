using BigDataPipeline.Interfaces;
using BigDataPipeline.ScriptModule.SimpleHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.ScriptModule
{
    public class ScriptModule : IActionModule
    { 
        public string GetDescription ()
        {
            return "Script execution Module - executes an csharp code that implements the IActionModule interface";
        }

        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("code", typeof (string), "Csharp code", true);
            yield return new ModuleParameterDetails ("mainClassName", typeof (string), "Name of the class that implements the IActionModule", true);
            yield return new ModuleParameterDetails ("assemblies", typeof (string), "CSV list of assemblies necessary to run the script", false);
        }

        public bool Execute (ISessionContext context)
        {
            var _logger = context.GetLogger ();
            var _options = context.Options;

            try
            {
                ScriptEvaluatorModule evaluator = new ScriptEvaluatorModule ();
                var assemblies = _options.Get ("assemblies", "").Split (',', ';', ' ', '|').Select (i =>
                {
                    try
                    {
                        return System.Reflection.Assembly.Load (i.Trim ());
                    }
                    catch
                    {
                        return null;
                    }
                }).ToList ();
                var ctx = new ScriptContext (_options.Get ("code", ""), _options.Get ("mainClassName", ""), assemblies);
                var action = evaluator.CreateInstance (ctx) as IActionModule;
                
                context.Error = ctx.Message;
                if (ctx.HasError)
                    return false;

                if (action == null)
                    throw new Exception ("Unable to create class derived from IActionModule");

                return action.Execute (context);
            }
            catch (Exception ex)
            {
                context.Error = ex.Message;
                _logger.Error (ex);
                return false;
            }
        }
    }
}
