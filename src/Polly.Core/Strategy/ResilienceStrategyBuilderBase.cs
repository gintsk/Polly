using System.ComponentModel.DataAnnotations;
using Polly.Strategy;

namespace Polly.Strategy;

/// <summary>
/// A builder that is used to create an instance of <see cref="ResilienceStrategy"/>.
/// </summary>
/// <remarks>
/// The builder supports chaining multiple strategies into a pipeline of strategies.
/// The resulting instance of <see cref="ResilienceStrategy"/> executes the strategies in the same order they were added to the builder.
/// The order of the strategies is important.
/// </remarks>
public abstract class ResilienceStrategyBuilderBase
{
    private readonly List<Entry> _entries = new();
    private bool _used;

    private protected ResilienceStrategyBuilderBase()
    {
    }

    private protected ResilienceStrategyBuilderBase(ResilienceStrategyBuilderBase other)
    {
        BuilderName = other.BuilderName;
        Properties = other.Properties;
        TimeProvider = other.TimeProvider;
        OnCreatingStrategy = other.OnCreatingStrategy;
    }

    /// <summary>
    /// Gets or sets the name of the builder.
    /// </summary>
    /// <remarks>This property is also included in the telemetry that is produced by the individual resilience strategies.</remarks>
    [Required(AllowEmptyStrings = true)]
    public string BuilderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the custom properties attached to builder options.
    /// </summary>
    public ResilienceProperties Properties { get; } = new();

    /// <summary>
    /// Gets or sets a <see cref="TimeProvider"/> that is used by strategies that work with time.
    /// </summary>
    /// <remarks>
    /// This property is internal until we switch to official System.TimeProvider.
    /// </remarks>
    [Required]
    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets or sets the callback that is invoked just before the final resilience strategy is being created.
    /// </summary>
    internal Action<IList<ResilienceStrategy>>? OnCreatingStrategy { get; set; }

    internal abstract bool IsGenericBuilder { get; }

    /// <summary>
    /// Adds an already created strategy instance to the builder.
    /// </summary>
    /// <param name="strategy">The strategy instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strategy"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this builder was already used to create a strategy. The builder cannot be modified after it has been used.</exception>
    public void AddStrategy(ResilienceStrategy strategy)
    {
        Guard.NotNull(strategy);

        AddStrategy(_ => strategy, EmptyOptions.Instance);
    }

    /// <summary>
    /// Adds a strategy to the builder.
    /// </summary>
    /// <param name="factory">The factory that creates a resilience strategy.</param>
    /// <param name="options">The options associated with the strategy. If none are provided the default instance of <see cref="ResilienceStrategyOptions"/> is created.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this builder was already used to create a strategy. The builder cannot be modified after it has been used.</exception>
    /// <exception cref="ValidationException">Thrown when the <paramref name="options"/> are invalid.</exception>
    public void AddStrategy(Func<ResilienceStrategyBuilderContext, ResilienceStrategy> factory, ResilienceStrategyOptions options)
    {
        Guard.NotNull(factory);
        Guard.NotNull(options);

        ValidationHelper.ValidateObject(options, $"The '{nameof(ResilienceStrategyOptions)}' options are not valid.");

        if (_used)
        {
            throw new InvalidOperationException("Cannot add any more resilience strategies to the builder after it has been used to build a strategy once.");
        }

        _entries.Add(new Entry(factory, options));
    }

    internal ResilienceStrategy BuildStrategy()
    {
        ValidationHelper.ValidateObject(this, $"The '{nameof(ResilienceStrategyBuilder)}' configuration is invalid.");

        _used = true;

        var strategies = _entries.Select(CreateResilienceStrategy).ToList();
        OnCreatingStrategy?.Invoke(strategies);

        if (strategies.Count == 0)
        {
            return NullResilienceStrategy.Instance;
        }

        if (strategies.Count == 1)
        {
            return strategies[0];
        }

        return ResilienceStrategyPipeline.CreatePipeline(strategies);
    }

    private ResilienceStrategy CreateResilienceStrategy(Entry entry)
    {
        var context = new ResilienceStrategyBuilderContext(
            builderName: BuilderName,
            builderProperties: Properties,
            strategyName: entry.Properties.StrategyName,
            strategyType: entry.Properties.StrategyType,
            timeProvider: TimeProvider,
            IsGenericBuilder);

        return entry.Factory(context);
    }

    private sealed record Entry(Func<ResilienceStrategyBuilderContext, ResilienceStrategy> Factory, ResilienceStrategyOptions Properties);

    internal sealed class EmptyOptions : ResilienceStrategyOptions
    {
        public static readonly EmptyOptions Instance = new();

        public override string StrategyType => "Empty";
    }
}