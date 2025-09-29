using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models
{
  public class NpcInteraction
  {
        [Key]
        public int Id { get; set; } // Chave primária da tabela

        public int SessionId { get; set; } // ID da sessão onde ocorreu

        public int CharacterId { get; set; } // ID do NPC que interagiu

        public double ResponseTimeMs { get; set; } // Tempo de resposta que queremos medir

        public DateTime Timestamp { get; set; } // Data e hora da interação
        public DateTime? CreatedAtUtc { get; set; }
    }
}
