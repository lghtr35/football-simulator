using Football.Simulation.Data;
namespace Football.Simulation.Core
{
    public sealed class SimulationContext
    {
        public SimulationContext(int seed) { Seed = seed; Random = new SeededRandom(seed); }
        public int Seed { get; private set; }
        public SeededRandom Random { get; private set; }
        public SimulationDate StartDate { get; set; } = new SimulationDate { Year = 2026, Month = 1, Day = 1 };
        public SimulationDate CurrentDate { get; set; } = new SimulationDate { Year = 2026, Month = 1, Day = 1 };
    }

    public class Simulation {
        public SimulationContext Context { get; set; }
        public Simulation(SimulationContext context) { Context = context; }
        public void Start() {
            Context.CurrentDate = Context.StartDate;
        }

        public void Step() {


            Context.CurrentDate = SimulationCalendar.DateFromDay(SimulationCalendar.DayFromDate(Context.CurrentDate) + 1);
        }
    }
}
