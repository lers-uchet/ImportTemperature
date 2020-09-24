using CommandLine;
using System;
using System.Threading.Tasks;

namespace ImportTemperatureMeteoInfo
{
	internal class Program
	{
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
			using var tempSaver = new LersTemperatureSaver(new Uri(options.Server));

			try
			{
				if (string.IsNullOrEmpty(options.Token) && (string.IsNullOrEmpty(options.Login) || string.IsNullOrEmpty(options.Password)))
				{
					throw new Exception("Необходимо задать токен или логин/пароль.");
				}

				using var importer = CreateReader(options.Source);

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

				// Подключаемся к серверу

				if (string.IsNullOrEmpty(options.Token))
				{
					await tempSaver.Authenticate(options.Login, options.Password);
				}
				else
				{
					tempSaver.SetToken(options.Token);
				}

				// Определяем территорию для импорта.

				var territory = await tempSaver.GetTerritory(options.DestinationTerritory);

				if (territory == null)
				{
					throw new Exception($"Территория '{territory}' не найдена на сервере.");
				}

				Console.WriteLine("Чтение среднесуточных температур с сайта.");

				// Считываем температуры с сайта.

				var temps = await importer.ReadTemperatures(options.SourceCity, territory.TimeZoneOffset, importStart, importEnd);

				Console.WriteLine($"Среднесуточные температуры сохраняются на сервер'");

				await tempSaver.Save(temps, territory, options.MissingOnly);
			}
			catch (Exception exc)
			{
				Console.WriteLine($"Ошибка чтение среднесуточных температур. {exc.Message}");

				Console.WriteLine($"{ Environment.NewLine }Нажмите любую клавишу для выхода...");
				Console.ReadKey();
			}
		}


		/// <summary>
		/// Создаёт объект для чтения температур с указанного источника.
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		private static ITempertatureReader CreateReader(ImportSource source)
		{
			return source switch
			{
				ImportSource.MeteoInfo => new Importers.MemeoInfoReader(),
				ImportSource.PogodaIKlimat => new Importers.PogodaIKlimatReader(),
				ImportSource.GisMeteo => new Importers.GisMeteoReader(),
				_ => throw new ArgumentOutOfRangeException(nameof(source))
			};
		}
	}
}