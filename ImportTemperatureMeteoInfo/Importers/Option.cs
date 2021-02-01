namespace ImportTemperatureMeteoInfo
{
	/// <summary>
	/// Информация о записях в контроле "select".
	/// </summary>
	class Option
	{
		public readonly string UrlPart;

		public readonly string Name;

		public Option(string url, string name)
		{
			UrlPart = url;
			Name = name;
		}		
	}
}
