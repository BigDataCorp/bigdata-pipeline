using System;
using System.Linq;

namespace BigDataPipeline.Extensions
{
    public static class CommonExtensions
    {
        /// <summary>
        /// Converts a string to the desirable type with default value an error handling.
        /// </summary>
        /// <param name="text">The string.</param>
        /// <param name="raiseOnError">The raise on error.</param>
        /// <returns></returns>
        public static T ConvertTo<T> (this string text, bool raiseOnError = true)
        {
            return text.ConvertTo<T> (default (T), raiseOnError);
        }

        /// <summary>
        /// Converts a string to the desirable type with default value an error handling.
        /// </summary>
        /// <param name="input">The string.</param>
        /// <param name="defaultValue">The default value, if the conversion is not possible.</param>
        /// <param name="raiseOnError">The raise on error.</param>
        /// <returns></returns>
        /// <remarks>
        /// Acknowledgments:
        /// http://csharp-extension.blogspot.com.br/2011/07/convert-type-extension.html
        /// http://tiredblogger.wordpress.com/2008/04/15/convertto-extension-method/
        /// </remarks>
        public static T ConvertTo<T> (this object input, T defaultValue = default(T), bool raiseOnError = false)
        {
            if (input != null)
            {
                try
                {
                    return (T)Convert.ChangeType (input, typeof (T));
                }
                catch (Exception ex)
                {
                    if (raiseOnError)
                        throw ex;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Converts a string to the desirable type with default value an error handling.
        /// </summary>
        /// <param name="input">The string.</param>
        /// <param name="defaultValue">The default value, if the conversion is not possible</param>
        /// <param name="format">The format provider.</param>
        /// <param name="raiseOnError">The raise on error.</param>
        /// <returns></returns>
        /// Acknowledgments:
        /// http://csharp-extension.blogspot.com.br/2011/07/convert-type-extension.html
        /// http://tiredblogger.wordpress.com/2008/04/15/convertto-extension-method/
        /// </remarks>
        public static T ConvertTo<T> (this object input, T defaultValue, IFormatProvider format, bool raiseOnError = false)
        {
            if (input != null)
            {
                try
                {
                    return (T)Convert.ChangeType (input, typeof (T), format);
                }
                catch (Exception ex)
                {
                    if (raiseOnError)
                        throw ex;
                }
            }
            return defaultValue;
        }
    }
}