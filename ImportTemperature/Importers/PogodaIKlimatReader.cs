using ImportTemperature;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImportTemperature.Importers
{
	class PogodaIKlimatReader : ITempertatureReader
	{
		private const string CityListUrl = "monitor.php";


		private readonly HttpClient _httpClient = new HttpClient
		{
			BaseAddress = new Uri("http://www.pogodaiklimat.ru")
		};

		public void Dispose() => _httpClient.Dispose();

		public async Task<List<TemperatureRecord>> ReadTemperatures(string city, int cityUtcOffset, DateTime from, DateTime to)
		{
			var cityId = await GetCityUrl(city);

			return await GetTemperatures(cityId, from, to);
		}


		private async Task<string> GetCityUrl(string city)
		{
			string content = await GetPage(CityListUrl);

			return ParseCityId(city, content);
		}

		private async Task<string> GetPage(string url)
		{
			var response = await _httpClient.GetAsync(url);

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"Ошибка получения страницы {url}.");
			}

			return await response.Content.ReadAsStringAsync();
		}

		private string ParseCityId(string city, string content)
		{
			var pattern = new Regex(@$"\<li class=""big-blue-billet__list_link""\>\<a href=""/monitor.php\?id=(?<cityId>[0-9]+)"">{city}\</a\>", RegexOptions.IgnoreCase);

			var match = pattern.Match(content);

			if (!match.Success)
			{
				throw new Exception("Не найдена страницы для города " + city);
			}

			return match.Groups["cityId"].Value;
		}

		private async Task<List<TemperatureRecord>> GetTemperatures(string cityId, DateTime from, DateTime to)
		{
			if (from > to)
			{
				throw new Exception("Дата начала больше даты окончания");
			}

			var result = new List<TemperatureRecord>();

			int month = 0;
			Dictionary<DateTime, float> monthTemperatures = null;

			for (DateTime date = from; date <= to; date = date.AddDays(1))
			{
				if (monthTemperatures == null || date.Month != month)
				{
					Console.WriteLine($"Загружаем страницу за {date}");

					monthTemperatures = await ReadAtMonth(cityId, from);

					month = date.Month;
				}

				if (monthTemperatures.TryGetValue(date, out float value))
				{
					result.Add(new TemperatureRecord
					{
						Date = date,
						Temperature = value
					});
				}
			}

			return result;
		}


		private async Task<Dictionary<DateTime, float>> ReadAtMonth(string cityId, DateTime date)
		{
			string url = $"{CityListUrl}?id={cityId}&month={date.Month}&year={date.Year}";

			string content = await GetPage(url);

			using var stringReader = new StringReader(content);

			var result = new Dictionary<DateTime, float>();

			while (true)
			{
				string line = stringReader.ReadLine()?.Trim();

				if (line == null)
				{
					break;
				}

				var regex = new Regex(@"\<td\>(?<day>[0-9]+)\</td\>\<td class=""blue-color""\>.*\</td\>\<td class=""green-color"">(?<temperature>[\+\-\.0-9]*)\</td\>\<td class=""red-color""\>.*\</td\>");

				var match = regex.Match(line);

				if (!match.Success)
				{
					continue;
				}
				else
				{
					int day = int.Parse(match.Groups["day"].Value);

					string sTemp = match.Groups["temperature"].Value;

					if (!string.IsNullOrEmpty(sTemp))
					{
						result.Add(new DateTime(date.Year, date.Month, day),
							float.Parse(sTemp, CultureInfo.InvariantCulture));
					}
				}
			}

			return result;
		}
	}
}
