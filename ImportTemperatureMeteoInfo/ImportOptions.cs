using CommandLine;

namespace ImportTemperatureMeteoInfo
{
	/// <summary>
	/// Параметры программы
	/// </summary>
	class ImportOptions
	{
		/// <summary>
		/// Город, для которого импортируется температура.
		/// </summary>
		[Option(longName:"incity", Required = true)]
		public string SourceCity { get; set; }

		/// <summary>
		/// Смещение города относительно московского времени.
		/// </summary>
		[Option (longName:"mscoffset", Required = true, HelpText = "Смещение часового пояса выбранной территории относительно Московского времени (в часах)")]
		public int TerritoryMoscowOffset { get; set; }

		/// <summary>
		/// Адрес сервера ЛЭРС УЧЁТ, на который импортируется температура.
		/// </summary>
		[Option(longName:"server", Required = true)]
		public string Server { get; set; }

		/// <summary>
		/// Порт сервера ЛЭРС УЧЁТ, на который импортируется температура.
		/// </summary>
		[Option(longName: "serverPort", Required = false, DefaultValue = 10000 )]
		public int ServerPort { get; set; }

		/// <summary>
		/// Логин на сервере.
		/// </summary>
		[Option(longName: "login", Required = true)]
		public string Login { get; set; }

		/// <summary>
		/// Пароль на сервере.
		/// </summary>
		[Option(longName: "password", Required = true)]
		public string Password { get; set; }

		/// <summary>
		/// Наименование территории, для которой сохраняется Тнв.
		/// Если пустая - Тнв сохраняется для текущей территории.
		/// </summary>
		[Option(longName:"destTerritory", DefaultValue = "", Required = false)]
		public string DestinationTerritory { get; set; }

		/// <summary>
		/// Дата, начиная с которой нужно импотрировать температуру.
		/// Если не передана, импортируются данные за вчерашний день.
		/// </summary>
		[Option(longName: "importStart", DefaultValue = "", Required = false)]
		public string ImportStartDate { get; set; }

		/// <summary>
		/// Количество дней, за которое проводится импорт данных.
		/// </summary>
		[Option(longName: "importDays", DefaultValue = 1, Required = false)]
		public int ImportDays { get; set; }

		/// <summary>
		/// Флаг указывает что нужно импортировать только температуры, которых ещё нет в справочнике.
		/// </summary>
		[Option(longName: "missingOnly", DefaultValue = false, Required = false,
				HelpText = "Импортировать только температуры, которых ещё нет в справочнике. Существующие температуры не перезаписываются.")]
		public bool MissingOnly { get; set; }
	}
}
