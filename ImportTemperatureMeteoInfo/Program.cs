using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImportTemperatureMeteoInfo
{
	internal class TemperatureRecord
	{
		public DateTime Date;
		public float Temperature;
	};

	internal class Program
	{
		/// <summary>
		/// Адрес архива погоды для Москвы. На этой странице есть перечень всех городов
		/// и меток времени, за которые есть данные.
		/// </summary>
		private const string WeatherArchiveHome = "https://meteoinfo.ru/archive-pogoda/russia/moscow";

		/// <summary>
		/// Адрес, по которому будет выполнятся POST запросы, для получения идентификаторов меток времени и данные по температуре.
		/// </summary>
		private const string HourArchiveHomeUrl = "https://meteoinfo.ru/hmc-output/observ/obs_arch.php";

		private static void Main(string[] args)
		{
			Parser.Default.ParseArguments<ImportOptions>(args)
				.WithParsed(options =>
				{
					Entry(options).Wait();
				});
		}

		private static async Task Entry(ImportOptions options)
		{
			try
			{
				string cityId = await GetCityId(WeatherArchiveHome, options.SourceCity);

				Console.WriteLine($"Идентификатор города '{cityId}'");

				// Получаем список всех меток времени, по которым есть данные
				var timeStampsRaw = await GetTimeStamps(cityId);

				var timeStamps = MeteoInfoParser.ParseTimeStamps(timeStampsRaw);

				// По умолчанию импортируем данные только за предыдущий день.
				// Этот параметр может быть переопределён из командной строки.

				var importStart = DateTime.Now.Date.AddDays(-options.ImportDays);

				// Если пользователь определил начальную дату для импорта, используем её.
				if (!string.IsNullOrEmpty(options.ImportStartDate))
				{
					importStart = DateTime.ParseExact(options.ImportStartDate, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
				}

				// Импорт данных осуществляется до сегодняшнего дня.
				var importEnd = DateTime.Now.Date;

				// Считываем данные по температурам.
				Console.WriteLine($"Чтение температур с {importStart} по {importEnd}");

				var temps = await ReadCityTemperatures(cityId, options.TerritoryUtcOffset, timeStamps, importStart, importEnd);

				await SaveTemperatures(options.Server, (ushort)options.ServerPort, options.Login, options.Password, options.DestinationTerritory, options.MissingOnly, temps);
			}
			catch (Exception exc)
			{
				Console.WriteLine($"Ошибка чтение среднесуточных температур. {exc.Message}");

				Console.WriteLine($"{ Environment.NewLine }Нажмите любую клавишу для выхода...");
				Console.ReadKey();
			}
		}

		/// <summary>
		/// Возвращает URL для загрузки температур по указанному городу.
		/// </summary>
		/// <param name="archiveHome"></param>
		/// <param name="city"></param>
		/// <returns></returns>
		private static string GetCityUrl(string archiveHome, string city)
		{
			// Грузим домашнюю страницу, на которой есть перечень городов

			var territories = MeteoInfoParser.ParseOptions(archiveHome, "id_city");

			var cityInfo = territories.Keys.Where(x => x.StartsWith(city)).FirstOrDefault();

			if (cityInfo == null)
			{
				throw new Exception($"На сайте не найден город '{city}'");
			}

			return territories[cityInfo];
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

							Console.WriteLine($"Получена температура за {localTime}");
						}
						else
						{
							Console.WriteLine($"Не найдена температура за {localTime}");
						}
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Сохраняет температуры на сервер ЛЭРС УЧЁТ.
		/// </summary>
		/// <param name="server"></param>
		/// <param name="port"></param>
		/// <param name="login"></param>
		/// <param name="password"></param>
		/// <param name="destinationTerritory"></param>
		/// <param name="missingOnly">Импортировать только данные, которых ещё нет в справочнике.</param>
		/// <param name="temps"></param>
		private static async Task SaveTemperatures(string server, ushort port, string login, string password, string destinationTerritory, bool missingOnly, List<TemperatureRecord> temps)
		{
			var tempGroups = from t in temps
							 group t.Temperature
							 by t.Date.Date
							 into g
							 select g;

			var averageTemperatures = new List<TemperatureRecord>();

			foreach (var tempGroup in tempGroups)
			{
				float avg = tempGroup.Average(x => x);

				Console.WriteLine($"Средняя температура за {tempGroup.Key} = {avg} °C");

				averageTemperatures.Add(new TemperatureRecord { Date = tempGroup.Key, Temperature = avg });
			}

			Console.WriteLine($"Среднесуточные температуры сохраняются на сервер '{server}:{port}'");
			var tempSaver = new LersTemperatureSaver();
			tempSaver.Connect(server, port, login, password);

			await tempSaver.Save(averageTemperatures, destinationTerritory, missingOnly);

			tempSaver.Close();
		}

		private static async Task<string> GetCityId(string url, string cityName)
		{
			var dataArray = await Post(HourArchiveHomeUrl, "0", "0");

			var cityList = MeteoInfoParser.GetOptions(dataArray[4].ToString().Replace("[", string.Empty).Replace("]", string.Empty).Trim()).ToList();

			var cityInfo = cityList.Find(x => x.Name.Contains(cityName));

			if (cityInfo == null)
			{
				throw new Exception($"На сайте не найден город '{cityName}'");
			}

			return cityInfo.UrlPart;
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

		private static async Task<string> GetTimeStamps(string cityId)
		{
			JArray dataArray = await Post(HourArchiveHomeUrl, cityId, "0");

			return dataArray[2].ToString();
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
	}
}