using System;
using Xamarin.Forms;

namespace EltraNavigoMPlayer.Views.MediaControl.Behaviors
{
    class ArtistPickerBehavior : Behavior<Picker>
    {
        private Picker _view;

        protected override void OnAttachedTo(Picker view)
        {
            base.OnAttachedTo(view);

            view.SelectedIndexChanged += OnSelectedIndexChanged;

            _view = view;
        }

        private void OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_view.BindingContext is MediaControlViewModel viewModel)
            {
                viewModel.OnArtistIndexChanged(_view.SelectedIndex);
            }
        }
    }
}
