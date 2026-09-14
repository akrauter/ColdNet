using ColdNet.Core.Properties;

namespace ColdNet.Core.Tests;

public class PropertyBagTests
{
    [Fact]
    public void Set_replaces_existing_values()
    {
        var bag = new PropertyBag();
        bag.Add("field", "a");
        bag.Add("field", "b");

        bag.Set("field", "c");

        Assert.Equal(["c"], bag.GetAll("field"));
    }

    [Fact]
    public void Add_accumulates_multi_value_fields()
    {
        var bag = new PropertyBag();
        bag.Add("field", "a");
        bag.Add("field", "b");

        Assert.Equal(["a", "b"], bag.GetAll("field"));
        Assert.Equal("a", bag.Get("field"));
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_values()
    {
        var bag = new PropertyBag();
        bag.Set("documentType", "INVOICE");
        bag.Add("lineItem", "1");
        bag.Add("lineItem", "2");

        var path = Path.Combine(Path.GetTempPath(), $"coldnet-test-{Guid.NewGuid():N}.properties.json");
        try
        {
            await bag.SaveAsync(path);
            var loaded = await PropertyBag.LoadAsync(path);

            Assert.Equal("INVOICE", loaded.Get("documentType"));
            Assert.Equal(["1", "2"], loaded.GetAll("lineItem"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_returns_empty_bag_for_missing_file()
    {
        var bag = await PropertyBag.LoadAsync(Path.Combine(Path.GetTempPath(), $"coldnet-missing-{Guid.NewGuid():N}.json"));

        Assert.Empty(bag.Values);
    }
}
