using MPlayerCommon.Contracts.Media;

namespace MPlayerMaster.Device.Contracts
{
    class PlannerComposition
    {
        public PlannerComposition(Composition composition)
        {
            Composition = composition;

            State = PlayingState.Ready;
        }

        public Composition Composition { get; private set; }

        public string FullPath => Composition.FullPath;

        public PlayingState State { get; set; }
    }
}
