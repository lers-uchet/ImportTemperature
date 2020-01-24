using Lers.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImportTemperatureMeteoInfo
{
	public class TerritoryOutdoorTemperature
	{
		public DateTime Date { get; set; }

		public float Value { get; set; }
	}

	class LersTemperatureSaver: IDisposable
	{
		private readonly RestClient _client;



		public LersTemperatureSaver(Uri baseUrl)
		{
			_client = new RestClient("Temperature import", null)
			{
				BaseAddress = baseUrl
			};
		}

		public void Dispose() => _client.Dispose();

		public Task Authenticate(string login, string password)
		{
			var authInfo = new Lers.Networking.BasicAuthenticationInfo(login, Lers.Networking.SecureStringHelper.ConvertToSecureString(password));

			return _client.Authenticate(authInfo, CancellationToken.None);
		}

		public async Task Save(List<TemperatureRecord> records, Lers.Models.Territory territory, bool missingOnly)
		{			
			if (territory == null)
				throw new ArgumentNullException(nameof(territory));

			var route = ApiRouteBuilder.CreateDefault($"Data/Territories/{territory.Id}/Weather").ToString();

			IDictionary<DateTime, TerritoryOutdoorTemperature> existingTemperature = null;

			if (missingOnly)
			{
				// Если импортируются только отсутствующие данные, получаем существующую температуру наружного воздуха.

				existingTemperature = (await _client.GetAsync<TerritoryOutdoorTemperature[]>(route))					
					.ToDictionary(x => x.Date);
			}

			var outdoorTemp = new List<TerritoryOutdoorTemperature>();

			foreach (var record in records)
			{
				if (!missingOnly || !existingTemperature.ContainsKey(record.Date))
				{
					var temp = new TerritoryOutdoorTemperature
					{
						Date = record.Date,
						Value = record.Temperature
					};
					outdoorTemp.Add(temp);
				}
			}

			if (outdoorTemp.Count <= 0)
			{
				Console.WriteLine("Нет данных для сохранения.");
				return;
			}

			await _client.PutAsync(route, outdoorTemp);
		}

		internal void SetToken(string token) => _client.SetToken(token);

		public async Task<Lers.Models.Territory> GetTerritory(string territoryName)
		{
			if (string.IsNullOrEmpty(territoryName))
			{
				var route = ApiRouteBuilder.CreateDefault("Core/Territories/1");
				return await _client.GetAsync<Lers.Models.Territory>(route.ToString());
			}
			else
			{
				var route = ApiRouteBuilder.CreateDefault("Core/Territories");

				var list = await _client.GetAsync<Lers.Models.Territory[]>(route.ToString());

				return list.FirstOrDefault(x => x.Name == territoryName);
			}
		}
	}
}
