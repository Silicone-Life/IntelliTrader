﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;

namespace SiliconeTrader.Core
{
    public abstract class LowResolutionTimedTask : ITimedTask
    {
        /// <summary>
        /// Raised on unhandled exception
        /// </summary>
        #pragma warning disable CS0067 
        public event UnhandledExceptionEventHandler UnhandledException;

        /// <summary>
        /// Delay before starting the task in milliseconds
        /// </summary>
        public double StartDelay { get; set; }

        /// <summary>
        /// Periodic execution interval in milliseconds
        /// </summary>
        public double Interval
        {
            get { return timer.Interval; }
            set { timer.Interval = value; }
        }

        /// <summary>
        /// How often to skip task execution (in RunCount)
        /// </summary>
        public int SkipIteration { get; set; } = 0;

        /// <summary>
        /// Stopwatch to use for timing the intervals
        /// </summary>
        public Stopwatch Stopwatch { get; set; }

        /// <summary>
        /// Indicates whether the task is currently running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Number of times the task has been run
        /// </summary>
        public long RunCount { get; private set; }

        /// <summary>
        /// Total time it took to run the task in milliseconds
        /// </summary>
        public double TotalRunTime { get; private set; }

        /// <summary>
        /// Total task run delay in milliseconds
        /// </summary>
        public double TotalLagTime { get; private set; }

        private readonly Timer timer = new Timer();
        private readonly Stopwatch runWatch = new Stopwatch();
        private readonly System.Threading.AutoResetEvent syncMutex = new System.Threading.AutoResetEvent(true);

        /// <summary>
        /// Class constructor. It allocates the memory for the background timer and
        /// initializes sync mutex.
        /// </summary>
        public LowResolutionTimedTask()
        {
            timer.Elapsed += this.OnTimerElapsed;
        }

        /// <summary>
        /// Starts the background task timer that is in charge of executing the Execute method each
        /// time the interval is elapsed.
        /// </summary>
        public void Start()
        {
            if (!this.IsRunning)
            {
                this.IsRunning = true;

                if (this.StartDelay > 0)
                {
                    Task.Delay((int)this.StartDelay).ContinueWith(t =>
                    {
                        if (this.IsRunning)
                        {
                            timer.Start();
                        }
                    });
                }
                else
                {
                    timer.Start();
                }
            }
        }

        /// <summary>
        /// Stops the background task timer that is in charge of executing the Execute method each
        /// time the interval is elapsed. If the Execute method was executing when this method is
        /// called, the caller thread will block waiting the Execute operation to finish. Later on,
        /// the timer will be stopped. Otherwise, if the Execute method is not executing when this
        /// method is called, the timer will be stopped without blocking the caller thread.
        /// </summary>
        public void Stop()
        {
            this.Stop(-1);
        }

        /// <summary>
        /// Stops the background task timer that is in charge of executing the Execute method each
        /// time the interval is elapsed. If the Execute method was executing when this method is
        /// called, the caller thread will block waiting the Execute operation to finish. Later on,
        /// the timer will be stopped. Otherwise, if the Execute method is not executing when this
        /// method is called, the timer will be stopped without blocking the caller thread.
        /// </summary>
        /// <param name="timeout">Timeout value in milliseconds to wait before killing the task</param>
        public void Stop(int timeout)
        {
            if (this.IsRunning)
            {
                syncMutex.WaitOne(timeout);
                syncMutex.Set();
                timer.Stop();
                this.IsRunning = false;
            }
        }

        /// <summary>
        /// Stops the periodic task executor without waiting the current task to stop.
        /// </summary>
        public void Terminate()
        {
            if (this.IsRunning)
            {
                timer.Stop();
                this.IsRunning = false;
            }
        }

        /// <summary>
        /// This method can operate in two different ways. If the Execute method is currently executing, it will
        /// block the caller thread until Execute finishes. However, if the Execute method is not being executed,
        /// this method will not block and will immediately return back the control to the caller thread.
        /// </summary>
        public void Join()
        {
            if (this.IsRunning)
            {
                syncMutex.WaitOne();
                syncMutex.Set();
            }
        }

        /// <summary>
        /// Temporarily pause the task
        /// </summary>
        public void Pause()
        {
            timer.Enabled = false;
        }

        /// <summary>
        ///  Continue running the task
        /// </summary>
        public void Continue()
        {
            timer.Enabled = true;
        }

        /// <summary>
        /// Wraps the Execute call abstracting the child class from the thread synchronization issues.
        /// </summary>
        /// <param name="sender">The thimer object that is calling the event listener.</param>
        /// <param name="e">The arguments passed by the timer to the method.</param>
        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            //Force other threads to wait until it's finished when calling join.
            syncMutex.Reset();

            //Avoid re-calling the method while it is still operating.
            timer.Stop();

            if (this.IsRunning)
            {
                runWatch.Restart();
                this.Run();
                long runTime = runWatch.ElapsedMilliseconds;
                this.TotalLagTime += (runTime > this.Interval) ? (runTime - this.Interval) : 0;
                this.TotalRunTime += runTime;
                this.RunCount++;
                runWatch.Stop();

                //Re-Start the timer to execute the worker function endlessly.
                timer.Start();
            }

            //Release threads that might be frozen in join operation.
            syncMutex.Set();
        }

        /// <summary>
        /// Manually run the task
        /// </summary>
        public void RunNow()
        {
            this.Run();
        }

        /// <summary>
        /// This method must be implemented by the child class and must contain the code
        /// to be executed periodically.
        /// </summary>
        protected abstract void Run();
    }
}

