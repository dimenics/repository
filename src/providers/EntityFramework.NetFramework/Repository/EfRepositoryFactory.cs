﻿using System.Data.Entity;
using System.Diagnostics.CodeAnalysis;

namespace Dime.Repositories
{
    /// <summary>
    ///  Factory class that is responsible for generating repositories
    /// </summary>
    /// <typeparam name="TContext">The DbContext implementation</typeparam>
    [ExcludeFromCodeCoverage]
    public class EfRepositoryFactory<TContext> : IMultiTenantRepositoryFactory where TContext : DbContext
    {
        /// <summary>
        /// Constructor that only accepts the DbContext Factory and uses the default repository configuration
        /// </summary>
        /// <param name="contextFactory"></param>
        public EfRepositoryFactory(IMultiTenantDbContextFactory<TContext> contextFactory)
            : this(contextFactory, GetDefaultRepositoryConfiguration())
        {
        }

        /// <summary>
        /// Constructor that accepts the DbContext Factory and uses custom repository configuration
        /// </summary>
        /// <param name="contextFactory">The factory that actually generates the DbContext instance</param>
        /// <param name="repositoryConfiguration">The configuration for the repository</param>
        public EfRepositoryFactory(
            IMultiTenantDbContextFactory<TContext> contextFactory,
            IMultiTenantRepositoryConfiguration repositoryConfiguration)
        {
            ContextFactory = contextFactory;
            RepositoryConfiguration = repositoryConfiguration;
        }

        protected IMultiTenantDbContextFactory<TContext> ContextFactory { get; }
        public IMultiTenantRepositoryConfiguration RepositoryConfiguration { get; set; }

        /// <summary>
        /// Gets the repository.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <returns></returns>
        public virtual IRepository<TEntity> Create<TEntity>() where TEntity : class, new()
            => Create<TEntity>(RepositoryConfiguration.Connection);

        /// <summary>
        /// Gets the repository.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
        public virtual IRepository<TEntity> Create<TEntity>(string connection) where TEntity : class, new()
        {
            TContext dbContext = ContextFactory.Create(connection ?? RepositoryConfiguration.Connection);
            return new EfRepository<TEntity, TContext>(dbContext, RepositoryConfiguration);
        }

        /// <summary>
        /// Gets the repository.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity</typeparam>
        /// <param name="tenant">The tenant's identifier</param>
        /// <param name="connection">The database connection string</param>
        /// <returns></returns>
        public virtual IRepository<TEntity> Create<TEntity>(string tenant, string connection) where TEntity : class, new()
        {
            TContext dbContext = ContextFactory.Create(connection ?? RepositoryConfiguration.Connection);
            return new EfRepository<TEntity, TContext>(dbContext, RepositoryConfiguration);
        }

        /// <summary>
        /// Default settings for the repository
        /// </summary>
        /// <returns></returns>
        private static IMultiTenantRepositoryConfiguration GetDefaultRepositoryConfiguration()
            => new RepositoryConfiguration
            {
                SaveInBatch = false,
                Cached = true,
                SaveStrategy = ConcurrencyStrategy.ClientFirst
            };
    }
}