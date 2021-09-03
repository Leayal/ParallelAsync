using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace System.Threading.Tasks
{
    /// <summary>Provides support for parallel asynchronously loops.</summary>
    public static class ParallelAsync
    {
        private static readonly ParallelOptions defaultAsyncOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        /// <summary>
        /// Executes an asynchronously foreach operation on an <seealso cref="System.Collections.IEnumerable"/> in which iterations may run in parallel.
        /// </summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>A task that represents <seealso cref="ParallelAsyncLoopResult"/>.</returns>
        public static async Task<ParallelAsyncLoopResult> ForEach<TSource>(IEnumerable<TSource> source, Func<TSource, Task> body)
            => await ForEach(source, defaultAsyncOptions, body);

        /// <summary>
        /// Executes an asynchronously foreach operation on an <seealso cref="System.Collections.IEnumerable"/> in which iterations may run in parallel.
        /// </summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>A task that represents <seealso cref="ParallelAsyncLoopResult"/>.</returns>
        public static async Task<ParallelAsyncLoopResult> ForEach<TSource>(IEnumerable<TSource> source, ParallelOptions parallelOptions, Func<TSource, Task> body)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (parallelOptions == null)
                throw new ArgumentNullException(nameof(parallelOptions));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            using (var bag = CreateCollectionFromSource(source))
            {
                return await InnerForEachAsyncWithoutState(bag, parallelOptions, body);
            }
        }

        /// <summary>
        /// Executes an asynchronously foreach operation on an <seealso cref="System.Collections.IEnumerable"/> in which iterations may run in parallel, and the state of the loop can be monitored and manipulated.
        /// </summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>A task that represents <seealso cref="ParallelAsyncLoopResult"/>.</returns>
        public static async Task<ParallelAsyncLoopResult> ForEach<TSource>(IEnumerable<TSource> source, Func<TSource, ParallelAsyncLoopState, Task> body)
            => await ForEach(source, defaultAsyncOptions, body);

#if NETSTANDARD2_1_OR_GREATER
        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="IAsyncEnumerable{TSource}"/> in which iterations may run in parallel.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the async iteration from <paramref name="source"/> and the all executions are both completed or when <seealso cref="ParallelAsyncLoopState.Break"/> or <seealso cref="ParallelAsyncLoopState.Stop"/> is called.
        /// </returns>
        public static async Task ForEachAsync<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, Task> body)
            => await ForEachAsync(source, defaultAsyncOptions, body);

        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="IAsyncEnumerable{TSource}"/> in which iterations may run in parallel.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the async iteration from <paramref name="source"/> and the all executions are both completed or when <seealso cref="ParallelAsyncLoopState.Break"/> or <seealso cref="ParallelAsyncLoopState.Stop"/> is called.
        /// </returns>
        public static async Task ForEachAsync<TSource>(IAsyncEnumerable<TSource> source, ParallelOptions parallelOptions, Func<TSource, Task> body)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (parallelOptions == null)
                throw new ArgumentNullException(nameof(parallelOptions));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            using (var bag = new BlockingCollection<TSource>())
            {
                var loopstate = new ParallelAsyncLoopState();
                var t = InnerForEachAsyncWithoutState(bag, parallelOptions, body);
                await foreach (var item in source)
                {
                    if (loopstate.ShouldExitCurrentIteration || loopstate.IsStopped)
                    {
                        break;
                    }
                    bag.Add(item);
                }
                bag.CompleteAdding();
                await t;
            }
        }

        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="IAsyncEnumerable{TSource}"/> in which iterations may run in parallel, and the state of the loop can be monitored and manipulated.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the async iteration from <paramref name="source"/> and the all executions are both completed or when <seealso cref="ParallelAsyncLoopState.Break"/> or <seealso cref="ParallelAsyncLoopState.Stop"/> is called.
        /// </returns>
        public static async Task ForEachAsync<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, ParallelAsyncLoopState, Task> body)
            => await ForEachAsync(source, defaultAsyncOptions, body);

        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="IAsyncEnumerable{TSource}"/> in which iterations may run in parallel, and the state of the loop can be monitored and manipulated.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the async iteration from <paramref name="source"/> and the all executions are both completed or when <seealso cref="ParallelAsyncLoopState.Break"/> or <seealso cref="ParallelAsyncLoopState.Stop"/> is called.
        /// </returns>
        public static async Task ForEachAsync<TSource>(IAsyncEnumerable<TSource> source, ParallelOptions parallelOptions, Func<TSource, ParallelAsyncLoopState, Task> body)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (parallelOptions == null)
                throw new ArgumentNullException(nameof(parallelOptions));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            using (var bag = new BlockingCollection<TSource>())
            {
                var loopstate = new ParallelAsyncLoopState();
                var t = InnerForEachAsync(bag, parallelOptions, body, loopstate);
                await foreach (var item in source)
                {
                    if (loopstate.ShouldExitCurrentIteration || loopstate.IsStopped)
                    {
                        break;
                    }
                    bag.Add(item);
                }
                bag.CompleteAdding();
                await t;
            }
        }
