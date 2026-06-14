using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Product category for organizing products hierarchically.
    /// Supports parent-child relationships for nested categories.
    /// </summary>
    public class Category : AggregateRoot
    {
        public string Name { get; set; } = string.Empty;
        public Guid? ParentCategoryId { get; set; }
        public int SortOrder { get; set; }

        public static Category Create(string name, Guid? parentCategoryId = null)
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                ParentCategoryId = parentCategoryId,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow
            };

            return category;
        }
    }
}
