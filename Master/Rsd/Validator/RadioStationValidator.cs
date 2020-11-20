using MPlayerCommon.Contracts;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MPlayerMaster.Rsd
{
    class RadioStationValidator
    {
        public List<RadioStationModel> RadioStations { get; set; }

        private Task _validationTask;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public bool SearchActive { get; set; }

        public void Start()
        {
            Stop();

            _validationTask = Task.Run(() => 
            { 
                DoValidation(); 
            }, _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();

            _validationTask.Wait();

            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void DoValidation()
        {
            foreach(var radioStation in RadioStations)
            {
                
            }
        }
    }
}
