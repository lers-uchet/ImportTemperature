using CommandLine;
using System;
using System.Collections.Generic;
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
			var tempSaver = new LersTemperatureSaver(new Uri(options.Server));

			try
			{
				if (string.IsNullOrEmpty(options.Token) && (string.IsNullOrEmpty(options.Login) || string.IsNullOrEmpty(options.Password)))
				{
					throw new Exception("Необходимо задать токен или логин/пароль.");
				}

				using var importer = new Importers.PogodaIKlimatReader();

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

				// Считываем температуры с сайта
				var temps = await importer.ReadTemperatures(options.SourceCity, territory.TimeZoneOffset, importStart, importEnd);

				await SaveTemperatures(tempSaver, territory, options.MissingOnly, temps);
			}
			catch (Exception exc)
			{
				Console.WriteLine($"Ошибка чтение среднесуточных температур. {exc.Message}");

				Console.WriteLine($"{ Environment.NewLine }Нажмите любую клавишу для выхода...");
				Console.ReadKey();
			}
			finally
			{
				tempSaver.Dispose();
			}
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
		private static Task SaveTemperatures(LersTemperatureSaver tempSaver, Lers.Models.Territory destinationTerritory, bool missingOnly, List<TemperatureRecord> temps)
		{			
			Console.WriteLine($"Среднесуточные температуры сохраняются на сервер'");
			
			return tempSaver.Save(temps, destinationTerritory, missingOnly);			
		}					
	}
}