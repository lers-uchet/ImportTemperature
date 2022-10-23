using CommandLine;
using ImportTemperature.Importers;
using ImportTemperatureMeteoInfo;
using ImportTemperatureMeteoInfo.Importers;
using System;
using System.Threading.Tasks;

var parser = new Parser(args => args.CaseInsensitiveEnumValues = true);
		
parser.ParseArguments<ImportOptions>(args)
	.WithParsed(options =>
	{
		Entry(options).Wait();
	});

static async Task Entry(ImportOptions options)
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
			throw new Exception($"Территория '{options.DestinationTerritory}' не найдена на сервере.");
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
static ITempertatureReader CreateReader(ImportSource source)
=> source switch
{
	ImportSource.MeteoInfo => new MemeoInfoReader(),
	ImportSource.PogodaIKlimat => new PogodaIKlimatReader(),
	ImportSource.GisMeteo => new GisMeteoReader(),
	_ => throw new ArgumentOutOfRangeException(nameof(source))
};
