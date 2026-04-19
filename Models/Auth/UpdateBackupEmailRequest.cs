namespace VoxTrade.Models.Auth
{
    public class UpdateBackupEmailRequest
    {
        public int UserId { get; set; }
        public string BackupEmail { get; set; } = string.Empty;
    }
}
