#region Disclaimer/Info

///////////////////////////////////////////////////////////////////////////////////////////////////
// Subtext WebLog
// 
// Subtext is an open source weblog system that is a fork of the .TEXT
// weblog system.
//
// For updated news and information please visit http://subtextproject.com/
// Subtext is hosted at Google Code at http://code.google.com/p/subtext/
// The development mailing list is at subtext-devs@lists.sourceforge.net 
//
// This project is licensed under the BSD license.  See the License.txt file for more information.
///////////////////////////////////////////////////////////////////////////////////////////////////

#endregion

using System;
using System.Linq;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Subtext.Framework.Configuration;
using Subtext.Framework.Routing;
using Subtext.Framework.Text;

namespace Subtext.Framework.Web
{
    /// <summary>
    /// Static containing helper methods for HTTP operations.
    /// </summary>
    public static class HttpHelper
    {
        private const int defaultTimeout = 60000;

        private static readonly string userAgent = VersionInfo.UserAgent
                                                   + " (" + Environment.OSVersion + "; .NET CLR " + Environment.Version +
                                                   ")";

        private static string referer = @"http://SubtextProject.com/Services/default.htm";

        /// <summary>
        /// Sets the file not found response.
        /// </summary>
        public static void SetFileNotFoundResponse()
        {
            if(HttpContext.Current != null && HttpContext.Current.Response != null)
            {
                SetFileNotFoundResponse(Config.GetFileNotFoundPage());
            }
        }

        /// <param name="fileNotFoundPage">The file not found page.</param>
        private static void SetFileNotFoundResponse(string fileNotFoundPage)
        {
            HttpContext.Current.Response.StatusCode = 404;
            if(fileNotFoundPage != null)
            {
                HttpContext.Current.Response.Redirect(fileNotFoundPage, true);
            }
        }

        /// <summary>
        /// Gets if modified since date.
        /// </summary>
        /// <returns></returns>
        public static DateTime GetIfModifiedSinceDateUtc(HttpRequestBase request)
        {
            if(request != null)
            {
                string ifModified = request.Headers["If-Modified-Since"];
                if(!string.IsNullOrEmpty(ifModified))
                {
                    return DateTimeHelper.ParseUnknownFormatUTC(ifModified);
                }
            }
            return NullValue.NullDateTime;
        }


        /// <summary>
        /// Creates an <see cref="HttpWebRequest" /> for the specified URL..
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static HttpWebRequest CreateRequest(Uri url)
        {
            WebRequest req = WebRequest.Create(url);
            SetProxy(req);
            var wreq = req as HttpWebRequest;
            if(null != wreq)
            {
                wreq.UserAgent = userAgent;
                wreq.Referer = referer;
                wreq.Timeout = defaultTimeout;
            }
            return wreq;
        }

        /// <summary>
        /// Returns an <see cref="HttpWebResponse" /> for the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static HttpWebResponse GetResponse(Uri url)
        {
            HttpWebRequest request = CreateRequest(url);

            return (HttpWebResponse)request.GetResponse();
        }

