namespace ImportTemperature.Importers
{
	/// <summary>
	/// Код города в системе GisMeteo.
	/// </summary>
	class GisMeteoCityCode
	{
		public long Code { get; }

		public string Name { get; }

		public GisMeteoCityCode(long code, string name)
		{
			Code = code;
			Name = name.ToUpperInvariant();
		}
	}
}
