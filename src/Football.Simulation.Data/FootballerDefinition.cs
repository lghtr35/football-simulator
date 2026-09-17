namespace Football.Simulation.Data
{
    public sealed class FootballerDefinition
    {
        public string Id;
        public bool IsActor; // Runtime host annotation; game saves own actor identity.
        public string Name;
        public string TeamId;
        public PositionType Position;
        public int Overall = 65;
        public PreferredFoot PreferredFoot;
        public FootballerSkills Skills;
        public FootballerAttributes Attributes;
    }

    public sealed class FootballerSkills
    {
        public int Speed;
        public int Acceleration;
        public int Stamina;
        public int StaminaRegen;
        public int Dribbling;
        public int FirstTouchControl;
        public int HitPower;
        public int Accuracy;
        public int Tackling;
        public int Strength;
        public int Goalkeeping;
    }

    public sealed class FootballerAttributes
    {
        public int Age;
        public int HeightCentimetres;
        public int WeightKilograms;
        public string Nationality;
    }

}
