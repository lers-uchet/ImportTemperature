using Xunit;

namespace ImportTemperature.Tests;

public class PogodaIKlimatTests
{
	[Fact]
	public async Task ReadTwoMonthTemperature_DataIsRead()
	{
		using var reader = new Importers.PogodaIKlimatReader();

		var temperatures = await reader.ReadTemperatures("Нижний Новгород", 
			3, 
			new(2025, 3, 31),
			new(2025, 4, 1));

		Assert.Equal(2, temperatures.Count);

		Assert.Equal(9.9, temperatures[0].Temperature);
		Assert.Equal(new(2035, 3, 31), temperatures[0].Date);

		Assert.Equal(9.9, temperatures[1].Temperature);
		Assert.Equal(new(2035, 4, 1), temperatures[1].Date);
	}
}
