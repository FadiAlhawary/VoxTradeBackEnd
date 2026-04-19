using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace VoxTrade.Models
{
    [Table("ui_themes")]
    public class UIThemes
    {
        public int id { get; set; }
        public int type { get; set; }
        public int purpose { get; set; }
        public JsonElement? style { get; set; }
    }
}
