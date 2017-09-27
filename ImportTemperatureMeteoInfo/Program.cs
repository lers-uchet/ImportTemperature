using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImportTemperatureMeteoInfo
{
	class TemperatureRecord
	{
		public DateTime Date;
		public float Temperature;
	};

	class Program
	{
		/// <summary>
		/// Адрес архива погоды для Москвы. На этой странице есть перечень всех городов
		/// и меток времени, за которые есть данные.
		/// </summary>
		const string WeatherArchiveHome = "http://meteoinfo.ru/archive-pogoda/russia/moscow";

		/// <summary>
		/// Адрес, по которому будет загружаться погода.
		/// </summary>
		const string HourArchiveHomeUrl = "http://meteoinfo.ru/archive-pogoda";

		static void Main(string[] args)
		{
			Entry(args).Wait();
		}


		static async Task Entry(string[] args)
		{
			try
			{
				var options = new ImportOptions();

				CommandLine.Parser.Default.ParseArgumentsStrict(args, options);

				var archiveHome = DownloadUrl(WeatherArchiveHome);

				// Получаем URL города.
				string cityUrl = GetCityUrl(archiveHome, options.SourceCity);

				Console.WriteLine($"Загрузка данных будет произведена с адреса '{cityUrl}'");

				// Получаем список всех меток времени, по которым есть данные
				var timeStampsRaw = MeteoInfoParser.ParseOptions(archiveHome, "date");

				var timeStamps = ParseTimeStamps(timeStampsRaw);

				// По умолчанию импортируем данные только за предыдущий день.
				// Этот параметр может быть переопределён из командной строки.

				var importStart = DateTime.Now.Date.AddDays(-options.ImportDays);

				// Если пользователь определил начальную дату для импорта, используем её.
				if (!string.IsNullOrEmpty(options.ImportStartDate))
				{
					importStart = DateTime.ParseExact(options.ImportStartDate, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
				}

				// Импорт данных осуществляется до сегодняшенго дня.
				var importEnd = DateTime.Now.Date;

				// Считываем данные по температурам.
				Console.WriteLine($"Чтение температур с {importStart} по {importEnd}");

				var temps = ReadCityTemperatures(cityUrl, options.TerritoryMoscowOffset, timeStamps, importStart, importEnd);

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
			// Грузим домашную страницу, на которой есть перечень городов

			var territories = MeteoInfoParser.ParseOptions(archiveHome, "stations");

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
		private static List<TemperatureRecord> ReadCityTemperatures(string cityUrl, int cityTimeOffset, Dictionary<DateTime, string> timeStamps, DateTime importStart, DateTime importEnd)
		{
			var result = new List<TemperatureRecord>();

			// Проходим по всем доступным меткам времени.

			foreach (var kvp in timeStamps)
			{
				// Все метки времени на этом сайте московские. Чтобы узнать местное время, нужно добавить смещение территории относительно Москвы.
				var localTime = kvp.Key.AddHours(cityTimeOffset);

				if (localTime >= importStart && localTime < importEnd)
				{
					string hourUrl = $"{HourArchiveHomeUrl}{cityUrl}{kvp.Value}";

					string hourContent = DownloadUrl(hourUrl);

					float? value = MeteoInfoParser.ExtractTemperature(hourContent);

					if (value.HasValue)
					{
						result.Add(new TemperatureRecord { Date = localTime, Temperature = value.Value });
					}
					else
					{
						Console.WriteLine($"Не найдена температура за {localTime}");
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
			var tempGroups = from	t in temps
							 group	t.Temperature
							 by		t.Date.Date
							 into	g
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


		private static string DownloadUrl(string url)
		{
			using (var webClient = new System.Net.WebClient())
			{
				Console.WriteLine($"Загрузка страницы '{url}'");

				var rawString = webClient.DownloadString(url);

				var rawBytes = Encoding.Default.GetBytes(rawString);

				return Encoding.UTF8.GetString(rawBytes);
			}
		}

		private static Dictionary<DateTime, string> ParseTimeStamps(Dictionary<string, string> raw)
		{
			var result = new Dictionary<DateTime, string>();

			foreach (var kvp in raw)
			{
				var dateTime = DateTime.ParseExact(kvp.Key, "dd-MM-yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

				result[dateTime] = kvp.Value;
			}

			return result;
		}
	}
}
