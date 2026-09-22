using Shouldly;
using Xunit;

namespace Personix.CodeStyle.Analyzers.Tests;

/// <summary>
/// PERSONIX006 — an <c>init</c> property without a default that every creation site sets should be <c>required</c>.
/// </summary>
public class RequiredModifierTests
{
    private const string NameReported = "PERSONIX006 Name";

    [Fact]
    public async Task Init_property_with_default_value_is_not_reported()
    {
        const string source = """
            class Person { public string Name { get; init; } = null!; }
            class Factory
            {
                Person Ann() => new Person { Name = "Ann" };
                Person Bob() => new() { Name = "Bob" };
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Init_property_without_default_set_at_every_creation_is_reported()
    {
        const string source = """
            class Person { public string? Name { get; init; } public int Age { get; init; } }
            class Factory
            {
                Person Ann() => new Person { Name = "Ann", Age = 30 };
                Person Bob() => new() { Name = "Bob" };
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBe([NameReported]);
    }

    [Fact]
    public async Task Init_property_omitted_at_one_creation_is_not_reported()
    {
        const string source = """
            class Person { public string? Name { get; init; } }
            class Factory
            {
                Person Ann() => new Person { Name = "Ann" };
                Person Nobody() => new Person();
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Type_never_created_in_source_is_not_reported()
    {
        const string source = """
            class Person { public string? Name { get; init; } }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Required_property_is_not_reported()
    {
        const string source = """
            class Person { public required string Name { get; init; } }
            class Factory { Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Init_property_assigned_in_constructor_is_not_reported()
    {
        const string source = """
            class Person
            {
                public Person() { Name = "Unknown"; }
                public string Name { get; init; }
            }
            class Factory { Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task With_expression_is_not_a_creation_site()
    {
        const string source = """
            record Person { public string? Name { get; init; } public int Age { get; init; } }
            class Factory
            {
                Person Create() => new Person { Name = "Ann", Age = 1 };
                Person Older(Person person) => person with { Age = 2 };
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBe(["PERSONIX006 Name", "PERSONIX006 Age"]);
    }

    [Fact]
    public async Task Base_property_omitted_when_creating_derived_type_is_not_reported()
    {
        const string source = """
            class Person { public string? Name { get; init; } }
            class Employee : Person { }
            class Factory
            {
                Person Ann() => new Person { Name = "Ann" };
                Employee Nobody() => new Employee();
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Positional_record_property_is_not_reported()
    {
        const string source = """
            record Person(string Name);
            class Factory { Person Create() => new Person("Ann") { Name = "Bob" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Public_type_in_packable_project_is_not_reported()
    {
        const string source = """
            public class Person { public string? Name { get; init; } }
            public class Factory { public Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source, isPackable: true);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Internal_type_in_packable_project_is_reported()
    {
        const string source = """
            internal class Person { public string? Name { get; init; } }
            internal class Factory { public Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source, isPackable: true);

        diagnostics.ShouldBe([NameReported]);
    }
}
