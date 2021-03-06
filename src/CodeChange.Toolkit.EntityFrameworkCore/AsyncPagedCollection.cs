﻿namespace CodeChange.Toolkit.EntityFrameworkCore
{
    using CodeChange.Toolkit.Collections;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An Entity Framework Core implementation of IAsyncPagedCollection
    /// </summary>
    /// <typeparam name="T">The type of objects to paginate</typeparam>
    public class AsyncPagedCollection<T> : IAsyncPagedCollection<T>
    {
        private readonly IQueryable<T> _source;

        private int? _cachedPageCount;
        private readonly Dictionary<int, IEnumerable<T>> _cachedPages;

        private int? _cachedTotalCount;
        private IEnumerable<T> _cachedItems;

        /// <summary>
        /// Constructs the paged collection with the collection data
        /// </summary>
        /// <param name="source">The source of data for the collection</param>
        /// <param name="pageSize">The maximum page size</param>
        public AsyncPagedCollection
            (
                IQueryable<T> source,
                int pageSize
            )
        {
            Validate.IsNotNull(source);
            Validate.IsGreaterThan(pageSize, 0);

            _source = source;
            _cachedPages = new Dictionary<int, IEnumerable<T>>();

            this.PageSize = pageSize;
        }

        /// <summary>
        /// Gets the maximum size of any page
        /// </summary>
        public int PageSize { get; }

        /// <summary>
        /// Asynchronously gets the total number of pages
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <remarks>The total number of pages</remarks>
        public async Task<int> GetPageCount
            (
                CancellationToken cancellationToken = default
            )
        {
            if (_cachedPageCount == null)
            {
                var pageSize = this.PageSize;
                var countTask = GetItemCount(cancellationToken);

                var totalCount = await countTask.ConfigureAwait(false);

                _cachedPageCount = CalculatePageCount
                (
                    pageSize,
                    totalCount
                );
            }

            return _cachedPageCount.Value;
        }

        /// <summary>
        /// Calculates the page count from the page and collection sizes
        /// </summary>
        /// <param name="pageSize">The page size</param>
        /// <param name="totalCount">The number of items in total</param>
        /// <returns>The page count</returns>
        protected int CalculatePageCount
            (
                int pageSize,
                int totalCount
            )
        {
            if (totalCount == 0)
            {
                return 0;
            }

            var remainder = totalCount % pageSize;

            var pageCount =
            (
                (totalCount / pageSize) + (remainder == 0 ? 0 : 1)
            );

            return pageCount;
        }

        /// <summary>
        /// Asynchronously gets a collection of items at the page number specified
        /// </summary>
        /// <param name="pageNumber">The page number</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A collection of the items from the page</returns>
        public async Task<IEnumerable<T>> GetPage
            (
                int pageNumber,
                CancellationToken cancellationToken = default
            )
        {
            Validate.IsGreaterThan(pageNumber, 0);

            if (false == _cachedPages.ContainsKey(pageNumber))
            {
                var countTask = GetItemCount(cancellationToken);
                var totalCount = await countTask.ConfigureAwait(false);

                if (totalCount == 0)
                {
                    return _source;
                }
                else
                {
                    var pageSize = this.PageSize;
                    var skipCount = pageNumber * pageSize;

                    var page = await _source
                        .Skip(skipCount)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    _cachedPages[pageNumber] = page;
                }
            }
            
            return _cachedPages[pageNumber];
        }

        /// <summary>
        /// Asynchronously gets a collection of items at the page number specified
        /// </summary>
        /// <param name="page">The page number</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A collection of the items from the page</returns>
        public Task<IEnumerable<T>> this[int page, CancellationToken cancellationToken = default]
        {
            get
            {
                return GetPage(page, cancellationToken);
            }
        }

        /// <summary>
        /// Asynchronously gets all pages in the collection
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A collection of collections, each representing a page</returns>
        public async Task<IEnumerable<(int PageNumber, IEnumerable<T> Items)>> GetAllPages
            (
                CancellationToken cancellationToken = default
            )
        {
            var countTask = GetPageCount(cancellationToken);
            var pageCount = await countTask.ConfigureAwait(false);

            var tasks = new List<Task<IEnumerable<T>>>();
            var pages = new List<(int, IEnumerable<T>)>();

            for (var number = 1; number <= pageCount; number++)
            {
                tasks.Add
                (
                    GetPage(number)
                );
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            {
                var number = 1;

                foreach (var page in results)
                {
                    pages.Add((number, page));
                    number++;
                }
            }

            return pages;
        }

        /// <summary>
        /// Asynchronously gets the total number of items in the collection
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <remarks>The total number of items</remarks>
        public async Task<int> GetItemCount
            (
                CancellationToken cancellationToken = default
            )
        {
            if (_cachedTotalCount == null)
            {
                var task = _source.CountAsync(cancellationToken);

                _cachedTotalCount = await task.ConfigureAwait(false);
            }

            return _cachedTotalCount.Value;
        }

        /// <summary>
        /// Asynchronously gets all items in the collection (not paginated)
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A collection of items</returns>
        public async Task<IEnumerable<T>> GetAllItems
            (
                CancellationToken cancellationToken = default
            )
        {
            if (_cachedItems == null)
            {
                var task = _source.ToListAsync(cancellationToken);

                _cachedItems = await task.ConfigureAwait(false);
            }

            return _cachedItems;
        }
    }
}
