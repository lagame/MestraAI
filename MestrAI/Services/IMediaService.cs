using Microsoft.AspNetCore.Http;
using RPGSessionManager.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGSessionManager.Services
{
    public interface IMediaService
    {
        Task<MediaInfo> SaveMediaAsync(IFormFile file, string mediaType, int sessionId, string uploadedBy);
        Task<List<MediaInfo>> GetSessionMediaAsync(int sessionId, string? mediaType = null);
        Task<Media?> GetMediaByIdAsync(int mediaId);
        Task<bool> DeleteMediaAsync(int mediaId, string requestedBy);
        Task BroadcastAudioStopAsync(int sessionId, int? mediaId = null);        
        Task BroadcastAudioPlayAsync(int sessionId, int mediaId, float volume, bool loop);
    }

    /// <summary>
    /// DTO de mídia retornado/consumido pelo serviço. 
    /// Inicializações evitam CS8618 sem impactar o schema do banco.
    /// </summary>
    public sealed class MediaInfo
    {
        public int Id { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string Url { get; set; } = string.Empty;

        public int SessionId { get; set; }

        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public string? Description { get; set; }

        public Dictionary<string, object>? Metadata { get; set; } = new();
    }
}
