using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using Xunit.Abstractions;

namespace Thinktecture.EntityFrameworkCore.Storage.NestedRelationalTransactionManagerTests
{
   [SuppressMessage("ReSharper", "UnusedVariable")]
   public class ResetState : NestedRelationalTransactionManagerTestBase
   {
      public ResetState([NotNull] ITestOutputHelper testOutputHelper)
         : base(testOutputHelper)
      {
      }

      [Fact]
      public void Should_do_nothing_if_not_transaction_is_active()
      {
         SUT.ResetState();

         SUT.CurrentTransaction.Should().BeNull();
      }

      [Fact]
      public void Should_dispose_open_root_transaction()
      {
         var rootTx = SUT.BeginTransaction();

         SUT.ResetState();

         SUT.CurrentTransaction.Should().BeNull();
         IsTransactionUsable(rootTx.GetDbTransaction()).Should().BeFalse();
      }

      [Fact]
      public void Should_dispose_current_child_and_root_transactions()
      {
         var rootTx = SUT.BeginTransaction();
         var childTx = SUT.BeginTransaction();
         var secondChildTx = SUT.BeginTransaction();

         SUT.ResetState();

         SUT.CurrentTransaction.Should().BeNull();
         IsTransactionUsable(rootTx.GetDbTransaction()).Should().BeFalse();
      }
   }
}
