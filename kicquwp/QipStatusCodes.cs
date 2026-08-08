using System.Collections.Generic;

namespace kicquwp
{
    /// <summary>
    /// Таблица соответствия GUID capability → числовой код QIP-статуса.
    /// Используется в HandleUserOnlineAsync/HandleUserStatusChangeAsync,
    /// чтобы заменить базовый OSCAR-статус (из TLV 0x0006) на QIP-расширенный,
    /// когда у контакта активен соответствующий QIP-режим.
    ///
    /// Источник: код Jasmine IM (qip_statuses.fromGuid).
    /// </summary>
    public static class QipStatusCodes
    {
        // GUID (hex, верхний регистр) → числовой код статуса (как хранится в TLV 0x0006)
        private static readonly Dictionary<string, uint> GuidToStatus = new Dictionary<string, uint>
        {
            // Free for Chat — базовый OSCAR 0x0020, оставляем как есть
            { "B7074378F50C777797775778502D0575", 0x0020 }, // Free for Chat

            // QIP-расширенные
            { "B7074378F50C777797775778502D0578", 0x2001 }, // Lunch (обед)
            { "B7074378F50C777797775778502D0579", 0x3000 }, // Evil / Angry
            { "B7074378F50C777797775778502D0570", 0x4000 }, // Depression
            { "B7074378F50C777797775778502D0576", 0x5000 }, // At Home
            { "B7074378F50C777797775778502D0577", 0x6000 }, // At Work
        };

        /// <summary>
        /// Возвращает числовой код статуса QIP по его GUID capability,
        /// или null, если GUID не является QIP-статусом.
        /// </summary>
        public static uint? FromGuid(string guidHexUpper)
        {
            if (string.IsNullOrEmpty(guidHexUpper)) return null;
            uint val;
            if (GuidToStatus.TryGetValue(guidHexUpper, out val))
                return val;
            return null;
        }

        /// <summary>
        /// Человекочитаемое имя QIP-статуса (русский).
        /// </summary>
        public static string GetName(uint status)
        {
            switch (status)
            {
                case 0x0020: return "Свободен для чата";
                case 0x2001: return "Обедает";
                case 0x3000: return "Злой";
                case 0x4000: return "Депрессия";
                case 0x5000: return "Дома";
                case 0x6000: return "На работе";
                default: return null;
            }
        }
    }
}
