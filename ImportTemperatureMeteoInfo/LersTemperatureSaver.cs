using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

		public async Task Save(List<TemperatureRecord> records, string territoryName, bool missingOnly)
		{
			// Обнуляем таймауты на выполнение запросов, т.к. сервер может быть занят обработкой данных:
			// "Ошибка чтения среднесуточных температур. Истекло время ожидания запроса "Просмотр справочника температур".
			this.server.DefaultRequestTimeout = 0;

			var territory = await GetTerritory(territoryName);

			if (territory == null)
			{
				throw new Exception($"Территория '{territoryName}' не найдена на сервере.");
			}

			IDictionary<DateTime, Lers.Data.OutdoorTemperatureRecord> existingTemperature = null;

			if (missingOnly)
			{
				// Если импортируются только отсутствующие данные, получаем существующую температуру наружного воздуха.

				existingTemperature = await Task.Run(() => this.server.OutdoorTemperature.Get()
					.Where(x => x.Territory.Id == territory.Id)
					.ToDictionary(x => x.Date));
					
			}

			var outdoorTemp = new List<Lers.Data.OutdoorTemperatureRecord>();

			foreach (var record in records)
			{
				if (!missingOnly || !existingTemperature.ContainsKey(record.Date))
				{
					var temp = new Lers.Data.OutdoorTemperatureRecord(record.Date, territory);
					temp.Value = record.Temperature;
					outdoorTemp.Add(temp);
				}
			}

			if (outdoorTemp.Count <= 0)
			{
				Console.WriteLine("Нет данных для сохранения.");
				return;
			}

			await Task.Run(() => this.server.OutdoorTemperature.Set(outdoorTemp.ToArray()));
		}

		private async Task<Territory> GetTerritory(string territoryName)
		{
			if (string.IsNullOrEmpty(territoryName))
			{
				return this.server.Territories.DefaultTerritory;
			}
			else
			{
				var list = await server.Territories.GetListAsync();

				return list.Where(x => x.Name == territoryName).FirstOrDefault();
			}
		}

		internal void Close()
		{
			this.server.Disconnect(10000);
		}
	}
}
