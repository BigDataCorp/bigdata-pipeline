using Nancy;
using Nancy.Bootstrapper;
using Nancy.Cookies;
using Nancy.Helpers;
using Nancy.Extensions;
using System;
using Nancy.Cryptography;

namespace BigDataPipeline.Web
{
    /// <summary>
    /// Nancy forms authentication implementation
    /// </summary>
    public static class FormsAuthentication
    {
        private static string formsAuthenticationCookieName = "_pcfa";

        // TODO - would prefer not to hold this here, but the redirect response needs it
        private static FormsAuthenticationConfiguration currentConfiguration;

        /// <summary>
        /// Gets or sets the forms authentication cookie name
        /// </summary>
        public static string FormsAuthenticationCookieName
        {
            get
            {
                return formsAuthenticationCookieName;
            }

            set
            {
                formsAuthenticationCookieName = value;
            }
        }

        /// <summary>
        /// Enables forms authentication for the application
        /// </summary>
        /// <param name="pipelines">Pipelines to add handlers to (usually "this")</param>
        /// <param name="configuration">Forms authentication configuration</param>
        public static void Enable (IPipelines pipelines, FormsAuthenticationConfiguration configuration)
        {
            if (pipelines == null)
            {
                throw new ArgumentNullException ("pipelines");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException ("configuration");
            }

            if (!configuration.IsValid)
            {
                throw new ArgumentException ("Configuration is invalid", "configuration");
            }

            currentConfiguration = configuration;

            pipelines.BeforeRequest.AddItemToStartOfPipeline (GetLoadAuthenticationHook (configuration));
            if (!configuration.DisableRedirect)
            {
                pipelines.AfterRequest.AddItemToEndOfPipeline (GetRedirectToLoginHook (configuration));
            }
        }        

        /// <summary>
        /// Creates a response that sets the authentication cookie and redirects
        /// the user back to where they came from.
        /// </summary>
        /// <param name="context">Current context</param>
        /// <param name="userIdentifier">User identifier session</param>
        /// <param name="cookieExpiry">Optional expiry date for the cookie (for 'Remember me')</param>
        /// <param name="fallbackRedirectUrl">Url to redirect to if none in the querystring</param>
        /// <returns>Nancy response with redirect.</returns>
        public static Response UserLoggedInRedirectResponse (NancyContext context, string userIdentifier, DateTime? cookieExpiry = null, string fallbackRedirectUrl = null)
        {
            var redirectUrl = fallbackRedirectUrl;

            if (string.IsNullOrEmpty (redirectUrl))
            {
                redirectUrl = context.Request.Url.BasePath;
            }

            if (string.IsNullOrEmpty (redirectUrl))
            {
                redirectUrl = "/";
            }

            string redirectQuerystringKey = GetRedirectQuerystringKey (currentConfiguration);

            if (context.Request.Query[redirectQuerystringKey].HasValue)
            {
                var queryUrl = (string)context.Request.Query[redirectQuerystringKey];

                if (context.IsLocalUrl (queryUrl))
                {
                    redirectUrl = queryUrl;
                }
            }

            var response = context.GetRedirect (redirectUrl);
            var authenticationCookie = BuildCookie (userIdentifier, cookieExpiry, currentConfiguration);
            response.AddCookie (authenticationCookie);

            return response;
        }

        /// <summary>
        /// Logs the user in.
        /// </summary>
        /// <param name="userIdentifier">User identifier session</param>
        /// <param name="cookieExpiry">Optional expiry date for the cookie (for 'Remember me')</param>
        /// <returns>Nancy response with status <see cref="HttpStatusCode.OK"/></returns>
        public static Response UserLoggedInResponse (string userIdentifier, DateTime? cookieExpiry = null)
        {
            var response =
                (Response)HttpStatusCode.OK;

            var authenticationCookie = 
                BuildCookie (userIdentifier, cookieExpiry, currentConfiguration);

            response.AddCookie (authenticationCookie);

            return response;
        }

        /// <summary>
        /// Logs the user out and redirects them to a URL
        /// </summary>
        /// <param name="context">Current context</param>
        /// <param name="redirectUrl">URL to redirect to</param>
        /// <returns>Nancy response</returns>
        public static Response LogOutAndRedirectResponse (NancyContext context, string redirectUrl)
        {
            var response = context.GetRedirect (redirectUrl);
            var authenticationCookie = BuildLogoutCookie (currentConfiguration);
            response.AddCookie (authenticationCookie);

            return response;
        }

