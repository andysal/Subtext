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
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Caching;
using Subtext.Configuration;
using Subtext.Framework.Components;
using Subtext.Framework.Text;
using Subtext.Framework.Util;
using Subtext.Infrastructure;

namespace Subtext.Framework.Data
{
    //TODO: Refactor. Static classes like this are a pain!
    /// <summary>
    /// Encapsulates obtaining content from the cache.
    /// </summary>
    public static class Cacher
    {
        private const string EntriesByCategoryKey = "EC:Count{0}Category{1}BlogId{2}";
        private const string EntryKeyId = "Entry{0}BlogId{1}";
        private const string EntryKeyName = "EntryName{0}BlogId{1}";
        public const int LongDuration = 30;
        public const int MediumDuration = 20;
        public const int ShortDuration = 10;
        private const string EntryDayKey = "EntryDay:Date{0:yyyyMMdd}Blog{1}";
        private const string EntryMonthKey = "EntryMonth:Date{0:yyyyMM}Blog{1}";
        private const string EntriesByTagKey = "ET:Count{0}Tag{1}BlogId{2}";
        private const string CategoryKey = "LC{0}BlogId{1}";
        private const string ParentCommentEntryKey = "ParentEntry:Comments:EntryID{0}:BlogId{1}";
        private const string TagsKey = "TagsCount{0}BlogId{1}";

        public static T GetOrInsert<T>(this ICache cache, string key, Func<T> retrievalFunction, int duration)
        {
            var item = cache[key];
            if(item == null)
            {
                item = retrievalFunction();
                cache.InsertDuration(key, item, duration);
            }
            return (T)item;
        }
        
        public static T GetOrInsert<T>(this ICache cache, string key, Func<T> retrievalFunction)
        {
            return cache.GetOrInsert(key, retrievalFunction, ShortDuration);
        }

        /// <summary>
        /// Gets the entries for the specified month.
        /// </summary>
        public static ICollection<Entry> GetEntriesForMonth(DateTime dateTime, ISubtextContext context)
        {
            string key = string.Format(CultureInfo.InvariantCulture, EntryMonthKey, dateTime, context.Blog.Id);
            return context.Cache.GetOrInsert(key, () => context.Repository.GetPostsByMonth(dateTime.Month, dateTime.Year), LongDuration);
        }

        public static EntryDay GetEntriesForDay(DateTime day, ISubtextContext context)
        {
            string key = string.Format(CultureInfo.InvariantCulture, EntryDayKey, day, context.Blog.Id);
            return context.Cache.GetOrInsert(key, () => context.Repository.GetEntryDay(day), LongDuration);
        }

        public static ICollection<Entry> GetEntriesByCategory(int count, int categoryId, ISubtextContext context)
        {
            string key = string.Format(EntriesByCategoryKey, count, categoryId, context.Blog.Id);
            return context.Cache.GetOrInsert(key, () => context.Repository.GetEntriesByCategory(count, categoryId, true /* activeOnly */));
        }

        public static ICollection<Entry> GetEntriesByTag(int count, string tag, ISubtextContext context)
        {
            string key = string.Format(EntriesByTagKey, count, tag, context.Blog.Id);
            return context.Cache.GetOrInsert(key, () => context.Repository.GetEntriesByTag(count, tag));
        }

        /// <summary>
        /// Returns a LinkCategory for a single category based on the request url.
        /// </summary>
        public static LinkCategory SingleCategory(ISubtextContext context)
        {
            if(context == null)
            {
                throw new ArgumentNullException("context");
            }

            string categorySlug = context.RequestContext.GetSlugFromRequest();
            if(categorySlug.IsNumeric())
            {
                int categoryId = Int32.Parse(categorySlug, CultureInfo.InvariantCulture);
                return SingleCategory(categoryId, true, context);
            }
            return SingleCategory(categorySlug, true, context);
        }

        public static LinkCategory SingleCategory(int categoryId, bool isActive, ISubtextContext context)
        {
            return SingleCategory(() => context.Repository.GetLinkCategory(categoryId, isActive), categoryId, context);
        }

        public static LinkCategory SingleCategory(string categoryName, bool isActive, ISubtextContext context)
        {
            string singleCategoryName = categoryName;
            LinkCategory category = SingleCategory(() => context.Repository.GetLinkCategory(singleCategoryName, isActive),
                                                   categoryName, context);
            if(category != null)
            {
                return category;
            }

            if(context.Blog.AutoFriendlyUrlEnabled)
            {
                string theCategoryName = categoryName;
                categoryName = categoryName.Replace(FriendlyUrlSettings.Settings.SeparatingCharacter, " ");
                return SingleCategory(() => context.Repository.GetLinkCategory(theCategoryName, isActive), categoryName,
                                      context);
            }

            return null; //couldn't find category
        }