#endif

        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="BlockingCollection{TSource}"/> in which iterations may run in parallel.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="real_time_source">An collection of data source.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <remarks>The <paramref name="real_time_source"/> collection can be modified while the operations are on-going. However, the collection should call <seealso cref="BlockingCollection{TSource}.CompleteAdding"/> to avoid non-ending operation. The collection shouldn't be disposed before all the operations are completed.</remarks>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the collection from <paramref name="real_time_source"/> has <seealso cref="BlockingCollection{T}.IsAddingCompleted"/> is true and the all executions are both completed (or the executions are stopped).
        /// </returns>
        public static async Task<ParallelAsyncLoopResult> ForEachAsync<TSource>(BlockingCollection<TSource> real_time_source, Func<TSource, Task> body)
            => await ForEachAsync(real_time_source, defaultAsyncOptions, body);

        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="BlockingCollection{TSource}"/> in which iterations may run in parallel.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="real_time_source">An collection of data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <remarks>The <paramref name="real_time_source"/> collection can be modified while the operations are on-going. However, the collection should call <seealso cref="BlockingCollection{TSource}.CompleteAdding"/> to avoid non-ending operation. The collection shouldn't be disposed before all the operations are completed.</remarks>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the collection from <paramref name="real_time_source"/> has <seealso cref="BlockingCollection{T}.IsAddingCompleted"/> is true and the all executions are both completed (or the executions are stopped).
        /// </returns>
        public static async Task<ParallelAsyncLoopResult> ForEachAsync<TSource>(BlockingCollection<TSource> real_time_source, ParallelOptions parallelOptions, Func<TSource, Task> body)
        {
            if (real_time_source == null)
                throw new ArgumentNullException(nameof(real_time_source));
            if (parallelOptions == null)
                throw new ArgumentNullException(nameof(parallelOptions));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if (real_time_source.IsCompleted || (real_time_source.IsAddingCompleted && real_time_source.Count == 0))
            {
                return new ParallelAsyncLoopResult(true);
            }

            return await InnerForEachAsyncWithoutState(real_time_source, parallelOptions, body);
        }

        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="BlockingCollection{TSource}"/> in which iterations may run in parallel, and the state of the loop can be monitored and manipulated.</summary>/// <summary>Executes an asynchronously foreach operation on an <seealso cref="BlockingCollection{TSource}"/> in which iterations may run in parallel.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="real_time_source">An collection of data source.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <remarks>The <paramref name="real_time_source"/> collection can be modified while the operations are on-going. However, the collection should call <seealso cref="BlockingCollection{TSource}.CompleteAdding"/> to avoid non-ending operation. The collection shouldn't be disposed before all the operations are completed.</remarks>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the collection from <paramref name="real_time_source"/> has <seealso cref="BlockingCollection{T}.IsAddingCompleted"/> is true and the all executions are both completed (or the executions are stopped).
        /// </returns>
        public static async Task<ParallelAsyncLoopResult> ForEachAsync<TSource>(BlockingCollection<TSource> real_time_source, Func<TSource, ParallelAsyncLoopState, Task> body)
            => await ForEachAsync(real_time_source, defaultAsyncOptions, body);

        /// <summary>Executes an asynchronously foreach operation on an <seealso cref="BlockingCollection{TSource}"/> in which iterations may run in parallel, and the state of the loop can be monitored and manipulated.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="real_time_source">An collection of data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <remarks>The <paramref name="real_time_source"/> collection can be modified while the operations are on-going. However, the collection should call <seealso cref="BlockingCollection{TSource}.CompleteAdding"/> to avoid non-ending operation. The collection shouldn't be disposed before all the operations are completed.</remarks>
        /// <returns>
        /// This method will return task that represents <seealso cref="ParallelAsyncLoopResult"/> which will be completed when the collection from <paramref name="real_time_source"/> has <seealso cref="BlockingCollection{T}.IsAddingCompleted"/> is true and the all executions are both completed (or the executions are stopped).
        /// </returns>
        public static async Task<ParallelAsyncLoopResult> ForEachAsync<TSource>(BlockingCollection<TSource> real_time_source, ParallelOptions parallelOptions, Func<TSource, ParallelAsyncLoopState, Task> body)
        {
            if (real_time_source == null)
                throw new ArgumentNullException(nameof(real_time_source));
            if (parallelOptions == null)
                throw new ArgumentNullException(nameof(parallelOptions));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if (real_time_source.IsCompleted || (real_time_source.IsAddingCompleted && real_time_source.Count == 0))
            {
                return new ParallelAsyncLoopResult(true);
            }

            var loopState = new ParallelAsyncLoopState();
            return await InnerForEachAsync(real_time_source, parallelOptions, body, loopState);
        }

        /// <summary>
        /// Executes an asynchronously foreach operation on an <seealso cref="System.Collections.IEnumerable"/> in which iterations may run in parallel, and the state of the loop can be monitored and manipulated.
        /// </summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        /// <exception cref="ArgumentNullException">The source argument is null.-or-The body argument is null.</exception>
        /// <returns>A task that represents <seealso cref="ParallelAsyncLoopResult"/>.</returns>
        public static async Task<ParallelAsyncLoopResult> ForEach<TSource>(IEnumerable<TSource> source, ParallelOptions parallelOptions, Func<TSource, ParallelAsyncLoopState, Task> body)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (parallelOptions == null)
                throw new ArgumentNullException(nameof(parallelOptions));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            var loopState = new ParallelAsyncLoopState();
            using (var bag = CreateCollectionFromSource(source))
            {
                return await InnerForEachAsync(bag, parallelOptions, body, loopState);
            }
        }

        private static async Task<ParallelAsyncLoopResult> InnerForEachAsyncWithoutState<TSource>(BlockingCollection<TSource> real_time_source, ParallelOptions parallelOptions, Func<TSource, Task> body)
        {
            long loopCount = 0;
            var canceltoken = parallelOptions.CancellationToken;
            {
                int parallelAllowance = Math.Min(parallelOptions.MaxDegreeOfParallelism, Environment.ProcessorCount);

                Task[] waiting = new Task[parallelAllowance];

                for (int i = 0; i < parallelAllowance; i++)
                {
                    waiting[i] = Task.Factory.StartNew(async () =>
                    {
                        while (!real_time_source.IsCompleted && !canceltoken.IsCancellationRequested)
                        {
                            if (real_time_source.TryTake(out var item))
                            {
                                Interlocked.Increment(ref loopCount);
                                await body.Invoke(item).ConfigureAwait(false);
                            }
                            else if (real_time_source.IsAddingCompleted)
                            {
                                break;
                            }
                            else
                            {
                                await Task.Delay(30);
                            }
                        }
                    }, canceltoken, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default).Unwrap();
                }

                await Task.WhenAll(waiting);

                if (canceltoken.IsCancellationRequested)
                {
                    return new ParallelAsyncLoopResult(false, loopCount);
                }
                else
                {
                    return new ParallelAsyncLoopResult(true);
                }
            }
        }

        private static async Task<ParallelAsyncLoopResult> InnerForEachAsync<TSource>(BlockingCollection<TSource> real_time_source, ParallelOptions parallelOptions, Func<TSource, ParallelAsyncLoopState, Task> body, ParallelAsyncLoopState loopState)
        {
            int parallelAllowance = Math.Min(parallelOptions.MaxDegreeOfParallelism, Environment.ProcessorCount);

            Task[] waiting = new Task[parallelAllowance];
            var canceltoken = parallelOptions.CancellationToken;

            for (int i = 0; i < parallelAllowance; i++)
            {
                waiting[i] = Task.Factory.StartNew(async () =>
                {
                    var localvar_loopstate = loopState;
                    foreach (var item in real_time_source.GetConsumingEnumerable())
                    {
                        if (canceltoken.IsCancellationRequested)
                        {
                            loopState.Stop();
                            break;
                        }
                        if (localvar_loopstate.ShouldExitCurrentIteration || localvar_loopstate.IsStopped)
                        {
                            break;
                        }
                        else
                        {
                            loopState.IncreaseIterationCount();
                            await body.Invoke(item, localvar_loopstate).ConfigureAwait(false);
                        }
                    }
                }, canceltoken, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default).Unwrap();
            }

            await Task.WhenAll(waiting);

            if (loopState.ShouldExitCurrentIteration || loopState.IsStopped)
            {
                return new ParallelAsyncLoopResult(false, loopState.LowestBreakIteration);
            }
            else
            {
                return new ParallelAsyncLoopResult(true);
            }
        }

        private static BlockingCollection<TSource> CreateCollectionFromSource<TSource>(IEnumerable<TSource> source)
        {
            BlockingCollection<TSource> bag;
            if (source is Collections.ICollection collection)
            {
                bag = new BlockingCollection<TSource>(collection.Count);
            }
            else if (source is ICollection<TSource> collection2)
            {
                bag = new BlockingCollection<TSource>(collection2.Count);
            }
            else
            {
                bag = new BlockingCollection<TSource>();
            }
            foreach (var item in source)
            {
                bag.Add(item);
            }
            bag.CompleteAdding();
            return bag;
        }
    }
}
