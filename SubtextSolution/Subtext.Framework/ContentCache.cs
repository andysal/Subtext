#region Disclaimer/Info
///////////////////////////////////////////////////////////////////////////////////////////////////
// Subtext WebLog
// 
// Subtext is an open source weblog system that is a fork of the .TEXT
// weblog system.
//
// For updated news and information please visit http://subtextproject.com/
// Subtext is hosted at SourceForge at http://sourceforge.net/projects/subtext
// The development mailing list is at subtext-devs@lists.sourceforge.net 
//
// This project is licensed under the BSD license.  See the License.txt file for more information.
///////////////////////////////////////////////////////////////////////////////////////////////////
#endregion

using System;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Web;
using System.Web.Caching;
using Subtext.Framework.Properties;

namespace Subtext.Framework
{
	/// <summary>
	/// This is a replacement for the <see cref="Cache"/> object. 
	/// Use this when caching content.  This ensures content is 
	/// cached according to key and Locale.
	/// </summary>
	public class ContentCache : IEnumerable
	{
		Cache cache;

		/// <summary>
		/// Instantiates the specified content cache from the specific <see cref="HttpContext"/>. 
		/// At some point, we might consider replacing HttpContext with a type we can extend.
		/// </summary>
		/// <returns></returns>
		public static ContentCache Instantiate()
		{
			//Check per-request cache.
			ContentCache cache = HttpContext.Current.Items["ContentCache"] as ContentCache;
            if (cache != null)
            {
                return cache;
            }

			cache = new ContentCache(HttpContext.Current.Cache);
			//Per-Request Cache.
			HttpContext.Current.Items["ContentCache"] = cache;
			return cache;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ContentCache"/> class. 
		/// The specified <see cref="Cache"/> instance is wrapped by this instance.
		/// </summary>
		/// <param name="cache">The cache.</param>
		private ContentCache(Cache cache)
		{
			this.cache = cache;
		}

		//Returns a language aware cache key.
		private static string GetCacheKey(string key)
		{
			return key + ":" + Thread.CurrentThread.CurrentCulture.LCID.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Gets or sets the <see cref="Object"/> with the specified key.
		/// </summary>
		/// <value></value>
		public object this[string key]
		{
			get
			{
				return cache[GetCacheKey(key)];
			}
			set
			{
				cache.Insert(GetCacheKey(key), value);
			}
		}

		/// <summary>
		/// Inserts the specified object to the <see cref="System.Web.Caching.Cache"/> object 
		/// with a cache key to reference its location and using default values provided by 
		/// the <see cref="System.Web.Caching.CacheItemPriority"/> enumeration.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Insert(string key, object value)
		{
            if (value == null)
            {
                throw new ArgumentNullException("value", Resources.ArgumentNull_Generic);
            }
			cache.Insert(GetCacheKey(key), value);
		}

		/// <summary>
		/// <para>Inserts the specified object to the <see cref="System.Web.Caching.Cache"/> object 
		/// with a cache key to reference its location and using default values provided by 
		/// the <see cref="System.Web.Caching.CacheItemPriority"/> enumeration.
		/// </para>
		/// <para>
		/// Allows specifying a general cache duration.
		/// </para>
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <param name="cacheDuration">The cache duration.</param>
		public void Insert(string key, object value, CacheDuration cacheDuration)
		{
            if (value == null)
            {
                throw new ArgumentNullException("value", Resources.ArgumentNull_Generic);
            }
			
			cache.Insert(GetCacheKey(key), value, null, DateTime.Now.AddSeconds((int)cacheDuration), TimeSpan.Zero, CacheItemPriority.Normal, null);
		}

		/// <summary>
		/// <para>Inserts the specified object to the <see cref="System.Web.Caching.Cache"/> object 
		/// with a cache key to reference its location and using default values provided by 
		/// the <see cref="System.Web.Caching.CacheItemPriority"/> enumeration.
		/// </para>
		/// <para>
		/// Allows specifying a <see cref="CacheDependency"/>
		/// </para>
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <param name="cacheDependency">The cache dependency.</param>
		public void Insert(string key, object value, CacheDependency cacheDependency)
		{
            if (value == null)
            {
                throw new ArgumentNullException("value", Resources.ArgumentNull_Generic);
            }
			
			cache.Insert(GetCacheKey(key), value, cacheDependency);
		}

		/// <summary>
		/// Retrieves the specified item from the <see cref="System.Web.Caching.Cache"/>.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public object Get(string key)
		{
			return cache.Get(GetCacheKey(key));
		}

		/// <summary>
		/// Removes the specified item from the cache.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public object Remove(string key)
		{
			return cache.Remove(GetCacheKey(key));
		}

		/// <summary>
		/// Returns an enumerator that can iterate through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator"/>
		/// that can be used to iterate through the collection.
		/// </returns>
		public IEnumerator GetEnumerator()
		{
			return cache.GetEnumerator();
		}
	}


}
