using System;

namespace ImportTemperatureMeteoInfo.Importers
{
	class ImportException : Exception
	{
		public ImportException(string message) : base(message)
		{
		}
	}
}
