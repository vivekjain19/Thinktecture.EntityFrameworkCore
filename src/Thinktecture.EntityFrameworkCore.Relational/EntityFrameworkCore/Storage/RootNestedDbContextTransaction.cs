using System;
using System.Data.Common;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Thinktecture.EntityFrameworkCore.Storage
{
   /// <summary>
   /// A root transaction.
   /// </summary>
   public sealed class RootNestedDbContextTransaction : NestedDbContextTransaction
   {
      private readonly IRelationalTransactionManager _innerManager;
      private readonly IDbContextTransaction _innerTx;

      /// <summary>
      /// Initializes new instance of <see cref="RootNestedDbContextTransaction"/>.
      /// </summary>
      /// <param name="logger">Logger.</param>
      /// <param name="nestedTransactionManager">Nested transaction manager.</param>
      /// <param name="innerManager">Inner transaction manager.</param>
      /// <param name="tx">The real transaction.</param>
      public RootNestedDbContextTransaction([NotNull] IDiagnosticsLogger<RelationalDbLoggerCategory.NestedTransaction> logger,
                                            [NotNull] NestedRelationalTransactionManager nestedTransactionManager,
                                            [NotNull] IRelationalTransactionManager innerManager,
                                            [NotNull] IDbContextTransaction tx)
         : base(logger, nestedTransactionManager, tx.TransactionId)
      {
         _innerManager = innerManager ?? throw new ArgumentNullException(nameof(innerManager));
         _innerTx = tx ?? throw new ArgumentNullException(nameof(tx));
      }

      /// <inheritdoc />
      protected internal override DbTransaction GetUnderlyingTransaction()
      {
         return _innerTx.GetDbTransaction();
      }

      /// <inheritdoc />
      public override void Commit()
      {
         base.Commit();

         _innerManager.CommitTransaction();
      }

      /// <inheritdoc />
      public override void Rollback()
      {
         base.Rollback();

         _innerManager.RollbackTransaction();
      }

      /// <inheritdoc />
      protected override void Dispose(bool disposing)
      {
         base.Dispose(disposing);

         if (disposing)
            _innerTx.Dispose();
      }
   }
}
