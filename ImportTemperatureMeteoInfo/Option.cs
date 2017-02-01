namespace ImportTemperatureMeteoInfo
{
	/// <summary>
	/// Информация о записях в контроле "select".
	/// </summary>
	class Option
	{
		public Option(string url, string name)
		{
			this.UrlPart = url;
			this.Name = name;
		}

		public readonly string UrlPart;

		public readonly string Name;
	}
}
