using ColdNet.Core.Domain;

namespace ColdNet.Core.Tests;

public class JobNumberGeneratorTests
{
    [Fact]
    public void GenerateUniqueJobId_returns_a_12_character_alphanumeric_value()
    {
        var id = JobNumberGenerator.GenerateUniqueJobId();

        Assert.Equal(12, id.Length);
        Assert.Matches("^[0-9A-Z]{12}$", id);
    }

    [Fact]
    public void MakeUnique_returns_the_candidate_unchanged_when_it_is_not_taken()
    {
        var existing = new HashSet<string>();

        var result = JobNumberGenerator.MakeUnique("Test", existing);

        Assert.Equal("Test", result);
        Assert.Contains("Test", existing);
    }

    [Fact]
    public void MakeUnique_appends_a_numeric_suffix_when_the_candidate_is_already_taken()
    {
        var existing = new HashSet<string> { "Test" };

        var result = JobNumberGenerator.MakeUnique("Test", existing);

        Assert.Equal("Test-2", result);
        Assert.Contains("Test-2", existing);
    }

    [Fact]
    public void MakeUnique_keeps_incrementing_the_suffix_past_multiple_collisions()
    {
        var existing = new HashSet<string> { "Test", "Test-2", "Test-3" };

        var result = JobNumberGenerator.MakeUnique("Test", existing);

        Assert.Equal("Test-4", result);
    }

    [Fact]
    public void MakeUnique_reserves_each_result_so_repeated_calls_never_collide_with_each_other()
    {
        var existing = new HashSet<string>();

        var first = JobNumberGenerator.MakeUnique("Test", existing);
        var second = JobNumberGenerator.MakeUnique("Test", existing);
        var third = JobNumberGenerator.MakeUnique("Test", existing);

        Assert.Equal(["Test", "Test-2", "Test-3"], new[] { first, second, third });
    }
}