        private static LinkCategory SingleCategory<T>(Func<LinkCategory> retrievalDelegate, T categoryKey,
                                                      ISubtextContext context)
        {
            string key = string.Format(CultureInfo.InvariantCulture, CategoryKey, categoryKey, context.Blog.Id);
            return context.Cache.GetOrInsert(key, retrievalDelegate);
        }

        //TODO: This should only be called in one place total. And it needs to be tested.
        public static Entry GetEntryFromRequest(bool allowRedirectToEntryName, ISubtextContext context)
        {
            string slug = context.RequestContext.GetSlugFromRequest();
            if(!String.IsNullOrEmpty(slug))
            {
                return GetEntry(slug, context);
            }

            int? id = context.RequestContext.GetIdFromRequest();
            if(id != null)
            {
                Entry entry = GetEntry(id.Value, context);
                if(entry == null)
                {
                    return null;
                }

                //TODO: Violation of SRP here!
                //Second condition avoids infinite redirect loop. Should never happen.
                if(allowRedirectToEntryName && entry.HasEntryName && !entry.EntryName.IsNumeric())
                {
                    HttpResponseBase response = context.RequestContext.HttpContext.Response;
                    response.StatusCode = 301;
                    response.Status = "301 Moved Permanently";
                    response.RedirectLocation =
                        context.UrlHelper.EntryUrl(entry).ToFullyQualifiedUrl(context.Blog).ToString();
                    response.End();
                }
                return entry;
            }

            return null;
        }

        /// <summary>
        /// Retrieves a single entry from the cache by the entry name.  
        /// If it is not in the cache, gets it from the database and 
        /// inserts it into the cache.
        /// </summary>
        public static Entry GetEntry(string entryName, ISubtextContext context)
        {
            Blog blog = context.Blog;
            ICache cache = context.Cache;
            string key = string.Format(CultureInfo.InvariantCulture, EntryKeyName, entryName, blog.Id);

            Func<Entry> retrieval = () =>
                {
                    var entry = context.Repository.GetEntry(entryName, true /* activeOnly */, true /* includeCategories */);
                    if(entry == null)
                    {
                        return null;
                    }
                    var cacheDependency = new CacheDependency(null, new[] {key});
                    string entryIdKey = string.Format(CultureInfo.InvariantCulture, EntryKeyId, entry.Id, blog.Id);
                    cache.Insert(entryIdKey, entry, cacheDependency);
                    return entry;
                };

            var cachedEntry = context.Cache.GetOrInsert(key, retrieval, MediumDuration);
            return cachedEntry.DateSyndicated > blog.TimeZone.Now ? null : cachedEntry;
        }

        /// <summary>
        /// Retrieves a single entry from the cache by the id.
        /// If it is not in the cache, gets it from the database and
        /// inserts it into the cache.
        /// </summary>
        public static Entry GetEntry(int entryId, ISubtextContext context)
        {
            string key = string.Format(CultureInfo.InvariantCulture, EntryKeyId, entryId, context.Blog.Id);
            return context.Cache.GetOrInsert(key, () => context.Repository.GetEntry(entryId, true /* activeOnly */, true /* includeCategories */));
        }

        /// <summary>
        /// Retrieves the current tags from the cache based on the ItemCount and
        /// Blog Id. If it is not in the cache, it gets it from the database and 
        /// inserts it into the cache.
        /// </summary>
        public static IEnumerable<Tag> GetTopTags(int itemCount, ISubtextContext context)
        {
            string key = string.Format(CultureInfo.InvariantCulture, TagsKey, itemCount, context.Blog.Id);
            return context.Cache.GetOrInsert(key, () => context.Repository.GetMostUsedTags(itemCount), LongDuration);
        }

        /// <summary>
        /// Clears the comment cache.
        /// </summary>
        public static void ClearCommentCache(int entryId, ISubtextContext context)
        {
            string key = string.Format(CultureInfo.InvariantCulture, ParentCommentEntryKey, entryId, context.Blog.Id);
            context.Cache.Remove(key);
        }

        /// <summary>
        /// Returns all the feedback for the specified entry. Checks the cache first.
        /// </summary>
        public static ICollection<FeedbackItem> GetFeedback(Entry parentEntry, ISubtextContext context)
        {
            string key = string.Format(CultureInfo.InvariantCulture, ParentCommentEntryKey, parentEntry.Id, context.Blog.Id);
            return context.Cache.GetOrInsert(key, () => context.Repository.GetFeedbackForEntry(parentEntry));
        }

        public static void InsertDuration(this ICache cache, string key, object value, int duration)
        {
            cache.Insert(key, value, null, DateTime.Now.AddSeconds(duration), TimeSpan.Zero, CacheItemPriority.Normal,
                         null);
        }
    }
}