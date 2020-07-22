﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Manipulation.Extensions.Internal;
using EntityFrameworkCore.Manipulation.Extensions.Internal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Manipulation.Extensions
{
    /// <summary>
    /// Extensions for <see cref="DbContext"/>.
    /// </summary>
    public static class SyncExtensions
    {
        public static Task<SyncResult<TEntity>> SyncAsync<TEntity>(this DbContext dbContext, IQueryable<TEntity> target, IReadOnlyCollection<TEntity> source, CancellationToken cancellationToken = default)
            where TEntity : class, new()
            => SyncInternalAsync(
                dbContext ?? throw new ArgumentNullException(nameof(dbContext)),
                target ?? throw new ArgumentNullException(nameof(target)),
                source ?? throw new ArgumentNullException(nameof(source)),
                ignoreUpdates: false, // this is a full sync
                cancellationToken);

        public static async Task<SyncWithoutUpdateResult<TEntity>> SyncWithoutUpdateAsync<TEntity>(this DbContext dbContext, IQueryable<TEntity> target, IReadOnlyCollection<TEntity> source, CancellationToken cancellationToken = default)
            where TEntity : class, new()
            => await SyncInternalAsync(
                dbContext ?? throw new ArgumentNullException(nameof(dbContext)),
                target ?? throw new ArgumentNullException(nameof(target)),
                source ?? throw new ArgumentNullException(nameof(source)),
                ignoreUpdates: true, // this is a partial sync
                cancellationToken);

        private static async Task<SyncResult<TEntity>> SyncInternalAsync<TEntity>(
            this DbContext dbContext, 
            IQueryable<TEntity> target, 
            IReadOnlyCollection<TEntity> source, 
            bool ignoreUpdates,
            CancellationToken cancellationToken = default)
            where TEntity : class, new()
        {
            var stringBuilder = new StringBuilder();

            IEntityType entityType = dbContext.Model.FindEntityType(typeof(TEntity));

            string tableName = entityType.GetTableName();
            IKey primaryKey = entityType.FindPrimaryKey();
            IProperty[] properties = entityType.GetProperties().ToArray();
            IProperty[] nonPrimaryKeyProperties = properties.Except(primaryKey.Properties).ToArray();

            (string targetCommand, var targetCommandParameters) = target.ToSqlCommand();

            List<object> parameters = new List<object>(source.Count * properties.Length);
            parameters.AddRange(targetCommandParameters);

            var isSqlite = dbContext.Database.IsSqlite();
            if (isSqlite)
            {
                string SourceAliaser(string columnName) => $"source_{columnName}";
                string TargetAliaser(string columnName) => $"target_{columnName}";

                stringBuilder.AppendLine("BEGIN TRANSACTION;")
                             .AppendLine("DROP TABLE IF EXISTS EntityFrameworkManipulationSync;")
                             .AppendLine("CREATE TEMP TABLE EntityFrameworkManipulationSync AS ")
                             .AppendLine("WITH source AS ( ")
                                .AppendSelectFromInlineTable(properties, source, parameters, "x", sqliteSyntax: true)
                                .AppendLine("), ")
                             .AppendLine("target AS ( ")
                                .Append(targetCommand)
                                .AppendLine(") ")
                             .Append("SELECT (CASE WHEN target.Id IS NULL THEN 'INSERT' ELSE 'UPDATE' END) AS _$action, ")
                                .AppendColumnNames(properties, false, "source", SourceAliaser).Append(", ")
                                .AppendColumnNames(properties, false, "target", TargetAliaser)
                                .Append("FROM source LEFT OUTER JOIN target ON ").AppendJoinCondition(primaryKey);

                // We ignore updates by not taking any matches in target (leaving us with only inserts, but crutially the target.* columns)
                if (ignoreUpdates)
                {
                    stringBuilder.Append("WHERE ").AppendJoin(" AND ", primaryKey.Properties.Select(property => FormattableString.Invariant($"target.{property.Name} IS NULL")));
                }

                stringBuilder
                    .AppendLine()
                    .AppendLine("UNION")
                    .Append("SELECT 'DELETE' AS _$action, ")
                        .AppendColumnNames(properties, false, "source", SourceAliaser).Append(", ")
                        .AppendColumnNames(properties, false, "target", TargetAliaser)
                        .Append("FROM target LEFT OUTER JOIN source ON ").AppendJoinCondition(primaryKey)
                        .Append("WHERE ").AppendJoin(" AND ", primaryKey.Properties.Select(property => FormattableString.Invariant($"source.{property.Name} IS NULL"))).AppendLine(";")
                    .Append("DELETE FROM ").Append(tableName).Append(" WHERE EXISTS (SELECT 1 FROM EntityFrameworkManipulationSync WHERE _$action='DELETE' AND ")
                        .AppendJoin(" AND ", primaryKey.Properties.Select(property => FormattableString.Invariant($"{property.Name}={TargetAliaser(property.Name)}"))).AppendLine(");")
                    .Append("INSERT OR REPLACE INTO ").Append(tableName).AppendColumnNames(properties, true)
                        .Append(" SELECT ").AppendJoin(",", properties.Select(m => SourceAliaser(m.GetColumnName())))
                        .AppendLine(" FROM EntityFrameworkManipulationSync WHERE _$action='INSERT' OR _$action='UPDATE';")
                    .Append("SELECT _$action, ")
                        .AppendJoin(", ", primaryKey.Properties.Select(m => SourceAliaser(m.GetColumnName()))).Append(", ")
                        .AppendJoin(", ", properties.Select(m => TargetAliaser(m.GetColumnName())))
                        .AppendLine(" FROM EntityFrameworkManipulationSync;")
                    .Append("COMMIT;");
            }
            else
            {
                stringBuilder
                    .AppendLine("WITH TargetData AS (").Append(targetCommand).Append(") ")
                    .AppendLine("MERGE INTO TargetData AS target ")
                    .Append("USING (").AppendSelectFromInlineTable(properties, source, parameters, "x").Append(") AS source ON ").AppendJoinCondition(primaryKey).AppendLine(" ")
                    .Append("WHEN NOT MATCHED BY TARGET THEN INSERT ")
                        .AppendColumnNames(properties, wrapInParanthesis: true).Append("VALUES ")
                        .AppendColumnNames(properties, wrapInParanthesis: true, identifierPrefix: "source")
                        .AppendLine(" ")
                    .AppendLine("WHEN NOT MATCHED BY SOURCE THEN DELETE");

                if (!ignoreUpdates)
                {
                    stringBuilder.Append("WHEN MATCHED THEN UPDATE SET ").AppendJoin(",", nonPrimaryKeyProperties.Select(property => FormattableString.Invariant($"{property.Name}=source.{property.Name}"))).AppendLine();
                }

                stringBuilder
                    .Append("OUTPUT $action, ")
                    .AppendColumnNames(primaryKey.Properties, wrapInParanthesis: false, "inserted").Append(", ")
                    .AppendColumnNames(properties, wrapInParanthesis: false, "deleted").Append(";");
            }

            using var reader = await dbContext.Database.ExecuteSqlQueryAsync(stringBuilder.ToString(), parameters.ToArray(), cancellationToken);

            List<TEntity> insertedEntities = new List<TEntity>();
            List<TEntity> deletedEntities = new List<TEntity>();
            List<(TEntity OldValue, TEntity NewValue)> updatedEntities = new List<(TEntity OldValue, TEntity NewValue)>();
            var deletedColumnOffset = 1 + primaryKey.Properties.Count; // action + PK key properties lengths

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new object[1 + primaryKey.Properties.Count + properties.Length];
                reader.DbDataReader.GetValues(row);

                var action = row[0] as string;
                var insertKeyPropertyValues = row.Skip(1).Take(primaryKey.Properties.Count).ToArray();

                if (string.Equals(action, "INSERT", StringComparison.OrdinalIgnoreCase))
                {
                    insertedEntities.Add(EntityUtils.FindEntityBasedOnKey(source, primaryKey, insertKeyPropertyValues));
                }
                else if (string.Equals(action, "DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    deletedEntities.Add(EntityUtils.EntityFromRow<TEntity>(row, properties, deletedColumnOffset, useExplicitConversion: isSqlite));
                }
                else
                {
                    // Update
                    var newValue = EntityUtils.FindEntityBasedOnKey(source, primaryKey, insertKeyPropertyValues);
                    var oldValue = EntityUtils.EntityFromRow<TEntity>(row, properties, deletedColumnOffset, useExplicitConversion: isSqlite);
                    updatedEntities.Add((oldValue, newValue));
                }
            }

            return new SyncResult<TEntity>(deletedEntities, insertedEntities, updatedEntities);
        }
    }
}