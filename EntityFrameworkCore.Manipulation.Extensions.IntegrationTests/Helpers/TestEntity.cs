﻿using System;
using System.ComponentModel.DataAnnotations;

namespace EntityFrameworkCore.Manipulation.Extensions.IntegrationTests.Helpers
{
    public class TestEntity
    {
        [Key]
        public string Id { get; set; }

        public int IntTestValue { get; set; }

        public bool BoolTestValue { get; set; }

        public long LongTestValue { get; set; }

        public DateTime DateTimeTestValue { get; set; }

		public DateTime? NullableDateTimeTestValue { get; set; }

		public Guid GuidValue { get; set; }

		public Guid? NullableGuidValue { get; set; }

		public TestEnum EnumValue { get; set; }

		// public TestEnum? NullableEnumValue { get; set; }
	}
}
