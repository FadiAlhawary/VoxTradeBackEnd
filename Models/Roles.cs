namespace VoxTrade.Models
{
    public class Roles
    {
       public int id { get; set; }
       public string role_name_en { get; set; }
       public string role_name_ar { get; set; }
       public string description_en { get; set; }
       public string description_ar { get; set; }
       public bool allow_delete { get; set; }
       public bool allow_create { get; set; }
       public bool allow_edit { get; set; }
       public bool allow_super_view { get; set; }
       public bool lock_all_user { get; set; }
       public DateTime crated_at { get; set; }
       public DateTime? delete_at { get; set; }
       public bool is_deleted { get; set; }

    }
}
