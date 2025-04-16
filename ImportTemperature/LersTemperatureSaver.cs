using Lers.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ImportTemperature;

/// <summary>
/// Сохраняет считанные температуры на сервер ЛЭРС УЧЁТ.
/// </summary>
class LersTemperatureSaver : IDisposable
{
	private readonly Uri _baseUri;

	private readonly HttpClient _httpClient;

	public LersTemperatureSaver(Uri baseUri)
	{
		_baseUri = baseUri;

		_httpClient = new HttpClient
		{
			BaseAddress = baseUri
		};
	}

	public void Dispose() => _httpClient.Dispose();

	public async Task Authenticate(string login, string password)
	{
		var authController = new LoginClient(_baseUri.ToString(), _httpClient);

		LoginResponseParameters response = await authController.LoginPlainAsync(new AuthenticatePlainRequestParameters
		{
			Application = "Утилита импорта температур",
			Login = login,
			Password = password
		});

		SetToken(response.Token);
	}


	/// <summary>
	/// Сохраняет температуры наружного воздуха в указанную территорию.
	/// </summary>
	/// <param name="records"></param>
	/// <param name="territory"></param>
	/// <param name="missingOnly"></param>
	/// <returns></returns>
	public async Task Save(List<TemperatureRecord> records, Territory territory, bool missingOnly)
	{
		ArgumentNullException.ThrowIfNull(territory);

		var weatherClient = new WeatherClient(_baseUri.ToString(), _httpClient);

		IDictionary<DateTimeOffset, TerritoryOutdoorTemperature>? existingTemperature = null;

		if (missingOnly)
		{
			// Если импортируются только отсутствующие данные, получаем существующую температуру наружного воздуха.

			existingTemperature = (await weatherClient.GetAsync(territory.Id))
				.ToDictionary(x => x.Date);
		}

		var outdoorTemp = new List<TerritoryOutdoorTemperature>();

		foreach (var record in records)
		{
			if (existingTemperature  == null || !existingTemperature.ContainsKey(record.Date))
			{
				outdoorTemp.Add(new TerritoryOutdoorTemperature
				{
					Date = new DateTimeOffset(DateTime.SpecifyKind(record.Date, DateTimeKind.Unspecified), TimeSpan.FromHours(territory.TimeZoneOffset)),
					Value = record.Temperature
				});
			}
		}

		if (outdoorTemp.Count <= 0)
		{
			Console.WriteLine("Нет данных для сохранения.");
		}
		else
		{
			await weatherClient.SetAsync(territory.Id, outdoorTemp);
		}
	}

	internal void SetToken(string token)
	{
		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
	}

	public async Task<Territory?> GetTerritory(string? territoryName)
	{
		var territoryClient = new TerritoriesClient(_baseUri.ToString(), _httpClient);

		if (string.IsNullOrEmpty(territoryName))
		{
			// Если территория не задана, возвращаем территорию по умолчанию (с идентификатором 1).

			return await territoryClient.GetByIdAsync(1);
		}
		else
		{
			ICollection<Territory> list = await territoryClient.GetListAsync();

			return list.FirstOrDefault(x => x.Name == territoryName);
		}
	}
}
