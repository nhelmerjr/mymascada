using Microsoft.EntityFrameworkCore.Storage;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Data;

/// <summary>
/// EF Core-backed implementation of <see cref="IUnitOfWork"/>. Wraps an
/// <see cref="IDbContextTransaction"/> so application-layer handlers can compose
/// atomic multi-repository writes without referencing EF Core types directly.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new UnitOfWorkTransaction(transaction);
    }

    private sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private bool _committed;

        public UnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                try
                {
                    await _transaction.RollbackAsync();
                }
                catch
                {
                    // Rollback failures are swallowed because the underlying transaction
                    // may already have been rolled back (e.g. connection drop). Dispose
                    // must not throw out of a using block.
                }
            }

            await _transaction.DisposeAsync();
        }
    }
}