        /// <summary>
        /// Logs the user out.
        /// </summary>
        /// <returns>Nancy response</returns>
        public static Response LogOutResponse ()
        {
            var response =
                (Response)HttpStatusCode.OK;

            var authenticationCookie = 
                BuildLogoutCookie (currentConfiguration);

            response.AddCookie (authenticationCookie);

            return response;
        }

        /// <summary>
        /// Gets the pre request hook for loading the authenticated user's details
        /// from the cookie.
        /// </summary>
        /// <param name="configuration">Forms authentication configuration to use</param>
        /// <returns>Pre request hook delegate</returns>
        private static Func<NancyContext, Response> GetLoadAuthenticationHook (FormsAuthenticationConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException ("configuration");
            }

            return context =>
            {
                string sessionId = GetAuthenticatedUserFromCookie (context, configuration);

                if (!String.IsNullOrEmpty (sessionId))
                {
                    context.CurrentUser = configuration.UserMapper.GetUserFromIdentifier (sessionId, context);
                }

                return null;
            };
        }

        /// <summary>
        /// Gets the post request hook for redirecting to the login page
        /// </summary>
        /// <param name="configuration">Forms authentication configuration to use</param>
        /// <returns>Post request hook delegate</returns>
        private static Action<NancyContext> GetRedirectToLoginHook (FormsAuthenticationConfiguration configuration)
        {
            return context =>
            {
                if (context.Response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    string redirectQuerystringKey = GetRedirectQuerystringKey (configuration);

                    context.Response = context.GetRedirect (
                        string.Format ("{0}?{1}={2}",
                        configuration.RedirectUrl,
                        redirectQuerystringKey,
                        context.ToFullPath ("~" + context.Request.Path + HttpUtility.UrlEncode (context.Request.Url.Query))));
                }
            };
        }

        /// <summary>
        /// Gets the authenticated user session from the incoming request cookie if it exists
        /// and is valid.
        /// </summary>
        /// <param name="context">Current context</param>
        /// <param name="configuration">Current configuration</param>
        /// <returns>Returns user session, or null if not present or invalid</returns>
        private static string GetAuthenticatedUserFromCookie (NancyContext context, FormsAuthenticationConfiguration configuration)
        {
            if (!context.Request.Cookies.ContainsKey (formsAuthenticationCookieName))
            {
                return null;
            }

            var cookieValueEncrypted = context.Request.Cookies[formsAuthenticationCookieName];

            if (string.IsNullOrEmpty (cookieValueEncrypted))
            {
                return null;
            }

            return DecryptAndValidateAuthenticationCookie (cookieValueEncrypted, configuration);
        }

        /// <summary>
        /// Build the forms authentication cookie
        /// </summary>
        /// <param name="userIdentifier">Authenticated user identifier</param>
        /// <param name="cookieExpiry">Optional expiry date for the cookie (for 'Remember me')</param>
        /// <param name="configuration">Current configuration</param>
        /// <returns>Nancy cookie instance</returns>
        private static INancyCookie BuildCookie (string userIdentifier, DateTime? cookieExpiry, FormsAuthenticationConfiguration configuration)
        {
            var cookieContents = EncryptAndSignCookie (userIdentifier.ToString (), configuration);

            var cookie = new NancyCookie (formsAuthenticationCookieName, cookieContents, true, configuration.RequiresSSL, cookieExpiry);

            if (!string.IsNullOrEmpty (configuration.Domain))
            {
                cookie.Domain = configuration.Domain;
            }

            if (!string.IsNullOrEmpty (configuration.Path))
            {
                cookie.Path = configuration.Path;
            }

            return cookie;
        }

        /// <summary>
        /// Builds a cookie for logging a user out
        /// </summary>
        /// <param name="configuration">Current configuration</param>
        /// <returns>Nancy cookie instance</returns>
        private static INancyCookie BuildLogoutCookie (FormsAuthenticationConfiguration configuration)
        {
            var cookie = new NancyCookie (formsAuthenticationCookieName, String.Empty, true, configuration.RequiresSSL, DateTime.Now.AddDays (-1));

            if (!string.IsNullOrEmpty (configuration.Domain))
            {
                cookie.Domain = configuration.Domain;
            }

            if (!string.IsNullOrEmpty (configuration.Path))
            {
                cookie.Path = configuration.Path;
            }

            return cookie;
        }

