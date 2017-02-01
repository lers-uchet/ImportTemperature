using System;
using System.Collections.Generic;
using System.Linq;
using Lers.Core;

namespace ImportTemperatureMeteoInfo
{
	class LersTemperatureSaver
	{
		private Lers.LersServer server;
		

		public LersTemperatureSaver()
		{
			this.server = new Lers.LersServer("Meteo Info Import");
		}

		public void Connect(string server, int port, string login, string password)
		{
			this.server.VersionMismatch += (sender, e) => e.Ignore = true;

			var authInfo = new Lers.Networking.BasicAuthenticationInfo(login, Lers.Networking.SecureStringHelper.ConvertToSecureString(password));

			this.server.Connect(server, (ushort)port, authInfo);
		}

		public void Save(List<TemperatureRecord> records, string territoryName)
		{
			var territory = GetTerritory(territoryName);

			if (territory == null)
			{
				throw new Exception($"Территория '{territoryName}' не найдена на сервере.");
			}

			var outdoorTemp = new List<Lers.Data.OutdoorTemperatureRecord>();

			foreach (var record in records)
			{
				var temp = new Lers.Data.OutdoorTemperatureRecord(record.Date, territory);
				temp.Value = record.Temperature;
				outdoorTemp.Add(temp);
			}

			this.server.OutdoorTemperature.Set(outdoorTemp.ToArray());
		}

		private Territory GetTerritory(string territoryName)
		{
			if (string.IsNullOrEmpty(territoryName))
			{
				return this.server.Territories.DefaultTerritory;
			}
			else
			{
				return server.Territories.GetList().Where(x => x.Name == territoryName).FirstOrDefault();
			}
		}

		internal void Close()
		{
			this.server.Disconnect(10000);
		}
	}
}
