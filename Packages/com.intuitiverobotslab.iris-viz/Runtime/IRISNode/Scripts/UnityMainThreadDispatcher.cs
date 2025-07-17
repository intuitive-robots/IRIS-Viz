using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IRIS.Node
{
    public class UnityMainThreadDispatcher : Singleton<UnityMainThreadDispatcher>
    {
        private readonly Queue<Action> _actions = new Queue<Action>();

        public void Enqueue(Action action)
        {
            lock (_actions)
            {
                _actions.Enqueue(action);
            }
        }

        // Execute a function on the main thread and wait for result
        public T EnqueueAndWait<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            
            Enqueue(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            return tcs.Task.Result;
        }

        // Execute an action on the main thread and wait for completion
        public void EnqueueAndWait(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            tcs.Task.Wait();
        }



        // Overloaded version with timeout support
        public T EnqueueAndWait<T>(Func<T> func, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<T>();
            
            Enqueue(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            if (tcs.Task.Wait(timeoutMs))
            {
                return tcs.Task.Result;
            }
            else
            {
                throw new TimeoutException($"Operation timed out after {timeoutMs}ms");
            }
        }

        void Update()
        {
            lock (_actions)
            {
                while (_actions.Count > 0)
                {
                    _actions.Dequeue().Invoke();
                }
            }
        }
    }
}