using System.Collections.Generic;
using System.Text;

namespace kicquwp
{
    /// <summary>
    /// Таблица соответствия GUID capability → идентификатор X-Status (Xtraz).
    /// Нумерация X-Status идёт с 0 (согласно QIP 2010 / ICQ 8).
    /// В коде OscarProtocol мы только ПАРСИМ — сами иконки X-Status
    /// пользователь дозальёт позже. Сейчас сохраняем идентификатор
    /// и локализованное название, чтобы UI мог показать "X-Status #14 — Сон".
    /// </summary>
    public static class XStatusCodes
    {
        public class XStatusInfo
        {
            public int Id;               // 0..36
            public string Title;          // "Сон"
            public string Description;    // "Я сплю"
            public string IconHint;       // ключ ресурса иконки, если есть
        }

        // GUID (hex) → Id X-Status
        private static readonly Dictionary<string, int> GuidToId = new Dictionary<string, int>
        {
            // Транспорт
            { "63627337A03F49FF80E5F709CDE0A4EE",  1 }, // Шоппинг
            { "5A581EA1E580430CA06F612298B7E4C7",  2 }, // Утка
            { "83C9B78E77E74378B2C5FB6CFCC35BEC",  3 }, // Устал
            { "E601E41C33734BD1BC06811D6C323D81",  4 }, // Вечеринка
            { "E601E41C33734BD1BC06811D6C323D82",  5 }, // Пиво
            { "E601E41C33734BD1BC06811D6C323D83",  6 }, // Размышляю
            { "E601E41C33734BD1BC06811D6C323D84",  7 }, // Смотрю ТВ
            { "E601E41C33734BD1BC06811D6C323D85",  8 }, // Друзья
            { "E601E41C33734BD1BC06811D6C323D86",  9 }, // Кофе
            { "E601E41C33734BD1BC06811D6C323D87", 10 }, // Еда
            { "E601E41C33734BD1BC06811D6C323D88", 11 }, // Бизнес
            { "E601E41C33734BD1BC06811D6C323D89", 12 }, // Игры
            { "E601E41C33734BD1BC06811D6C323D8A", 13 }, // Путешествую
            { "E601E41C33734BD1BC06811D6C323D8B", 14 }, // Сон
            { "E601E41C33734BD1BC06811D6C323D8C", 15 }, // Учеба
            { "E601E41C33734BD1BC06811D6C323D8D", 16 }, // Спорт
            { "E601E41C33734BD1BC06811D6C323D8E", 17 }, // Встреча
            { "E601E41C33734BD1BC06811D6C323D8F", 18 }, // Дома
            { "E601E41C33734BD1BC06811D6C323D90", 19 }, // Курение
            { "E601E41C33734BD1BC06811D6C323D91", 20 }, // Музыка
            { "E601E41C33734BD1BC06811D6C323D92", 21 }, // В машине
            { "E601E41C33734BD1BC06811D6C323D93", 22 }, // Телефон
            { "E601E41C33734BD1BC06811D6C323D94", 23 }, // Пишу
            { "E601E41C33734BD1BC06811D6C323D95", 24 }, // ПК
            { "E601E41C33734BD1BC06811D6C323D96", 25 }, // Командировка
            { "E601E41C33734BD1BC06811D6C323D97", 26 }, // В отпуске
            { "E601E41C33734BD1BC06811D6C323D98", 27 }, // Заболел
            { "E601E41C33734BD1BC06811D6C323D99", 28 }, // Больше 18
            { "E601E41C33734BD1BC06811D6C323D9A", 29 }, // Ищу девушку/парня
            { "E601E41C33734BD1BC06811D6C323D9B", 30 }, // Влюблён
            { "E601E41C33734BD1BC06811D6C323D9C", 31 }, // Купаюсь
            { "E601E41C33734BD1BC06811D6C323D9D", 32 }, // Секс
            { "E601E41C33734BD1BC06811D6C323D9E", 33 }, // Голосует
            { "E601E41C33734BD1BC06811D6C323D9F", 34 }, // В туалете
            { "E601E41C33734BD1BC06811D6C323DA0", 35 }, // Работаю
            { "E601E41C33734BD1BC06811D6C323DA1", 36 }, // Готовлю
        };

