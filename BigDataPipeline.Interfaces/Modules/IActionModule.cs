using System.Collections.Generic;

namespace BigDataPipeline.Interfaces
{
    /// <summary>
    /// Basic Interface of a BigDataPipeline generic plugin/module
    /// </summary>
    public interface IActionModule
    {
        /// <summary>
        /// the description of the module.
        /// </summary>
        string GetDescription ();

        /// <summary>
        /// list of module parameters, used to request that a parameter and
        /// also contains the description of each of this module parameters.
        /// </summary>
        /// <returns>List of parameters details</returns>
        IEnumerable<ModuleParameterDetails> GetParameterDetails ();
        
        /// <summary>
        /// Executes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="dataStreams">The data streams.</param>
        /// <returns></returns>
        bool Execute (ISessionContext context);

    }
}
