using Shouldly;
using Xunit;

namespace Personix.CodeStyle.Analyzers.Tests;

/// <summary>
/// PERSONIX005 — a settable property written only while the object is being created should use <c>init</c>.
/// </summary>
public class InitAccessorTests
{
    private const string NameReported = "PERSONIX005 Name";

    [Fact]
    public async Task Setter_used_only_in_object_initializer_is_reported()
    {
        const string source = """
            class Person { public string Name { get; set; } = ""; }
            class Factory { Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBe([NameReported]);
    }

    [Fact]
    public async Task Setter_used_after_construction_is_not_reported()
    {
        const string source = """
            class Person { public string Name { get; set; } = ""; }
            class Factory
            {
                Person Create() => new Person { Name = "Ann" };
                void Rename(Person person) => person.Name = "Bob";
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Setter_never_used_in_source_is_not_reported()
    {
        const string source = """
            class Person { public string Name { get; set; } = ""; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Setter_used_in_own_constructor_is_reported()
    {
        const string source = """
            class Person
            {
                public Person(string name) { Name = name; }
                public string Name { get; set; }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBe([NameReported]);
    }

    [Fact]
    public async Task Setter_used_in_derived_constructor_is_reported()
    {
        const string source = """
            class Person { public string Name { get; set; } = ""; }
            class Employee : Person { public Employee() { Name = "Ann"; } }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBe([NameReported]);
    }

    [Fact]
    public async Task Setter_used_in_lambda_inside_constructor_is_not_reported()
    {
        const string source = """
            using System;
            class Person
            {
                public Person() { Action rename = () => Name = "Bob"; rename(); }
                public string Name { get; set; } = "";
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Setter_used_in_with_expression_is_reported()
    {
        const string source = """
            record Person { public string Name { get; set; } = ""; }
            class Factory { Person Copy(Person person) => person with { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBe([NameReported]);
    }

    [Fact]
    public async Task Setter_used_in_nested_member_initializer_is_not_reported()
    {
        const string source = """
            class Person { public string Name { get; set; } = ""; }
            class Team { public Person Lead { get; } = new(); }
            class Factory { Team Create() => new Team { Lead = { Name = "Ann" } }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("counter.Count += 1;")]
    [InlineData("counter.Count++;")]
    [InlineData("--counter.Count;")]
    [InlineData("(counter.Count, _) = (1, 2);")]
    [InlineData("counter.Count ??= 1;")]
    public async Task Other_writes_after_construction_are_not_reported(string write)
    {
        var source = $$"""
            class Counter { public int? Count { get; set; } }
            class Usage
            {
                Counter Create() => new Counter { Count = 0 };
                void Change(Counter counter) { {{write}} }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Init_property_is_not_reported()
    {
        const string source = """
            class Person { public string Name { get; init; } = ""; }
            class Factory { Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldNotContain(NameReported);
    }

    [Fact]
    public async Task Static_property_is_not_reported()
    {
        const string source = """
            class Settings
            {
                static Settings() { Name = "Ann"; }
                public static string Name { get; set; }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Interface_implementation_is_not_reported()
    {
        const string source = """
            interface INamed { string Name { get; set; } }
            class Person : INamed { public string Name { get; set; } = ""; }
            class Factory { Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Override_is_not_reported()
    {
        const string source = """
            abstract class Named { public abstract string Name { get; set; } }
            class Person : Named { public override string Name { get; set; } = ""; }
            class Factory { Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Public_setter_in_packable_project_is_not_reported()
    {
        const string source = """
            public class Person { public string Name { get; set; } = ""; }
            public class Factory { public Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source, isPackable: true);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Internal_setter_in_packable_project_is_reported()
    {
        const string source = """
            internal class Person { public string Name { get; set; } = ""; }
            internal class Factory { public Person Create() => new Person { Name = "Ann" }; }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(source, isPackable: true);

        diagnostics.ShouldBe([NameReported]);
    }
}
