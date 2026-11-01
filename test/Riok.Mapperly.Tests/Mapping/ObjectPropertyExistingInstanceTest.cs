using Riok.Mapperly.Diagnostics;

namespace Riok.Mapperly.Tests.Mapping;

public class ObjectPropertyExistingInstanceTest
{
    [Fact]
    public void ReadOnlyPropertyShouldMap()
    {
        var source = TestSourceBuilder.Mapping(
            "A",
            "B",
            """
            public class A
            {
                public int? IntValue { get; set; }
                public C NestedValue { get; } = null!;
            }
            """,
            """
            public class B
            {
                public int? IntValue { get; set; }
                public D NestedValue { get; }= null!;
            }
            """,
            """
            public class C
            {
                public int Value { get; set; }
                public int? Value2 { get; set; }
                public int? NullableValue { get; set; }
            }
            """,
            """
            public class D
            {
                public string? Value { get; set; }
                public string Value2 { get; set; }
                public string? NullableValue { get; set; }
            }
            """
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowAndIncludeAllDiagnostics)
            .Should()
            .HaveDiagnostic(
                DiagnosticDescriptors.NullableSourceValueToNonNullableTargetValue,
                "Mapping the nullable source property Value2 of C to the target property Value2 of D which is not nullable"
            )
            .HaveAssertedAllDiagnostics()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                target.IntValue = source.IntValue;
                target.NestedValue.Value = source.NestedValue.Value.ToString();
                if (source.NestedValue.Value2 != null)
                {
                    target.NestedValue.Value2 = source.NestedValue.Value2.Value.ToString();
                }
                if (source.NestedValue.NullableValue != null)
                {
                    target.NestedValue.NullableValue = source.NestedValue.NullableValue.Value.ToString();
                }
                else
                {
                    target.NestedValue.NullableValue = null;
                }
                return target;
                """
            );
    }

    [Fact]
    public void ReadOnlyNullablePropertyShouldMap()
    {
        var source = TestSourceBuilder.Mapping(
            "A",
            "B",
            """
            public class A
            {
                public int? IntValue { get; set; }
                public C? CValue { get; }
            }
            """,
            """
            public class B
            {
                public int? IntValue { get; set; }
                public C? CValue { get; }
            }
            """,
            """
            public class C
            {
                public int IntValue { get; set; }
            }
            """
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowAndIncludeAllDiagnostics)
            .Should()
            .HaveAssertedAllDiagnostics()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                target.IntValue = source.IntValue;
                if (source.CValue != null && target.CValue != null)
                {
                    target.CValue.IntValue = source.CValue.IntValue;
                }
                return target;
                """
            );
    }

    [Fact]
    public void ObjectsWithoutPropertiesShouldNotDiagnostic()
    {
        var source = TestSourceBuilder.Mapping(
            "A",
            "B",
            "public class A { public C? IntValue { get; } }",
            "public class B { public C? IntValue { get; } }",
            "public class C;"
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowAndIncludeAllDiagnostics)
            .Should()
            .HaveAssertedAllDiagnostics()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                return target;
                """
            );
    }

    [Fact]
    public void SameReadOnlyPropertyShouldNotDiagnostic()
    {
        var source = TestSourceBuilder.Mapping(
            "A",
            "B",
            """
            public class A
            {
                public int? IntValue { get; set; }
                public bool? ComputedIntValue => IntValue == 42;
            }
            """,
            """
            public class B
            {
                public int? IntValue { get; set; }
                public bool? ComputedIntValue => IntValue == 42;
            }
            """
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowAndIncludeAllDiagnostics)
            .Should()
            .HaveAssertedAllDiagnostics()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                target.IntValue = source.IntValue;
                return target;
                """
            );
    }

    [Fact]
    public Task UnmappedRequiredPropertyShouldDiagnostic()
    {
        var source = TestSourceBuilder.MapperWithBodyAndTypes(
            "public partial void Update(A source, B target);",
            """
            public class A
            {
                public required string Value { get; set; }
                public required string OtherValue { get; set; }
            }
            """,
            """
            public class B
            {
                public required string Value2 { get; set; }
                public required string OtherValue { get; set; }
            }
            """
        );
        return TestHelper.VerifyGenerator(source);
    }

    [Fact]
    public void ReadOnlyEnumerablePropertyWithNullableSourceParentShouldGuardParent()
    {
        var source = TestSourceBuilder.MapperWithBodyAndTypes(
            "[MapProperty(\"Nested.Items\", \"Items\")] private partial B Map(A source);",
            "public class A { public C? Nested { get; set; } }",
            "public class B { public List<int> Items { get; } = new(); }",
            "public class C { public IEnumerable<int>? Items { get; set; } }"
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowInfoDiagnostics)
            .Should()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                if (source.Nested != null)
                {
                    if (source.Nested.Items != null)
                    {
                        if (global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source.Nested.Items, out var sourceCount))
                        {
                            target.Items.EnsureCapacity(sourceCount + target.Items.Count);
                        }
                        foreach (var item in source.Nested.Items)
                        {
                            target.Items.Add(item);
                        }
                    }
                }
                return target;
                """
            );
    }

    [Fact]
    public void ReadOnlyObjectPropertyWithNullableSourceParentAndNonNullableLeafShouldGuardParent()
    {
        var source = TestSourceBuilder.MapperWithBodyAndTypes(
            "[MapProperty(\"Nested.Value\", \"Value\")] private partial B Map(A source);",
            "public class A { public C? Nested { get; set; } }",
            "public class B { public E Value { get; } = new(); }",
            "public class C { public D Value { get; set; } = new(); }",
            "public class D { public int Id { get; set; } }",
            "public class E { public int Id { get; set; } }"
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowInfoDiagnostics)
            .Should()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                if (source.Nested != null)
                {
                    target.Value.Id = source.Nested.Value.Id;
                }
                return target;
                """
            );
    }

    [Fact]
    public void ReadOnlyObjectPropertyWithNullableSourceParentAndValueTypeLeafShouldGuardParent()
    {
        var source = TestSourceBuilder.MapperWithBodyAndTypes(
            "[MapProperty(\"Nested.Value\", \"Value\")] private partial B Map(A source);",
            "public class A { public C? Nested { get; set; } }",
            "public class B { public E Value { get; } = new(); }",
            "public class C { public D Value { get; set; } }",
            "public struct D { public int Id { get; set; } }",
            "public class E { public int Id { get; set; } }"
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowInfoDiagnostics)
            .Should()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                if (source.Nested != null)
                {
                    target.Value.Id = source.Nested.Value.Id;
                }
                return target;
                """
            );
    }

    [Fact]
    public void ReadOnlyPropertyWithNullableSourceParentShouldShareGuardWithOtherMembers()
    {
        var source = TestSourceBuilder.MapperWithBodyAndTypes(
            """
            [MapProperty("Nested.Id", "Id")]
            [MapProperty("Nested.Value", "Value")]
            private partial B Map(A source);
            """,
            "public class A { public C? Nested { get; set; } }",
            "public class B { public int Id { get; set; } public E Value { get; } = new(); }",
            "public class C { public int Id { get; set; } public D Value { get; set; } = new(); }",
            "public class D { public int Id { get; set; } }",
            "public class E { public int Id { get; set; } }"
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.AllowInfoDiagnostics)
            .Should()
            .HaveMapMethodBody(
                """
                var target = new global::B();
                if (source.Nested != null)
                {
                    target.Id = source.Nested.Id;
                    target.Value.Id = source.Nested.Value.Id;
                }
                return target;
                """
            );
    }

    [Fact]
    public void ReadOnlyFieldWithSourceParentAndDisabledNullableShouldGuardParent()
    {
        var source = TestSourceBuilder.MapperWithBodyAndTypes(
            "[MapProperty(\"Nested.Items\", \"Items\")] private partial B Map(A source);",
            "public class A { public C Nested { get; set; } }",
            "public class B { public readonly List<int> Items = new(); }",
            "public class C { public IEnumerable<int> Items { get; set; } }"
        );

        TestHelper
            .GenerateMapper(source, TestHelperOptions.DisabledNullable)
            .Should()
            .HaveMapMethodBody(
                """
                if (source == null)
                    return default;
                var target = new global::B();
                if (source.Nested != null)
                {
                    if (source.Nested.Items != null && target.Items != null)
                    {
                        if (global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source.Nested.Items, out var sourceCount))
                        {
                            target.Items.EnsureCapacity(sourceCount + target.Items.Count);
                        }
                        foreach (var item in source.Nested.Items)
                        {
                            target.Items.Add(item);
                        }
                    }
                }
                return target;
                """
            );
    }
}
