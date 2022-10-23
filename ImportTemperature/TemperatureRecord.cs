using System;

namespace ImportTemperatureMeteoInfo
{
	/// <summary>
	/// Запись со среднесуточной температурой.
	/// </summary>
	internal class TemperatureRecord
	{
		/// <summary>
		/// Дата записи.
		/// </summary>
		public DateTime Date { get; set; }

		/// <summary>
		/// Температура.
		/// </summary>
		public float Temperature { get; set; }
	};
}
