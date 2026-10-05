using System.Reflection;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A stand-in for any service interface whose calls fail the way a real async service
/// fails: the returned task is faulted (not a synchronous throw). Used to check what a
/// page does with a failure in work nothing awaits — a reload started by a filter change
/// or a grid click.
///
/// <see cref="Create{T}"/> fails every call. <see cref="Wrap{T}"/> forwards to a real
/// service until the switch is turned, so the page can load normally first.
/// </summary>
public class FailingService : DispatchProxy
{
    /// <summary>Turns a wrapped service from working to failing.</summary>
    public sealed class Switch
    {
        public bool Failing { get; set; }
    }

    private object? _inner;
    private Switch _switch = new() { Failing = true };

    /// <summary>A <typeparamref name="T"/> whose methods all return faulted tasks.</summary>
    public static T Create<T>() where T : class => Create<T, FailingService>();

    /// <summary>A <typeparamref name="T"/> that behaves like <paramref name="inner"/>
    /// until <paramref name="failing"/> is turned on.</summary>
    public static T Wrap<T>(T inner, out Switch failing) where T : class
    {
        var proxy = Create<T, FailingService>();
        var self = (FailingService)(object)proxy;
        self._inner = inner;
        self._switch = failing = new Switch();
        return proxy;
    }

    private static readonly MethodInfo FromExceptionOfT = typeof(Task)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Task.FromException) && m.IsGenericMethodDefinition);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;

        if (!_switch.Failing && _inner is not null)
        {
            try
            {
                return method.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        var returnType = method.ReturnType;
        var failure = new InvalidOperationException($"{method.Name} failed (test double)");

        // Event accessors and plain void members do nothing: only the work fails.
        if (returnType == typeof(void)) return null;
        if (returnType == typeof(Task)) return Task.FromException(failure);
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            return FromExceptionOfT.MakeGenericMethod(returnType.GetGenericArguments()[0])
                                   .Invoke(null, [failure]);

        throw failure;
    }
}
