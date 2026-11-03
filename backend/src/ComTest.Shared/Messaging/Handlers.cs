using ComTest.Shared.Results;

namespace ComTest.Shared.Messaging;

/// <summary>A use case that changes state.</summary>
public interface ICommand<TResponse>;

/// <summary>A use case that only reads state.</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
