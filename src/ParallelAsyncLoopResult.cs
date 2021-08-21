namespace System.Threading.Tasks
{
    /// <summary>Provides completion status on the execution of a <seealso cref="ParallelAsync"/> loop.</summary>
    public readonly struct ParallelAsyncLoopResult
    {
        /// <summary>
        /// Gets whether the loop ran to completion, such that all iterations of the loop were executed and the loop didn't receive a request to end prematurely.
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// Gets the index of the lowest iteration from which <seealso cref="ParallelAsyncLoopState.Break"/> was called.
        /// </summary>
        public long? LowestBreakIteration { get; }

        internal ParallelAsyncLoopResult(bool completed) : this(completed, null) { }

        internal ParallelAsyncLoopResult(bool completed, long? breakIteration)
        {
            this.IsCompleted = completed;
            this.LowestBreakIteration = breakIteration;
        }
    }
}
