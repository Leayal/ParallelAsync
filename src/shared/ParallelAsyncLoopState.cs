namespace System.Threading.Tasks
{
    /// <summary>Enables iterations of parallel async loops to interact with other iterations. An instance of this class is provided by the <seealso cref="ParallelAsync"/> class to each loop; you can not create instances in your code.</summary>
    public sealed class ParallelAsyncLoopState
    {
        // State:
        //  -1: Not started
        //  0: Stopped
        //  1: Running
        //  2: Pending break
        private long loopState, loopCount, isException;
        private long? _lowestBreakIteration;

        /// <summary>Gets whether any iteration of the loop has thrown an exception that went unhandled by that iteration.</summary>
        public bool IsExceptional => (Interlocked.Read(ref this.isException) == 0);

        /// <summary>Gets whether any iteration of the loop has called the <seealso cref="Stop"/> method.</summary>
        public bool IsStopped => (Interlocked.Read(ref this.loopState) == 0);

        /// <summary>Gets the lowest iteration of the loop from which <seealso cref="Break"/> was called.</summary>
        public long? LowestBreakIteration => this._lowestBreakIteration;

        /// <summary>Gets whether the current iteration of the loop should exit based on requests made by this or other iterations.</summary>
        public bool ShouldExitCurrentIteration => (Interlocked.Read(ref this.loopState) == 2);

        /// <summary>Communicates that the System.Threading.Tasks.Parallel loop should cease execution of iterations beyond the current iteration at the system's earliest convenience.</summary>
        /// <exception cref="InvalidOperationException">The <seealso cref="Break"/> method was called previously. <seealso cref="Break"/> and <seealso cref="Stop"/> may not be used in combination by iterations of the same loop.</exception>
        public void Break()
        {
            long state = Interlocked.CompareExchange(ref this.loopState, 2, 1);
            switch (state)
            {
                case -1:
                    break;
                case 2:
                case 0:
                    throw new InvalidOperationException("The System.Threading.Tasks.ParallelLoopState.Break method was called previously. System.Threading.Tasks.ParallelLoopState.Break and System.Threading.Tasks.ParallelLoopState.Stop may not be used in combination by iterations of the same loop.");
            }
        }

        /// <summary>Communicates that the System.Threading.Tasks.Parallel loop should cease execution at the system's earliest convenience.</summary>
        /// <exception cref="InvalidOperationException">The <seealso cref="Break"/> method was called previously. <seealso cref="Break"/> and <seealso cref="Stop"/> may not be used in combination by iterations of the same loop.</exception>
        public void Stop()
        {
            long state = Interlocked.CompareExchange(ref this.loopState, 0, 1);
            switch (state)
            {
                case -1:
                    break;
                case 2:
                case 0:
                    throw new InvalidOperationException("The System.Threading.Tasks.ParallelLoopState.Break method was called previously. System.Threading.Tasks.ParallelLoopState.Break and System.Threading.Tasks.ParallelLoopState.Stop may not be used in combination by iterations of the same loop.");
            }
        }

        internal long IncreaseIterationCount() => Interlocked.Increment(ref this.loopCount);

        internal void SetLowestBreakIteration()
        {
            this._lowestBreakIteration = Interlocked.Read(ref this.loopCount);
        }

        internal bool SetExceptional() => (Interlocked.CompareExchange(ref this.isException, 1, 0) == 0);

        internal ParallelAsyncLoopState()
        {
            this.loopState = -1;
            this.loopCount = 0;
            this.isException = 0;
            this._lowestBreakIteration = null;
        }
    }
}
