using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImportTemperature
{
	/// <summary>
	/// Предоставляет методы для чтения температуры с сайта.
	/// </summary>
	interface ITempertatureReader : IDisposable
	{
		/// <summary>
		/// Возвращает температуры с сайта.
		/// </summary>
		/// <param name="city"></param>
		/// <param name="cityUtcOffset"></param>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns></returns>
		Task<List<TemperatureRecord>> ReadTemperatures(string city, int cityUtcOffset, DateTime from, DateTime to);
	}
}
