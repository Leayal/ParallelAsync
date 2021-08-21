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
        public static Task<ParallelAsyncLoopResult> ForEach<TSource>(IEnumerable<TSource> source, Func<TSource, Task> body)
            => ForEach(source, defaultAsyncOptions, body);

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

            long loopCount = 0;
            var canceltoken = parallelOptions.CancellationToken;
            using (var bag = CreateCollectionFromSource(source))
            {
                int parallelAllowance = Math.Min(parallelOptions.MaxDegreeOfParallelism, bag.Count);

                Task[] waiting = new Task[parallelAllowance];

                for (int i = 0; i < parallelAllowance; i++)
                {
                    waiting[i] = Task.Factory.StartNew(async () =>
                    {
                        foreach (var item in bag.GetConsumingEnumerable())
                        {
                            if (!canceltoken.IsCancellationRequested)
                            {
                                Interlocked.Increment(ref loopCount);
                                await body.Invoke(item).ConfigureAwait(false);
                            }
                        }
                    }, canceltoken, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default).Unwrap();
                }

                await Task.WhenAll(waiting);
            }

            if (canceltoken.IsCancellationRequested)
            {
                return new ParallelAsyncLoopResult(false, loopCount);
            }
            else
            {
                return new ParallelAsyncLoopResult(true);
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
        public static Task<ParallelAsyncLoopResult> ForEach<TSource>(IEnumerable<TSource> source, Func<TSource, ParallelAsyncLoopState, Task> body)
            => ForEach(source, defaultAsyncOptions, body);

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
                int parallelAllowance = Math.Min(parallelOptions.MaxDegreeOfParallelism, bag.Count);

                Task[] waiting = new Task[parallelAllowance];
                var canceltoken = parallelOptions.CancellationToken;

                for (int i = 0; i < parallelAllowance; i++)
                {
                    waiting[i] = Task.Factory.StartNew(async () =>
                    {
                        foreach (var item in bag.GetConsumingEnumerable())
                        {
                            if (canceltoken.IsCancellationRequested)
                            {
                                loopState.Stop();
                                break;
                            }
                            if (loopState.ShouldExitCurrentIteration || loopState.IsStopped)
                            {
                                break;
                            }
                            else
                            {
                                loopState.IncreaseIterationCount();
                                await body.Invoke(item, loopState).ConfigureAwait(false);
                            }
                        }
                    }, canceltoken, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default).Unwrap();
                }

                await Task.WhenAll(waiting);
            }

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