        /// <summary>
        /// Encrypt and sign the cookie contents
        /// </summary>
        /// <param name="cookieValue">Plain text cookie value</param>
        /// <param name="configuration">Current configuration</param>
        /// <returns>Encrypted and signed string</returns>
        private static string EncryptAndSignCookie (string cookieValue, FormsAuthenticationConfiguration configuration)
        {
            var encryptedCookie = configuration.CryptographyConfiguration.EncryptionProvider.Encrypt (cookieValue);
            var hmacBytes = GenerateHmac (encryptedCookie, configuration);
            var hmacString = Convert.ToBase64String (hmacBytes);

            return String.Format ("{1}{0}", encryptedCookie, hmacString);
        }

        /// <summary>
        /// Generate a hmac for the encrypted cookie string
        /// </summary>
        /// <param name="encryptedCookie">Encrypted cookie string</param>
        /// <param name="configuration">Current configuration</param>
        /// <returns>Hmac byte array</returns>
        private static byte[] GenerateHmac (string encryptedCookie, FormsAuthenticationConfiguration configuration)
        {
            return configuration.CryptographyConfiguration.HmacProvider.GenerateHmac (encryptedCookie);
        }

        /// <summary>
        /// Decrypt and validate an encrypted and signed cookie value
        /// </summary>
        /// <param name="cookieValue">Encrypted and signed cookie value</param>
        /// <param name="configuration">Current configuration</param>
        /// <returns>Decrypted value, or empty on error or if failed validation</returns>
        public static string DecryptAndValidateAuthenticationCookie (string cookieValue, FormsAuthenticationConfiguration configuration)
        {
            // TODO - shouldn't this be automatically decoded by nancy cookie when that change is made?
            var decodedCookie = Nancy.Helpers.HttpUtility.UrlDecode (cookieValue);

            var hmacStringLength = Base64Helpers.GetBase64Length (configuration.CryptographyConfiguration.HmacProvider.HmacLength);

            var encryptedCookie = decodedCookie.Substring (hmacStringLength);
            var hmacString = decodedCookie.Substring (0, hmacStringLength);

            var encryptionProvider = configuration.CryptographyConfiguration.EncryptionProvider;

            // Check the hmacs, but don't early exit if they don't match
            var hmacBytes = Convert.FromBase64String (hmacString);
            var newHmac = GenerateHmac (encryptedCookie, configuration);
            var hmacValid = HmacComparer.Compare (newHmac, hmacBytes, configuration.CryptographyConfiguration.HmacProvider.HmacLength);

            var decrypted = encryptionProvider.Decrypt (encryptedCookie);

            // Only return the decrypted result if the hmac was ok
            return hmacValid ? decrypted : string.Empty;
        }

