﻿using System;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dime.Repositories
{
    /// <summary>
    /// Represents a repository with Entity Framework as the backbone for connecting to SQL databases
    /// </summary>
    /// <typeparam name="TEntity">The domain model that is registered in the underlying DbContext</typeparam>
    /// <typeparam name="TContext"></typeparam>
    [ExcludeFromCodeCoverage]
    public partial class EfRepository<TEntity, TContext> : ISqlRepository<TEntity>
        where TEntity : class, new()
        where TContext : DbContext
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="EfRepository{TEntity,TContext}"/> class
        /// </summary>
        /// <param name="dbContext">The DbContext instance</param>
        public EfRepository(TContext dbContext)
        {
            Context = dbContext;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EfRepository{TEntity,TContext}"/> class
        /// </summary>
        /// <param name="dbContext">The DbContext instance</param>
        /// <param name="configuration">Repository behavior configuration</param>
        public EfRepository(TContext dbContext, IMultiTenantRepositoryConfiguration configuration)
        {
            Context = dbContext;
            Configuration = configuration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EfRepository{TEntity,TContext}"/> class
        /// </summary>
        /// <param name="dbContextFactory">Context factory</param>
        public EfRepository(IMultiTenantDbContextFactory<TContext> dbContextFactory)
        {
            Factory = dbContextFactory;
            Configuration = new RepositoryConfiguration();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EfRepository{TEntity,TContext}"/> class
        /// </summary>
        /// <param name="dbContextFactory">Context factory</param>
        /// <param name="configuration">Repository behavior configuration</param>
        public EfRepository(IMultiTenantDbContextFactory<TContext> dbContextFactory, IMultiTenantRepositoryConfiguration configuration)
        {
            Factory = dbContextFactory;
            Configuration = configuration;
        }

        #endregion Constructor

        #region Properties

        private TContext _context;

        protected TContext Context
        {
            get => _context ?? Factory.Create("");
            set => _context = value;
        }

        private IMultiTenantDbContextFactory<TContext> Factory { get; }
        public IMultiTenantRepositoryConfiguration Configuration { get; set; }

        #endregion Properties

        #region Methods

        #region Save Changes Region

        /// <summary>
        ///
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public virtual bool SaveChanges(TContext context)
        {
            int retryMax;
            bool saveFailed;
            do
            {
                try
                {
                    if (Configuration?.SaveInBatch ?? false)
                        return false;

                    int result = context.SaveChanges();
                    return 0 < result;
                }
                catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx)
                {
                    if (Configuration?.SaveStrategy == ConcurrencyStrategy.ClientFirst)
                    {
                        foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                        {
                            PropertyValues dbValues = failedEntry.GetDatabaseValues();
                            if (dbValues == null)
                                continue;

                            failedEntry.OriginalValues.SetValues(dbValues);
                            return SaveChanges(context);
                        }
                        return true;
                    }

                    foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries) failedEntry.Reload();
                    return true;
                }
                catch (DbUpdateException dbUpdateEx)
                {
                    if (dbUpdateEx.InnerException?.InnerException == null)
                        throw;

                    if (!(dbUpdateEx.InnerException.InnerException is SqlException sqlException))
                        throw new DatabaseAccessException(dbUpdateEx.Message, dbUpdateEx.InnerException);

                    throw sqlException.Number switch
                    {
                        // Unique constraint error
                        2627 => (Exception)new ConcurrencyException(sqlException.Message, sqlException),
                        // Constraint check violation
                        // Duplicated key row error
                        547 => new ConstraintViolationException(sqlException.Message,
                            sqlException) // A custom exception of yours for concurrency issues
                        ,
                        2601 => new ConstraintViolationException(sqlException.Message,
                            sqlException) // A custom exception of yours for concurrency issues
                        ,
                        _ => new DatabaseAccessException(sqlException.Message, sqlException)
                    };
                }
            }
            while (saveFailed && retryMax <= 3);
        }

        #endregion Save Changes Region

        #region Dispose region

        /// <summary>
        /// Releases all resources used by the Entities
        /// </summary>
        public void Dispose()
        {
            if (Context == null)
                return;

            Context.Dispose();
            Context = null;
        }

        #endregion Dispose region

        /// <summary>
        ///
        /// </summary>
        /// <param name="repository"></param>
        public static explicit operator TContext(EfRepository<TEntity, TContext> repository)
            => repository.Context;

        #endregion Methods
    }
}