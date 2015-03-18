using System;
using System.Collections.Generic;

namespace BigDataPipeline.Interfaces
{
    /// <summary>
    /// Module Container.
    /// </summary>
    public interface IModuleContainer
    {
        /// <summary>
        /// Gets an instance for a registered type.
        /// </summary>
        /// <typeparam name="T">The type of the T.</typeparam>
        /// <param name="fullTypeName">Full name of the type.</param>
        /// <returns></returns>
        T GetInstanceAs<T> (string fullTypeName) where T : class;

        /// <summary>
        /// Gets an instance for a registered type.
        /// </summary>
        /// <typeparam name="T">The type of the T.</typeparam>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        T GetInstanceAs<T> (Type type) where T : class;

        /// <summary>
        /// Gets an instance for a registered type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        object GetInstance (Type type);

        /// <summary>
        /// Gets an instance for a registered type by its full type name.
        /// </summary>
        /// <param name="fullTypeName">Full name of the type, namespace and class name.</param>
        /// <returns></returns>
        object GetInstance (string fullTypeName);

        /// <summary>
        /// Gets instances for all registered types for a given interface or base type.
        /// </summary>
        /// <typeparam name="T">The interface or base type.</typeparam>
        /// <returns>List of intances of registered types</returns>
        IEnumerable<T> GetInstancesOf<T> () where T : class;

        /// <summary>
        /// Gets an instance that implements the desired type T.
        /// </summary>
        /// <param name="type">The base type.</param>
        /// <returns></returns>
        T GetInstanceOf<T> () where T : class;

        /// <summary>
        /// Gets all registered types for a given interface or base type.
        /// </summary>
        /// <typeparam name="T">The interface or base type.</typeparam>
        /// <returns>List of registered types</returns>
        IEnumerable<Type> GetTypesOf<T> () where T : class;
    }
}