using CommandLine;

namespace ImportTemperature
{
	/// <summary>
	/// Параметры программы
	/// </summary>
	class ImportOptions
	{
		/// <summary>
		/// Город, для которого импортируется температура.
		/// </summary>
		[Option(longName: "incity", Required = true)]
		public string SourceCity { get; set; }

		/// <summary>
		/// Адрес сервера ЛЭРС УЧЁТ, на который импортируется температура.
		/// </summary>
		[Option(longName: "server", Required = true)]
		public string Server { get; set; }

		/// <summary>
		/// Логин на сервере.
		/// </summary>
		[Option(longName: "login", Required = false)]
		public string Login { get; set; }

		/// <summary>
		/// Пароль на сервере.
		/// </summary>
		[Option(longName: "password", Required = false)]
		public string Password { get; set; }

		/// <summary>
		/// Токен для авторизации на сервере.
		/// </summary>
		[Option(longName: "token", Required = false)]
		public string Token { get; set; }

		/// <summary>
		/// Наименование территории, для которой сохраняется Тнв.
		/// Если пустая - Тнв сохраняется для текущей территории.
		/// </summary>
		[Option(longName: "destTerritory", Default = "", Required = false)]
		public string DestinationTerritory { get; set; }

		/// <summary>
		/// Дата, начиная с которой нужно импортировать температуру.
		/// Если не передана, импортируются данные за вчерашний день.
		/// </summary>
		[Option(longName: "importStart", Default = "", Required = false)]
		public string ImportStartDate { get; set; }

		/// <summary>
		/// Количество дней, за которое проводится импорт данных.
		/// </summary>
		[Option(longName: "importDays", Default = 1, Required = false)]
		public int ImportDays { get; set; }

		/// <summary>
		/// Флаг указывает что нужно импортировать только температуры, которых ещё нет в справочнике.
		/// </summary>
		[Option(longName: "missingOnly", Default = false, Required = false,
				HelpText = "Импортировать только температуры, которых ещё нет в справочнике. Существующие температуры не перезаписываются.")]
		public bool MissingOnly { get; set; }

		/// <summary>
		/// Сайт, с которого производится импорт температур.
		/// </summary>
		[Option(longName: "source",
				Default = ImportSource.MeteoInfo,
				Required = false,
				HelpText = "Сайт, с которого производится импорт температур.")]
		public ImportSource Source { get; set; }
	}
}