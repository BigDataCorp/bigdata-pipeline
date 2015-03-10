using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace BigDataPipeline.Interfaces
{
    /// <summary>
    /// Basic Interface of a BigDataPipeline generic plugin/module
    /// </summary>
    public interface IScriptEvaluator
    {  
        /// <summary>
        /// Executes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        object CreateInstance (ScriptContext context);

    }

    public class ScriptContext : FlexibleObject
    {
        public string Code { get; set; }

        public bool HasError { get; set; }

        public string Message { get; set; }
        
        public string MainClassName { get; set; }

        public List<Assembly> Assemblies { get; set; }

        public ScriptContext () {}

        public ScriptContext (string csharpCode, string mainClassName)
        {
            Code = csharpCode;
            MainClassName = mainClassName;
        }

        public ScriptContext (string csharpCode, string mainClassName, IEnumerable<Assembly> assemblies)
        {
            Code = csharpCode;
            MainClassName = mainClassName;
            foreach (var a in assemblies)
                AddReference (a);
        }

        public ScriptContext AddReference (Assembly assembly)
        {
            if (Assemblies == null)
                Assemblies = new List<Assembly> ();
            if (!Assemblies.Any (i => i.FullName == assembly.FullName))
                Assemblies.Add (assembly);
            return this;
        }

        public ScriptContext AddReference (Type type)
        {
            return AddReference (type.Assembly);            
        }
    }
}
