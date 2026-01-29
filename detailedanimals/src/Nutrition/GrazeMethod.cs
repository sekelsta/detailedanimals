namespace DetailedAnimals {
    public enum GrazeMethod {
        Graze,
        NibbleGraze,
        Root
    }

    static class GrazeMethodMethods {
        public static NutritionData Nutrition(this GrazeMethod graze) {
            switch (graze) {
                case GrazeMethod.Graze:
                    return NutritionData.Get("grass");
                case GrazeMethod.NibbleGraze:
                    return NutritionData.Get("nibbleCrop");
                case GrazeMethod.Root:
                    return NutritionData.Get("vegetable");
                default:
                    return null;
            }
        }
    }
}
