﻿using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;

namespace Dime.Repositories
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public abstract partial class MultiTenantContextFactory<TContext> : IMultiTenantDbContextFactory<TContext> where TContext : DbContext
    {
        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        protected MultiTenantContextFactory()
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionString"></param>
        protected MultiTenantContextFactory(string connectionString) : this()
        {
            Connection = connectionString;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="tenant"></param>
        protected MultiTenantContextFactory(string connectionString, string tenant) : this(connectionString)
        {
            Tenant = tenant;
        }

        #endregion Constructor

        #region Properties

        protected static ConcurrentDictionary<Tuple<string, string>, DbCompiledModel> ModelCache = new ConcurrentDictionary<Tuple<string, string>, DbCompiledModel>();
        protected static ConcurrentDictionary<Tuple<string, string, string>, DbCompiledModel> NamedModelCache = new ConcurrentDictionary<Tuple<string, string, string>, DbCompiledModel>();

        public string Connection { get; set; }
        public string Tenant { get; set; }

        #endregion Properties

        /// <summary>
        /// Creates the instance of <typeparamref name="TContext"/> with the default settings
        /// </summary>
        /// <returns></returns>
        public virtual TContext Create()
        {
            if (!string.IsNullOrEmpty(Tenant) && !string.IsNullOrEmpty(Connection))
                return Create(Tenant, Connection);
            else
                return Create("dbo", Connection);
        }

        /// <summary>
        /// Creates the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
        public virtual TContext Create(string connection)
        {
            return Create("dbo", connection);
        }

        /// <summary>
        /// Creates the specified tenant.
        /// </summary>
        /// <param name="tenant">The tenant.</param>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
        /// <history>
        /// [HB] 15/02/2016 - Reintroduce tuple mechanism to prevent memory leaks
        /// </history>
        public TContext Create(string tenant, string connection, string context)
        {
            if (string.IsNullOrEmpty(context))
                return Create(tenant, connection);

            SqlConnectionFactory connectionFactory = new SqlConnectionFactory();
            DbConnection dbConnection = connectionFactory.CreateConnection(connection);
            Database.SetInitializer<TContext>(null);

            DbCompiledModel compiledModel = NamedModelCache.GetOrAdd(
                Tuple.Create(tenant, dbConnection.ConnectionString, context),
                t => GetContextModel(dbConnection, tenant));

            return ConstructContext(dbConnection, compiledModel, false);
        }

        /// <summary>
        /// Creates the specified tenant.
        /// </summary>
        /// <param name="tenant">The tenant.</param>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
        /// <history>
        /// [HB] 15/02/2016 - Reintroduce tuple mechanism to prevent memory leaks
        /// </history>
        public TContext Create(string tenant, string connection)
        {
            SqlConnectionFactory connectionFactory = new SqlConnectionFactory();
            DbConnection dbConnection = connectionFactory.CreateConnection(connection);
            Database.SetInitializer<TContext>(null);

            DbCompiledModel compiledModel = ModelCache.GetOrAdd(
               Tuple.Create(tenant, dbConnection.ConnectionString),
               t => GetContextModel(dbConnection, tenant));

            return ConstructContext(dbConnection, compiledModel, false);
        }

        /// <summary>
        /// Constructs the context.
        /// </summary>
        /// <returns></returns>
        protected abstract TContext ConstructContext(DbConnection existingConnection, DbCompiledModel model, bool contextOwnsConnection);

        /// <summary>
        /// Constructs the context.
        /// </summary>
        /// <returns></returns>
        protected abstract TContext ConstructContext();

        /// <summary>
        /// Gets the scheduler context model.
        /// </summary>
        /// <param name="dbConnection">The database connection.</param>
        /// <returns></returns>
        protected abstract DbCompiledModel GetContextModel(DbConnection dbConnection, string schema);
    }
}