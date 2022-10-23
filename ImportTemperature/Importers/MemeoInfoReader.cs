using ImportTemperatureMeteoInfo;
using ImportTemperatureMeteoInfo.Importers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ImportTemperature.Importers;

/// <summary>
/// Реализует чтение среднесуточных температур с сайта MeteoInfo.
/// </summary>
class MemeoInfoReader : ITempertatureReader
{
	/// <summary>
	/// Адрес, по которому будет выполнятся POST запросы, для получения идентификаторов меток времени и данные по температуре.
	/// </summary>
	private const string HourArchiveHomeUrl = "https://meteoinfo.ru/hmc-output/observ/obs_arch.php";

	public void Dispose() { }

	public async Task<List<TemperatureRecord>> ReadTemperatures(string city, int cityUtcOffset, DateTime from, DateTime to)
	{
		string cityId = await GetCityId(city);

		Console.WriteLine($"Идентификатор города '{cityId}'");

		// Получаем список всех меток времени, по которым есть данные
		var timeStampsRaw = await GetTimeStamps(cityId);

		var timeStamps = MeteoInfoParser.ParseTimeStamps(timeStampsRaw);

		return await ReadCityTemperatures(cityId, cityUtcOffset, timeStamps, from, to);
	}

	private static async Task<string> GetCityId(string cityName)
	{
		var dataArray = await Post(HourArchiveHomeUrl, "0", "0");

		var cityList = MeteoInfoParser.GetOptions(dataArray[4].ToString().Replace("[", string.Empty).Replace("]", string.Empty).Trim()).ToList();

		// Ищем нормализованное имя города, чтобы не учитывать регистр.

		var cityInfo = cityList.Find(x => x.Name.ToUpperInvariant().Contains(cityName.ToUpperInvariant()));

		if (cityInfo == null)
		{
			throw new Exception($"На сайте не найден город '{cityName}'");
		}

		return cityInfo.UrlPart;
	}

	private static async Task<string> GetTimeStamps(string cityId)
	{
		JArray dataArray = await Post(HourArchiveHomeUrl, cityId, "0");

		return dataArray[2].ToString();
	}

	/// <summary>
	/// Считывает температуры с указанного URL за период importStart - importEnd.
	/// </summary>
	/// <param name="cityUrl"></param>
	/// <param name="cityTimeOffset"></param>
	/// <param name="timeStamps"></param>
	/// <param name="importStart"></param>
	/// <param name="importEnd"></param>
	/// <returns></returns>
	private static async Task<List<TemperatureRecord>> ReadCityTemperatures(string cityUrl, int cityTimeOffset, Dictionary<DateTime, string> timeStamps, DateTime importStart, DateTime importEnd)
	{
		// Считываем данные по температурам.
		Console.WriteLine($"Чтение температур с {importStart} по {importEnd}");

		var result = new List<TemperatureRecord>();

		// Проходим по всем доступным меткам времени.

		foreach (var kvp in timeStamps)
		{
			// Все метки времени на этом сайте в UTC.
			// Чтобы узнать местное время, нужно добавить смещение территории относительно UTC.
			var localTime = kvp.Key.AddHours(cityTimeOffset);

			if (localTime >= importStart && localTime < importEnd)
			{
				string hourContent = await GetTimeData(cityUrl, kvp.Value);

				if (string.IsNullOrWhiteSpace(hourContent))
				{
					Console.WriteLine($"Не удалось получить температуру за {localTime}");
				}
				else
				{
					float? value = MeteoInfoParser.ExtractTemperature(hourContent);

					if (value.HasValue)
					{
						result.Add(new TemperatureRecord { Date = localTime, Temperature = value.Value });

						Console.WriteLine($"Получена температура за {localTime} ({value.Value})");
					}
					else
					{
						Console.WriteLine($"Не найдена температура за {localTime}");
					}
				}
			}
		}

		return AggregateTemperatures(result);
	}

	private static async Task<string> GetTimeData(string cityId, string dateId)
	{
		JArray dataArray = await Post(HourArchiveHomeUrl, cityId, dateId);

		if (dataArray[1].ToString().Replace("[", string.Empty).Replace("]", string.Empty).Trim() == dateId)
		{
			return dataArray[3].ToString();
		}

		return string.Empty;
	}

	private static async Task<JArray> Post(string url, string cityId, string dataId)
	{
		// Раньше сайт возвращал всю страницу сразу. Теперь список меток времени и температуры приходят отдельным POST запросом.
		// Причём всегда возвращается и список меток времени и запрошенная температура.
		// Для получения списка меток времени можно выполнить данный запрос с передачей dataId = 0.
		// Ответ от сервера представлен в Json формате следующей структуры:
		// [id местности][id метки времени][список всех доступных меток времени с идентификаторами][таблица с данными на запрошенную метку времени]
		var values = new Dictionary<string, string>
		{
			{ "lang", "ru-RU" },
			{ "id_city", cityId },
			{ "dt", dataId },
			{ "has_db", "1" },
			{ "dop", "42" }
		};

		using (var client = new HttpClient())
		{
			var content = new FormUrlEncodedContent(values);

			var response = await client.PostAsync(url, content);

			var responseString = await response.Content.ReadAsStringAsync();

			return JsonConvert.DeserializeObject<JArray>(responseString);
		}
	}

	private static List<TemperatureRecord> AggregateTemperatures(IEnumerable<TemperatureRecord> input)
	{
		if (input == null)
			throw new ArgumentNullException(nameof(input));

		var tempGroups = from t in input
						 group t.Temperature
						 by t.Date.Date
						 into g
						 select g;

		var result = tempGroups.Select(tempGroup
			=> new TemperatureRecord
			{
				Date = tempGroup.Key,
				Temperature = tempGroup.Average(x => x)
			}
		)
		.ToList();

		foreach (var record in result)
		{
			Console.WriteLine($"Средняя температура за {record.Date} = {record.Temperature} °C");
		}

		return result;
	}
}
