using System;

namespace ImportTemperature.Importers
{
	class ImportException : Exception
	{
		public ImportException(string message) : base(message)
		{
		}
	}
}
