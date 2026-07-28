namespace AshesOfRum
{
    public enum ProductionItem
    {
        Worker,
        Spearmen,
        Archers,
        Cavalry
    }

    public static class ProductionItemExtensions
    {
        public static ProductionItem ToProductionItem(this FormationType type) => type switch
        {
            FormationType.Spearmen => ProductionItem.Spearmen,
            FormationType.Archers => ProductionItem.Archers,
            _ => ProductionItem.Cavalry
        };

        public static FormationType ToFormationType(this ProductionItem item) => item switch
        {
            ProductionItem.Spearmen => FormationType.Spearmen,
            ProductionItem.Archers => FormationType.Archers,
            ProductionItem.Cavalry => FormationType.Cavalry,
            _ => throw new System.InvalidOperationException("Workers are not formations.")
        };
    }
}
