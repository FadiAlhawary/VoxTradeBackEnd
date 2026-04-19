using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("user_local_themes")]
    public class UserLocalTheme
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("primary_color")]
        public string PrimaryColor { get; set; }

        [Column("secondary_color")]
        public string SecondaryColor { get; set; }

        [Column("background_color")]
        public string BackgroundColor { get; set; }

        [Column("text_color")]
        public string TextColor { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}