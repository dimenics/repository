﻿using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// Generic repository using Entity Framework
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TContext"></typeparam>
    public partial class EfRepository<TEntity, TContext>
    {
        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> SaveChangesAsync()
        {
            int retryMax = 0;
            bool saveFailed = false;
            do
            {
                try
                {
                    return !Configuration.SaveInBatch && 0 < await Context.SaveChangesAsync().ConfigureAwait(false);
                }
                catch (DbEntityValidationException validationEx)
                {
                    foreach (DbEntityValidationResult entityValidationResult in validationEx.EntityValidationErrors)
                        foreach (DbValidationError validationError in entityValidationResult.ValidationErrors)
                            Trace.WriteLine($"Property: \"{validationError.PropertyName}\", Error: \"{validationError.ErrorMessage}\"");

                    throw;
                }
                catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx)
                {
                    if (Configuration.SaveStrategy == ConcurrencyStrategy.ClientFirst)
                    {
                        foreach (DbEntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                        {
                            if (failedEntry.State == EntityState.Deleted)
                            {
                                failedEntry.State = EntityState.Detached;
                                continue;
                            }

                            DbPropertyValues dbValues = failedEntry.GetDatabaseValues();
                            if (dbValues == null)
                                continue;

                            failedEntry.OriginalValues.SetValues(dbValues);
                            return await SaveChangesAsync().ConfigureAwait(false);
                        }
                        return true;
                    }
                    else
                    {
                        foreach (DbEntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                            await failedEntry.ReloadAsync().ConfigureAwait(false);

                        return true;
                    }
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

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> SaveChangesAsync(TContext context)
        {
            int retryMax = 0;
            bool saveFailed = false;
            do
            {
                try
                {
                    if (!Configuration.SaveInBatch)
                    {
                        int result = await context.SaveChangesAsync().ConfigureAwait(false);
                        return 0 < result;
                    }
                    else
                        return false;
                }
                catch (DbEntityValidationException validationEx)
                {
                    foreach (DbEntityValidationResult entityValidationResult in validationEx.EntityValidationErrors)
                        foreach (DbValidationError validationError in entityValidationResult.ValidationErrors)
                            Debug.WriteLine("Property: \"{0}\", Error: \"{1}\"", validationError.PropertyName, validationError.ErrorMessage);

                    throw;
                }
                catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx)
                {
                    if (Configuration.SaveStrategy == ConcurrencyStrategy.ClientFirst)
                    {
                        bool retried = false;
                        foreach (DbEntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                        {
                            if (failedEntry.State == EntityState.Deleted)
                            {
                                failedEntry.State = EntityState.Detached;
                                retried = true;
                                continue;
                            }

                            DbPropertyValues dbValues = failedEntry.GetDatabaseValues();
                            if (dbValues == null)
                                continue;

                            retried = true;
                            failedEntry.OriginalValues.SetValues(dbValues);
                            return await SaveChangesAsync(context).ConfigureAwait(false);
                        }

                        if (!retried)
                            throw;

                        return retried;
                    }
                    else
                    {
                        foreach (DbEntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                            await failedEntry.ReloadAsync().ConfigureAwait(false);

                        return true;
                    }
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
    }
}