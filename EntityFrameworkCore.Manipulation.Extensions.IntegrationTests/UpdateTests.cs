﻿using EntityFrameworkCore.Manipulation.Extensions.IntegrationTests.Helpers;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EntityFrameworkCore.Manipulation.Extensions.UnitTests
{
	[TestClass]
    public class UpdateTests
    {
		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnEmptyCollection_WhenThereAreNoEntitiesInDbNorInput(DbProvider provider)
        {
            using TestDbContext context = await ContextFactory.GetDbContextAsync(provider); // Note: no seed data => no entities exist

			// Invoke the method and check that the result is empty
			var result = await context.UpdateAsync(Array.Empty<TestEntityCompositeKey>());
			result.Should().BeEmpty();

			// Validate in the DB
			context.TestEntitiesWithCompositeKey.Should().BeEmpty();
        }

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnEmptyCollection_WhenThereAreNoEntitiesInInput(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should not be touched 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should not be touched 2", IdPartB ="B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
			};

			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result is empty
			var result = await context.UpdateAsync(Array.Empty<TestEntityCompositeKey>());
			result.Should().BeEmpty();

			// Validate that the DB is untouched
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(existingEntities);
		}

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnEmptyCollection_WhenThereAreNoMatchingEntitiesBasedOnKey(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should not be touched 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should not be touched 2", IdPartB ="B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
			};

			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result is empty
			var result = await context.UpdateAsync(
				new[]
				{
					new TestEntityCompositeKey { IdPartA = "Non-matching key 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
					new TestEntityCompositeKey { IdPartA = "Non-matching key 2", IdPartB ="B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
				});
			result.Should().BeEmpty();

			// Validate that the DB is untouched
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(existingEntities);
		}

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnEmptyCollection_WhenThereAreNoMatchingEntitiesBasedOnCondition(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should not be touched 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should not be touched 2", IdPartB = "B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
			};

			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result is empty
			var result = await context.UpdateAsync(
				new[]
				{
					new TestEntityCompositeKey { IdPartA = "Should not be touched 1", IdPartB = "B", IntTestValue = 0, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
					new TestEntityCompositeKey { IdPartA = "Should not be touched 2", IdPartB = "B", IntTestValue = 0, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
				},
				condition: updateEntry => updateEntry.Incoming.IntTestValue > updateEntry.Current.IntTestValue);
			result.Should().BeEmpty();

			// Validate that the DB is untouched
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(existingEntities);
		}

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnUpdatedCollection_WhenAllEntitiesAreMatchingWithoutCondition(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should be updated 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 2", IdPartB = "B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
			};
			var expectedEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should be updated 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 781 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 2", IdPartB = "B", IntTestValue = 111, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 6613 },
			};
			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result the updated expected entities
			var result = await context.UpdateAsync(expectedEntities);
			result.Should().BeEquivalentTo(expectedEntities);

			// Validate that the DB is updated
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(expectedEntities);
		}

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnAffectedUpdatedCollection_WhenASubsetOfEntitiesAreMatchingWithoutCondition(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should be updated 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should not be updated 2", IdPartB = "B", IntTestValue = 56123, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 1231 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
				new TestEntityCompositeKey { IdPartA = "Should not be updated 4", IdPartB = "B", IntTestValue = 12, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 897546 },
			};
			var expectedEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should be updated 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 781 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 111, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 6613 },
			};
			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result the updated expected entities
			var result = await context.UpdateAsync(expectedEntities);
			result.Should().BeEquivalentTo(expectedEntities);

			// Validate that the DB is updated
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(new[] { expectedEntities[0], existingEntities[1], expectedEntities[1], existingEntities[3] });
		}

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnAffectedUpdatedCollection_WhenASubsetOfEntitiesAreMatchingWithCondition(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should be updated 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should not be updated 2", IdPartB = "B", IntTestValue = 56123, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 1231 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
				new TestEntityCompositeKey { IdPartA = "Should not be updated 4", IdPartB = "B", IntTestValue = 12, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 897546 },
			};
			var expectedEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should be updated 1", IdPartB = "B", IntTestValue = 561645231, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 781 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 561235164, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 6613 },
				new TestEntityCompositeKey { IdPartA = "Should not be updated 4", IdPartB = "B", IntTestValue = -1, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 897546 },
			};
			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result the updated expected entities
			var result = await context.UpdateAsync(
				expectedEntities,
				condition: x => x.Incoming.IntTestValue > x.Current.IntTestValue); // Only update if IntTestValue is greater than the incoming value, which rules out "Should not be updated 4"
			result.Should().BeEquivalentTo(expectedEntities.Where(x => x.IdPartA != "Should not be updated 4"));

			// Validate that the DB is updated
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(new[] { expectedEntities[0], existingEntities[1], expectedEntities[1], existingEntities[3] });
		}

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnCollectionWithOnlyIncludedPropertiesUpdated_WhenEntitiesMatchAndIncludedPropertyExpresionsArePassed(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should not be updated 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should not be updated 2", IdPartB = "B", IntTestValue = 56123, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 1231 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
			};
			var expectedEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should not be updated 1", IdPartB = "B", IntTestValue = -1, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 781 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 561235164, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 165465132165 },
			};
			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result the updated expected entities
			var result = await context.UpdateAsync(
				expectedEntities,
				condition: x => x.Incoming.IntTestValue > x.Current.IntTestValue, // Only update if IntTestValue is greater than the incoming value, which rules out "Should not be updated 1"
				includedProperties: new Expression<Func<TestEntityCompositeKey, object>>[] { x => x.LongTestValue, x => x.DateTimeTestValue });

			var expectedUpdatedEntity = new TestEntityCompositeKey
			{
				IdPartA = expectedEntities[1].IdPartA,
				IdPartB = expectedEntities[1].IdPartB,
				IntTestValue = existingEntities[2].IntTestValue, // We did not include this field in the update => it should have its original value
				BoolTestValue = existingEntities[2].BoolTestValue, // We did not include this field in the update => it should have its original value
				DateTimeTestValue = expectedEntities[1].DateTimeTestValue,
				LongTestValue = expectedEntities[1].LongTestValue,
			};

			result.Should().BeEquivalentTo(new[] { expectedUpdatedEntity });

			// Validate that the DB is updated
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(new[] { existingEntities[0], existingEntities[1], expectedUpdatedEntity });
		}

		[DataTestMethod]
		[DataRow(DbProvider.Sqlite)]
		[DataRow(DbProvider.SqlServer)]
		public async Task UpdateAsync_ShouldReturnCollectionWithOnlyIncludedPropertiesUpdated_WhenEntitiesMatchAndIncludedPropertyNamesArePassed(DbProvider provider)
		{
			var existingEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should not be updated 1", IdPartB = "B", IntTestValue = 561645, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 54123 },
				new TestEntityCompositeKey { IdPartA = "Should not be updated 2", IdPartB = "B", IntTestValue = 56123, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 1231 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 111, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow, LongTestValue = 65465132165 },
			};
			var expectedEntities = new[]
			{
				new TestEntityCompositeKey { IdPartA = "Should not be updated 1", IdPartB = "B", IntTestValue = -1, BoolTestValue = true, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 781 },
				new TestEntityCompositeKey { IdPartA = "Should be updated 3", IdPartB = "B", IntTestValue = 561235164, BoolTestValue = false, DateTimeTestValue = DateTime.UtcNow.AddDays(1), LongTestValue = 165465132165 },
			};
			using TestDbContext context = await ContextFactory.GetDbContextAsync(provider, seedData: existingEntities);

			// Invoke the method and check that the result the updated expected entities
			var result = await context.UpdateAsync(
				expectedEntities,
				condition: x => x.Incoming.IntTestValue > x.Current.IntTestValue, // Only update if IntTestValue is greater than the incoming value, which rules out "Should not be updated 1"
				includedProperties: new[] { nameof(TestEntityCompositeKey.LongTestValue), nameof(TestEntityCompositeKey.DateTimeTestValue) });

			var expectedUpdatedEntity = new TestEntityCompositeKey
			{
				IdPartA = expectedEntities[1].IdPartA,
				IdPartB = expectedEntities[1].IdPartB,
				IntTestValue = existingEntities[2].IntTestValue, // We did not include this field in the update => it should have its original value
				BoolTestValue = existingEntities[2].BoolTestValue, // We did not include this field in the update => it should have its original value
				DateTimeTestValue = expectedEntities[1].DateTimeTestValue,
				LongTestValue = expectedEntities[1].LongTestValue,
			};

			result.Should().BeEquivalentTo(new[] { expectedUpdatedEntity });

			// Validate that the DB is updated
			context.TestEntitiesWithCompositeKey.Should().BeEquivalentTo(new[] { existingEntities[0], existingEntities[1], expectedUpdatedEntity });
		}
	}
}
