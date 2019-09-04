using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NLog.AzureLog
{
    public class TaskQueue
    {
        private readonly int _maxConcurrentTasksCount;
        private readonly int _maxQueueSize;
        private readonly ConcurrentQueue<Func<Task>> _processingQueue = new ConcurrentQueue<Func<Task>>();
        private readonly ConcurrentDictionary<int, Task> _runningTasks = new ConcurrentDictionary<int, Task>();
        private TaskCompletionSource<bool> _taskCompletionQueue = new TaskCompletionSource<bool>();

        public TaskQueue(int? maxConcurrentTasksCount = null, int? maxQueueSize = null)
        {
            _maxConcurrentTasksCount = maxConcurrentTasksCount ?? int.MaxValue;
            _maxQueueSize = maxQueueSize ?? int.MaxValue;
        }

        public bool Queue(Func<Task> futureTask)
        {
            if (_processingQueue.Count < _maxQueueSize)
            {
                _processingQueue.Enqueue(futureTask);
                return true;
            }

            return false;
        }

        public int GetQueueCount()
        {
            return _processingQueue.Count;
        }

        public int GetRunningCount()
        {
            return _runningTasks.Count;
        }

        public async Task Process()
        {
            var t = _taskCompletionQueue.Task;
            StartTasks();
            await t;
        }

        public void ProcessBackground(Action<Exception> exception = null)
        {
            Task.Run(Process).ContinueWith(t => { exception?.Invoke(t.Exception); },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private void StartTasks()
        {
            var startMaxCount = _maxConcurrentTasksCount - _runningTasks.Count;
            for (var i = 0; i < startMaxCount; i++)
            {
                Func<Task> futureTask;
                if (!_processingQueue.TryDequeue(out futureTask))
                    // Queue is most likely empty
                    break;

                var t = Task.Run(futureTask);
                if (!_runningTasks.TryAdd(t.GetHashCode(), t))
                    throw new Exception("Should not happen, hash codes are unique");

                t.ContinueWith(t2 =>
                {
                    Task _temp;
                    if (!_runningTasks.TryRemove(t2.GetHashCode(), out _temp))
                        throw new Exception("Should not happen, hash codes are unique");

                    // Continue the queue processing
                    StartTasks();
                });
            }

            if (_processingQueue.IsEmpty && _runningTasks.IsEmpty)
            {
                // Interlocked.Exchange might not be necessary
                var _oldQueue = Interlocked.Exchange(
                    ref _taskCompletionQueue, new TaskCompletionSource<bool>());
                _oldQueue.TrySetResult(true);
            }
        }
    }
}
