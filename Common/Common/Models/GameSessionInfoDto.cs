using System.Text.Json.Serialization;

namespace Common.Models
{
    public class GameSessionInfoDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int PlayersCount { get; set; }
        public int MaxPlayers { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatorName { get; set; } = string.Empty;

        [JsonIgnore]
        public bool CanJoin => PlayersCount < MaxPlayers && Status == "Ожидание игроков"; // Изменено на русский

        // Метод для удобного отображения
        [JsonIgnore]
        public string DisplayInfo => $"{Name} ({PlayersCount}/{MaxPlayers}) - {Status} - Создатель: {CreatorName}";

        // Метод для краткого отображения в списке
        [JsonIgnore]
        public string ShortInfo => $"{Name} 👤{PlayersCount}/{MaxPlayers}";
    }
}