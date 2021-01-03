using EltraNavigoMPlayer.Views.MediaControl;
using EltraNavigoMPlayer.Views.MPlayerControl;
using EltraUiCommon.Controls;
using EltraXamCommon.Dialogs;
using EltraXamCommon.Plugins;
using MPlayerMaster.Views.Dialogs;
using System.Collections.Generic;
using Xamarin.Forms;
using Xamarin.Forms.Internals;

namespace EltraNavigoMPlayer.Plugins
{
    [Preserve(AllMembers = true)]
    public class EltraNavigoMPlayerPlugin : EltraNavigoPluginService
    {
        #region Private fields

        private MPlayerControlViewModel _mPlayerControlViewModel;
        private MediaControlViewModel _mediaControlViewModel;
        private MPlayerControlView _mPlayerControlView;
        private MediaControlView _mediaControlView;
        private StationDialogViewModel _stationDialogViewModel;

        #endregion

        #region Properties

        private MPlayerControlViewModel MPlayerControlViewModel
        {
            get => _mPlayerControlViewModel ?? (_mPlayerControlViewModel = CreateMPlayerControlViewModel());
        }

        private MediaControlViewModel MediaControlViewModel
        {
            get => _mediaControlViewModel ?? (_mediaControlViewModel = CreateMediaControlViewModel());
        }

        private MPlayerControlView MPlayerControlView
        {
            get => _mPlayerControlView ?? (_mPlayerControlView = new MPlayerControlView());
        }

        private MediaControlView MediaControlView
        {
            get => _mediaControlView ?? (_mediaControlView = new MediaControlView());
        }

        private StationDialogViewModel StationDialogViewModel
        {
            get => _stationDialogViewModel ?? (_stationDialogViewModel = new StationDialogViewModel());
        }

        #endregion

        #region Methods

        private MPlayerControlViewModel CreateMPlayerControlViewModel()
        {
            var result = new MPlayerControlViewModel();

            result.PluginService = this;

            return result;
        }

        private MediaControlViewModel CreateMediaControlViewModel()
        {
            var result = new MediaControlViewModel();

            result.PluginService = this;

            return result;
        }

        public override List<ToolViewModel> GetViewModels()
        {
            var result = new List<ToolViewModel>();

            result.Add(MPlayerControlViewModel);
            result.Add(MediaControlViewModel);

            return result;
        }

        public override View ResolveView(ToolViewModel viewModel)
        {
            ContentView result = null;

            if (viewModel is MPlayerControlViewModel)
            {
                result = MPlayerControlView;
            }
            else if (viewModel is MediaControlViewModel)
            {
                result = MediaControlView;
            }

            return result;
        }

        public override List<XamDialogViewModel> GetDialogViewModels()
        {
            return new List<XamDialogViewModel>() { StationDialogViewModel };
        }

        public override View ResolveDialogView(XamDialogViewModel viewModel)
        {
            View result = null;
            
            if(viewModel is StationDialogViewModel)
            {
                result = new StationDialogView();
            }

            return result;
        }

        #endregion
    }
}
