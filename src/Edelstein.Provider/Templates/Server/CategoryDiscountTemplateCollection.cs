using System.Collections.Generic;
using System.Linq;

namespace Edelstein.Provider.Templates.Server
{
    public class CategoryDiscountTemplateCollection : AbstractEagerTemplateCollection
    {
        public override TemplateCollectionType Type => TemplateCollectionType.CashPackage;

        public CategoryDiscountTemplateCollection(IDataDirectoryCollection collection) : base(collection)
        {
        }

        protected override IEnumerable<ITemplate> Load()
        {
            var property = Collection.Resolve("Server/CategoryDiscount.img");
            var id = 0;
            
            return property.Children
                .SelectMany(c => c.Children)
                .Select(c => new CategoryDiscountTemplate(
                    ++id,
                    c.ResolveAll()
                ));
        }
    }
}