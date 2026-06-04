using Microsoft.Extensions.Logging;
using Npgsql;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<UnitOfWork> _logger;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public UnitOfWork(NpgsqlConnectionFactory connectionFactory, ILogger<UnitOfWork> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _connection = _connectionFactory.CreateConnection();
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            _transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar transação PostgreSQL.");
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction is not null)
            {
                await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar transação PostgreSQL.");
            throw;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction is not null)
            {
                await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desfazer transação PostgreSQL.");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