        /// <summary>
        /// Gets the redirect query string key from <see cref="FormsAuthenticationConfiguration"/>
        /// </summary>
        /// <param name="configuration">The forms authentication configuration.</param>
        /// <returns>Redirect Querystring key</returns>
        private static string GetRedirectQuerystringKey (FormsAuthenticationConfiguration configuration)
        {
            string redirectQuerystringKey = null;

            if (configuration != null)
            {
                redirectQuerystringKey = configuration.RedirectQuerystringKey;
            }

            if (string.IsNullOrWhiteSpace (redirectQuerystringKey))
            {
                redirectQuerystringKey = FormsAuthenticationConfiguration.DefaultRedirectQuerystringKey;
            }

            return redirectQuerystringKey;
        }
    }

    /// <summary>
    /// Configuration options for forms authentication
    /// </summary>
    public class FormsAuthenticationConfiguration
    {
        internal const string DefaultRedirectQuerystringKey = "returnUrl";

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsAuthenticationConfiguration"/> class.
        /// </summary>
        public FormsAuthenticationConfiguration ()
            : this (CryptographyConfiguration.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsAuthenticationConfiguration"/> class.
        /// </summary>
        /// <param name="cryptographyConfiguration">Cryptography configuration</param>
        public FormsAuthenticationConfiguration (CryptographyConfiguration cryptographyConfiguration)
        {
            CryptographyConfiguration = cryptographyConfiguration;
            RedirectQuerystringKey = DefaultRedirectQuerystringKey;
        }

        /// <summary>
        /// Gets or sets the forms authentication query string key for storing the return url
        /// </summary>
        public string RedirectQuerystringKey { get; set; }

        /// <summary>
        /// Gets or sets the redirect url for pages that require authentication
        /// </summary>
        public string RedirectUrl { get; set; }

        /// <summary>
        /// Gets or sets the username/identifier mapper
        /// </summary>
        public IAccessControlMapper UserMapper { get; set; }

        /// <summary>
        /// Gets or sets RequiresSSL property
        /// </summary>
        /// <value>The flag that indicates whether SSL is required</value>
        public bool RequiresSSL { get; set; }

        /// <summary>
        /// Gets or sets whether to redirect to login page during unauthorized access.
        /// </summary>
        public bool DisableRedirect { get; set; }

        /// <summary>
        /// Gets or sets the domain of the auth cookie
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Gets or sets the path of the auth cookie
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the cryptography configuration
        /// </summary>
        public CryptographyConfiguration CryptographyConfiguration { get; set; }

        /// <summary>
        /// Gets a value indicating whether the configuration is valid or not.
        /// </summary>
        public virtual bool IsValid
        {
            get
            {
                if (!this.DisableRedirect && string.IsNullOrEmpty (this.RedirectUrl))
                {
                    return false;
                }

                if (this.UserMapper == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration.EncryptionProvider == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration.HmacProvider == null)
                {
                    return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Module extensions for login/logout of forms auth
    /// </summary>
    public static class ModuleExtensions
    {
        /// <summary>
        /// Logs the user in and returns either an empty 200 response for ajax requests, or a redirect response for non-ajax. <seealso cref="RequestExtensions.IsAjaxRequest"/>
        /// </summary>
        /// <param name="module">Nancy module</param>
        /// <param name="userIdentifier">User identifier session</param>
        /// <param name="cookieExpiry">Optional expiry date for the cookie (for 'Remember me')</param>
        /// <param name="fallbackRedirectUrl">Url to redirect to if none in the querystring</param>
        /// <returns>Nancy response with redirect if request was not ajax, otherwise with OK.</returns>
        public static Response Login (this INancyModule module, string userIdentifier, DateTime? cookieExpiry = null, string fallbackRedirectUrl = "/")
        {
            return module.Context.Request.IsAjaxRequest () ?
                LoginWithoutRedirect (module, userIdentifier, cookieExpiry) :
                LoginAndRedirect (module, userIdentifier, cookieExpiry, fallbackRedirectUrl);
        }

        /// <summary>
        /// Logs the user in with the given user session and redirects.
        /// </summary>
        /// <param name="module">Nancy module</param>
        /// <param name="userIdentifier">User identifier session</param>
        /// <param name="cookieExpiry">Optional expiry date for the cookie (for 'Remember me')</param>
        /// <param name="fallbackRedirectUrl">Url to redirect to if none in the querystring</param>
        /// <returns>Nancy response instance</returns>
        public static Response LoginAndRedirect (this INancyModule module, string userIdentifier, DateTime? cookieExpiry = null, string fallbackRedirectUrl = "/")
        {
            return FormsAuthentication.UserLoggedInRedirectResponse (module.Context, userIdentifier, cookieExpiry, fallbackRedirectUrl);
        }

        /// <summary>
        /// Logs the user in with the given user session and returns ok response.
        /// </summary>
        /// <param name="module">Nancy module</param>
        /// <param name="userIdentifier">User identifier session</param>
        /// <param name="cookieExpiry">Optional expiry date for the cookie (for 'Remember me')</param>
        /// <returns>Nancy response instance</returns>
        public static Response LoginWithoutRedirect (this INancyModule module, string userIdentifier, DateTime? cookieExpiry = null)
        {
            return FormsAuthentication.UserLoggedInResponse (userIdentifier, cookieExpiry);
        }

        /// <summary>
        /// Logs the user out and returns either an empty 200 response for ajax requests, or a redirect response for non-ajax. <seealso cref="RequestExtensions.IsAjaxRequest"/>
        /// </summary>
        /// <param name="module">Nancy module</param>
        /// <param name="redirectUrl">URL to redirect to</param>
        /// <returns>Nancy response with redirect if request was not ajax, otherwise with OK.</returns>
        public static Response Logout (this INancyModule module, string redirectUrl)
        {
            return module.Context.Request.IsAjaxRequest () ?
               FormsAuthentication.LogOutResponse () :
               FormsAuthentication.LogOutAndRedirectResponse (module.Context, redirectUrl);
        }

        /// <summary>
        /// Logs the user out and redirects
        /// </summary>
        /// <param name="module">Nancy module</param>
        /// <param name="redirectUrl">URL to redirect to</param>
        /// <returns>Nancy response instance</returns>
        public static Response LogoutAndRedirect (this INancyModule module, string redirectUrl)
        {
            return FormsAuthentication.LogOutAndRedirectResponse (module.Context, redirectUrl);
        }

        /// <summary>
        /// Logs the user out without a redirect
        /// </summary>
        /// <param name="module">Nancy module</param>
        /// <returns>Nancy response instance</returns>
        public static Response LogoutWithoutRedirect (this INancyModule module)
        {
            return FormsAuthentication.LogOutResponse ();
        }
    }
}