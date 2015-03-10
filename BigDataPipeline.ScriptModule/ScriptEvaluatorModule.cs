using BigDataPipeline.Interfaces;
using BigDataPipeline.ScriptModule.SimpleHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigDataPipeline.ScriptModule
{
    public class ScriptEvaluatorModule : IScriptEvaluator
    {        
        static Dictionary<long, Mono.CSharp.CompiledMethod> cache = new Dictionary<long, Mono.CSharp.CompiledMethod> ();

        public object CreateInstance (ScriptContext context)
        {
            context.HasError = false;
            context.Message = null;
            try
            {
                Mono.CSharp.CompiledMethod _ctor;
                long hash = context.Code.GetHashCode () + 
                    context.MainClassName.GetHashCode() + 
                    context.Assemblies.OrderBy (i => i.FullName).Select (i => i.FullName.GetHashCode ()).Sum();
                
                if (!cache.TryGetValue(hash, out _ctor))
                {
                    var s = new ScriptEvaluator (context.Code, context.MainClassName);
                    s.AddReference (typeof (IScriptEvaluator));
                    s.AddReference (this.GetType ());
                    foreach (var a in context.Assemblies)
                        s.AddReference (a);

                    s.Compile ();

                    context.HasError = s.HasError;
                    context.Message = s.Message;

                    _ctor = s.CreateMethod;
                    if (_ctor == null)
                        return null;
                    
                    lock (cache)
                    {
                        cache[hash] = _ctor;                        
                    }
                }

                object result = null;
                _ctor (ref result);
                return result;       
            }
            catch (Exception ex)
            {
                context.HasError = true;
                context.Message = ex.Message;
            }
            return null;
        }
    }
}
