using System;
using System.Globalization;
using System.Windows.Data;

namespace ClientWPF.Converters
{
    public class IconIdToSymbolConverter : IValueConverter
    {
        private static readonly string[] Symbols = new[]
        {
            "💣", // 0 - Взрывной котенок
            "🛡️", // 1 - Обезвредить
            "🙅", // 2 - Нет
            "⚔️", // 3 - Атаковать
            "⏭️", // 4 - Пропустить
            "🎭", // 5 - Одолжение
            "🔀", // 6 - Перемешать
            "🔮", // 7 - Заглянуть в будущее
            "🌈", // 8 - Радужный кот
            "🧔", // 9 - Котобородач
            "🥔", // 10 - Картошка кот
            "🍉", // 11 - Арбузный кот
            "🌮", // 12 - Такокот
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte iconId && iconId < Symbols.Length)
                return Symbols[iconId];
            return "🃏";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}