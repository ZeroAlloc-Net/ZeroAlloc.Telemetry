namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// A custom task-like pair, <c>PooledTask</c> and <c>PooledTask&lt;T&gt;</c>, in the shape of
/// PooledAwait's: a struct marked <c>[AsyncMethodBuilder]</c>, with a builder that forwards to the
/// BCL one. Appended to a probe source, so it uses fully-qualified names only.
/// </summary>
/// <remarks>
/// The proxy is an <c>async</c> method returning the task-like type, so the builder has to be
/// complete for the probe to compile. That is the point: it proves the emitted proxy is valid C#.
/// </remarks>
internal static class TaskLikeTypes
{
    public const string Declarations = """

        namespace Pooled
        {
            [System.Runtime.CompilerServices.AsyncMethodBuilder(typeof(PooledTaskMethodBuilder))]
            public readonly struct PooledTask
            {
                private readonly System.Threading.Tasks.Task _task;
                public PooledTask(System.Threading.Tasks.Task task) => _task = task;
                public System.Runtime.CompilerServices.TaskAwaiter GetAwaiter() => _task.GetAwaiter();
            }

            [System.Runtime.CompilerServices.AsyncMethodBuilder(typeof(PooledTaskMethodBuilder<>))]
            public readonly struct PooledTask<T>
            {
                private readonly System.Threading.Tasks.Task<T> _task;
                public PooledTask(System.Threading.Tasks.Task<T> task) => _task = task;
                public System.Runtime.CompilerServices.TaskAwaiter<T> GetAwaiter() => _task.GetAwaiter();
            }

            public struct PooledTaskMethodBuilder
            {
                private System.Runtime.CompilerServices.AsyncTaskMethodBuilder _inner;
                public static PooledTaskMethodBuilder Create() =>
                    new() { _inner = System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Create() };
                public PooledTask Task => new(_inner.Task);
                public void Start<TStateMachine>(ref TStateMachine stateMachine)
                    where TStateMachine : System.Runtime.CompilerServices.IAsyncStateMachine =>
                    _inner.Start(ref stateMachine);
                public void SetStateMachine(System.Runtime.CompilerServices.IAsyncStateMachine stateMachine) =>
                    _inner.SetStateMachine(stateMachine);
                public void SetResult() => _inner.SetResult();
                public void SetException(System.Exception exception) => _inner.SetException(exception);
                public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                    where TAwaiter : System.Runtime.CompilerServices.INotifyCompletion
                    where TStateMachine : System.Runtime.CompilerServices.IAsyncStateMachine =>
                    _inner.AwaitOnCompleted(ref awaiter, ref stateMachine);
                public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                    where TAwaiter : System.Runtime.CompilerServices.ICriticalNotifyCompletion
                    where TStateMachine : System.Runtime.CompilerServices.IAsyncStateMachine =>
                    _inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
            }

            public struct PooledTaskMethodBuilder<T>
            {
                private System.Runtime.CompilerServices.AsyncTaskMethodBuilder<T> _inner;
                public static PooledTaskMethodBuilder<T> Create() =>
                    new() { _inner = System.Runtime.CompilerServices.AsyncTaskMethodBuilder<T>.Create() };
                public PooledTask<T> Task => new(_inner.Task);
                public void Start<TStateMachine>(ref TStateMachine stateMachine)
                    where TStateMachine : System.Runtime.CompilerServices.IAsyncStateMachine =>
                    _inner.Start(ref stateMachine);
                public void SetStateMachine(System.Runtime.CompilerServices.IAsyncStateMachine stateMachine) =>
                    _inner.SetStateMachine(stateMachine);
                public void SetResult(T result) => _inner.SetResult(result);
                public void SetException(System.Exception exception) => _inner.SetException(exception);
                public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                    where TAwaiter : System.Runtime.CompilerServices.INotifyCompletion
                    where TStateMachine : System.Runtime.CompilerServices.IAsyncStateMachine =>
                    _inner.AwaitOnCompleted(ref awaiter, ref stateMachine);
                public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                    where TAwaiter : System.Runtime.CompilerServices.ICriticalNotifyCompletion
                    where TStateMachine : System.Runtime.CompilerServices.IAsyncStateMachine =>
                    _inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
            }
        }
        """;
}