        /// <summary>
        /// Returns the text of the page specified by the URL..
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static string GetPageText(Uri url)
        {
            HttpWebResponse response = GetResponse(url);
            using(Stream s = response.GetResponseStream())
            {
                string enc = response.ContentEncoding;
                if(enc == null || enc.Trim().Length == 0)
                {
                    enc = "us-ascii";
                }
                Encoding encode = Encoding.GetEncoding(enc);
                using(var sr = new StreamReader(s, encode))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Returns the IP Address of the user making the current request.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static IPAddress GetUserIpAddress(HttpContextBase context)
        {
            if(context == null)
            {
                return IPAddress.None;
            }

            string result = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if(String.IsNullOrEmpty(result))
            {
                result = HttpContext.Current.Request.UserHostAddress;
            }
            else
            {
                // Requests behind a proxy might contain multiple IP 
                // addresses in the forwarding header.
                if(result.IndexOf(",", StringComparison.Ordinal) > 0)
                {
                    result = StringHelper.LeftBefore(result, ",");
                }
            }

            IPAddress ipAddress;
            if(IPAddress.TryParse(result, out ipAddress))
            {
                return ipAddress;
            }
            return IPAddress.None;
        }

        /// <summary>
        /// Combines Two Web Paths much like the Path.Combine method.
        /// </summary>
        /// <param name="uriOne">The URI one.</param>
        /// <param name="uriTwo">The URI two.</param>
        /// <returns></returns>
        public static string CombineWebPaths(string uriOne, string uriTwo)
        {
            string newUri = (uriOne + uriTwo);
            return newUri.Replace("//", "/");
        }

        /// <summary>
        /// Determines whether the request is for a static file.
        /// </summary>
        /// <returns>
        /// 	<c>true</c> if [is static file request]; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsStaticFileRequest(this HttpRequestBase request)
        {
            if(request == null)
            {
                throw new ArgumentNullException("request");
            }

            return request.Url.IsStaticFileRequest();
        }

        /// <summary>
        /// Determines whether the request is for a static file.
        /// </summary>
        /// <returns>
        /// 	<c>true</c> if [is static file request]; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsStaticFileRequest(this Uri url)
        {
            string filePath = url.AbsolutePath;

            return filePath.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                   || filePath.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sets the proxy on the request if a proxy is configured in Web.config.
        /// </summary>
        /// <param name="request"></param>
        public static void SetProxy(WebRequest request)
        {
            IWebProxy proxy = GetProxy();
            if(proxy != null)
            {
                request.Proxy = proxy;
            }
        }

        internal static IWebProxy GetProxy()
        {
            if(String.IsNullOrEmpty(ConfigurationManager.AppSettings["ProxyHost"]))
            {
                return null;
            }

            string proxyHost = ConfigurationManager.AppSettings["ProxyHost"];

            int proxyPort;
            IWebProxy proxy = int.TryParse(ConfigurationManager.AppSettings["ProxyPort"], out proxyPort) ? new WebProxy(proxyHost, proxyPort) : new WebProxy(proxyHost);
            if(!String.IsNullOrEmpty(ConfigurationManager.AppSettings["ProxyUsername"]))
            {
                proxy.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["ProxyUsername"],
                                                          ConfigurationManager.AppSettings["ProxyPassword"]);
            }
            return proxy;
        }

        /// <summary>
        /// If the URL is is the format ~/SomePath, this 
        /// method expands the tilde using the app path.
        /// </summary>
        /// <param name="path"></param>
        public static string ExpandTildePath(string path)
        {
            if(String.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string reference = path;
            if(reference.Substring(0, 2) == "~/")
            {
                string appPath = HttpContext.Current.Request.ApplicationPath ?? string.Empty;
                if(appPath.EndsWith("/", StringComparison.Ordinal))
                {
                    appPath = appPath.Left(appPath.Length - 1);
                }
                return appPath + reference.Substring(1);
            }
            return path;
        }

        /// <summary>
        /// If the URL is is the format ~/SomePath, this 
        /// method expands the tilde using the app path.
        /// </summary>
        public static VirtualPath ExpandTildePath(this HttpContextBase httpContext, string path)
        {
            if(String.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string reference = path;
            if(reference.Substring(0, 2) == "~/")
            {
                string appPath = httpContext.Request.ApplicationPath ?? string.Empty;

                if(appPath.EndsWith("/", StringComparison.Ordinal))
                {
                    appPath = appPath.Left(appPath.Length - 1);
                }
                return appPath + reference.Substring(1);
            }
            return path;
        }

        /// <summary>
        /// gets the bytes for the posted file
        /// </summary>
        /// <returns></returns>
        public static byte[] GetFileStream(this HttpPostedFile httpPostedFile)
        {
            if(httpPostedFile != null)
            {
                int contentLength = httpPostedFile.ContentLength;
                var input = new byte[contentLength];
                Stream file = httpPostedFile.InputStream;
                file.Read(input, 0, contentLength);
                return input;
            }
            return null;
        }

        /// <summary>
        /// Returns a MimeType from a URL
        /// </summary>
        /// <param name="fullUrl">The URL to check for a mime type</param>
        /// <returns>A string representation of the mimetype</returns>
        public static string GetMimeType(this string fullUrl)
        {
            string extension = Path.GetExtension(fullUrl);

            if(string.IsNullOrEmpty(extension))
            {
                return string.Empty;
            }

            switch(extension.ToUpperInvariant())
            {
                case ".PNG":
                    return "image/png";
                case ".JPG":
                case ".JPEG":
                    return "image/jpeg";
                case ".BMP":
                    return "image/bmp";
                case ".GIF":
                    return "image/gif";
                default:
                    return "none";
            }
        }

        public static string GetSafeFileName(this string text)
        {
            if(string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException("text");
            }
            var badChars = Path.GetInvalidFileNameChars();
            foreach(var badChar in badChars)
            {
                if(text.Contains(badChar))
                {
                    text = text.Replace("" + badChar, string.Empty);
                }
            }
            return text;
        }
    }
}