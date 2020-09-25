using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Thinktecture.EntityFrameworkCore.Migrations;
using Thinktecture.EntityFrameworkCore.Query.ExpressionTranslators;
using Thinktecture.EntityFrameworkCore.Storage;

namespace Thinktecture.EntityFrameworkCore.Infrastructure
{
   /// <summary>
   /// Extensions for DbContextOptions.
   /// </summary>
   public class RelationalDbContextOptionsExtension : IDbContextOptionsExtension
   {
      private static readonly IRelationalDbContextComponentDecorator _defaultDecorator = new RelationalDbContextComponentDecorator();

      private readonly List<ServiceDescriptor> _serviceDescriptors;
      private bool _activateExpressionFragmentTranslatorPluginSupport;

      /// <inheritdoc />
      [NotNull]
      public string LogFragment => $@"
{{
   'ExpressionFragmentTranslatorPluginSupport'={_activateExpressionFragmentTranslatorPluginSupport},
   'Number of custom services'={_serviceDescriptors.Count},
   'Default schema respecting components added'={AddSchemaRespectingComponents},
   'DescendingSupport'={AddDescendingSupport},
   'NestedTransactionsSupport'={AddNestedTransactionsSupport}
}}";

      /// <summary>
      /// Indication whether to add support for "order by desc".
      /// </summary>
      public bool AddDescendingSupport { get; set; }

      /// <summary>
      /// Adds components so Entity Framework Core can handle changes of the database schema at runtime.
      /// </summary>
      public bool AddSchemaRespectingComponents { get; set; }

      /// <summary>
      /// Decorates components.
      /// </summary>
      public IRelationalDbContextComponentDecorator ComponentDecorator { get; set; }

      /// <summary>
      /// Adds support for nested transactions.
      /// </summary>
      public bool AddNestedTransactionsSupport { get; set; }

      /// <summary>
      /// Initializes new instance of <see cref="RelationalDbContextOptionsExtension"/>.
      /// </summary>
      public RelationalDbContextOptionsExtension()
      {
         _serviceDescriptors = new List<ServiceDescriptor>();
      }

      /// <inheritdoc />
      public bool ApplyServices(IServiceCollection services)
      {
         services.TryAddSingleton(this);

         if (_activateExpressionFragmentTranslatorPluginSupport)
            RegisterCompositeExpressionFragmentTranslator(services);

         if (AddSchemaRespectingComponents)
            RegisterDefaultSchemaRespectingComponents(services);

         if (AddNestedTransactionsSupport)
            RegisterNestedTransactionManager(services);

         foreach (var descriptor in _serviceDescriptors)
         {
            services.Add(descriptor);
         }

         return false;
      }

      private static void RegisterNestedTransactionManager([NotNull] IServiceCollection services)
      {
         var lifetime = GetLifetime<IRelationalConnection>();

         services.Add(ServiceDescriptor.Describe(typeof(NestedRelationalTransactionManager),
                                                 provider => new NestedRelationalTransactionManager(provider.GetRequiredService<IDiagnosticsLogger<RelationalDbLoggerCategory.NestedTransaction>>(), provider.GetRequiredService<IRelationalConnection>()),
                                                 lifetime));
         services.Add(ServiceDescriptor.Describe(typeof(IDbContextTransactionManager), provider => provider.GetRequiredService<NestedRelationalTransactionManager>(), lifetime));
         services.Add(ServiceDescriptor.Describe(typeof(IRelationalTransactionManager), provider => provider.GetRequiredService<NestedRelationalTransactionManager>(), lifetime));
      }

      private static ServiceLifetime GetLifetime<TService>()
      {
         return EntityFrameworkRelationalServicesBuilder.RelationalServices[typeof(TService)].Lifetime;
      }

      private void RegisterDefaultSchemaRespectingComponents([NotNull] IServiceCollection services)
      {
         services.AddSingleton<IMigrationOperationSchemaSetter, MigrationOperationSchemaSetter>();
         var decorator = ComponentDecorator ?? _defaultDecorator;

         decorator.RegisterDecorator<IModelCacheKeyFactory>(services, typeof(DefaultSchemaRespectingModelCacheKeyFactory<>));
         decorator.RegisterDecorator<IModelCustomizer>(services, typeof(DefaultSchemaModelCustomizer<>));
         decorator.RegisterDecorator<IMigrationsAssembly>(services, typeof(DefaultSchemaRespectingMigrationAssembly<>));
      }

      private void RegisterCompositeExpressionFragmentTranslator([NotNull] IServiceCollection services)
      {
         var decorator = ComponentDecorator ?? _defaultDecorator;

         decorator.RegisterDecorator<IExpressionFragmentTranslator>(services, typeof(CompositeExpressionFragmentTranslator<>));
      }

      /// <inheritdoc />
      public long GetServiceProviderHashCode()
      {
         return 0;
      }

      /// <inheritdoc />
      public void Validate(IDbContextOptions options)
      {
      }

      /// <summary>
      /// Adds a custom <see cref="IExpressionFragmentTranslatorPlugin"/> to dependency injection.
      /// </summary>
      /// <param name="type">Translator plugin to add.</param>
      /// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
      public void AddExpressionFragmentTranslatorPlugin([NotNull] Type type)
      {
         if (type == null)
            throw new ArgumentNullException(nameof(type));

         if (!typeof(IExpressionFragmentTranslatorPlugin).IsAssignableFrom(type))
            throw new ArgumentException($"The provided type '{type.DisplayName()}' must implement '{nameof(IExpressionFragmentTranslatorPlugin)}'.", nameof(type));

         Add(ServiceDescriptor.Singleton(typeof(IExpressionFragmentTranslatorPlugin), type));
         _activateExpressionFragmentTranslatorPluginSupport = true;
      }

      /// <summary>
      /// Adds a service descriptor for registration of custom services with internal dependency injection container of Entity Framework Core.
      /// </summary>
      /// <param name="serviceDescriptor">Service descriptor to add.</param>
      /// <exception cref="ArgumentNullException"><paramref name="serviceDescriptor"/> is <c>null</c>.</exception>
      public void Add([NotNull] ServiceDescriptor serviceDescriptor)
      {
         if (serviceDescriptor == null)
            throw new ArgumentNullException(nameof(serviceDescriptor));

         _serviceDescriptors.Add(serviceDescriptor);
      }
   }
}
