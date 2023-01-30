using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImportTemperature.Importers;

/// <summary>
/// Обеспечивает чтение данных с gismeteo.
/// </summary>
class GisMeteoReader : ITempertatureReader
{
	private readonly HttpClient _httpClient = new HttpClient
	{
		BaseAddress = new Uri("https://gismeteo.ru")
	};

	public void Dispose() => _httpClient.Dispose();


	public Task<List<TemperatureRecord>> ReadTemperatures(string city, int cityUtcOffset, DateTime from, DateTime to)
	{
		city = city.ToUpperInvariant();

		if (!long.TryParse(city, out long cityCode))
		{
			// Если не задан код города, ищем его по имени в справочнике.

			var cityCodes = GetCityCodes();

			if (!cityCodes.Contains(city))
			{
				throw new ImportException($"Не удалось найти код города {city}.");
			}

			if (cityCodes[city].Count() > 1)
			{
				throw new ImportException($"Для города {city} указано несколько кодов. Пожалуйста, используйте код, используемый на сайте gismeteo.ru");
			}

			cityCode = cityCodes[city].First();
		}

		string url = $"/diary/{cityCode}";

		return LoadCityTemperatures(url, from, to);
	}


    /// <summary>
    /// Записывает показания температуры в таблицу.
    /// </summary>
    /// <param name="importUrl">
    /// Адрес страницы с таблицами среднесуточных температур.
    /// </param>
    /// <returns>
    /// Возвращает таблицу со среднесуточными температурами за указанный период.
    /// </returns>
    private async Task<List<TemperatureRecord>> LoadCityTemperatures(string importUrl, DateTime from, DateTime to)
	{
		var tempRecord = new List<TemperatureRecord>();

		var monthContent = new Dictionary<DateTime, string>();

		for (DateTime date = from; date < to; date = date.AddDays(1))
		{
			var readMonth = new DateTime(date.Year, date.Month, 1);

            if (!monthContent.TryGetValue(readMonth, out var htmlMonthPage))
			{
                Console.WriteLine($"Загружаем с сайта температуры за {readMonth:d}");

                // Получаем текст страницы по указанной дате.
                htmlMonthPage = await GetPageContent($"{importUrl}/{date.Year}/{date.Month}");

				monthContent[readMonth] = htmlMonthPage;
            }

			// Считываем температуру со страницы.
			float? temperature = FindTemperatureOnPage(date, htmlMonthPage);

			if (temperature != null)
			{
				Console.WriteLine(string.Format("Считана температура за {0}", date.ToString("d")));

				// Записываем температуру в таблицу.

				tempRecord.Add(new TemperatureRecord
				{
					Date = date,
					Temperature = temperature.Value
				});
			}
		}

		return tempRecord;
	}


	/// <summary>
	/// Ищет на странице с таблицами температур среднесуточную
	/// по указанной дате.
	/// </summary>
	/// <param name="date">
	/// Дата, по которой ищется температура на странице.
	/// </param>
	/// <param name="htmlMonthPage">
	/// Html - код странице на которой ищется температура.
	/// </param>
	/// <returns>
	/// Возвращает значение среднесуточной температуры по указанной дате.
	/// </returns>
	private static float? FindTemperatureOnPage(DateTime date, string htmlMonthPage)
	{
		// Регулярное выражение для нахождения среднесуточной температуры на странице.
		//  <td class=first>1</td>
		//	<td class='first_in_group positive'>+26</td>
		//	<td>751</td>
		//	<td><img src=http://st6.gisstatic.ru/static/diary/img/sunc.png class='label_icon label_small screen_icon' />
		//  <img src=http://st7.gisstatic.ru/static/diary/img/sunc-bw.gif class='label_icon label_small print_icon' /></td>
		//	<td></td>
		//	<td><span><img src="http://st8.gisstatic.ru/static/diary/img/w7.gif" class='screen_icon' />
		//	<img src="http://st4.gisstatic.ru/static/diary/img/w7-bw.gif" class='print_icon' /><br />СЗ 2м/с</span></td>
		//	<td class='first_in_group positive'>+27</td>

		Regex regex = new Regex($@"<td\sclass=first>{date.Day}</td>[\w\s]+<td\sclass=.+>(.+)</td>\s+<td>.+</td>\s+<td>.+\s.+\s.+\s+<td\sclass=.+>(.+)</td>");

		// Ищем совпадения на странице.
		Match match = regex.Match(htmlMonthPage);

		if (!match.Success)
		{
			throw new ImportException(string.Format("Температура за дату: {0} не найдена.", date.ToString("d")));
		}

		try
		{
			return (ToSingle(match.Groups[1].Value) + ToSingle(match.Groups[2].Value)) / 2;
		}
		catch (FormatException exc)
		{
			Console.Error.WriteLine($"Ошибка разбора температуры за {date}. {exc.Message}");
			return null;
		}
	}


	/// <summary>
	/// Получает текст html - страницы в виде строки.
	/// </summary>
	/// <param name="link">
	/// Адрес, по которому нужно получить html - страницу.
	/// </param>
	/// <returns>
	/// Возвращает текст html страницы в виде строки.
	/// </returns>
	private async Task<string> GetPageContent(string link)
	{
		try
		{
			var response = await _httpClient.GetAsync(link);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsStringAsync();
		}
		catch (Exception exc)
		{
			throw new ImportException(String.Format("Ошибка загрузки веб страницы {0} ", link) + exc.Message);
		}
	}

	
	private static ILookup<string, long> GetCityCodes()
	{
		var result = new List<GisMeteoCityCode>();

		using var stringReader = new StringReader(Properties.Resources.Cities);

		var pattern = new Regex(@"(?<cityName>.+)\s(?<cityCode>[0-9]+)");

		while (true)
		{
			var line = stringReader.ReadLine();

			if (line == null)
			{
				break;
			}

			var match = pattern.Match(line);

			if (match.Success)
			{					
				result.Add(new GisMeteoCityCode(
					long.Parse(match.Groups["cityCode"].Value),
					match.Groups["cityName"].Value.Trim()));
			}
		}

		return result.ToLookup(x => x.Name, y => y.Code);
	}

    private static float ToSingle(string value)
    {
        string decimalSeparator = System.Threading.Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        return Convert.ToSingle(
            value.Replace(".", decimalSeparator)
                .Replace(",", decimalSeparator));
    }
}
