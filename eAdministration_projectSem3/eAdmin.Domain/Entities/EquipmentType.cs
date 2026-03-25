using System.Collections.Generic;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Loại thiết bị: Computer, Printer, LCD, AC, DigitalBoard
    /// </summary>
    public class EquipmentType
    {
        public int EquipmentTypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
    }
}