        // Локализованные названия (русский) и описания X-Status.
        // Источник: QIP 2010 / ICQ 8 client strings.
        private static readonly Dictionary<int, XStatusInfo> IdToInfo = new Dictionary<int, XStatusInfo>
        {
            {  0, new XStatusInfo { Id=0,  Title="Нет",            Description="X-Status не установлен" } },
            {  1, new XStatusInfo { Id=1,  Title="Шоппинг",         Description="Занимаюсь шоппингом" } },
            {  2, new XStatusInfo { Id=2,  Title="Утка",            Description="Я — утка" } },
            {  3, new XStatusInfo { Id=3,  Title="Устал",           Description="Хочу спать" } },
            {  4, new XStatusInfo { Id=4,  Title="Вечеринка",       Description="Я на вечеринке" } },
            {  5, new XStatusInfo { Id=5,  Title="Пиво",            Description="Пью пиво" } },
            {  6, new XStatusInfo { Id=6,  Title="Размышляю",       Description="Думаю о вечном" } },
            {  7, new XStatusInfo { Id=7,  Title="Смотрю ТВ",       Description="Смотрю телевизор" } },
            {  8, new XStatusInfo { Id=8,  Title="С друзьями",      Description="Я с друзьями" } },
            {  9, new XStatusInfo { Id=9,  Title="Кофе",            Description="Пью кофе" } },
            { 10, new XStatusInfo { Id=10, Title="Еда",             Description="Кушаю" } },
            { 11, new XStatusInfo { Id=11, Title="Бизнес",          Description="На деловой встрече" } },
            { 12, new XStatusInfo { Id=12, Title="Игры",            Description="Играю в игры" } },
            { 13, new XStatusInfo { Id=13, Title="Путешествую",     Description="В путешествии" } },
            { 14, new XStatusInfo { Id=14, Title="Сон",             Description="Сплю" } },
            { 15, new XStatusInfo { Id=15, Title="Учёба",           Description="Учусь" } },
            { 16, new XStatusInfo { Id=16, Title="Спорт",           Description="Занимаюсь спортом" } },
            { 17, new XStatusInfo { Id=17, Title="Встреча",         Description="На встрече" } },
            { 18, new XStatusInfo { Id=18, Title="Дома",            Description="Я дома" } },
            { 19, new XStatusInfo { Id=19, Title="Курю",            Description="Курю" } },
            { 20, new XStatusInfo { Id=20, Title="Музыка",          Description="Слушаю музыку" } },
            { 21, new XStatusInfo { Id=21, Title="В машине",        Description="За рулём" } },
            { 22, new XStatusInfo { Id=22, Title="Телефон",         Description="Разговариваю по телефону" } },
            { 23, new XStatusInfo { Id=23, Title="Пишу",            Description="Пишу" } },
            { 24, new XStatusInfo { Id=24, Title="ПК",              Description="За компьютером" } },
            { 25, new XStatusInfo { Id=25, Title="Командировка",    Description="В командировке" } },
            { 26, new XStatusInfo { Id=26, Title="Отпуск",          Description="В отпуске" } },
            { 27, new XStatusInfo { Id=27, Title="Болею",           Description="Заболел" } },
            { 28, new XStatusInfo { Id=28, Title="Больше 18",       Description="Только для взрослых" } },
            { 29, new XStatusInfo { Id=29, Title="Ищу любовь",      Description="Ищу девушку / парня" } },
            { 30, new XStatusInfo { Id=30, Title="Влюблён",         Description="Влюблён" } },
            { 31, new XStatusInfo { Id=31, Title="Купаюсь",         Description="Плавать!" } },
            { 32, new XStatusInfo { Id=32, Title="Секс",            Description="Секс" } },
            { 33, new XStatusInfo { Id=33, Title="Голосую",         Description="Голосую" } },
            { 34, new XStatusInfo { Id=34, Title="В туалете",       Description="Отлучусь" } },
            { 35, new XStatusInfo { Id=35, Title="Работа",          Description="Работаю" } },
            { 36, new XStatusInfo { Id=36, Title="Готовлю",         Description="Готовлю еду" } },
        };

        /// <summary>
        /// Возвращает числовой ID X-Status (0..36) по GUID capability,
        /// или null, если GUID не является X-Status.
        /// </summary>
        public static int? FromGuid(string guidHexUpper)
        {
            if (string.IsNullOrEmpty(guidHexUpper)) return null;
            int id;
            if (GuidToId.TryGetValue(guidHexUpper, out id))
                return id;
            return null;
        }

        /// <summary>
        /// Возвращает XStatusInfo по ID (0..36) или null.
        /// </summary>
        public static XStatusInfo GetInfo(int id)
        {
            XStatusInfo info;
            if (IdToInfo.TryGetValue(id, out info))
                return info;
            return null;
        }
    }
}
