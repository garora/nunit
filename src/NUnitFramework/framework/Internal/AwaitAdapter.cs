// ***********************************************************************
// Copyright (c) 2018 Charlie Poole, Rob Prouse
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;

#if ASYNC
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
#endif

namespace NUnit.Framework.Internal
{
    internal abstract class AwaitAdapter
    {
        public abstract bool IsCompleted { get; }
        public abstract void OnCompleted(Action action);
        public abstract void BlockUntilCompleted();

#if ASYNC
        public static AwaitAdapter FromAwaitable(object awaitable)
        {
            if (awaitable == null) throw new ArgumentNullException(nameof(awaitable));

            var task = awaitable as Task;
            if (task == null)
                throw new NotImplementedException("Proper awaitable implementation to follow.");

#if NET40
            // TODO: use the general reflection-based awaiter if net40 build is running against a newer BCL
            return new Net40BclTaskAwaitAdapter(task);
#else
            return new TaskAwaitAdapter(task);
#endif
        }

#if NET40
        private sealed class Net40BclTaskAwaitAdapter : AwaitAdapter
        {
            private readonly Task _task;

            public Net40BclTaskAwaitAdapter(Task task)
            {
                _task = task;
            }

            public override bool IsCompleted => _task.IsCompleted;

            public override void OnCompleted(Action action)
            {
                if (action == null) return;

                // Mimick TaskAwaiter.UnsafeOnCompleted
                _task.ContinueWith(_ => action.Invoke(), TaskScheduler.FromCurrentSynchronizationContext());
            }

            public override void BlockUntilCompleted()
            {
                // Mimick TaskAwaiter.GetResult
                try
                {
                    _task.Wait();
                }
                catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
                {
                    ExceptionHelper.Rethrow(ex.InnerException);
                }
            }
        }
#else
        private sealed class TaskAwaitAdapter : AwaitAdapter
        {
            private readonly TaskAwaiter _awaiter;

            public TaskAwaitAdapter(Task task)
            {
                _awaiter = task.GetAwaiter();
            }

            public override bool IsCompleted => _awaiter.IsCompleted;

            [SecuritySafeCritical]
            public override void OnCompleted(Action action) => _awaiter.UnsafeOnCompleted(action);

            // Assumption that GetResult blocks until complete is only valid for System.Threading.Tasks.Task.
            public override void BlockUntilCompleted() => _awaiter.GetResult();
        }
#endif
#endif
    }
}
